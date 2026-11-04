using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Careers;

// Every check of the Company app's new logic and screens, in the order they were built. Run in the full list, and alone by --company.
//
// And the run of it: a hundred and twenty game days of a seeded company and its city, every day through the rule entry points the director's day pass calls, in its order
// (CompanyDirector.Day.cs: the parts' day, ManagerDay, LeaseDay, then HierarchyDay), the NPC side acting in the same days (the employees who ask, the
// landlords who offer, the city's own renewals, the executive who settles what is handed down, the finance chief who tops the reserve up, the city that decides
// a licence, a rival who hires from the board). The director itself touches the game (CompanyGame, the save, the notices), so a run cannot call it; what keeps
// the run and the director from drifting apart is read from the director's source, not remembered (CompanyRunPins). A save written on day 30 is read back into
// a second company that then lives the same next thirty days; a save from before the new fields loads and lives sixty.
internal static partial class Program
{
    private static void CompanyAll()
    {
        CompanyAnswers();   // a bonus and a raise of the limit as answers (CompanyAnswerChecks.cs)
        CompanyDown();      // the managers' grades and a question handed down to the executive (CompanyDownChecks.cs)
        CompanyLate();      // late days and the manager who lets go (CompanyLateChecks.cs)
        CompanyLeases();    // leases, the manager's marketing and lease answers (CompanyLeaseChecks.cs)
        CompanyPlans();     // rota plans: in use, the cap, the floor, copies and the save (CompanyPlanChecks.cs)
        CompanyLicenceKinds();  // the four licence kinds added, applications and the fire certificate's link to the cover (CompanyLicenceChecks.cs)
        CompanyTaxReserve();    // the quarter's tax, the reserve, the past returns and the save (CompanyTaxChecks.cs)
        CompanyRun();       // a hundred and twenty days of all of it together, a save on day 30 and an old save (this file)
        CompanyRunPins();   // the director's source calls what the run calls, in the order the run calls it (this file)
    }

    // ---------------------------------------------------------------- the world of the run

    private sealed class CoWorld
    {
        public int Seed;
        public readonly CompanyState S = new CompanyState();
        public readonly ManagerState M = new ManagerState();
        public readonly HierarchyState H = new HierarchyState();
        public readonly LeaseState L = new LeaseState();
        public readonly Dictionary<string, RotaPlanSet> Plans = new Dictionary<string, RotaPlanSet>();
        public readonly List<string> Board = new List<string>();      // people the city has no work for, who may be hired
        public long Cash = 4000000;
        public int Day;
        // What the days did: the changes of state the run asserts, and the NPC side's acts.
        public int LateCounted, LetGo, Hired, RivalHired, Asked, Answered, Bonuses, Speeds, Offers, Renewals, Holds, ManagerLeases, OwnerLeases, CityRenewals, TrendKept;
        public int HandedDown, ExecSettled, Closed, Reserved, Filed, Applied, Granted, PlanTurns, Floors;
        public int PlanNow;                                           // the plan of the place in use now
        public readonly List<string> Log = new List<string>();        // one line a day with what changed, to tell two runs apart
    }

    // The run starts on day 1101 of a game (Epoch + 1), the fourth year: the city's own leases renew once a term, none has its first term end before day 180, and the
    // longest term is 36 months, so that the player's leases began after day 1 and their ends are in the run.
    private const int RunSeed = 5151, RunMonth = 30, RunKeep = 10, RunDays = 120, Epoch = 1100;
    private static readonly string[] RunDistricts = { "Midtown", "Harbour", "Uptown", "Brooklyn" };

    private static CoWorld NewCoWorld(int seed)
    {
        var w = new CoWorld { Seed = seed };
        w.S.Seed = seed;
        w.S.FirstDay = 1;
        string[] places = { "Broadway~12", "Elm~7", "Dock~4", "Mill~9" };
        for (int i = 0; i < places.Length; i++)
        {
            w.S.Shops.Add(new ShopFile { Place = places[i], FirstDay = 1 });
        }
        for (int i = 0; i < RunKeep; i++)
        {
            w.S.Staff.Add(new StaffFile { Id = "e" + i, Place = i < 6 ? places[0] : places[1], Name = "Person " + i, Going = 1900, Wage = i % 3 == 0 ? 1650 : 1900, Skill = 30 + i * 6 });
        }
        for (int i = 0; i < 14; i++)
        {
            w.Board.Add("b" + i);
        }
        // A manager at Broadway: raises, hiring and leases given, the hand-down switch on, a month's budget, a lease limit wide enough for the landlord's draw.
        var m = new ManagerFile
        {
            Place = places[0], Id = "m0", Name = "Ray Walsh", Role = ManagerCatalogue.RolePlace, From = 1, Skill = 70, Seen = true, HandDown = true, Budget = 4, LeaseStep = 5, MarketingStep = 3, WageBand = 0,
            Tasks = ManagerRules.Bit(ManagerTask.Raises) | ManagerRules.Bit(ManagerTask.Hiring) | ManagerRules.Bit(ManagerTask.Leases)
        };
        w.M.Managers.Add(m);
        w.M.MonthFrom = Epoch + 1;
        // The executives are in post: the people chief takes what is handed down, the finance chief tops the reserve up.
        w.H.Executives.Add(new ExecutiveFile { Area = (int)ExecSeat.Coo, Id = "x1", Name = "Ethan Brooks" });
        w.H.Executives.Add(new ExecutiveFile { Area = (int)ExecSeat.Chro, Id = "x2", Name = "Priya Nair" });
        w.H.Executives.Add(new ExecutiveFile { Area = (int)ExecSeat.Cfo, Id = "x3", Name = "Noah Feldman" });
        w.S.Tax.QuarterFrom = Epoch - 20;   // the quarter began before the run: it closes on its day 71
        w.S.Tax.ReserveAuto = true;
        // Three rented places whose leases end on days 40, 70 and 100 of the run: the offer comes 14 days before each.
        int[] ends = { Epoch + 40, Epoch + 70, 0, Epoch + 100 }, rents = { 300, 180, 0, 220 };
        for (int i = 0; i < places.Length; i++)
        {
            if (rents[i] > 0)
            {
                LeaseFile l = LeaseRules.Begin(seed, places[i], rents[i], 1, RunMonth);
                l.Start = ends[i] - l.Term;
                l.District = i % 2 == 0 ? "Midtown" : "Harbour";
                w.L.Leases.Add(l);
            }
        }
        // Three plans at Broadway: the standing one, a holiday week that holds for days 40 to 46 with a floor of crew, and a plan for odd weeks.
        var set = new RotaPlanSet { Place = places[0] };
        RotaPlanFile standing = RotaPlanRules.Add(set, "Standard week");
        RotaPlanFile holiday = RotaPlanRules.Add(set, "Holiday week");
        holiday.From = Epoch + 40;
        holiday.To = Epoch + 46;
        holiday.Blocks.Add(new RotaBlock { From = 9, To = 13, Crew = 2 });
        RotaPlanFile odd = RotaPlanRules.Add(set, "Odd weeks");
        odd.Parity = RotaPlanRules.Odd;
        set.Active = standing.Id;
        w.Plans[places[0]] = set;
        return w;
    }

    // One game day, in the order of the director's: the parts' day (staff, tax, licences), the managers' day, the leases, the hierarchy's day, the rota plan the
    // Sunday write puts on.
    private static void CoDay(CoWorld w, int day)
    {
        w.Day = day;
        w.S.LastDay = day;
        w.S.Age++;
        int before = w.LateCounted + w.LetGo + w.Asked + w.Answered + w.Offers + w.Renewals + w.CityRenewals + w.HandedDown + w.Closed + w.Applied + w.Granted + w.PlanTurns;
        CoStaff(w, day);
        CoTax(w, day);
        CoLicences(w, day);
        CoManagers(w, day);
        CoLeases(w, day);
        CoHierarchy(w, day);
        CoRotaPlan(w, day);
        int after = w.LateCounted + w.LetGo + w.Asked + w.Answered + w.Offers + w.Renewals + w.CityRenewals + w.HandedDown + w.Closed + w.Applied + w.Granted + w.PlanTurns;
        w.Log.Add(day + ":" + (after - before) + ":" + w.Cash + ":" + w.S.Tax.Reserve + ":" + w.PlanNow);
    }

    // The staff part's day: the day's lateness counted from the hours worked against the plan, a person underpaid long enough asks for a raise.
    private static void CoStaff(CoWorld w, int day)
    {
        for (int i = 0; i < w.S.Staff.Count; i++)
        {
            StaffFile f = w.S.Staff[i];
            bool late = CityModel.Draw(w.Seed, 90 + i % 3, day * 31 + i, 1000) < (i % 4 == 0 ? 350 : 20);
            int lateBefore = f.LateDays;
            StaffRules.LateDay(f, 8, late ? 6 : 8, false);
            w.LateCounted += f.LateDays > lateBefore ? 1 : 0;
            StaffNote note = StaffRules.Day(f, 1, day);
            w.Asked += note == StaffNote.Asked ? 1 : 0;
            w.Cash -= StaffRules.Pay(f.Paid, 800);
        }
    }

    // The tax part's day: the year turning over, a quarter that has closed (the return's figures, the finance chief's top-up), the return filed three days on.
    private static void CoTax(CoWorld w, int day)
    {
        TaxFile t = w.S.Tax;
        int released;
        if (TaxRules.Year(t, TaxRules.YearStart(day, 4 * TaxRules.QuarterDays(RunMonth)), out released))
        {
            w.Cash += released;
        }
        if (TaxRules.QuarterOver(t, day, TaxRules.QuarterDays(RunMonth)))
        {
            TaxRules.Close(t, 600000 + (day * 37) % 9000, day, 20);
            w.Closed++;
            int moved = t.ReserveAuto && w.H.Executives.Exists(x => x.Area == (int)ExecSeat.Cfo) ? TaxRules.TopUp(t, (int)Math.Min(int.MaxValue, w.Cash)) : 0;
            w.Cash -= moved;
            w.Reserved += moved > 0 ? 1 : 0;
        }
        if (t.ReturnWaiting && day >= t.ReturnDue - CompanyCatalogue.ReturnDays + 3)
        {
            TaxRules.File(t, day);
            w.Filed++;
        }
        TaxRules.LateDay(t, day);
    }

    // The licences part's day: a business applies for the outdoor seating permit on a day, the city decides on its day and the permit runs from it.
    private static void CoLicences(CoWorld w, int day)
    {
        int kind = (int)LicenceKind.OutdoorSeating;
        Licence l = w.S.Shops[0].Permits[kind];
        if (day == Epoch + 8 && LicenceRules.CanApply(l))
        {
            w.Applied += LicenceRules.Apply(l, kind, day) ? 1 : 0;
        }
        if (LicenceRules.Day(l, day) == LicenceNote.Granted)
        {
            LicenceRules.Grant(l, kind, day);
            w.Granted++;
        }
    }

    // The managers' day, at their hour: a request that waits is answered inside the wage band (a bonus where a refusal would be and the budget left covers it);
    // somebody three days late is let go if the board has somebody for their place, and the place is filled again.
    private static void CoManagers(CoWorld w, int day)
    {
        ManagerFile m = w.M.Managers[0];
        if (ManagerRules.MonthOver(w.M.MonthFrom, day, RunMonth))
        {
            m.EndMonth();
            w.M.MonthFrom = day;
        }
        for (int i = 0; i < w.S.Staff.Count; i++)
        {
            StaffFile f = w.S.Staff[i];
            if (f.Place != m.Place || f.AskDay == 0 || !ManagerRules.Answers(true, m, ManagerTask.Raises, f.Id))
            {
                continue;
            }
            int ceiling = ManagerRules.Ceiling(f.Going, m.WageBand);
            bool mistake = CityModel.Draw(w.Seed, 55, day * 17 + i, 1000) < ManagerRules.MistakeChance(m.Skill, 0);
            int bonus = StaffRules.BonusFor(m.WageBand, 100);
            int left = ManagerRules.Left(ManagerCatalogue.Step(ManagerCatalogue.Budgets, m.Budget), m.Spent);
            RaiseAnswer answer = ManagerRules.OrBonus(ManagerRules.RaiseAnswerFor(f.Paid, f.AskTo, ceiling, mistake), bonus, left, mistake);
            int waited = day - f.AskDay;
            StaffRules.Answer(f, answer, day);
            ManagerRules.AddSpeed(m.Speeds, waited);
            w.Speeds++;
            if (answer == RaiseAnswer.Bonus)
            {
                m.Spent += bonus;
                w.Cash -= bonus;
                w.Bonuses++;
            }
            m.Decided++;
            m.Mistakes += mistake ? 1 : 0;
            w.Answered++;
        }
        int pick = -1;
        for (int i = 0; i < w.S.Staff.Count; i++)
        {
            StaffFile f = w.S.Staff[i];
            if (f.Place == m.Place && f.LeaveDay == 0 && ManagerRules.LetGo(f.LateDays, w.Board.Count > 0) && (pick < 0 || f.LateDays > w.S.Staff[pick].LateDays))
            {
                pick = i;
            }
        }
        if (pick >= 0)
        {
            StaffFile gone = w.S.Staff[pick];
            w.Board.Add(gone.Id + "r" + day);   // the person who left is on the board for a rival or the player to hire
            string hired = w.Board[0];
            w.Board.RemoveAt(0);
            w.S.Staff[pick] = new StaffFile { Id = hired, Place = m.Place, Name = "Hire " + hired, Going = 1900, Wage = 1900, Skill = 40 };
            w.LetGo++;
            w.Hired++;
        }
        // The city's side of the same board: a rival hires somebody from it now and then, so that it is not only filled.
        if (day % 11 == 0 && w.Board.Count > 6)
        {
            w.Board.RemoveAt(w.Board.Count - 1);
            w.RivalHired++;
        }
    }

    // The leases' day (CompanyDirector.LeaseDay): the city's month, the rise paid, a lease that ends renewed or held, an offer made 14 days before its end; the manager
    // with the Leases task answers inside their limit and past it asks the owner, who answers on a day of their own.
    private static void CoLeases(CoWorld w, int day)
    {
        if (w.L.TrendDay == 0)
        {
            w.L.TrendDay = day;
        }
        else if (day - w.L.TrendDay >= RunMonth)
        {
            int sum = 0, renewed = 0, trend = LeaseRules.TrendOf(w.L.Trend);
            for (int d = w.L.TrendDay; d < w.L.TrendDay + RunMonth; d++)
            {
                for (int k = 0; k < RunDistricts.Length; k++)
                {
                    int count;
                    sum += LeaseRules.CityDay(w.Seed, RunDistricts[k], d, RunMonth, trend, 0, out count);
                    renewed += count;
                }
            }
            LeaseRules.KeepTrend(w.L.Trend, sum, renewed);
            w.L.TrendDay += RunMonth;
            w.CityRenewals += renewed;
            w.TrendKept++;
        }
        int[] rents = { 300, 180, 0, 220 };
        for (int i = 0; i < w.S.Shops.Count; i++)
        {
            if (rents[i] <= 0)
            {
                continue;
            }
            string place = w.S.Shops[i].Place;
            LeaseFile l = w.L.Of(place);
            if (l == null)
            {
                l = LeaseRules.Begin(w.Seed, place, rents[i], day, RunMonth);   // an old save's premises: a lease from the day it loads
                w.L.Leases.Add(l);
            }
            l.Rent = rents[i];
            l.District = i % 2 == 0 ? "Midtown" : "Harbour";
            w.Cash -= LeaseRules.TopUp(l);
            if (LeaseRules.Settle(l, day, w.Seed, RunMonth))
            {
                w.Renewals++;
                w.Holds += l.Hold ? 1 : 0;
            }
            else if (LeaseRules.OfferDue(l, day))
            {
                l.Offer = LeaseRules.Offer(l, w.Seed, LeaseRules.TrendOf(w.L.Trend), 0);
                l.Asked = day;
                l.Answer = 0;
                w.Offers++;
            }
            else if (LeaseRules.Waiting(l, day))
            {
                ManagerFile m = w.M.Managers[0];
                if (place == m.Place && ManagerRules.Has(m.Tasks, ManagerTask.Leases) && LeaseRules.Within(l, ManagerCatalogue.Step(ManagerCatalogue.LeaseRises, m.LeaseStep)))
                {
                    LeaseRules.Respond(l, day, true);
                    ManagerRules.AddSpeed(m.Speeds, day - l.Asked);
                    w.ManagerLeases++;
                }
                else if (day - l.Asked >= 3)
                {
                    LeaseRules.Respond(l, day, (day + i) % 3 != 0);   // the owner, three days on
                    w.OwnerLeases++;
                }
            }
        }
        w.L.Leases.Sort((a, b) => a.End.CompareTo(b.End));
    }

    // The hierarchy's day: a question the owner holds is handed down a day on to the area's executive, who settles it by the policy, once.
    private static void CoHierarchy(CoWorld w, int day)
    {
        if (day % 6 == 1)
        {
            w.H.Add(EscalationKind.Exception, "Broadway~12", EscalationRules.LevelOwner, day);
        }
        ManagerFile m = w.M.Managers[0];
        ExecutiveFile exec = w.H.Executives.Find(x => x.Area == (int)EscalationRules.AreaOf(EscalationKind.Exception));
        for (int i = 0; i < w.H.Escalations.Count; i++)
        {
            Escalation e = w.H.Escalations[i];
            if (!e.Open || !EscalationRules.HandsDown((EscalationKind)e.Kind, e.Level, e.Moved, day, e.Down, m.HandDown, exec != null, 1, 1))
            {
                continue;
            }
            e.Level = EscalationRules.LevelExecutive;
            e.Moved = day;
            e.Down = true;
            w.HandedDown++;
            if (EscalationRules.Tries((EscalationKind)e.Kind, e.Level, 1, 1))
            {
                e.State = (int)EscalationState.Resolved;
                e.By = exec.Name;
                w.ExecSettled++;
            }
        }
    }

    // The Sunday write: the plan in use for the week, a dated plan over the standing one while its days last, the odd weeks' plan on odd weeks.
    private static void CoRotaPlan(CoWorld w, int day)
    {
        RotaPlanFile now = RotaPlanRules.InUse(w.Plans["Broadway~12"], day);
        int id = now != null ? now.Id : 0;
        if (id != w.PlanNow)
        {
            w.PlanTurns++;
            w.PlanNow = id;
        }
        if (now != null && now.Blocks.Count > 0)
        {
            var cells = new int[RotaCatalogue.Days * RotaCatalogue.Hours];
            RotaPlanRules.FloorCells(now, cells);
            w.Floors += cells[10] >= 2 ? 1 : 0;
        }
    }

    // ---------------------------------------------------------------- the save

    private static string[] CoWrite(CoWorld w)
    {
        return new[] { CompanyStore.Write(w.S), ManagerStore.Write(w.M), HierarchyStore.Write(w.H), LeaseStore.Write(w.L), RotaPlanStore.Write(w.Plans) };
    }

    // What the game keeps that a save does not: the cash, the board, the names and going rates of the staff, the managers' skill, the plan on now.
    private static CoWorld CoSnap(CoWorld w)
    {
        var s = new CoWorld { Seed = w.Seed, Cash = w.Cash, Day = w.Day, PlanNow = w.PlanNow, LetGo = w.LetGo, Renewals = w.Renewals, HandedDown = w.HandedDown };
        s.Board.AddRange(w.Board);
        foreach (StaffFile f in w.S.Staff)
        {
            s.S.Staff.Add(new StaffFile { Id = f.Id, Name = f.Name, Going = f.Going, Wage = f.Wage, Skill = f.Skill });
        }
        foreach (ManagerFile m in w.M.Managers)
        {
            s.M.Managers.Add(new ManagerFile { Skill = m.Skill, Name = m.Name });
        }
        return s;
    }

    // The save read back into a second company, with what the game keeps put back as the game would.
    private static CoWorld CoRead(string[] lines, CoWorld game)
    {
        var w = new CoWorld { Seed = game.Seed, Cash = game.Cash, Day = game.Day, PlanNow = game.PlanNow };
        w.Board.AddRange(game.Board);
        int v;
        CompanyState s = CompanyStore.Read(lines[0], out v);
        ManagerState m = ManagerStore.Read(lines[1], out v);
        HierarchyState h = HierarchyStore.Read(lines[2], out v);
        LeaseState l = lines[3] != null ? LeaseStore.Read(lines[3], out v) : new LeaseState();
        w.S.Seed = s.Seed;
        w.S.FirstDay = s.FirstDay;
        w.S.LastDay = s.LastDay;
        w.S.Age = s.Age;
        w.S.SickPolicy = s.SickPolicy;
        w.S.PoachRest = s.PoachRest;
        w.S.NextClaim = s.NextClaim;
        CopyTax(s.Tax, w.S.Tax);
        w.S.Shops.AddRange(s.Shops);
        w.S.Staff.AddRange(s.Staff);
        w.S.Claims.AddRange(s.Claims);
        w.S.Reports.AddRange(s.Reports);
        w.M.MonthFrom = m.MonthFrom;
        w.M.NextId = m.NextId;
        w.M.Managers.AddRange(m.Managers);
        w.M.Decisions.AddRange(m.Decisions);
        w.H.Executives.AddRange(h.Executives);
        w.H.Escalations.AddRange(h.Escalations);
        w.H.Entries.AddRange(h.Entries);
        w.H.NextId = h.NextId;
        w.L.Leases.AddRange(l.Leases);
        w.L.Trend.AddRange(l.Trend);
        w.L.TrendDay = l.TrendDay;
        if (lines[4] != null)
        {
            RotaPlanStore.Read(lines[4], w.Plans);
        }
        for (int i = 0; i < w.S.Staff.Count; i++)
        {
            StaffFile o = game.S.Staff.Find(x => x.Id == w.S.Staff[i].Id);
            if (o != null)
            {
                w.S.Staff[i].Name = o.Name;
                w.S.Staff[i].Going = o.Going;
                w.S.Staff[i].Wage = o.Wage;
                w.S.Staff[i].Skill = o.Skill;
            }
        }
        for (int i = 0; i < w.M.Managers.Count && i < game.M.Managers.Count; i++)
        {
            w.M.Managers[i].Skill = game.M.Managers[i].Skill;
            w.M.Managers[i].Name = game.M.Managers[i].Name;
            w.M.Managers[i].Seen = true;   // the game's read finds them at work
        }
        return w;
    }

    private static void CopyTax(TaxFile from, TaxFile into)
    {
        into.QuarterFrom = from.QuarterFrom;
        into.Quarter = from.Quarter;
        Array.Copy(from.Costs, into.Costs, from.Costs.Length);
        into.ReturnDue = from.ReturnDue;
        into.ReturnQuarter = from.ReturnQuarter;
        into.ReturnTakings = from.ReturnTakings;
        into.ReturnCosts = from.ReturnCosts;
        into.ReturnPenalties = from.ReturnPenalties;
        into.LateRun = from.LateRun;
        into.Accountant = from.Accountant;
        into.AccountantNext = from.AccountantNext;
        into.Risk = from.Risk;
        into.Saved = from.Saved;
        into.Owed = from.Owed;
        into.LastAudit = from.LastAudit;
        into.CaseNumber = from.CaseNumber;
        into.QuietUntil = from.QuietUntil;
        into.ReturnTax = from.ReturnTax;
        into.Reserve = from.Reserve;
        into.ReserveAuto = from.ReserveAuto;
        into.YearTax = from.YearTax;
        into.YearFrom = from.YearFrom;
        into.Settled.AddRange(from.Settled);
    }

    // Where two sets of lines differ, for a failure to say: the store and the first place they part.
    private static string CoDiff(string[] x, string[] y)
    {
        string[] names = { "company", "managers", "hierarchy", "leases", "plans" };
        for (int i = 0; i < x.Length && i < y.Length; i++)
        {
            if (!string.Equals(x[i], y[i], StringComparison.Ordinal))
            {
                int at = 0;
                while (x[i] != null && y[i] != null && at < x[i].Length && at < y[i].Length && x[i][at] == y[i][at])
                {
                    at++;
                }
                return " (the " + names[i] + " line differs at " + at + ": '" + (x[i] != null && at < x[i].Length ? x[i].Substring(Math.Max(0, at - 20), Math.Min(50, x[i].Length - Math.Max(0, at - 20))) : "") + "' against '"
                       + (y[i] != null && at < y[i].Length ? y[i].Substring(Math.Max(0, at - 20), Math.Min(50, y[i].Length - Math.Max(0, at - 20))) : "") + "')";
            }
        }
        return string.Empty;
    }

    // The records of one part of a line, cut by a function: the part between its tag and the next, split at ';', each record handed to the cut.
    private static string CutPart(string line, string tag, string next, Func<string, string> cut)
    {
        int at = line.IndexOf(tag, StringComparison.Ordinal), end = next != null ? line.IndexOf(next, StringComparison.Ordinal) : line.Length;
        if (at < 0 || end < at)
        {
            return line;
        }
        string[] records = line.Substring(at + tag.Length, end - at - tag.Length).Split(';');
        for (int i = 0; i < records.Length; i++)
        {
            records[i] = cut(records[i]);
        }
        return line.Substring(0, at + tag.Length) + string.Join(";", records) + line.Substring(end);
    }

    // A line as the version before the new fields wrote it, by the same cuts the store checks make: the staff records without their late days, the shop records
    // without the four added kinds of permit, the tax record up to its rent's rise, the manager records after their names, the escalation records without their
    // flag, and none of the leases or the rota plans, which are new keys.
    private static string[] CoOld(string[] lines)
    {
        string company = CutPart(lines[0], "^e:", "^c:", r => r.Substring(0, Math.Max(0, r.LastIndexOf('|'))));
        company = CutPart(company, "^s:", "^e:", r =>
        {
            string[] f = r.Split('|');
            return string.Join("|", f, 0, Math.Max(1, f.Length - 16));
        });
        company = TaxRecordCut(company, 22);
        string manager = CutPart(lines[1], "^m:", "^d:", r =>
        {
            int name = r.IndexOf("Ray Walsh", StringComparison.Ordinal);
            return name >= 0 ? r.Substring(0, name + "Ray Walsh".Length) : r;
        });
        string hier = CutPart(lines[2], "^e:", "^l:", r => r.Length == 0 ? r : r.Substring(0, Math.Max(0, r.LastIndexOf('|'))));
        return new[] { company, manager, hier, null, null };
    }

    // ---------------------------------------------------------------- the run

    private static void CompanyRun()
    {
        int before = _run, failedBefore = _failed;
        // Ninety days, twice from the same seed: the same days; another seed: other ones.
        CoWorld a = NewCoWorld(RunSeed), b = NewCoWorld(RunSeed), other = NewCoWorld(RunSeed + 1);
        string[] at30 = null;
        CoWorld game30 = null;
        for (int day = Epoch + 1; day <= Epoch + RunDays; day++)
        {
            CoDay(a, day);
            CoDay(b, day);
            CoDay(other, day);
            if (day == Epoch + 30)
            {
                at30 = CoWrite(a);
                game30 = CoSnap(a);
            }
        }
        Check(string.Join("|", a.Log) == string.Join("|", b.Log) && string.Join(";", CoWrite(a)) == string.Join(";", CoWrite(b)), "company run: the same seed lives the same hundred and twenty days twice, and writes the same save");
        Check(string.Join("|", a.Log) != string.Join("|", other.Log), "company run: another seed lives other days");

        // Each new item's state changes as it should over the days.
        Check(a.LateCounted >= 5 && a.LetGo >= 1 && a.Hired == a.LetGo && a.S.Staff.Count == RunKeep, "company run: lateness is counted, a person three days late is let go and the place filled from the board, the crew stays at " + RunKeep
              + " (" + a.LateCounted + " late days, " + a.LetGo + " let go)");
        Check(a.Asked >= 1 && a.Answered >= 1 && a.Speeds == a.Answered, "company run: a person underpaid long enough asks, the manager answers, and the days it waited go in their ring of speed (" + a.Asked + " asked, " + a.Answered + " answered)");
        ManagerFile m = a.M.Managers[0];
        Check(m.Speeds.Count >= 5 && m.Speeds.Count <= ManagerCatalogue.SpeedKept && ManagerRules.Speed(m.Speeds) >= 0 && ManagerRules.Speed(m.Speeds) <= 100 && m.Decided + m.LastDecided > 0 && m.Decided + m.LastDecided <= a.Answered, "company run: the ring of settled waits fills and grades speed in range (" + m.Speeds.Count + " kept, speed " + ManagerRules.Speed(m.Speeds) + ")");
        Check(a.Bonuses >= 1 && a.Bonuses <= a.Answered, "company run: where the manager's answer would be a refusal and the month's budget covers it, it is a bonus (" + a.Bonuses + " of " + a.Answered + ")");
        Check(a.Offers >= 2 && a.Offers <= a.Renewals + 3 && a.Renewals >= 1 && a.ManagerLeases >= 1 && a.OwnerLeases >= 1, "company run: the landlord's offers come before the renewals and are answered, by the manager inside their limit and by the owner where there is none ("
              + a.Offers + " offers, " + a.Renewals + " renewals, " + a.ManagerLeases + " by the manager, " + a.OwnerLeases + " by the owner)");
        Check(a.TrendKept >= 2 && a.L.Trend.Count >= 2 && a.CityRenewals >= 1, "company run: the city's own leases renew by the same rule and set the trend: " + a.TrendKept + " months kept, " + a.CityRenewals + " city renewals");
        Check(a.HandedDown >= 3 && a.ExecSettled >= 1 && a.ExecSettled <= a.HandedDown, "company run: a question the owner leaves a day is handed down to the executive, once, and settled by them (" + a.HandedDown + " handed down, " + a.ExecSettled + " settled)");
        Check(a.Closed >= 1 && a.Reserved >= 1 && a.Filed >= 1 && a.S.Tax.Settled.Count >= 1 && a.S.Tax.Settled.Count <= TaxRules.SettledKept && a.S.Tax.Reserve > 0,
            "company run: a quarter closes, the finance chief tops the reserve up, the return is filed and the ring of past returns fills (" + a.Closed + " closed, " + a.Reserved + " topped up, " + a.S.Tax.Settled.Count + " kept)");
        Licence permit = a.S.Shops[0].Permits[(int)LicenceKind.OutdoorSeating];
        Check(LicenceRules.Decision((int)LicenceKind.OutdoorSeating) > 0 && a.Applied == 1 && a.Granted == 1 && permit.Until > 0 && !permit.Pending, "company run: an application for a permit waits for the city's decision day, is granted on it and holds a term");
        Check(a.PlanTurns >= 3 && a.Floors >= 1, "company run: the plan in use turns to the holiday week for its days and back, the odd weeks' plan on odd weeks, and the holiday week's floor of crew is put under the planner (" + a.PlanTurns + " turns)");
        Check(a.RivalHired >= 1 && a.Board.Count > 0, "company run: a rival hires from the same board the manager fills the place from, so the city's side acts too (" + a.RivalHired + " hired by a rival)");

        // The save written on day 30, read back, and the next thirty days lived by both.
        CoWorld back = CoRead(at30, game30);
        Check(string.Join(";", CoWrite(back)) == string.Join(";", at30), "company run: the save of day 30 read back writes the same lines" + CoDiff(CoWrite(back), at30));
        CoWorld replay = CoRead(at30, game30);
        for (int day = Epoch + 31; day <= Epoch + 60; day++)
        {
            CoDay(replay, day);
        }
        CoWorld first = NewCoWorld(RunSeed);
        for (int day = Epoch + 1; day <= Epoch + 60; day++)
        {
            CoDay(first, day);
        }
        Check(string.Join(";", CoWrite(replay)) == string.Join(";", CoWrite(first)), "company run: the company read from day 30 lives the next thirty days as the one that never stopped: the same saved state on day 60"
              + CoDiff(CoWrite(replay), CoWrite(first)));
        Check(replay.LetGo + game30.LetGo == first.LetGo && replay.Renewals + game30.Renewals == first.Renewals && replay.HandedDown + game30.HandedDown == first.HandedDown, "company run: and did the same things in them (let go, renewals, handed down)");

        // An old save: the lines before the new fields (and no leases or plans) load and the company lives on.
        string[] old = CoOld(at30);
        CoWorld aged = CoRead(old, game30);
        bool loads = aged.S.Staff.Count == RunKeep && aged.M.Managers.Count == 1 && !aged.M.Managers[0].HandDown && aged.M.Managers[0].Speeds.Count == 0 && aged.L.Leases.Count == 0 && aged.Plans.Count == 0
                     && aged.S.Staff.TrueForAll(x => x.LateDays == 0) && aged.S.Shops.Count == 4 && aged.S.Tax.Reserve == 0 && aged.S.Tax.Settled.Count == 0;
        Check(loads, "company run: an old save loads: nobody late, no hand-down, no waits, no reserve, no leases and no plans");
        aged.Plans["Broadway~12"] = NewCoWorld(RunSeed).Plans["Broadway~12"];   // the plans are made on first use, as the page makes them
        aged.M.Managers[0].HandDown = true;
        for (int day = Epoch + 31; day <= Epoch + RunDays; day++)
        {
            CoDay(aged, day);
        }
        Check(aged.S.Staff.Count == RunKeep && aged.L.Leases.Count == 3 && aged.Renewals == 0 && aged.TrendKept >= 1 && aged.Closed >= 1 && aged.HandedDown >= 1,
            "company run: an old save plays on for ninety days: the leases begin on the day it loads, the crew holds at " + RunKeep + ", the quarter closes, questions are handed down ("
            + aged.L.Leases.Count + " leases, " + aged.Closed + " quarter closed, " + aged.HandedDown + " handed down)");
        Console.WriteLine("  the run: " + a.LateCounted + " late days, " + a.LetGo + " let go and " + a.Hired + " hired, " + a.Asked + " asked and " + a.Answered + " answered (" + a.Bonuses + " bonuses), " + a.Offers + " lease offers, "
                          + a.Renewals + " renewals, " + a.ManagerLeases + " answered by the manager and " + a.OwnerLeases + " by the owner, " + a.CityRenewals + " city renewals, " + a.HandedDown + " handed down and "
                          + a.ExecSettled + " settled by the executive, " + a.Closed + " quarter closed and " + a.Reserved + " topped up, " + a.PlanTurns + " plan turns, " + a.RivalHired + " hired by a rival");
        Console.WriteLine("  a hundred and twenty days of the company's new logic, a save on day 30 and an old save: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    // ---------------------------------------------------------------- the director reads what the run drives

    // The run drives the rule entry points in the director's order. This reads the director's source and checks that it calls each of them, and in that order, so
    // that a change to the director that the run does not make fails here.
    private static void CompanyRunPins()
    {
        int before = _run, failedBefore = _failed;
        string dir = FindCompanyDirectory();
        if (dir == null)
        {
            Check(false, "company run pins: the Company economy folder is found");
            return;
        }
        string day = SourceText(dir, "CompanyDirector.Day.cs"), leases = SourceText(dir, "CompanyDirector.Leases.cs"), tax = SourceText(dir, "CompanyDirector.Tax.cs");
        string reserve = SourceText(dir, "CompanyDirector.TaxReserve.cs"), esc = SourceText(dir, "CompanyDirector.Escalations.cs"), decisions = SourceText(dir, "CompanyDirector.ManagerDecisions.cs");
        string managers = SourceText(dir, "CompanyDirector.Managers.cs"), plans = SourceText(dir, "CompanyDirector.RotaPlans.cs"), lic = SourceText(dir, "CompanyDirector.Licences.cs");
        string game = SourceText(dir, "CompanyGame.cs"), staff = SourceText(dir, "CompanyDirector.Staff.cs"), rotaRead = SourceText(dir, "RotaGame.Read.cs");
        Check(RunInOrder(day, "Read(day, false, true", "PartDay(part, day)", "ManagerDay(day)", "LeaseDay(day)", "Books(day, next)", "HierarchyDay(day)"),
            "company run pins: the day reads, runs the parts, then the managers, the leases, the books and the hierarchy, as the run does");
        Check(RunInOrder(leases, "CityMonth(day, monthDays)", "LeaseRules.Begin(", "LeaseRules.TopUp(l)", "LeaseRules.Settle(l, day", "LeaseRules.OfferDue(l, day)", "MakeOffer(l, shop, day)"),
            "company run pins: the lease day: the city's month, a lease begun, the rise paid, the lease settled or the offer made, in the run's order");
        Check(RunInOrder(leases, "LeaseRules.Offer(l, _state.Seed", "ManagerRules.Runs(mp.Own.Tasks", "AskOwner(l, shop, mp, day)") && RunInOrder(leases, "LeaseRules.Within(l, limit)", "LeaseRules.Respond(l, day, accept)")
              && leases.Contains("LeaseRules.CityDay(") && leases.Contains("LeaseRules.KeepTrend("),
            "company run pins: the offer, the manager's limit and answer, the owner asked past it, and the city's renewals that keep the trend");
        Check(RunInOrder(tax, "ReserveYear(day)", "TaxRules.QuarterOver(t, day, QuarterDays)", "TaxRules.Close(t, takings", "ReserveClose(day)", "FileReturn(true)", "TaxRules.LateDay(t, day)")
              && reserve.Contains("TaxRules.TopUp(t, CompanyGame.Cash)") && reserve.Contains("TaxRules.Year(t,"),
            "company run pins: the tax day: the year, the quarter's close, the finance chief's top-up, the return filed, the late day, in the run's order");
        Check(esc.Contains("EscalationRules.HandsDown(kind, e.Level, e.Moved, day, e.Down, m.HandDown") && esc.Contains("EscalationRules.Tries("),
            "company run pins: the hierarchy's day hands down by EscalationRules.HandsDown and the executive settles by EscalationRules.Tries");
        Check(decisions.Contains("ManagerRules.OrBonus(") && managers.Contains("ManagerRules.AddSpeed(") && decisions.Contains("ManagerRules.RaiseAnswerFor("),
            "company run pins: a manager's answer is a bonus where a refusal would be (OrBonus) and its wait goes in the ring (AddSpeed)");
        Check(decisions.Contains("ManagerRules.LetGo(worst.LateDays, replace)") && game.Contains("StaffRules.LateDay(") && staff.Contains("StaffRules.Day(f, _level, day"),
            "company run pins: lateness is counted by StaffRules.LateDay, an ask comes from StaffRules.Day, and the manager lets go by ManagerRules.LetGo");
        Check(plans.Contains("RotaPlanRules.InUse(") && rotaRead.Contains("RotaPlanRules.FloorCells(") && lic.Contains("LicenceRules.Day(") && lic.Contains("LicenceRules.Grant("),
            "company run pins: the rota write asks RotaPlanRules.InUse and puts its floor under the planner, and the licence day asks LicenceRules.Day and grants on the decision day");
        Console.WriteLine("  the director's source calls what the run calls, in its order: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    private static string FindCompanyDirectory()
    {
        string dir = AppContext.BaseDirectory;
        for (int up = 0; up < 9 && dir != null; up++)
        {
            string candidate = System.IO.Path.Combine(dir, "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Economy", "Company");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = System.IO.Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static string SourceText(string dir, string file)
    {
        string path = System.IO.Path.Combine(dir, file);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    // Whether each of the markers is found in the text, each after the one before.
    private static bool RunInOrder(string text, params string[] markers)
    {
        int at = 0;
        for (int i = 0; i < markers.Length; i++)
        {
            int found = text.IndexOf(markers[i], at, StringComparison.Ordinal);
            if (found < 0)
            {
                return false;
            }
            at = found + markers[i].Length;
        }
        return true;
    }
}
