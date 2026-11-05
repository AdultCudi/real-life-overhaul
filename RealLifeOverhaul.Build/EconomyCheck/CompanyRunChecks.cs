using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Rivals;
using PolicyField = RealLifeOverhaul.Economy.PolicyField;

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
        RivalFirmChecks();  // the rivals' businesses hire, let go and lease by the same rules (RivalFirmChecks.cs)
        RivalSeatChecks();  // ...on the game's own rival businesses: their people, wages and rent (RivalSeatChecks.cs)
        CompanyRun();       // a hundred and twenty days of all of it together, a save on day 30 and an old save (this file)
        CompanyRunPins();   // the director's source calls what the run calls, in the order the run calls it (this file)
        CompanyDayVariants();   // the day entries through their switches, a long jump of days and the market rent (CompanyDayChecks.cs)
        CompanyLocaleChecks();  // the words of the screen in pt and zh-cn are not English (CompanyLocaleChecks.cs)
        CompanyDoesChecks();    // every action of the screen through the live map against a recording director (CompanyDoesChecks.cs)
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
        public readonly List<StaffFile> Board = new List<StaffFile>();   // people the city has no work for, who may be hired: their wage, going rate and skill
        public long Cash = 4000000;
        public int Day, Hour;
        public bool ManagersOn = true, HierarchyOn = true;           // the switches (CompanyDay's gates)
        public bool Broke;                                            // the cash is not there today: nothing can be paid
        public readonly bool[] PartOff = new bool[5];                // the options of the parts, off
        // What the days did: the changes of state the run asserts, and the NPC side's acts.
        public int LateCounted, LetGo, Hired, Asked, Answered, Bonuses, Speeds, LeaseSpeeds, Offers, Renewals, Holds, ManagerLeases, OwnerLeases, CityRenewals, TrendKept;
        public int HandedDown, ExecSettled, ExecPassed, Closed, Reserved, Filed, Applied, Granted, PlanTurns, Floors;
        public int PayFailed, OffSick, Escalated, OwnerNew, LeaseNotices, PassedUp, KeptQuiet, Skipped, SilentRenewals, OwnerExceptions, OwnerAgreed, BonusBroke, Haggles;
        public int RivalRenewals;
        public long RivalPaid;
        public RivalFile Rivals = new RivalFile();                    // the tycoons and their businesses (RivalFirms)
        public int PlanNow;                                           // the plan of the place in use now
        public readonly List<string> NoticeLog = new List<string>();  // "place@day" for each lease notice said
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
            // Every fifth person's going rate is far above their pay: a raise past even the people chief's ceiling; the others ask for a raise inside it.
            w.S.Staff.Add(new StaffFile { Id = "e" + i, Place = i < 6 ? places[0] : places[1], Name = "Person " + i, Going = i % 5 == 0 ? 2600 : 1900, Wage = i % 3 == 0 || i % 5 == 0 ? 1650 : 1900,
                Skill = 30 + i * 6 });
        }
        for (int i = 0; i < 14; i++)
        {
            w.Board.Add(new StaffFile { Id = "b" + i, Name = "Applicant " + i, Going = 1900, Wage = 1500 + (i * 37) % 450, Skill = 20 + (i * 5) % 60 });
        }
        // A manager at Broadway: raises, hiring and leases given, the hand-down switch on, a month's budget, and a lease limit of none (any rise is the owner's).
        var m = new ManagerFile
        {
            Place = places[0], Id = "m0", Name = "Ray Walsh", Role = ManagerCatalogue.RolePlace, From = 1, Skill = 70, Seen = true, HandDown = true, Budget = 4, LeaseStep = 0, MarketingStep = 3, WageBand = 0,
            Keep = 6, Tasks = ManagerRules.Bit(ManagerTask.Raises) | ManagerRules.Bit(ManagerTask.Hiring) | ManagerRules.Bit(ManagerTask.Leases)
        };
        w.M.Managers.Add(m);
        // A second at Mill: leases only, a limit wide enough for the landlord's draw, so the manager answers inside it.
        w.M.Managers.Add(new ManagerFile { Place = places[3], Id = "m1", Name = "Mia Cole", Role = ManagerCatalogue.RolePlace, From = 1, Skill = 60, Seen = true, Budget = 2, LeaseStep = 5, Tasks = ManagerRules.Bit(ManagerTask.Leases) });
        w.M.MonthFrom = Epoch + 1;
        // The executives are in post: the people chief takes what is handed down, the finance chief tops the reserve up.
        w.H.Executives.Add(new ExecutiveFile { Area = (int)ExecSeat.Coo, Id = "x1", Name = "Ethan Brooks" });
        w.H.Executives.Add(new ExecutiveFile { Area = (int)ExecSeat.Chro, Id = "x2", Name = "Priya Nair" });
        w.H.Executives.Add(new ExecutiveFile { Area = (int)ExecSeat.Cfo, Id = "x3", Name = "Noah Feldman" });
        // The company's policy: a raise is at most five per cent; nobody is named to approve what the band keeps a manager from agreeing, until the owner names one.
        w.H.Company.Start();
        w.H.Company.Steps[(int)PolicyField.RaiseMost] = 1;   // (the owner names the people chief as approver on day 45 and tightens the margin on day 80: CoHierarchy)
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
        // A rival tycoon in play with an outlet in Midtown that opened some time ago, its lease ending on day 55 of the run: its crew and its lease run in the days below
        // (CoRivals) by the same staff and lease rules the player's company runs by.
        w.Rivals = new RivalFile { Seed = seed };
        w.Rivals.Tycoons[0].ActiveDay = 1;
        w.Rivals.Tycoons[0].Worth = RivalStart;
        var move = new RivalMove { Id = 1, Tycoon = 0, Kind = RivalMoveKind.Outlet, State = RivalMoveState.Running, Land = Epoch - 100, End = Epoch + 200, District = "Midtown", Type = "restaurant", Place = "Broadway~12" };
        w.Rivals.Moves.Add(move);
        RivalOutlet outlet = RivalFirms.Open(move, seed, Epoch - 100, RunMonth, RunIndex);
        outlet.Lease.Start = Epoch + 55 - outlet.Lease.Term;
        w.Rivals.Outlets.Add(outlet);
        return w;
    }

    private const long RivalStart = 5000000;

    // One game day, in the order of the director's (CompanyDirector.Day.cs): the parts' day (staff, tax, licences), the managers' day, the leases' day and the
    // hierarchy's day, and then the game's hours, at which the managers decide (CompanyDirector.Managers.cs Hour): everything the managers and the hierarchy
    // decide is CompanyDay's, the one code the director calls.
    private static void CoDay(CoWorld w, int day)
    {
        w.Day = day;
        w.Hour = 0;
        w.S.LastDay = day;
        w.S.Age++;
        w.Broke = day >= Epoch + 20 && day <= Epoch + 24;   // a short spell with no cash to spare
        int before = w.LateCounted + w.LetGo + w.Asked + w.Answered + w.Offers + w.Renewals + w.CityRenewals + w.HandedDown + w.Closed + w.Applied + w.Granted + w.PlanTurns + w.PassedUp + w.ExecPassed;
        var world = new CoDayWorld(w);
        CoRivals(w, day);
        CoStaff(w, day);
        CoTax(w, day);
        CoLicences(w, day);
        CoManagersDay(w, day);
        CoLeases(w, world, day);
        CoHierarchy(w, world, day);
        CoHours(w, world, day);
        CoRotaPlan(w, day);
        int after = w.LateCounted + w.LetGo + w.Asked + w.Answered + w.Offers + w.Renewals + w.CityRenewals + w.HandedDown + w.Closed + w.Applied + w.Granted + w.PlanTurns + w.PassedUp + w.ExecPassed;
        w.Log.Add(day + ":" + (after - before) + ":" + w.Cash + ":" + w.S.Tax.Reserve + ":" + w.PlanNow);
    }

    // The rival tycoons' day (RivalDirector.PlayDay calls RivalFirms.Day the same way): their outlets' crews and leases run, what they pay comes off the tycoon's worth, and the
    // rises their landlords ask are kept with the month's for the trend the player's landlords ask by.
    private static void CoRivals(CoWorld w, int day)
    {
        w.RivalPaid += RivalFirms.Day(w.Rivals, w.Seed, day, RunMonth, LeaseRules.TrendOf(w.L.Trend), RunIndex, (district, rise) =>
        {
            w.L.RivalRenewed(rise);
            w.RivalRenewals++;
        });
    }

    // What the city pays for the work of a person, hundredths an hour: their own rate (every fifth of the first crew is paid a good deal under it), and the city's moves.
    private static int CoGoing(StaffFile f, int day)
    {
        int index;
        int own = f.Id.Length > 1 && f.Id[0] == 'e' && int.TryParse(f.Id.Substring(1), out index) && index % 5 == 0 ? 2600 : 1900;
        return own * (1000 + (day >= Epoch + 45 ? 100 : 0) + (day >= Epoch + 80 ? 100 : 0)) / 1000;
    }

    // The staff part's day: the day's lateness counted from the hours worked against the plan, a person underpaid long enough asks for a raise.
    private static void CoStaff(CoWorld w, int day)
    {
        for (int i = 0; i < w.S.Staff.Count; i++)
        {
            StaffFile f = w.S.Staff[i];
            f.Going = CoGoing(f, day);   // the going rate moves with the city: ten per cent up on day 45 and again on day 80
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

    // The managers' day (CompanyDirector.ManagerDay): the month's report turns the month over for each of them.
    private static void CoManagersDay(CoWorld w, int day)
    {
        if (!ManagerRules.MonthOver(w.M.MonthFrom, day, RunMonth))
        {
            return;
        }
        for (int i = 0; i < w.M.Managers.Count; i++)
        {
            w.M.Managers[i].EndMonth();
        }
        w.M.MonthFrom = day;
    }

    // The game's hours after the day pass (CompanyDirector.Managers.cs: Hour): each kind of decision at its hour (ManagerRules.Due), and the hierarchy's hour after the
    // staff hour, when an exception to the pay band goes to its approver at once.
    private static void CoHours(CoWorld w, CoDayWorld world, int day)
    {
        for (int hour = 1; hour < 24; hour++)
        {
            w.Hour = hour;
            if (w.ManagersOn)
            {
                for (int t = 0; t < (int)ManagerTask.Count; t++)
                {
                    var task = (ManagerTask)t;
                    if ((task != ManagerTask.Raises && task != ManagerTask.Leases && task != ManagerTask.Hiring) || !ManagerRules.Due(day, hour, task))
                    {
                        continue;
                    }
                    for (int i = 0; i < w.M.Managers.Count; i++)
                    {
                        CoRound(w, world, w.M.Managers[i], task, day, hour);
                    }
                }
            }
            if (w.HierarchyOn && hour == ManagerCatalogue.StaffHour)
            {
                CoHandle(w, world, day, hour, true);
            }
        }
        w.Hour = 0;
    }

    // One manager's round of one kind of decision (CompanyDirector.Managers.cs: Round): the gates are CompanyDay's, so is every decision after them.
    private static void CoRound(CoWorld w, CoDayWorld world, ManagerFile m, ManagerTask task, int day, int hour)
    {
        int skill, review;
        string who;
        if (!CompanyDay.Deciding(world, m.Place, ManagedKind.Shop, m, task, out skill, out review, out who))
        {
            return;
        }
        switch (task)
        {
            case ManagerTask.Raises:
                for (int i = 0; i < w.S.Staff.Count; i++)
                {
                    StaffFile f = w.S.Staff[i];
                    if (f.AskDay > 0 && CompanyDay.Raise(world, m.Place, m, f, skill, review, who, day, hour))
                    {
                        Decision d = w.M.Decisions[w.M.Decisions.Count - 1];
                        w.Answered++;
                        w.Bonuses += d.F == (int)RaiseAnswer.Bonus ? 1 : 0;
                        w.BonusBroke += d.F == (int)RaiseAnswer.Bonus && w.Broke ? 1 : 0;
                    }
                }
                break;
            case ManagerTask.Leases:
                int speeds = w.Speeds;
                LeaseHour was = CompanyDay.Lease(world, m.Place, m, w.L.Of(m.Place), skill, review, who, day, hour);
                w.LeaseSpeeds += w.Speeds - speeds;
                w.ManagerLeases += was == LeaseHour.Accepted || was == LeaseHour.Declined ? 1 : 0;
                w.PassedUp += was == LeaseHour.PassedUp ? 1 : 0;
                w.KeptQuiet += was == LeaseHour.Kept ? 1 : 0;
                break;
            default:
                CoHiring(w, world, m, skill, review, who, day, hour);
                break;
        }
    }

    // Hiring: somebody three days late is let go if the board has somebody for their place, and the place is filled again from the board by the policy's pick.
    private static void CoHiring(CoWorld w, CoDayWorld world, ManagerFile m, int skill, int review, string who, int day, int hour)
    {
        string place = m.Place;
        int count;
        int[] wages, goings, skills;
        Func<bool> replacement = () =>
        {
            CoCandidates(w, out count, out wages, out goings, out skills);
            return count > 0 && PolicyRules.Pick(wages, goings, skills, count, world.Rule(place, PolicyField.PayBand), world.Rule(place, PolicyField.HirePayCap), world.Rule(place, PolicyField.SkillFloor),
                ManagerRules.Left(world.BudgetOf(m), m.Spent), false) >= 0;
        };
        int staff = w.S.Staff.FindAll(x => x.Place == place).Count;
        if (CompanyDay.LetGo(world, place, m, staff, replacement, who, day, hour))
        {
            staff--;
        }
        if (!CompanyDay.Wants(m, staff))
        {
            return;
        }
        CoCandidates(w, out count, out wages, out goings, out skills);
        string name;
        CompanyDay.Taker take = (int pick, out string id, out string called) =>
        {
            StaffFile c = w.Board[pick];
            id = c.Id;
            called = c.Name;
            w.Board.RemoveAt(pick);
            w.S.Staff.Add(new StaffFile { Id = c.Id, Place = place, Name = c.Name, Going = c.Going, Wage = c.Wage, Skill = c.Skill });
            return true;
        };
        w.Hired += count > 0 && CompanyDay.Hire(world, place, m, count, wages, goings, skills, take, skill, review, who, day, hour, out name) == CompanyDay.HireWas.Hired ? 1 : 0;
    }

    // The job board's candidates as the arrays the pick looks at: their pay, the going rate for them and their skill.
    private static void CoCandidates(CoWorld w, out int count, out int[] wages, out int[] goings, out int[] skills)
    {
        count = w.Board.Count;
        wages = new int[count];
        goings = new int[count];
        skills = new int[count];
        for (int i = 0; i < count; i++)
        {
            wages[i] = w.Board[i].Wage;
            goings[i] = w.Board[i].Going;
            skills[i] = w.Board[i].Skill;
        }
    }

    // What a rival buying up Midtown adds to the rises landlords ask there (CompanyBridges.Pressure), per mille: from day 10 of the run.
    private static int RunPressure(string district, int day)
    {
        return district == "Midtown" && day >= Epoch + 10 ? 100 : 0;
    }

    // Days the owner takes to answer a lease question, by place: Elm's owner answers on the third day (asking the landlord to come down on the second), Broadway's is
    // away and does not answer at all.
    private static int OwnerDelay(string place)
    {
        return place == "Elm~7" ? 3 : 0;
    }

    // The leases' day (CompanyDirector.LeaseDay): the city's month, the rise paid, a lease that ends renewed or held, an offer made 14 days before its end (CompanyDay.Offer:
    // the manager with the Leases task answers inside their limit and past it asks the owner, once; where nobody manages the place the owner is asked at once), and the
    // owner's own answer on a day of their own.
    private static void CoLeases(CoWorld w, CoDayWorld world, int day)
    {
        int monthRenewed = LeaseRules.CityMonth(w.L, w.Seed, RunDistricts, day, RunMonth, RunPressure);
        if (monthRenewed >= 0)
        {
            w.CityRenewals += monthRenewed;
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
            ManagerFile m = w.M.At(place);
            bool manager = w.ManagersOn && m != null && ManagerRules.Runs(m.Tasks, ManagedKind.Shop, ManagerTask.Leases);
            // The very step the director's day takes (CompanyDay.LeaseShop): a lease made for premises seen first, the rise, the end settled or the offer made.
            CompanyDay.LeaseDayWas was = CompanyDay.LeaseShop(world, w.L, place, place, i % 2 == 0 ? "Midtown" : "Harbour", rents[i], manager, LeaseRules.TrendOf(w.L.Trend), RunMonth, day);
            LeaseFile l = was.Lease;
            w.Cash -= was.TopUp;
            if (was.Settled)
            {
                w.Renewals++;
                w.Holds += l.Hold ? 1 : 0;
                w.SilentRenewals += was.Silent ? 1 : 0;
                CompanyDay.LeaseClosed(world, place, day);
            }
            else if (was.Offered)
            {
                w.Offers++;
                w.Renewals += was.Offer == OfferWas.Renewed ? 1 : 0;
                w.Skipped += was.Offer == OfferWas.Renewed ? 1 : 0;
            }
            else if (l.Told != 0 && LeaseRules.Waiting(l, day) && OwnerDelay(place) > 0)
            {
                int wait = day - l.Told;
                if (wait == OwnerDelay(place) - 1)
                {
                    bool asked;
                    CompanyDay.OwnerHaggles(world, place, l, day, out asked);
                    w.Haggles += asked ? 1 : 0;
                }
                else if (wait >= OwnerDelay(place))
                {
                    w.OwnerLeases += CompanyDay.OwnerAnswers(world, place, l, (day + i) % 3 != 0, "You", day) ? 1 : 0;
                }
            }
        }
        w.L.Leases.Sort((a, b) => a.End.CompareTo(b.End));
    }

    // The hierarchy's day (CompanyDirector.HierarchyDay): a question the owner left a day is handed down to the area's executive (CompanyDay.HandDown), every question
    // held below the owner is tried by its holder (CoHandle), and the owner answers the exceptions that stay with them, a few days on. The owner names the people chief
    // as the approver on day 45, and narrows the pay band and the margin the approver may go over it by on day 80.
    private static void CoHierarchy(CoWorld w, CoDayWorld world, int day)
    {
        if (!w.HierarchyOn)
        {
            return;
        }
        if (day == Epoch + 45)
        {
            w.H.Company.Steps[(int)PolicyField.Approver] = PolicyCatalogue.ApprovePeople;
        }
        if (day == Epoch + 80)
        {
            w.H.Company.Steps[(int)PolicyField.PayBand] = 0;
            w.H.Company.Steps[(int)PolicyField.ExceptionMargin] = 0;
        }
        w.HandedDown += CompanyDay.HandDownRound(world, "You", day, 0);
        CoHandle(w, world, day, 0, false);
        for (int i = 0; i < w.H.Escalations.Count; i++)
        {
            Escalation e = w.H.Escalations[i];
            if (!e.Open || e.Kind != (int)EscalationKind.Exception || e.Level < EscalationRules.LevelOwner || day - e.Moved < 4)
            {
                continue;
            }
            // The owner's own answer: agree it, or say no, by turns.
            bool agree = (e.Id & 1) == 0;
            int agreed;
            if (agree)
            {
                agree = CompanyDay.Approve(world, e, e.Place, EscalationRules.LevelOwner, "You", true, day, 0, out agreed);
            }
            else
            {
                StaffFile f = w.S.Employee(e.Subject);
                if (f != null && f.AskDay > 0)
                {
                    StaffRules.Answer(f, RaiseAnswer.Refuse, day);
                }
            }
            if (e.Open)
            {
                w.H.Close(e, EscalationState.Resolved, "You", day);
                w.OwnerExceptions++;
                w.OwnerAgreed += agree ? 1 : 0;
            }
        }
    }

    // Every question held below the owner tried once by its holder (CompanyDirector.Handle): the holder settles it only if they are in post, the policy lets them, and
    // the settle for its kind (the exceptions' own approval, CompanyDay.Approve) says it is settled; otherwise it goes up. What is counted is what the settle returned.
    private static void CoHandle(CoWorld w, CoDayWorld world, int day, int hour, bool exceptionsOnly)
    {
        CompanyDay.HandleRound(world, day, hour, exceptionsOnly);
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
        return new[] { CompanyStore.Write(w.S), ManagerStore.Write(w.M), HierarchyStore.Write(w.H), LeaseStore.Write(w.L), RotaPlanStore.Write(w.Plans), RivalStore.Write(w.Rivals) };
    }

    // What the game keeps that a save does not: the cash, the board, the names and going rates of the staff, the managers' skill, the plan on now.
    private static CoWorld CoSnap(CoWorld w)
    {
        var s = new CoWorld { Seed = w.Seed, Cash = w.Cash, Day = w.Day, PlanNow = w.PlanNow, LetGo = w.LetGo, Renewals = w.Renewals, HandedDown = w.HandedDown, ExecSettled = w.ExecSettled, ExecPassed = w.ExecPassed,
            PassedUp = w.PassedUp, OwnerExceptions = w.OwnerExceptions, OwnerLeases = w.OwnerLeases };
        s.Board.AddRange(CoBoard(w.Board));
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
        w.Board.AddRange(CoBoard(game.Board));
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
        w.M.Today = m.Today;
        w.M.Managers.AddRange(m.Managers);
        w.M.Decisions.AddRange(m.Decisions);
        w.H.Executives.AddRange(h.Executives);
        Array.Copy(h.Company.Steps, w.H.Company.Steps, h.Company.Steps.Length);
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
        w.L.RivalSum = l.RivalSum;
        w.L.RivalCount = l.RivalCount;
        RivalFile rivals = lines.Length > 5 && lines[5] != null ? RivalStore.Read(lines[5], out v) : null;
        w.Rivals = rivals ?? w.Rivals;
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

    // The job board's people copied: what the game keeps, put into the second company as the game would have it.
    private static List<StaffFile> CoBoard(List<StaffFile> board)
    {
        var copy = new List<StaffFile>(board.Count);
        foreach (StaffFile c in board)
        {
            copy.Add(new StaffFile { Id = c.Id, Name = c.Name, Going = c.Going, Wage = c.Wage, Skill = c.Skill });
        }
        return copy;
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
        string[] names = { "company", "managers", "hierarchy", "leases", "plans", "rivals" };
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

    // A check whose words carry a measured cost: the words are said as well, so that the cost is in the run's output (the Company app's cost lines).
    private static void CheckSay(bool ok, string words)
    {
        Console.WriteLine("    " + words);
        Check(ok, words);
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
            int ray = r.IndexOf("Ray Walsh", StringComparison.Ordinal), mia = r.IndexOf("Mia Cole", StringComparison.Ordinal);
            return ray >= 0 ? r.Substring(0, ray + "Ray Walsh".Length) : mia >= 0 ? r.Substring(0, mia + "Mia Cole".Length) : r;
        });
        string hier = CutPart(lines[2], "^e:", "^l:", r => r.Length == 0 ? r : r.Substring(0, Math.Max(0, r.LastIndexOf('|'))));
        string rivals = lines[5].IndexOf("\nO|", StringComparison.Ordinal) >= 0 ? lines[5].Substring(0, lines[5].IndexOf("\nO|", StringComparison.Ordinal)) : lines[5];   // (no businesses of theirs yet)
        return new[] { company, manager, hier, null, null, rivals };
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
        Check(a.LateCounted >= 5 && a.LetGo >= 1 && a.Hired >= 1 && a.S.Staff.Count >= RunKeep - 1 && a.S.Staff.Count <= RunKeep, "company run: lateness is counted, a person three days late is let go (CompanyDay.LetGo, which needs the board to have somebody) and the place filled from the board by the policy's pick (CompanyDay.Hire), the crew holds at " + RunKeep
              + " (" + a.LateCounted + " late days, " + a.LetGo + " let go, " + a.Hired + " hired, " + a.S.Staff.Count + " on the staff)");
        Check(a.Asked >= 1 && a.Answered >= 1 && a.Speeds >= a.Answered, "company run: a person underpaid long enough asks, the manager answers (CompanyDay.Raise), and the days it waited go in their ring of speed (" + a.Asked + " asked, " + a.Answered + " answered)");
        ManagerFile m = a.M.Managers[0];
        Check(m.Speeds.Count >= 5 && m.Speeds.Count <= ManagerCatalogue.SpeedKept && ManagerRules.Speed(m.Speeds) >= 0 && ManagerRules.Speed(m.Speeds) <= 100 && m.Decided + m.LastDecided > 0, "company run: the ring of settled waits fills and grades speed inside its bounds, and the decisions are counted for the manager who made them");
        Check(a.Bonuses >= 1 && a.Bonuses <= a.Answered, "company run: where the manager's answer would be a refusal and the month's budget covers it, it is a bonus (" + a.Bonuses + " of " + a.Answered + ")");
        Check(a.PayFailed >= 1 && a.BonusBroke == 0, "company run: with the cash not there for some days the bonus cannot be paid, so the request is refused as before and no bonus is written down (" + a.PayFailed + " payments failed, " + a.BonusBroke + " bonuses given then)");
        Check(a.OffSick >= 1, "company run: a manager off sick decides nothing that day (CompanyDay.Deciding asks who decides): " + a.OffSick + " manager hours missed");
        Check(a.Offers >= 3 && a.Renewals >= 1 && a.ManagerLeases >= 1 && a.OwnerLeases >= 1, "company run: the landlord's offers come before the renewals and are answered, by the manager inside their limit and by the owner where there is none ("
              + a.Offers + " offers, " + a.Renewals + " renewals, " + a.ManagerLeases + " by the manager, " + a.OwnerLeases + " by the owner)");
        int leaseQuestions = a.H.Escalations.FindAll(x => x.Kind == (int)EscalationKind.Lease).Count, distinctNotices = new HashSet<string>(a.NoticeLog).Count;
        Check(a.PassedUp >= 1 && a.KeptQuiet >= 8, "company run: an offer past the manager's limit is passed up to the owner once, and the owner's silence for days after it is only kept quiet (" + a.PassedUp + " passed up, " + a.KeptQuiet + " manager hours that found it open and said nothing)");
        Check(a.LeaseSpeeds == a.ManagerLeases + a.PassedUp, "company run: a lease question puts one entry in the manager's ring of waits, when they answer it or pass it up, however many days the owner stays silent (" + a.LeaseSpeeds + " entries for "
              + a.ManagerLeases + " answered and " + a.PassedUp + " passed up)");
        Check(a.NoticeLog.Count == distinctNotices && a.LeaseNotices == leaseQuestions && leaseQuestions >= 2,
            "company run: the owner is told of an offer once, and the question is raised once (" + a.LeaseNotices + " notices, " + leaseQuestions + " lease questions, " + distinctNotices + " different)");
        Check(a.SilentRenewals >= 1, "company run: a lease the owner never answered renews at the landlord's own figure on its day (" + a.SilentRenewals + ")");
        Check(a.TrendKept >= 2 && a.L.Trend.Count >= 2 && a.CityRenewals >= 1, "company run: the city's own leases renew by the same rule and set the trend: " + a.TrendKept + " months kept, " + a.CityRenewals + " city renewals");
        Check(a.HandedDown >= 1 && a.ExecSettled >= 1 && a.ExecPassed >= 1 && a.OwnerExceptions >= 1,
            "company run: an exception goes to the people chief, who settles it only when their approval says so and otherwise passes it up to the owner, who answers it (" + a.HandedDown + " handed down, " + a.ExecSettled + " settled by the executive, "
            + a.ExecPassed + " passed back up, " + a.OwnerExceptions + " answered by the owner)");
        int settledEntries = a.H.Entries.FindAll(x => x.Kind == (int)HierarchyLog.Settled && x.Level == EscalationRules.LevelExecutive).Count, decided = 0;
        a.H.Executives.ForEach(x => decided += x.Decided + x.LastDecided);
        Check(settledEntries == a.ExecSettled && decided == a.ExecSettled && a.H.Settled == a.ExecSettled + a.OwnerExceptions + a.OwnerLeases,
            "company run: what is called settled is what the settle returned: the log's settled entries (" + settledEntries + "), the executives' counts (" + decided + ") and the week's settled (" + a.H.Settled + ") agree with the " + a.ExecSettled
            + " the run saw settled");
        Check(a.Closed >= 1 && a.Reserved >= 1 && a.Filed >= 1 && a.S.Tax.Settled.Count >= 1 && a.S.Tax.Settled.Count <= TaxRules.SettledKept && a.S.Tax.Reserve > 0,
            "company run: a quarter closes, the finance chief tops the reserve up, the return is filed and the ring of past returns fills (" + a.Closed + " closed, " + a.Reserved + " topped up, " + a.S.Tax.Settled.Count + " kept)");
        Licence permit = a.S.Shops[0].Permits[(int)LicenceKind.OutdoorSeating];
        Check(LicenceRules.Decision((int)LicenceKind.OutdoorSeating) > 0 && a.Applied == 1 && a.Granted == 1 && permit.Until > 0 && !permit.Pending, "company run: an application for a permit waits for the city's decision day, is granted on it and holds a term");
        Check(a.PlanTurns >= 3 && a.Floors >= 1, "company run: the plan in use turns to the holiday week for its days and back, the odd weeks' plan on odd weeks, and the holiday week's floor of crew is put under the planner (" + a.PlanTurns + " turns)");
        RivalOutlet outlet = a.Rivals.Outlets.Count > 0 ? a.Rivals.Outlets[0] : null;
        Check(outlet != null && outlet.Hires >= RivalFirms.CrewStart && outlet.Hires - outlet.LetGo == outlet.Crew.Count && a.RivalPaid > 0 && a.Rivals.Tycoons[0].Worth == RivalStart - a.RivalPaid,
            "company run: a rival tycoon's outlet hires (StaffRules.Hire), lets go (StaffRules.LetGo) and pays rent and wages out of the tycoon's worth, in the rival day (" + (outlet != null ? outlet.Hires + " hired, " + outlet.LetGo + " let go, " : string.Empty)
            + a.RivalPaid + " paid)");
        Check(outlet != null && outlet.Renewals >= 1 && a.RivalRenewals == outlet.Renewals && a.CityRenewals >= a.RivalRenewals,
            "company run: the rival's lease renews by the lease rules and its rise goes into the city's month, which sets the trend the player's landlords ask by (" + a.RivalRenewals + " rival renewals of " + a.CityRenewals + " counted)");

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
        bool loads = aged.S.Staff.Count == RunKeep && aged.M.Managers.Count == 2 && !aged.M.Managers[0].HandDown && aged.M.Managers[0].Speeds.Count == 0 && aged.L.Leases.Count == 0 && aged.Plans.Count == 0
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
                          + a.ExecSettled + " settled by the executive, " + a.Closed + " quarter closed and " + a.Reserved + " topped up, " + a.PlanTurns + " plan turns, " + a.Rivals.Outlets.Count + " rival outlet(s) with " + (a.Rivals.Outlets.Count > 0 ? a.Rivals.Outlets[0].Hires + " hired and " + a.Rivals.Outlets[0].LetGo + " let go" : "no crew"));
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
        string game = SourceText(dir, "CompanyGame.cs"), staff = SourceText(dir, "CompanyDirector.Staff.cs"), rotaRead = SourceText(dir, "RotaGame.Read.cs"), day2 = SourceText(dir, "CompanyDay.cs");
        Check(RunInOrder(day, "Read(day, false, true", "PartDay(part, day)", "ReserveFreed()", "ManagerDay(day)", "LeaseDay(day)", "Books(day, next)", "HierarchyDay(day)")
              && reserve.Contains("TaxRules.Freed(_state.Tax, On(CompanyPart.Tax))"),
            "company run pins: the day reads, runs the parts, frees a reserve the tax part no longer runs, then the managers, the leases, the books and the hierarchy, as the run does");
        var held = new TaxFile { Reserve = 1840 };
        int kept = TaxRules.Freed(held, true), freed = TaxRules.Freed(held, false);
        Check(kept == 0 && freed == 1840 && held.Reserve == 0 && TaxRules.Freed(held, false) == 0, "company run pins: money set aside goes back whole, once (" + freed + "), which the day does when the tax part is off");
        Check(RunInOrder(leases, "CityMonth(day, monthDays)", "CompanyDay.LeaseShop(this", "Settled(l, shop, day)", "OfferMade(l, shop, was, day)")
              && RunInOrder(day2, "public static LeaseDayWas LeaseShop(", "LeaseRules.Begin(", "LeaseRules.TopUp(l)", "LeaseRules.Settle(l, day", "LeaseRules.OfferDue(l, day)", "Offer(w, place, l, managerRuns"),
            "company run pins: the lease day: the city's month, then one shared step for each place (CompanyDay.LeaseShop: a lease begun, the rise worked out, the lease settled or the offer made), then the director's notices");
        Check(leases.Contains("CompanyDay.Lease(this") && day2.Contains("Offer(w, place, l, managerRuns") && leases.Contains("CompanyDay.OwnerAnswers(this") && leases.Contains("CompanyDay.OwnerHaggles(this")
              && day2.Contains("LeaseRules.Within(l, limit)") && day2.Contains("LeaseRules.Respond(l, day, accept)") && leases.Contains("LeaseRules.CityMonth(_leases") && leases.Contains("_leases.RivalRenewed("),
            "company run pins: the offer, the manager's limit and answer, the owner asked past it once, the owner's answers, and the city's renewals that keep the trend");
        Check(RunInOrder(tax, "ReserveYear(day)", "TaxRules.QuarterOver(t, day, QuarterDays)", "TaxRules.Close(t, takings", "ReserveClose(day)", "FileReturn(true)", "TaxRules.LateDay(t, day)")
              && reserve.Contains("TaxRules.TopUp(t, CompanyGame.Cash)") && reserve.Contains("TaxRules.Year(t,"),
            "company run pins: the tax day: the year, the quarter's close, the finance chief's top-up, the return filed, the late day, in the run's order");
        Check(esc.Contains("CompanyDay.HandDownRound(this") && esc.Contains("CompanyDay.HandleRound(this") && esc.Contains("CompanyDay.TryOne(this") && esc.Contains("CompanyDay.Approve(this")
              && day2.Contains("HandDown(h, e, w.Managers.At(e.Place)") && day2.Contains("Try(e, inPost,") && day2.Contains("Settled(h, e, who,") && day2.Contains("PassedUp(h, e, next,")
              && day2.Contains("EscalationRules.HandsDown(") && day2.Contains("EscalationRules.Tries(") && day2.Contains("public static bool Approve("),
            "company run pins: the hierarchy's day hands down by CompanyDay.HandDown, the holder's try is CompanyDay.Try with the settle's own result, and what settles an exception is CompanyDay.Approve");
        Check(decisions.Contains("CompanyDay.Raise(this") && day2.Contains("ManagerRules.OrBonus(") && day2.Contains("ManagerRules.AddSpeed(") && day2.Contains("ManagerRules.RaiseAnswerFor(") && decisions.Contains("CompanyDay.LetGo(this")
              && day2.Contains("ManagerRules.LetGo(worst.LateDays, replacement())") && game.Contains("StaffRules.LateDay(") && staff.Contains("StaffRules.Day(f, _level, day") && decisions.Contains("CompanyDay.Hire(this"),
            "company run pins: a manager's answer is CompanyDay.Raise (a bonus where a refusal would be, OrBonus, and its wait in the ring, AddSpeed), the let-go is CompanyDay.LetGo, the hire CompanyDay.Hire, lateness is counted by StaffRules.LateDay and an ask comes from StaffRules.Day");
        // The director and the run call the very same entries: each is called from the director's source and from the run's, so that neither can do it another way.
        string runText = RunSource();
        string[] entries = { "Deciding", "Raise", "Lease", "LeaseShop", "LetGo", "Hire", "HandDownRound", "HandleRound", "Approve", "OwnerAnswers", "OwnerHaggles", "LeaseClosed" };
        string missing = string.Empty;
        string directors = decisions + leases + esc + managers + SourceText(dir, "CompanyDirector.DayWorld.cs");
        foreach (string entry in entries)
        {
            missing += directors.Contains("CompanyDay." + entry + "(") && runText.Contains("CompanyDay." + entry + "(") ? string.Empty : " " + entry;
        }
        Check(missing.Length == 0 && runText.Contains(": ICompanyDay") && SourceText(dir, "CompanyDirector.DayWorld.cs").Contains("ICompanyDay"), "company run pins: the director and the run call the same day entries (CompanyDay), and each answers ICompanyDay for them" + (missing.Length > 0 ? ", not these:" + missing : string.Empty));
        Check(decisions.Contains("ManagerRules.Move(now, fill") && decisions.Contains("d.Detail = move;") && SourceText(dir, "CompanyApp.Managers.cs").Contains("reallife_mgr_did_rota_moved"),
            "company run pins: a week the manager writes that moves somebody from one station to another says so in the log (ManagerRules.Move, the decision's detail, the words)");
        Check(plans.Contains("RotaPlanRules.InUse(") && rotaRead.Contains("RotaPlanRules.FloorCells(") && lic.Contains("LicenceRules.Day(") && lic.Contains("LicenceRules.Grant("),
            "company run pins: the rota write asks RotaPlanRules.InUse and puts its floor under the planner, and the licence day asks LicenceRules.Day and grants on the decision day");
        // The live source reads the director and holds no sample: no invented name of the previews and no money literal in any of its files.
        string ui = dir.Replace(System.IO.Path.Combine("Scripts", "Economy", "Company"), System.IO.Path.Combine("Scripts", "Ui", "Company"));
        string[] invented = { "Ray Walsh", "Dev Kapoor", "Tessa Brandt", "Meera Iyer", "Lena Adler", "Pioneer", "Speedy Bites", "Bloom & Co", "Hudson", "Corner Cafe", "Noah Feldman", "Ethan Brooks", "Priya Nair", "Maya Okafor", "Alex Moreno", "Huang Guo" };
        string found = "none";
        int live = 0;
        if (System.IO.Directory.Exists(ui))
        {
            foreach (string file in System.IO.Directory.GetFiles(ui, "CompanyLive*.cs"))
            {
                live++;
                string text = File.ReadAllText(file);
                foreach (string name in invented)
                {
                    found = text.Contains(name) ? name + " in " + System.IO.Path.GetFileName(file) : found;
                }
                found = System.Text.RegularExpressions.Regex.IsMatch(text, "\"[$][0-9]") ? "a money literal in " + System.IO.Path.GetFileName(file) : found;
            }
        }
        Check(live >= 8 && found == "none", "company run pins: the live source holds no sample (" + live + " live files read; found: " + found + ")");
        Console.WriteLine("  the director's source calls what the run calls, in its order: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    // The source of the run's own world and days: this file and the world it answers the day entries with.
    private static string RunSource()
    {
        string dir = AppContext.BaseDirectory;
        for (int up = 0; up < 9 && dir != null; up++)
        {
            string candidate = System.IO.Path.Combine(dir, "RealLifeOverhaul.Build", "EconomyCheck");
            if (Directory.Exists(candidate))
            {
                return SourceText(candidate, "CompanyRunChecks.cs") + SourceText(candidate, "CompanyRunWorld.cs");
            }
            dir = System.IO.Path.GetDirectoryName(dir);
        }
        return string.Empty;
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
