using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RealLifeOverhaul.Dynasty;
using RealLifeOverhaul.Population;

// The dynasty's review fixes (run with --dynasty): what the estate counts and takes off (the tax reserve, the own bank, loans out, the student and construction loans, the game's
// unpaid taxes, what is owed to friends), the length of a marriage that frees a partner's part, a bequest to a person found by their name too, the heir's courses and brothers and
// sisters, the debt shown and charged for every day whatever the option, the retired founder's monthly living, the page's estate kept for a day, a household that moved is no estate,
// and a gift never lost.
internal static partial class Program
{
    private static void DynastyFixChecks()
    {
        DynastyHoldingsChecks();
        DynastyWeddingChecks();
        DynastyIdentityChecks();
        DynastyHeirFixChecks();
        DynastyDebtFixChecks();
        DynastyElderFixChecks();
        DynastyCacheChecks();
        DynastyEndedChecks();
        DynastyGiftFixChecks();
        DynastyRetaxChecks();
    }

    // ---- what the estate counts (the Finance app's net worth is the base, the cash in it)

    private static void DynastyHoldingsChecks()
    {
        var none = new Holdings { Wealth = 9000000 };
        Check(none.NetWorth == 9000000 && none.Assets == 0 && none.Debts == 0, "dynasty holdings: with nothing besides, the estate is the Finance app's net worth");
        // each line alone moves the estate by exactly itself, up for what is owned and down for what is owed
        string[] names = { "the tax reserve", "the own bank's capital", "loans out", "what friends owe", "the student and construction loans", "the game's tax bill", "back taxes", "an audit's tax", "what is owed to friends" };
        for (int i = 0; i < names.Length; i++)
        {
            var h = new Holdings { Wealth = 9000000 };
            const long amount = 1234567;
            switch (i)
            {
                case 0: h.TaxReserve = amount; break;
                case 1: h.BankCapital = amount; break;
                case 2: h.LoansOut = amount; break;
                case 3: h.FriendsOwe = amount; break;
                case 4: h.CreditLoans = amount; break;
                case 5: h.UnpaidTaxes = amount; break;
                case 6: h.BackTaxes = amount; break;
                case 7: h.AuditOwed = amount; break;
                default: h.FriendsOwed = amount; break;
            }
            long expected = i < 4 ? 9000000 + amount : 9000000 - amount;
            Check(h.NetWorth == expected, "dynasty holdings: " + names[i] + (i < 4 ? " is counted in the estate" : " is taken off the estate") + " (" + h.NetWorth + ")");
        }
        // a freeze-window move into any of the assets is taxed as it would be in the wallet: cash into the reserve, the bank, a loan to a friend
        EstateIn kept = DynastyEstate(10000000, 0, 0);
        long before = DynastyRules.Settle(kept, new Will(), DynastyFamily()[1], true, DynastyFamily()).Tax;
        for (int i = 0; i < 4; i++)
        {
            var h = new Holdings { Wealth = 10000000 - 3000000 };   // three millions left the wallet ...
            switch (i)
            {
                case 0: h.TaxReserve = 3000000; break;
                case 1: h.BankCapital = 3000000; break;
                case 2: h.LoansOut = 3000000; break;
                default: h.FriendsOwe = 3000000; break;
            }
            EstateIn moved = DynastyEstate(10000000 - 3000000, 0, h.NetWorth - (10000000 - 3000000));   // ... and are in an asset the estate counts
            long after = DynastyRules.Settle(moved, new Will(), DynastyFamily()[1], true, DynastyFamily()).Tax;
            Check(after == before, "dynasty holdings: three millions moved into " + names[i] + " just before are taxed all the same (" + after + " against " + before + ")");
        }
        // debts the Finance app does not know come off, so the tax falls by their share and the heir takes them
        var owing = new Holdings { Wealth = 10000000, CreditLoans = 2000000, UnpaidTaxes = 300000, BackTaxes = 100000, AuditOwed = 50000, FriendsOwed = 25000 };
        Statement owes = DynastyRules.Settle(DynastyEstate(10000000, 0, owing.NetWorth - 10000000), new Will(), DynastyFamily()[1], true, DynastyFamily());
        Check(owes.Net == 10000000 - 2475000 && owes.Other == -2475000 && DynastyRules.Balanced(owes), "dynasty holdings: the debts the net worth leaves out are taken off before anything is taxed (" + owes.Net + ")");
        // a negative figure from a feature is no asset and no debt of the other kind
        var bad = new Holdings { Wealth = 100, TaxReserve = -5, BankCapital = -9, LoansOut = -1, FriendsOwe = -2, CreditLoans = -3, UnpaidTaxes = -4, BackTaxes = -5, AuditOwed = -6, FriendsOwed = -7 };
        Check(bad.NetWorth == 100, "dynasty holdings: a figure under nothing counts for nothing");
    }

    // ---- the length of a marriage

    private static void DynastyWeddingChecks()
    {
        const int year = 60;
        int window = DynastyRules.WindowDays(year);
        Check(DynastyRules.WedLongEnough(100, 100 + window, year) && !DynastyRules.WedLongEnough(100, 100 + window - 1, year) && !DynastyRules.WedLongEnough(0, 5000, year)
              && DynastyRules.WedLongEnough(DynastyCatalogue.WedOld, 1, year), "dynasty marriage: the relief is for a marriage at least the window old; one that began before this build counts; none is no marriage");
        // the note of the day: kept, first seen, or before this build
        Check(DynastyRules.NoteWedding(0, true, 250, false, 300) == 250 && DynastyRules.NoteWedding(0, true, 0, false, 300) == 300 && DynastyRules.NoteWedding(300, true, 0, false, 400) == 300
              && DynastyRules.NoteWedding(0, true, 0, true, 300) == DynastyCatalogue.WedOld && DynastyRules.NoteWedding(300, false, 0, false, 400) == 0,
            "dynasty marriage: the day is the one family life kept, else the first day it was seen married, else (a save never seen before) before this build");
        Check(DynastyRules.NoteWedding(DynastyCatalogue.WedOld, true, 0, false, 900) == DynastyCatalogue.WedOld && DynastyRules.NoteWedding(DynastyCatalogue.WedOld, false, 0, false, 900) == 0
              && DynastyRules.NoteWedding(DynastyCatalogue.WedOld, true, 700, false, 900) == 700, "dynasty marriage: a marriage from before stays so until a divorce, and a later wedding day replaces it");
        // a wedding the day before the estate passes frees nothing; the same estate a window later does
        List<KinPerson> family = DynastyFamily();
        family[0].Relief = DynastyRules.WedLongEnough(900, 1000, year);
        Statement fresh = DynastySettle(DynastyEstate(8000000, 0, 2000000), new Will(), family);
        family[0].Relief = DynastyRules.WedLongEnough(900, 900 + window, year);
        Statement aged = DynastySettle(DynastyEstate(8000000, 0, 2000000), new Will(), family);
        Check(fresh.Spouse == 0 && fresh.Taxable == 10000000 && !fresh.SpouseHeir && aged.Spouse == 5000000 && aged.Taxable == 5000000 && aged.SpouseHeir && fresh.Tax > aged.Tax,
            "dynasty marriage: a partner married a few days has no relief (" + fresh.Tax + " tax) and one married for the window has half (" + aged.Tax + ")");
        // ... and for a bequest to the partner the same
        var will = new Will { Written = true, HeirKind = KinKind.Child, HeirName = "Tom" };
        will.Bequests.Add(new Bequest { Kind = KinKind.Partner, Name = "Maya", Share = 300 });
        family[0].Relief = false;
        Statement newlyWed = DynastySettle(DynastyEstate(8000000, 0, 2000000), will, family);
        family[0].Relief = true;
        Statement longWed = DynastySettle(DynastyEstate(8000000, 0, 2000000), will, family);
        Check(newlyWed.Spouse == 0 && !newlyWed.Lines[0].Relief && newlyWed.Taxable == 10000000 && longWed.Spouse == 3000000 && longWed.Lines[0].Relief && longWed.Taxable == 7000000,
            "dynasty marriage: a part left to a partner of a few days is taxed, to one of the window's length it is free up to half");
        family[0].Married = false;
        family[0].Relief = true;
        Check(DynastySettle(DynastyEstate(8000000, 0, 2000000), will, family).Spouse == 0, "dynasty marriage: a partner who is not married has no relief however long they have been together");
        // old and new saves: the day is kept
        var s = new DynastyState { Wed = 777 };
        DynastyState back = DynastyStore.Read(DynastyStore.Write(s), out int _);
        var old = new DynastyState { Wed = DynastyCatalogue.WedOld };
        Check(back.Wed == 777 && DynastyStore.Read(DynastyStore.Write(old), out int _).Wed == DynastyCatalogue.WedOld && DynastyStore.Read("v=1^h:5|1|1|0|0|0|0", out int _).Wed == 0,
            "dynasty marriage: the wedding day is in the save, and a line from before it has none (the first look then notes it)");
    }

    // ---- who is who

    private static void DynastyIdentityChecks()
    {
        List<KinPerson> people = DynastyFamily();
        // the cast gave place 4 to somebody else: a bequest to Nadia (place 4) is not a bequest to them
        people[4] = new KinPerson { Kind = KinKind.Person, Id = 4, Name = "Stranger", Age = 44, Family = true, Closeness = 50, Household = true, Room = 900000000 };
        var will = new Will { Written = true, HeirKind = KinKind.Child, HeirName = "Tom" };
        will.Bequests.Add(new Bequest { Kind = KinKind.Person, Id = 4, Name = "Nadia", Share = 100 });
        Statement s = DynastySettle(DynastyEstate(8000000, 0, 2000000), will, people);
        Check(s.Lines[0].Landing == Landing.None && s.Lines[0].Amount == 0 && s.Paid == 0, "dynasty identity: a bequest to a person whose place in the cast has been given to somebody else lands nowhere and stays in the estate");
        people[4].Name = "Nadia";
        s = DynastySettle(DynastyEstate(8000000, 0, 2000000), will, people);
        Check(s.Lines[0].Landing == Landing.Household && s.Lines[0].Amount == 1000000, "dynasty identity: the same person at the same place receives it");
        var bq = new Bequest { Kind = KinKind.Person, Id = 4, Name = "Nadia" };
        Check(bq.Same(KinKind.Person, 4, "Nadia") && !bq.Same(KinKind.Person, 4, "Stranger") && !bq.Same(KinKind.Person, 5, "Nadia") && new Bequest { Kind = KinKind.Charity, Id = 1 }.Same(KinKind.Charity, 1, null)
              && !new Bequest { Kind = KinKind.Charity, Id = 1 }.Same(KinKind.Charity, 2, null) && new Bequest { Kind = KinKind.Child, Name = "Tom" }.Same(KinKind.Child, 0, "Tom"),
            "dynasty identity: a person is found by their place and their name, a child or a partner by name, a charity by its number");
        // the heir by name too
        var named = new Will { Written = true, HeirKind = KinKind.Person, HeirId = 4, HeirName = "Nadia" };
        people[4].Name = "Stranger";
        KinPerson heir = DynastyRules.Choose(named, people.Where(c => c.Kind != KinKind.Partner && c.Kind != KinKind.Child).ToList(), out bool byLaw);
        Check(heir == null && byLaw, "dynasty identity: an heir named by a place in the cast does not pass to somebody else who now has it");
        Check(DynastyWill.IsHeir(named, KinKind.Person, 4, "Nadia") && !DynastyWill.IsHeir(named, KinKind.Person, 4, "Stranger"), "dynasty identity: the draft knows its heir by name as well");
        var draft = new Will();
        DynastyWill.Move(draft, KinKind.Person, 4, "Nadia", 1);
        DynastyWill.Move(draft, KinKind.Person, 4, "Stranger", 1);
        Check(draft.Bequests.Count == 2 && draft.Others() == 100, "dynasty identity: two people who had the same place are two bequests");
    }

    // ---- the heir

    private sealed class DynastyCourse
    {
        public string Name = string.Empty;
    }

    private static void DynastyHeirFixChecks()
    {
        // a course the founder finished stays finished with its hours; one part done is the heir's share of the hours
        Check(DynastyRules.CourseMinutes(1200, true, 300) == 1200 && DynastyRules.CourseMinutes(1200, false, 300) == 360 && DynastyRules.CourseMinutes(-5, true, 300) == 0
              && DynastyRules.CourseMinutes(0, false, 300) == 0 && DynastyRules.CourseMinutes(900, false, 5000) == 900,
            "dynasty heir: a finished course stays finished (the recruitment agency, the headquarters and the offices are run on them), a part-done one is the heir's share of the hours");
        // the brothers and sisters stay with the family, the heir does not
        var kids = new List<DynastyCourse> { new DynastyCourse { Name = "Tom" }, new DynastyCourse { Name = "Ann" }, new DynastyCourse { Name = "Hana" }, null };
        List<DynastyCourse> stay = DynastyRules.Staying(kids, c => c.Name, "Tom");
        Check(stay.Count == 2 && stay[0].Name == "Ann" && stay[1].Name == "Hana" && ReferenceEquals(stay[0], kids[1]), "dynasty heir: when a child carries on, the other children stay with the family as they are, with their school and their fund");
        Check(DynastyRules.Staying(kids, c => c.Name, null).Count == 3 && DynastyRules.Staying<DynastyCourse>(null, c => c.Name, "Tom").Count == 0, "dynasty heir: when a partner carries on every child stays");
    }

    // ---- the debt, for every day, whatever the option

    private static void DynastyDebtFixChecks()
    {
        const int year = 60;
        // many days at once give exactly what the days one at a time give
        EstateLoan once = MakeLoan(900000, 100, year), daily = MakeLoan(900000, 100, year);
        long paidOnce = 0, paidDaily = 0;
        int charged = DynastyRules.LoanDays(once, 100 + 1500, long.MaxValue / 4, year, c => { paidOnce += c; return c; }, null);
        for (int d = 101; d <= 1600; d++)
        {
            DynastyRules.LoanDays(daily, d, long.MaxValue / 4, year, c => { paidDaily += c; return c; }, null);
        }
        Check(charged >= 250 && charged <= 340 && !once.Open, "dynasty debt: a skip charges each of its days until the debt is paid (" + charged + " days)");
        Check(!once.Open && !daily.Open && paidOnce == paidDaily && once.Owed == daily.Owed && once.LastDay == daily.LastDay && once.PaidCents == paidOnce,
            "dynasty debt: " + (1500) + " days at once are charged exactly as " + 1500 + " days one by one (" + paidOnce / 100 + " paid)");
        // a long absence (a save loaded after years with the option off): not 400 days of it, all of it
        EstateLoan away = MakeLoan(600000, 50, year);
        long interestBefore = away.Owed;
        int days = DynastyRules.LoanDays(away, 50 + 4000, 0, year, c => 0L, null);
        Check(days == 4000 && away.LastDay == 4050 && away.Owed > interestBefore, "dynasty debt: " + days + " days gone by with no cash and nothing booked are charged day by day, interest on every one, not only the last 400");
        EstateLoan late = MakeLoan(600000, 50, year);
        int told = 0, forced = 0;
        DynastyRules.LoanDays(late, 50 + 400, 0, year, c => 0L, (c, f, l) => { told++; forced += f ? 1 : 0; });
        Check(told == 400 && forced == 400 - DynastyCatalogue.LoanLateMost && late.Late == 400 && late.Open, "dynasty debt: every day is told, the first " + DynastyCatalogue.LoanLateMost + " late and the rest forced (" + forced + ")");
        // a part the game refuses stays owed, and the cents are never counted twice
        EstateLoan refuse = MakeLoan(500000, 10, year);
        long owedBefore = refuse.Owed;
        DynastyRules.LoanDays(refuse, 11, long.MaxValue / 4, year, c => 0L, null);
        Check(refuse.Owed > owedBefore - 1 && refuse.PaidCents == 0 && refuse.LastDay == 11, "dynasty debt: a part the game would not book is still owed and counted as not paid");
        // the debt among the Finance app's debts whatever the option: the line of a save with the option off still has it
        DynastyState s = new DynastyState();
        DynastyRules.Borrow(s.Loan, 400000, 20, year);
        DynastyState read = DynastyStore.Load(new Dictionary<string, string> { { DynastyStore.Key, DynastyStore.Write(s) } }, out int _);
        Check(read != null && read.Loan.Open && read.Loan.Owed == 40000000 && read.Loan.LastDay == 20, "dynasty debt: the debt is read from the save's line whatever the option, so the Finance app can show it");
    }

    private static EstateLoan MakeLoan(long principal, int day, int year)
    {
        var l = new EstateLoan();
        DynastyRules.Borrow(l, principal, day, year);
        return l;
    }

    // ---- the retired founder's living

    private static void DynastyElderFixChecks()
    {
        const int year = 60, month = 5;
        Elder e = DynastyRules.Retired("Alex", 62 * year, 1000, 1200000, 5000000, 1400000, year, month);
        long fund = e.Fund, came = 0;
        int months = 0;
        for (int m = 200; m < 200 + 40; m++)
        {
            long drew = DynastyRules.ElderPaid(e, m, d => d);
            came += drew;
            months += drew > 0 ? 1 : 0;
        }
        Check(months == 40 && came == 40 * e.Draw && e.Fund == fund - came && came + e.Fund == fund, "dynasty elder: each month's living is paid to the head of the family: " + came + " in " + months + " months, and the fund is the rest");
        long again = DynastyRules.ElderPaid(e, 239, d => d);
        Check(again == 0, "dynasty elder: a month is paid once");
        // what the game does not take comes back into the fund
        Elder f = DynastyRules.Retired("Alex", 62 * year, 1000, 1200000, 5000000, 1400000, year, month);
        long got = DynastyRules.ElderPaid(f, 300, d => d / 2);
        Check(got == f.Draw / 2 && f.Fund == 1200000 - got, "dynasty elder: a draw that only half came in leaves the rest in the fund (" + f.Fund + ")");
        // lived out: what was drawn plus what is left at the end is the fund, and the end is taxed as before
        Elder g = DynastyRules.Retired("Alex", 62 * year, 1000, 1200000, 5000000, 1400000, year, month);
        long drawn = 0;
        for (int m = 0; m < 300; m++)
        {
            drawn += DynastyRules.ElderPaid(g, m, d => d);
        }
        Statement end = DynastyRules.SettleElder(g, "Sam", 1000 + 28 * year, 1000, year, 1);
        Check(drawn + g.Fund == 1200000 && g.Fund > 0 && drawn == 300 * g.Draw && end.Net == g.Fund && end.Residue == g.Fund - end.Tax && DynastyRules.Balanced(end),
            "dynasty elder: the living drawn and what is left at the end come to the fund (" + drawn + " and " + g.Fund + ")");
    }

    // ---- the page's estate

    private static void DynastyCacheChecks()
    {
        var cache = new DynastyEstateCache<EstatePair>();
        int worked = 0;
        Func<EstatePair> work = () => { worked++; return new EstatePair { Death = new Statement(), Retire = new Statement() }; };
        EstatePair a = cache.Get(10, 3, 5000, work);
        EstatePair b = null;
        for (int i = 0; i < 5000; i++)
        {
            b = cache.Get(10, 3, 5000, work);
        }
        Check(worked == 1 && ReferenceEquals(a, b) && cache.Worked == 1, "dynasty page: a page rebuilt five thousand times in a day is worked out once");
        cache.Get(11, 3, 5000, work);
        cache.Get(11, 4, 5000, work);
        cache.Get(11, 4, 5001, work);
        Check(worked == 4, "dynasty page: a new day, a new revision and a change of cash are each worked out afresh (" + worked + ")");
        cache.Drop();
        cache.Get(11, 4, 5001, work);
        Check(worked == 5, "dynasty page: a dropped figure is worked out again");
        // the cost of the two settlements one reading serves: measured
        List<KinPerson> people = DynastyFamily();
        Will w = DynastyWillSample();
        var watch = Stopwatch.StartNew();
        const int rounds = 2000;
        long sink = 0;
        for (int i = 0; i < rounds; i++)
        {
            EstateIn e = DynastyEstate(8000000 + i, 0, 2000000);
            KinPerson heir = DynastyRules.Choose(w, people, out bool byLaw);
            sink += DynastyRules.Settle(e, w, heir, byLaw, people).Tax + DynastyRules.Settle(e.For(HandMode.Retire, 2), w, heir, byLaw, people).Tax;
        }
        double perPair = watch.Elapsed.TotalMilliseconds / rounds;
        watch.Restart();
        for (int i = 0; i < rounds; i++)
        {
            sink += cache.Get(11, 4, 5001, work).Death.Tax;
        }
        double perHit = watch.Elapsed.TotalMilliseconds / rounds;
        Check(sink > 0 && perPair < 0.2 && perHit < 0.001, "dynasty page: both statements cost " + perPair.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture) + " ms and a rebuild from memory "
              + perHit.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " ms");
        EstateIn one = DynastyEstate(8000000, 100, 2000000, 5, HandMode.Death, 3);
        EstateIn two = one.For(HandMode.Retire, 1);
        Check(two.Mode == HandMode.Retire && two.Keep == 1 && two.Cash == one.Cash && two.Other == one.Other && two.Gifts == one.Gifts && one.Mode == HandMode.Death && one.Keep == 3,
            "dynasty page: the same reading serves both ways the estate could pass, and the first is not changed");
    }

    // ---- a household that moved away is no estate

    private static void DynastyEndedChecks()
    {
        int ended = 0;
        long savings = 0, debt = 0;
        LifeRules.Ended = (s, d) => { ended++; savings = s; debt = d; };
        try
        {
            // a household of an adult and a child: the adult dies, and the child alone is taken in by family beyond the city: an estate
            PopulationState dies = DynastyAdultAndChild(7001);
            LifeRules.Die(dies, null, dies.Households.First[0], 400);
            Check(ended == 1 && savings == 4200 && debt == 300 && dies.Households.Alive(0) == false, "dynasty city: a household left with a child alone after a death is an estate (" + savings + " and " + debt + " owed)");
            // the same household, but the adult leaves the city: nothing died, so there is no estate
            ended = 0;
            PopulationState moves = DynastyAdultAndChild(7002);
            LifeRules.LeaveCity(moves, moves.Households.First[0], 400);
            Check(ended == 0 && moves.Households.Alive(0) == false, "dynasty city: a household whose adult moved away is not counted as an estate");
            // a household left with another adult goes on, and is no estate either way
            ended = 0;
            PopulationState two = DynastyAdultAndChild(7003);
            int extra = two.People.Add(99);
            two.People.Household[extra] = 0;
            two.People.Birth[extra] = -9000;
            two.People.Status[extra] = (byte)Status.Worker;
            two.People.Next[extra] = two.Households.First[0];
            two.Households.First[0] = extra;
            two.Households.Size[0] = 3;
            int adult = two.Households.First[0];
            LifeRules.Die(two, null, adult, 400);
            Check(ended == 0 && two.Households.Alive(0), "dynasty city: a household with an adult left goes on and leaves no estate");
        }
        finally
        {
            LifeRules.Ended = null;
        }
    }

    private static PopulationState DynastyAdultAndChild(int seed)
    {
        PopulationState s = GivingCity(1, seed);
        HouseholdTable hh = s.Households;
        PersonTable pt = s.People;
        hh.Savings[0] = 4200;
        hh.Debt[0] = 300;
        hh.Flags[0] = 0;
        // the second person of the household becomes a child of five years
        int adult = hh.First[0];
        int child = pt.Next[adult];
        pt.Birth[child] = s.Day - 5 * s.YearDays;
        pt.Status[child] = (byte)Status.Home;
        pt.Job[adult] = -1;
        pt.Job[child] = -1;
        pt.Partner[adult] = -1;
        pt.Partner[child] = -1;
        return s;
    }

    // ---- no gift lost

    private static void DynastyGiftFixChecks()
    {
        var data = new Dictionary<string, string>();
        // no line yet: the gift makes one
        Check(DynastyGifts.Keep(data, 100, 5000) == "line" && DynastyStore.Load(data, out int _).Gifts.Sum(g => g.Amount) == 5000 && !data.ContainsKey(DynastyGifts.Key), "dynasty gifts: with the option off and no line yet a gift makes the line");
        // a line that reads: the gift is added
        DynastyGifts.Keep(data, 101, 700);
        Check(DynastyStore.Load(data, out int _).Gifts.Sum(g => g.Amount) == 5700, "dynasty gifts: with the option off the gift is dated in the line");
        // a line that cannot be read: the gift is kept in a line of its own, and nothing is dropped
        var damaged = new Dictionary<string, string> { { DynastyStore.Key, "this is not a line of this feature" } };
        Check(DynastyGifts.Keep(damaged, 200, 9000) == "side" && DynastyGifts.Keep(damaged, 200, 1000) == "side" && DynastyGifts.Keep(damaged, 230, 50) == "side" && damaged[DynastyStore.Key] == "this is not a line of this feature",
            "dynasty gifts: a line that cannot be read is left as it is and the gifts go in a line of their own");
        var back = new DynastyState();
        Check(DynastyGifts.Merge(damaged, back) == 2 && back.Gifts.Sum(g => g.Amount) == 10050 && !damaged.ContainsKey(DynastyGifts.Key) && DynastyRules.GiftsInWindow(back.Gifts, 230, 60) == 10050,
            "dynasty gifts: the next time the dynasty runs they are counted with the window, all " + back.Gifts.Sum(g => g.Amount) + " of them");
        Check(DynastyGifts.Keep(null, 1, 100) == "nowhere" && DynastyGifts.Keep(new Dictionary<string, string>(), 1, 0) == "nowhere" && DynastyGifts.Merge(new Dictionary<string, string>(), new DynastyState()) == 0, "dynasty gifts: no gift and no line are no trouble");
        // the gift is kept through the window all the same: a gift told while the line was unreadable is still taxed
        EstateIn e = DynastyEstate(3000000, 0, 0, DynastyRules.GiftsInWindow(back.Gifts, 230, 60));
        Check(DynastySettle(e, new Will(), DynastyFamily()).Gifts == 10050, "dynasty gifts: a gift told to a damaged line is counted in the estate");
    }

    // ---- the tax worked again

    private static void DynastyRetaxChecks()
    {
        // for any estate, a bequest that lands in part (or not at all) never lowers the tax, never changes the identity, and only changes anything if something exempt did not land
        List<KinPerson> people = DynastyFamily();
        people[0].Household = true;
        people[0].Room = long.MaxValue / 4;
        people[0].Id = 3;
        bool never = true, balanced = true, exact = true, same = true;
        int cases = 0;
        for (int i = 0; i < 1500; i++)
        {
            long cash = 1000000 + DynastyRules.Hash(41, 1, i, 0) % 9000000u;
            long other = (long)(DynastyRules.Hash(41, 2, i, 0) % 20000000u) - 2000000;
            var w = new Will { Written = true, HeirKind = KinKind.Child, HeirName = "Tom" };
            w.Bequests.Add(new Bequest { Kind = KinKind.Partner, Name = "Maya", Share = 50 * (int)(1 + DynastyRules.Hash(41, 3, i, 0) % 4) });
            w.Bequests.Add(new Bequest { Kind = KinKind.Charity, Id = i % 3, Name = "c", Share = 50 * (int)(1 + DynastyRules.Hash(41, 4, i, 0) % 3) });
            w.Bequests.Add(new Bequest { Kind = KinKind.Person, Id = 4, Name = "Nadia", Share = 50 });
            Statement s = DynastySettle(DynastyEstate(cash, 0, other), w, people);
            if (s.Lines.Count != 3)
            {
                continue;
            }
            cases++;
            var h = new Statement();
            // the same statement, every line landing a different part of what was asked
            for (int part = 0; part < 4; part++)
            {
                Statement t = DynastySettle(DynastyEstate(cash, 0, other), w, people);
                for (int k = 0; k < t.Lines.Count; k++)
                {
                    long landed = t.Lines[k].Amount * (part == 0 ? 1000 : part == 1 ? 0 : part == 2 ? 500 : (k + 1) * 250) / 1000;
                    long back = t.Lines[k].Amount - landed;
                    t.Lines[k].Amount = landed;
                    t.Paid -= back;
                    t.Residue += back;
                }
                long before = t.Tax;
                long taxable = t.Taxable;
                long extra = DynastyRules.Retax(t);
                never &= extra >= 0 && t.Tax == before + extra && t.Loan == s.Loan + extra && t.Taxable >= taxable - 0;
                balanced &= DynastyRules.Balanced(t);
                exact &= t.Tax == DynastyRules.TaxOn(t.Taxable, t.Prior, t.Level) && t.Charity == t.Lines.Where(l => l.Landing == Landing.Pot).Sum(l => l.Amount);
                same &= part != 0 || (extra == 0 && t.Tax == s.Tax && t.Taxable == s.Taxable && t.Spouse == s.Spouse && t.Charity == s.Charity);
            }
        }
        Check(cases > 1200, "dynasty retax: seeded estates with three bequests are found (" + cases + ")");
        Check(same, "dynasty retax: when every bequest landed whole nothing changes");
        Check(never, "dynasty retax: a bequest that lands short never lowers the tax: the difference is added to the debt");
        Check(balanced, "dynasty retax: and the estate still comes to what was kept, the tax, the bequests paid and what the heir is left");
        Check(exact, "dynasty retax: the tax is the bands' on the exempt parts that really landed");
    }
}
