using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;

// The bank branches (Scripts/Economy/OwnBank/Branch): the catalogue, an hour's customers and the counter that sees to
// them, every visit booked in the player's own bank with its books balanced and its fees taken with the next day, the
// reach of the last week's visits and its diminishing returns over many branches, a branch that pays for itself and one
// too many that does not, the save's line with branches and without, the bank as it was with the option off, the floor's
// plan, the texts, and how long an hour takes.
internal static partial class Program
{
    private const int BranchYear = 60;   // the game's own year: a month of five days

    private static void Branches()
    {
        int before = _run, failedBefore = _failed;
        var watch = Stopwatch.StartNew();
        BranchCatalogueChecks();
        BranchHourChecks();
        BranchVisitChecks();
        BranchReachChecks();
        BranchPaysChecks();
        BranchStoreChecks();
        BranchOffChecks();
        BranchFloorChecks();
        BranchTextChecks();
        BranchTimingChecks();
        Console.WriteLine("  Bank branches: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // A branch of a mid-sized shop unit: the game's count of 40 an hour, half promoted, even satisfaction, two tellers of middling skill.
    private static BranchHourIn MidBranch(bool open = true, int tellers = 2)
    {
        return new BranchHourIn { Open = open, ShopCustomers = 40f, Promotion = 50, BaseMultiplier = 0.5f, Satisfaction = 50, Tellers = tellers, Skill = 50 };
    }

    // A bank of a year in a quiet city of the game's own year: savers and loans as the city shares them out.
    private static OwnBank YearBank(BankCity c, int seed = 7)
    {
        OwnBank b = NewBank(c, 1, seed);
        RunBank(b, c, 2, BranchYear, null, new BankDay());
        return b;
    }

    // ---------------------------------------------------------------- the catalogue

    private static void BranchCatalogueChecks()
    {
        int share = 0;
        foreach (int s in BranchCatalogue.KindShare)
        {
            share += s;
        }
        Check(BranchCatalogue.HourWeight.Length == 24 && BranchCatalogue.KindShare.Length == BranchCatalogue.Kinds && BranchCatalogue.KindCents.Length == BranchCatalogue.Kinds
              && share == 1000 && BranchCatalogue.AskBy.Length == 3,
            "bank branch: the hours, the kinds of visit and who asks for a loan are all there, the kinds sharing out every visit (" + share + ")");
        Check(BranchCatalogue.TypeName.StartsWith("reallife_", StringComparison.Ordinal) && BranchCatalogue.TypeName.IndexOf(':') < 0 && BranchCatalogue.BuildingType == "ba:buildingtype_retail",
            "bank branch: a business type of the mod's own, opened in a shop unit");
        Check(BranchCatalogue.ReachMost > 0 && BranchCatalogue.ReachMost <= 1000 && BranchCatalogue.ReachHalf > 0 && BranchCatalogue.FirmsReach > 0 && BranchCatalogue.FirmsReach < 1000,
            "bank branch: the reach is bounded, and businesses get a part of it");
        Check(BranchCatalogue.SkillLeast < 1000 && BranchCatalogue.SkillMost > 1000 && BranchCatalogue.OnFloorMost <= 4 && BranchCatalogue.ShownMost >= BranchCatalogue.OnFloorMost,
            "bank branch: skill moves a teller both ways, and the floor asks no more people than the pool spares");
    }

    // ---------------------------------------------------------------- an hour's customers and the counter

    private static void BranchHourChecks()
    {
        BranchHourIn mid = MidBranch();
        Check(BranchRules.VisitorThousandths(MidBranch(false), 12) == 0, "bank branch: a closed branch has no customers");
        Check(BranchRules.VisitorThousandths(mid, 12) > BranchRules.VisitorThousandths(mid, 9) && BranchRules.VisitorThousandths(mid, 9) > BranchRules.VisitorThousandths(mid, 3),
            "bank branch: lunchtime is busier than the morning, the morning than the night");
        BranchHourIn promoted = mid, liked = mid, disliked = mid;
        promoted.Promotion = 100;
        liked.Satisfaction = 100;
        disliked.Satisfaction = 0;
        Check(BranchRules.VisitorThousandths(promoted, 12) > BranchRules.VisitorThousandths(mid, 12)
              && BranchRules.VisitorThousandths(liked, 12) > BranchRules.VisitorThousandths(mid, 12) && BranchRules.VisitorThousandths(disliked, 12) < BranchRules.VisitorThousandths(mid, 12),
            "bank branch: promotion and satisfaction bring more customers, a poor name fewer");
        int carry = 0, sum = 0;
        for (int h = 0; h < 24; h++)
        {
            sum += BranchRules.Visitors(mid, h, ref carry);
        }
        long thousandths = 0;
        for (int h = 0; h < 24; h++)
        {
            thousandths += BranchRules.VisitorThousandths(mid, h);
        }
        Check(Math.Abs(sum - thousandths / 1000) <= 1 && carry >= 0 && carry < 1000, "bank branch: whole customers over a day, the parts carried hour to hour, add up (" + sum + ")");
        int closedCarry = 500;
        Check(BranchRules.Visitors(MidBranch(false), 12, ref closedCarry) == 0 && closedCarry == 0, "bank branch: nothing is carried past a closed hour");
        Check(sum >= 40 && sum <= 160, "bank branch: a mid-sized branch sees between 40 and 160 customers a day (" + sum + ")");

        Check(BranchRules.Capacity(MidBranch(true, 0)) == 0 && BranchRules.Capacity(MidBranch(true, 2)) == 2 * BranchCatalogue.ServePerTeller,
            "bank branch: with nobody at the counter nobody is seen to; two tellers of middling skill see to twice " + BranchCatalogue.ServePerTeller + " an hour");
        BranchHourIn skilled = MidBranch(true, 2), raw = MidBranch(true, 2), player = MidBranch(true, 0);
        skilled.Skill = 100;
        raw.Skill = 0;
        player.Player = true;
        Check(BranchRules.Capacity(skilled) > BranchRules.Capacity(MidBranch()) && BranchRules.Capacity(raw) < BranchRules.Capacity(MidBranch()),
            "bank branch: a skilled teller sees to more, a raw one fewer");
        Check(BranchRules.Capacity(player) == BranchCatalogue.ServePerTeller && BranchRules.Capacity(MidBranch(false, 3)) == 0,
            "bank branch: the player at the counter works as a teller of middling skill; a closed branch sees to nobody");
    }

    // ---------------------------------------------------------------- visits in the bank's books

    private static void BranchVisitChecks()
    {
        BankCity c = BankCity.Quiet(BranchYear);
        OwnBank b = YearBank(c);
        int level = c.Level;
        long people = b.Deposits[OwnBankCatalogue.People], reserves = b.Reserves, lent = b.Loans[OwnBankCatalogue.Good];
        VisitResult r = OwnBankRules.Visit(b, BranchCatalogue.Open, OwnBankCatalogue.Fair, 40000, level, out long fee, out long moved);
        Check(r == VisitResult.PaidIn && moved == 40000 && b.Deposits[OwnBankCatalogue.People] == people + 40000 && b.Reserves == reserves + 40000
              && fee == BranchCatalogue.OpenFeeCents && b.CounterFees == fee && OwnBankRules.Balanced(b),
            "bank branch: an account opened is a deposit of the city's people and cash in the reserves, its fee waiting for the day, the books balanced");
        r = OwnBankRules.Visit(b, BranchCatalogue.Withdraw, OwnBankCatalogue.Fair, 30000, level, out fee, out moved);
        Check(r == VisitResult.TakenOut && moved == 30000 && b.Deposits[OwnBankCatalogue.People] == people + 10000 && fee == BranchCatalogue.CounterFeeCents && OwnBankRules.Balanced(b),
            "bank branch: a withdrawal takes from both, for the counter's fee");
        Check(!OwnBankRules.CounterLends(b, OwnBankCatalogue.Good, 600000) || OwnBankRules.Ratio(b) >= OwnBankCatalogue.LendRatio,
            "bank branch: a bank lent up to its line makes no loan at the counter");
        OwnBankRules.PutIn(b, 100000000);   // capital over the line: room to lend
        lent = b.Loans[OwnBankCatalogue.Good];
        r = OwnBankRules.Visit(b, BranchCatalogue.Loan, OwnBankCatalogue.Good, 600000, level, out fee, out moved);
        Check(r == VisitResult.Lent && b.Loans[OwnBankCatalogue.Good] == lent + 600000 && fee == Math.Max(BranchCatalogue.LoanFeeLeastCents, 600000L * BranchCatalogue.LoanFee / 1000)
              && OwnBankRules.Balanced(b),
            "bank branch: a loan at the counter is the bank's loan out of its reserves, with its arrangement fee");
        r = OwnBankRules.Visit(b, BranchCatalogue.Enquiry, OwnBankCatalogue.Fair, 0, level, out fee, out moved);
        Check(r == VisitResult.None && fee == 0 && moved == 0, "bank branch: a question costs nothing and moves nothing");
        r = OwnBankRules.Visit(b, BranchCatalogue.Loan, OwnBankCatalogue.Poor, 600000, level, out fee, out moved);
        Check(r == VisitResult.Refused && fee == 0 && !b.Lending[OwnBankCatalogue.Poor], "bank branch: no loan to those the bank does not lend to");

        // A withdrawal never takes more than the people hold or the bank has in cash.
        OwnBank small = NewBank(c);
        small.Deposits[OwnBankCatalogue.People] = 5000;
        small.Reserves += 5000;
        r = OwnBankRules.Visit(small, BranchCatalogue.Withdraw, OwnBankCatalogue.Fair, 30000, level, out fee, out moved);
        Check(r == VisitResult.TakenOut && moved == 5000 && small.Deposits[OwnBankCatalogue.People] == 0 && OwnBankRules.Balanced(small), "bank branch: a withdrawal takes no more than is there");
        r = OwnBankRules.Visit(small, BranchCatalogue.Withdraw, OwnBankCatalogue.Fair, 30000, level, out fee, out moved);
        Check(r == VisitResult.None && fee == 0, "bank branch: nothing to take out, and no fee");

        // No loan in a run or a breach, or beyond the reserve and the lending line.
        OwnBank run = YearBank(c);
        run.Run = true;
        Check(!OwnBankRules.CounterLends(run, OwnBankCatalogue.Good, 600000), "bank branch: no loan at the counter in a run");
        run.Run = false;
        run.Stage = Regulator.Breach;
        Check(!OwnBankRules.CounterLends(run, OwnBankCatalogue.Good, 600000), "bank branch: no loan at the counter in a breach");
        run.Stage = Regulator.Clear;
        Check(!OwnBankRules.CounterLends(run, OwnBankCatalogue.Good, run.Reserves), "bank branch: no loan that would take the reserves under what the deposits need");
        Check(!OwnBankRules.CounterLends(run, OwnBankCatalogue.Good, OwnBankRules.CapitalNow(run) * 1000 / OwnBankCatalogue.LendRatio * 2),
            "bank branch: no loan beyond the lending line");
        Check(BranchRules.Kind(0, true) == BranchCatalogue.Withdraw && BranchRules.Kind(BranchCatalogue.KindShare[0] + 1, true) == BranchCatalogue.Withdraw
              && BranchRules.Kind(999, false) == BranchCatalogue.Enquiry, "bank branch: in a run those who came to pay in take out instead");

        // The fees are the bank's income with the next day, kept in its capital; the day's books balance.
        OwnBank day = YearBank(c);
        OwnBank same = YearBank(c);
        OwnBankRules.Visit(day, BranchCatalogue.Open, OwnBankCatalogue.Fair, 40000, level, out long f1, out _);
        same.Deposits[OwnBankCatalogue.People] += 40000;
        same.Reserves += 40000;
        var d1 = new BankDay();
        var d2 = new BankDay();
        OwnBankRules.Day(day, c, BranchYear + 2, null, d1);
        OwnBankRules.Day(same, c, BranchYear + 2, null, d2);
        Check(d1.Counter == f1 && d2.Counter == 0 && d1.Income == d2.Income + f1 && day.CounterFees == 0 && OwnBankRules.Balanced(day)
              && day.Capital == same.Capital + f1 && day.Reserves == same.Reserves + f1,
            "bank branch: the counter's fees are the next day's takings, kept in the bank's capital and its cash, and nothing else differs");

        // An hour of a branch: every visit seen to is booked; nothing comes to a bank that is not running.
        OwnBank hourBank = YearBank(c);
        var br = new BankBranch { Place = "Main%20Street~12", Name = "Main Street", MonthFrom = BranchYear };
        long savings = hourBank.Savings, loans = hourBank.Lent;
        BranchHourOut o = BranchRules.Hour(hourBank, br, MidBranch(), (BranchYear + 2) * 24 + 12, level, 5);
        Check(o.Visitors > 0 && o.Served == Math.Min(o.Visitors, 2 * BranchCatalogue.ServePerTeller) && o.Lost == o.Visitors - o.Served && OwnBankRules.Balanced(hourBank)
              && hourBank.Savings == savings + o.PaidIn - o.TakenOut && hourBank.Lent == loans + o.Lent && hourBank.CounterFees == o.Fees,
            "bank branch: an hour's visits are what the bank's deposits and loans moved by, and its fees are what waits (" + o.Served + " seen to)");
        Check(br.Month[BranchFigure.Served] == o.Served && br.WeekServed(BranchYear + 2) == o.Served && br.HourVisitors == o.Visitors && br.HourServed == o.Served,
            "bank branch: the branch counts the hour in its month and its week");
        var closed = new OwnBank();
        var none = new BankBranch { Place = "x" };
        BranchHourOut nothing = BranchRules.Hour(closed, none, MidBranch(), 100, level, 5);
        Check(nothing.Visitors == 0 && none.Month[BranchFigure.Visitors] == 0 && closed.CounterFees == 0, "bank branch: a branch of a bank that is not running sees nobody");

        // The same bank, hour and branch come to the same visits on every machine.
        OwnBank one = YearBank(c, 31), two = YearBank(c, 31);
        var b1 = new BankBranch { Place = "Elm~4" };
        var b2 = new BankBranch { Place = "Elm~4" };
        bool sameHours = true;
        for (int h = 0; h < 48; h++)
        {
            BranchHourOut x = BranchRules.Hour(one, b1, MidBranch(), (BranchYear + 2) * 24 + h, level, 5);
            BranchHourOut y = BranchRules.Hour(two, b2, MidBranch(), (BranchYear + 2) * 24 + h, level, 5);
            sameHours &= x.Served == y.Served && x.Fees == y.Fees && x.PaidIn == y.PaidIn && x.Lent == y.Lent;
        }
        Check(sameHours && OwnBankStore.Write(one) == OwnBankStore.Write(two), "bank branch: the same bank, hours and branch come to the same books");
        Check(BranchRules.PlaceKey("Elm~4") == BranchRules.PlaceKey("Elm~4") && BranchRules.PlaceKey("Elm~4") != BranchRules.PlaceKey("Elm~5"), "bank branch: a branch's number is its place's");

        // The month rolls: last month kept, this month begun.
        var roll = new BankBranch { Place = "Roll~1", MonthFrom = 10 };
        roll.Month[BranchFigure.Served] = 77;
        BranchRules.Roll(roll, 14, 5);
        Check(roll.Month[BranchFigure.Served] == 77, "bank branch: a month is not over before its days are");
        BranchRules.Roll(roll, 15, 5);
        Check(roll.Last[BranchFigure.Served] == 77 && roll.Month[BranchFigure.Served] == 0 && roll.MonthFrom == 15, "bank branch: a month over is kept as last month's");
        roll.CountServed(20, 5);
        roll.CountServed(27, 9);
        Check(roll.WeekServed(27) == 9 && roll.WeekServed(26) == 0, "bank branch: the week holds the last seven days and lets a slot of a week ago go");
    }

    // ---------------------------------------------------------------- reach, and many branches

    private static void BranchReachChecks()
    {
        Check(BranchRules.Reach(0) == 0 && BranchRules.Reach(BranchCatalogue.ReachHalf) == BranchCatalogue.ReachMost / 2 && BranchRules.Reach(1000000000L) < BranchCatalogue.ReachMost,
            "bank branch: no reach with no visits, half at the half mark, never the whole");
        int last = 0, step = int.MaxValue;
        bool rising = true, smaller = true;
        var steps = new List<int>();
        for (int n = 1; n <= 8; n++)
        {
            int r = BranchRules.Reach(700L * n);
            rising &= r > last;
            smaller &= r - last < step;
            step = r - last;
            steps.Add(step);
            last = r;
        }
        Check(rising && smaller, "bank branch: each busy branch more brings more reach, and less than the one before (" + string.Join(", ", steps) + " thousandths)");

        BankCity c = BankCity.Quiet(BranchYear);
        OwnBank b = YearBank(c);
        int trust = OwnBankRules.Trust(c, b, BranchYear + 1, OwnBankRules.Confidence(c, b, OwnBankRules.Ratio(b)));
        BankCity with = c;
        with.Reach = 300;
        Check(OwnBankRules.SaversWanted(with, b, OwnBankCatalogue.People, trust) > OwnBankRules.SaversWanted(c, b, OwnBankCatalogue.People, trust)
              && OwnBankRules.LoansWanted(with, b, OwnBankCatalogue.Good) > OwnBankRules.LoansWanted(c, b, OwnBankCatalogue.Good),
            "bank branch: with reach the bank holds more of the city's people, savers and borrowers");
        long peopleMore = OwnBankRules.SaversWanted(with, b, OwnBankCatalogue.People, trust) * 1000 / Math.Max(1, OwnBankRules.SaversWanted(c, b, OwnBankCatalogue.People, trust));
        long firmsMore = OwnBankRules.SaversWanted(with, b, OwnBankCatalogue.Firms, trust) * 1000 / Math.Max(1, OwnBankRules.SaversWanted(c, b, OwnBankCatalogue.Firms, trust));
        Check(firmsMore > 1000 && firmsMore < peopleMore, "bank branch: businesses come with a part of the reach, people with all of it (" + peopleMore + ", " + firmsMore + " thousandths)");
        Check(OwnBankRules.Held(70, 0, true) == 70.0 && OwnBankRules.Held(70, -50, true) == 70.0, "bank branch: no reach, or less than none, leaves the hold as it was");

        // Many branches over a year: the books balance every day, and the bank grows by less with each.
        long[] grown = new long[4];
        int[] counts = { 0, 1, 3, 8 };
        bool balanced = true;
        for (int i = 0; i < counts.Length; i++)
        {
            OwnBank many = YearBank(c, 5);
            var list = new List<BankBranch>();
            for (int n = 0; n < counts[i]; n++)
            {
                var br = new BankBranch { Place = "Branch~" + n, MonthFrom = BranchYear };
                many.Branches.Add(br);
                list.Add(br);
            }
            var day = new BankDay();
            for (int d = BranchYear + 1; d < 2 * BranchYear + 1 && many.Running; d++)
            {
                for (int h = 8; h < 18; h++)
                {
                    for (int n = 0; n < list.Count; n++)
                    {
                        BranchRules.Hour(many, list[n], MidBranch(), d * 24 + h, c.Level, 5);
                    }
                }
                BankCity city = c;
                city.Reach = BranchRules.Reach(many, d);
                OwnBankRules.Day(many, city, d, null, day);
                balanced &= !many.Running || OwnBankRules.Balanced(many);
            }
            grown[i] = many.Savings + many.Lent;
        }
        Check(balanced, "bank branch: a year with none, one, three and eight branches, and the books balance every day");
        Check(grown[1] > grown[0] && grown[2] > grown[1] && grown[3] > grown[2] && grown[3] - grown[2] < (grown[2] - grown[1]) * 5 / 2 && grown[1] - grown[0] > (grown[3] - grown[2]) / 5,
            "bank branch: more branches, a bigger bank, each by less (" + (grown[1] - grown[0]) / 100 + ", " + (grown[2] - grown[1]) / 100 + " for two more, " + (grown[3] - grown[2]) / 100 + " for five more)");
    }

    // ---------------------------------------------------------------- a branch that pays

    // What a mid-sized branch costs: the game's rent of a shop unit of its size and two tellers on eight-hour shifts, at the start's prices.
    private const long BranchRentDay = 45000;         // $450 a day
    private const long BranchWagesDay = 2 * 8 * 2200;  // two tellers at $22 an hour for eight hours
    private const long BranchFitOut = 9000000;         // $90,000: a ready-made design's furniture and the firm's fee

    private static void BranchPaysChecks()
    {
        BankCity c = BankCity.Quiet(BranchYear);
        OwnBank b = YearBank(c);
        var br = new BankBranch { Place = "Pays~1", MonthFrom = BranchYear };
        b.Branches.Add(br);
        long fees = 0;
        int days = 7;
        for (int d = BranchYear + 1; d <= BranchYear + days; d++)
        {
            for (int h = 0; h < 24; h++)
            {
                fees += BranchRules.Hour(b, br, MidBranch(h >= 9 && h < 18), d * 24 + h, c.Level, 5).Fees;
            }
        }
        long feesDay = fees / days;
        int reach = BranchRules.Reach(b, BranchYear + days);
        BankCity with = c;
        with.Reach = reach;
        int trust = OwnBankRules.Trust(c, b, BranchYear + days, OwnBankRules.Confidence(c, b, OwnBankRules.Ratio(b)));
        BranchWorth w = BranchRules.Worth(with, b, trust);
        long costs = BranchRentDay + BranchWagesDay, net = feesDay + w.DayCents - costs;
        int monthDays = BankCity.Quiet(BranchYear).MonthDays;
        long payback = net > 0 ? (BranchFitOut + net * monthDays - 1) / (net * monthDays) : long.MaxValue;
        Console.WriteLine("    a mid-sized branch: " + br.WeekServed(BranchYear + days) + " seen to a week, reach " + reach + " thousandths; fees " + feesDay / 100 + " a day, the reach's margin "
                          + w.DayCents / 100 + " a day on " + w.Deposits / 100 + " more deposits and " + w.Loans / 100 + " more loans; rent and wages " + costs / 100 + " a day; net "
                          + net / 100 + " a day; the fit-out of " + BranchFitOut / 100 + " paid back in " + payback + " month(s) of " + monthDays + " days");
        Check(feesDay > 0 && w.DayCents > 0 && w.Deposits > 0 && w.Loans > 0, "bank branch: a branch brings fees and a margin on more deposits and loans");
        Check(net > 0 && payback <= BranchCatalogue.PaybackMonthsMost, "bank branch: a mid-sized branch pays its rent and wages and its fit-out back within " + BranchCatalogue.PaybackMonthsMost + " months (" + payback + ")");
        Check(feesDay < costs * 3, "bank branch: the counter's fees alone are not a fortune: the reach is the reason to have branches (" + feesDay / 100 + " a day)");

        // The tenth busy branch adds less than it costs: many branches do not pay.
        BankCity nine = c, ten = c;
        long week = br.WeekServed(BranchYear + days);
        nine.Reach = BranchRules.Reach(9 * week);
        ten.Reach = BranchRules.Reach(10 * week);
        long marginal = BranchRules.Worth(ten, b, trust).DayCents - BranchRules.Worth(nine, b, trust).DayCents + feesDay;
        Console.WriteLine("    the tenth such branch: " + marginal / 100 + " a day more for the bank against " + costs / 100 + " a day of rent and wages");
        Check(marginal < costs, "bank branch: a tenth busy branch brings the bank less than its rent and wages: many branches do not pay (" + marginal / 100 + " a day)");
        BankCity noReach = c;
        Check(BranchRules.Worth(noReach, b, trust).DayCents == 0 && BranchRules.Worth(with, new OwnBank(), trust).Deposits == 0, "bank branch: no reach, or no bank, is worth nothing");
    }

    // ---------------------------------------------------------------- the save

    private static void BranchStoreChecks()
    {
        BankCity c = BankCity.Quiet(BranchYear);
        OwnBank b = YearBank(c);
        var br = new BankBranch { Place = "Pipe%7C~3", Name = "Pipe|Caret^Semi;", MonthFrom = BranchYear };
        b.Branches.Add(br);
        b.Branches.Add(new BankBranch { Place = "Second~9", Name = "Second", MonthFrom = BranchYear });
        for (int h = 0; h < 30; h++)
        {
            BranchRules.Hour(b, br, MidBranch(), (BranchYear + 1) * 24 + h, c.Level, 5);
        }
        b.CounterHour = (BranchYear + 2) * 24 + 5;
        br.Last[BranchFigure.Fees] = 4321;
        string line = OwnBankStore.Write(b);
        OwnBank back = OwnBankStore.Read(line, out _);
        BankBranch got = back != null ? back.Branch(br.Place) : null;
        Check(back != null && OwnBankStore.Write(back) == line && back.Branches.Count == 2 && got != null && got.Name == br.Name && got.Carry == br.Carry
              && got.WeekServed(BranchYear + 2) == br.WeekServed(BranchYear + 2) && got.Month[BranchFigure.Fees] == br.Month[BranchFigure.Fees] && got.Last[BranchFigure.Fees] == 4321
              && back.CounterFees == b.CounterFees && back.CounterHour == b.CounterHour && OwnBankRules.Balanced(back),
            "bank branch: the save's line keeps the branches, their week and months, and the fees waiting for the day");
        string old = line.Substring(0, line.IndexOf("^k:", StringComparison.Ordinal));
        OwnBank before = OwnBankStore.Read(old, out _);
        Check(before != null && before.Branches.Count == 0 && before.CounterFees == 0 && before.CounterHour == int.MinValue && OwnBankRules.Balanced(before),
            "bank branch: a line written before branches reads with none");
        OwnBank later = OwnBankStore.Read(line.Replace("^b:", "^b:x|y|1|2;").Replace(";Second~9", ";Second~9"), out _);
        Check(later != null && later.Branches.Count == 3 && later.Branch("x") != null, "bank branch: a short record of a branch reads with its figures as none");
        var many = new OwnBank();
        OwnBankRules.Found(many, 1, 3, OwnBankRules.CapitalLeast(c.Level), "Many");
        for (int i = 0; i < BranchCatalogue.BranchesMost + 4; i++)
        {
            many.Branches.Add(new BankBranch { Place = "P~" + i });
        }
        OwnBank capped = OwnBankStore.Read(OwnBankStore.Write(many), out _);
        Check(capped != null && capped.Branches.Count == BranchCatalogue.BranchesMost, "bank branch: at most " + BranchCatalogue.BranchesMost + " branches are kept");
    }

    // ---------------------------------------------------------------- the option off

    private static void BranchOffChecks()
    {
        // Off, the director gives the bank no reach and works no hour: the bank's days are a bank's without branches.
        BankCity c = BankCity.Quiet(BranchYear);
        OwnBank with = YearBank(c, 13), without = YearBank(c, 13);
        for (int i = 0; i < 3; i++)
        {
            with.Branches.Add(new BankBranch { Place = "Off~" + i });
            with.Branches[i].CountServed(BranchYear, 500);
        }
        var day = new BankDay();
        RunBank(with, c, BranchYear + 1, BranchYear, null, day);
        RunBank(without, c, BranchYear + 1, BranchYear, null, day);
        with.Branches.Clear();
        Check(OwnBankStore.Write(with) == OwnBankStore.Write(without), "bank branch: with no reach and no visits, a bank with branches runs as one without");
        Check(BankCity.Quiet(BranchYear).Reach == 0, "bank branch: a city as it comes gives no reach");
        string director = MemberText(BranchSource("OwnBankDirector.Branches.cs"), "public int Reach()");
        Check(director.Contains("Settings.EconomyBranch"), "bank branch: the reach is read only with the option on");
        string hour = MemberText(BranchSource("OwnBankDirector.Branches.cs"), "private void OnHour()");
        Check(hour.Contains("!Settings.EconomyBranch") && hour.Contains("_bank.CounterHour = now"), "bank branch: off, no hour is worked and none is owed when it is on again");
    }

    private static string BranchSource(string file)
    {
        return System.IO.File.ReadAllText(System.IO.Path.Combine(DesignScripts, "Economy", "OwnBank", "Branch", file));
    }

    // ---------------------------------------------------------------- the floor

    private static void BranchFloorChecks()
    {
        var plan = new List<FloorCustomer>();
        BranchRules.Floor(7, 1000, "Main~1", 20, 10, false, plan);
        int served = 0;
        bool sorted = true;
        for (int i = 0; i < plan.Count; i++)
        {
            served += plan[i].Served ? 1 : 0;
            sorted &= i == 0 || plan[i - 1].Minute <= plan[i].Minute;
            sorted &= plan[i].Minute >= 0 && plan[i].Minute < 60;
        }
        Check(plan.Count == BranchCatalogue.ShownMost && served == BranchCatalogue.ShownMost / 2 && sorted, "bank branch: the floor shows at most " + BranchCatalogue.ShownMost
                                                                                                             + " customers of an hour, the hour's share seen to, in the order they come");
        var again = new List<FloorCustomer>();
        BranchRules.Floor(7, 1000, "Main~1", 20, 10, false, again);
        bool same = again.Count == plan.Count;
        for (int i = 0; same && i < plan.Count; i++)
        {
            same = plan[i].Minute == again[i].Minute && plan[i].Served == again[i].Served && plan[i].Kind == again[i].Kind;
        }
        Check(same, "bank branch: the same hour shows the same customers");
        BranchRules.Floor(7, 1000, "Main~1", 0, 0, false, plan);
        Check(plan.Count == 0, "bank branch: nobody on the floor of an hour with no customers");
        BranchRules.Floor(7, 1000, "Main~1", 3, 0, false, plan);
        Check(plan.Count == 3 && !plan[0].Served && !plan[2].Served, "bank branch: nobody at the counter, nobody seen to on the floor");
    }

    // ---------------------------------------------------------------- the texts

    private static void BranchTextChecks()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Dictionary<string, string> pt = LoadWords(System.IO.Path.Combine(DesignLocales, "pt.json"));
        Dictionary<string, string> zh = LoadWords(System.IO.Path.Combine(DesignLocales, "zh-cn.json"));
        if (en == null || pt == null || zh == null)
        {
            Check(false, "bank branch: the three locales are where the checks look");
            return;
        }
        var keys = new List<string>
        {
            BranchCatalogue.TypeName, "reallife_branch_enabled", "reallife_opt_sec_bankbranch", "reallife_opt_tip_economy_bank_branch", "reallife_meta_title_economy_bank_branch",
            "reallife_guide_economy_bank_branch_about", "reallife_guide_economy_bank_branch", "reallife_branch_test_hour", "reallife_branch_test_floor",
            "reallife_branch_test_no_branch", "reallife_branch_test_not_inside", "reallife_branch_test_no_door", "reallife_branch_tab", "reallife_branch_col_branch",
            "reallife_branch_col_visitors", "reallife_branch_col_served", "reallife_branch_col_lost", "reallife_branch_col_opened", "reallife_branch_col_loans",
            "reallife_branch_col_fees", "reallife_branch_col_costs", "reallife_branch_empty", "reallife_branch_box_off", "reallife_branch_box_count", "reallife_branch_box_week",
            "reallife_branch_box_reach", "reallife_branch_box_reach_value", "reallife_branch_box_brings", "reallife_branch_box_brings_value", "reallife_branch_box_margin",
            "reallife_branch_box_fees", "reallife_branch_box_costs", "reallife_branch_box_none", "reallife_branch_box_pick", "reallife_branch_box_intro",
            "reallife_branch_chip_open", "reallife_branch_chip_closed", "reallife_branch_box_tellers", "reallife_branch_box_tellers_value", "reallife_branch_box_month",
            "reallife_branch_box_last", "reallife_branch_box_visitors", "reallife_branch_box_visitors_value", "reallife_branch_box_money", "reallife_branch_box_money_value",
            "reallife_branch_box_lending", "reallife_branch_box_lending_value", "reallife_branch_box_counter_note", "reallife_branch_counter_title", "reallife_branch_counter_line",
            "reallife_branch_counter_staff", "reallife_branch_counter_hours", "reallife_branch_counter_closed", "reallife_branch_toast_queue", "reallife_branch_toast_counter"
        };
        int missing = 0;
        foreach (string key in keys)
        {
            missing += en.ContainsKey(key) && pt.ContainsKey(key) && zh.ContainsKey(key) ? 0 : 1;
        }
        Check(missing == 0, "bank branch: every text is in en, pt and zh-cn (" + missing + " missing)");
        string[] names = { "branch", "lost" };
        bool placeholders = true;
        foreach (string name in names)
        {
            placeholders &= Has(en, "reallife_branch_toast_queue", "{" + name + "}") && Has(pt, "reallife_branch_toast_queue", "{" + name + "}") && Has(zh, "reallife_branch_toast_queue", "{" + name + "}");
        }
        Check(placeholders && Has(zh, "reallife_branch_toast_counter", "{served}") && Has(pt, "reallife_branch_box_reach_value", "{firms}"), "bank branch: the notices' names are in every language");
        Check(en["reallife_branch_toast_queue"].Length + 40 <= 240 && en["reallife_branch_toast_counter"].Length + 40 <= 240, "bank branch: the notices fit the game's notification with their names filled");
        string page = en["reallife_guide_economy_bank_branch"];
        bool facts = true;
        foreach (string fact in new[] { "branch_serve", "branch_open_fee", "branch_counter_fee", "branch_loan_fee", "branch_reach_most", "branch_reach_half", "branch_firms", "branch_designs_most" })
        {
            facts &= page.Contains("{" + fact + "}");
        }
        Check(facts, "bank branch: the Guide page takes its figures from the code");
        Check(en[BranchCatalogue.TypeName] != "Bank", "bank branch: the type's name is not the game's own banks'");
        bool line = false;
        foreach (KeyValuePair<string, string> row in en)
        {
            line |= row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal) && row.Value.IndexOf("bank branch of your own", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        Check(line, "bank branch: What's new has the bank branch's line, in the next row or a version's");
    }

    // ---------------------------------------------------------------- time

    private static void BranchTimingChecks()
    {
        BankCity c = BankCity.Quiet(BranchYear);
        OwnBank b = YearBank(c);
        var list = new List<BankBranch>();
        for (int i = 0; i < BranchCatalogue.BranchesMost; i++)
        {
            var br = new BankBranch { Place = "Time~" + i, MonthFrom = BranchYear };
            b.Branches.Add(br);
            list.Add(br);
        }
        BranchHours(b, list, c.Level, 24);   // warmed
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        BranchHours(b, list, c.Level, 240);
        double perHour = watch.Elapsed.TotalMilliseconds / 240.0;
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Check(perHour < 2.0, "bank branch: an hour of " + BranchCatalogue.BranchesMost + " branches in under 2 ms (" + perHour.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " ms)");
        Check(allocated < 4096, "bank branch: an hour of the branches allocates nothing (" + allocated + " bytes over 240 hours)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void BranchHours(OwnBank b, List<BankBranch> list, int level, int hours)
    {
        BranchHourIn mid = MidBranch();
        for (int h = 0; h < hours; h++)
        {
            for (int n = 0; n < list.Count; n++)
            {
                BranchRules.Hour(b, list[n], mid, (BranchYear + 3) * 24 + h, level, 5);
            }
        }
    }
}
