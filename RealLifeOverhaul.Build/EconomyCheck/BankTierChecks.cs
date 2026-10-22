using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;

// The bank's levels and the owner's pay (Scripts/Economy/OwnBank/Tiers): the table; each level's limits binding in its day
// and at its counters, and a bank over a limit keeping what it holds; moving up and each of its conditions, the record
// begun again by a breach; an older save's bank fitted to its level with its loans, savers and branches kept; the save's
// new fields; the owner's pay splitting each day's profit exactly and the same on every run; the balance figures against
// their aims; the texts and the slider.
internal static partial class Program
{
    private const int TierYear = 60;   // the game's own year: a month of five days

    // The aims the table is held to besides BankTiers.PaybackMonthsMost.
    private const long FoundMostBefore = 50000000L;   // $500,000: founding a community bank without the end game's scale, a shop or two's takings in the middle of a game
    private const long FoundMostScale = 200000000L;   // $2,000,000: the same with the scale on (its default), which raises only the late ventures' prices
    private const int FoundShareMost = 100;           // thousandths: a community bank costs at most a tenth of founding a bank before levels
    private const int PaybackMonthsLeast = 3;         // its licence is not paid back in less than this many months: a licence is no gift
    private const int UpPaybackMonthsMost = 36;       // a move up's licence is paid back by what the larger bank adds within this many months (the end game's own aim at the top)
    private const int GrowMonthsMost = 36;            // a community bank keeping its profit has a regional bank's capital within this many months

    private static void BankLevels()
    {
        int before = _run, failedBefore = _failed;
        var watch = Stopwatch.StartNew();
        TierTableChecks();
        TierLimitChecks();
        TierCounterChecks();
        TierMoveUpChecks();
        TierOldSaveChecks();
        TierStoreChecks();
        TierPayChecks();
        TierBalanceChecks();
        TierTextChecks();
        TierTimingChecks();
        Console.WriteLine("  Bank levels and the owner's pay: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // A quiet city of the game's year at a share of the end game's scale.
    private static BankCity TierCity(int share, int year = TierYear)
    {
        BankCity c = BankCity.Quiet(year);
        c.Scale = share;
        return c;
    }

    // A bank founded at a level with that level's least capital, as the director founds one, in a city at its share of the scale.
    private static OwnBank TierBank(BankCity c, int tier, int seed = 7, long extra = 0)
    {
        var b = new OwnBank();
        int buy = EndScaleRules.Buying(c.Scale > 0);
        OwnBankRules.Found(b, 1, seed, OwnBankRules.TierCapital(tier, c.Level, 0, buy) + extra, "Tier Savings", tier);
        b.FoundedLeast = OwnBankRules.TierLeastAt(tier, buy, 0);
        return b;
    }

    // ---------------------------------------------------------------- the table

    private static void TierTableChecks()
    {
        int n = BankTiers.Count;
        Check(BankTiers.NameKeys.Length == n && BankTiers.LicenceCents.Length == n && BankTiers.LicenceCentsScale.Length == n && BankTiers.CapitalCents.Length == n
              && BankTiers.CapitalCentsScale.Length == n && BankTiers.BranchesMost.Length == n && BankTiers.LoansCents.Length == n && BankTiers.LoansCentsScale.Length == n
              && BankTiers.DepositsCents.Length == n && BankTiers.DepositsCentsScale.Length == n && BankTiers.RecordMonths.Length == n && BankTiers.HallDayCents.Length == n
              && BankTiers.SystemsDayCents.Length == n && BankTiers.ComplianceLeast.Length == n,
            "bank levels: a figure of every table for every level");
        int top = BankTiers.National;
        Check(BankTiers.LicenceCents[top] == OwnBankCatalogue.LicenceCents && BankTiers.LicenceCentsScale[top] == OwnBankCatalogue.LicenceCentsScale
              && BankTiers.CapitalCents[top] == OwnBankCatalogue.CapitalLeastCents && BankTiers.CapitalCentsScale[top] == OwnBankCatalogue.CapitalLeastCentsScale
              && BankTiers.LoansCents[top] == BankTiers.NoLimit && BankTiers.LoansCentsScale[top] == BankTiers.NoLimit && BankTiers.DepositsCents[top] == BankTiers.NoLimit
              && BankTiers.DepositsCentsScale[top] == BankTiers.NoLimit && BankTiers.BranchesMost[top] == BranchCatalogue.BranchesMost
              && BankTiers.HallDayCents[top] == OwnBankCatalogue.StaffDayCents && BankTiers.SystemsDayCents[top] == OwnBankCatalogue.SystemsDayCents
              && BankTiers.ComplianceLeast[top] == OwnBankCatalogue.ComplianceLeast,
            "bank levels: a national bank is the bank as it was before levels: its licence, capital, staff, systems and compliance, sixteen branches and no limit but the ratio");
        bool rising = true;
        for (int t = 1; t < n; t++)
        {
            rising &= BankTiers.LicenceCents[t] > BankTiers.LicenceCents[t - 1] && BankTiers.LicenceCentsScale[t] > BankTiers.LicenceCentsScale[t - 1]
                      && BankTiers.CapitalCents[t] > BankTiers.CapitalCents[t - 1] && BankTiers.CapitalCentsScale[t] > BankTiers.CapitalCentsScale[t - 1]
                      && BankTiers.BranchesMost[t] > BankTiers.BranchesMost[t - 1] && BankTiers.LoansCents[t] > BankTiers.LoansCents[t - 1]
                      && BankTiers.LoansCentsScale[t] > BankTiers.LoansCentsScale[t - 1] && BankTiers.DepositsCents[t] > BankTiers.DepositsCents[t - 1]
                      && BankTiers.DepositsCentsScale[t] > BankTiers.DepositsCentsScale[t - 1] && BankTiers.RecordMonths[t] > BankTiers.RecordMonths[t - 1]
                      && BankTiers.HallDayCents[t] > BankTiers.HallDayCents[t - 1] && BankTiers.SystemsDayCents[t] > BankTiers.SystemsDayCents[t - 1]
                      && BankTiers.ComplianceLeast[t] > BankTiers.ComplianceLeast[t - 1];
        }
        Check(rising && BankTiers.RecordMonths[BankTiers.Community] == 0, "bank levels: each level up costs more, holds more, asks a longer record and costs more to run");
        bool deposits = true;
        for (int t = 0; t < top; t++)
        {
            // a level's loans are funded by its savings and capital: its book fits in what it may hold, its reserve kept
            deposits &= BankTiers.LoansCents[t] <= (BankTiers.DepositsCents[t] * (1000 - OwnBankCatalogue.ReserveShare) / 1000) + BankTiers.CapitalCents[t]
                        && BankTiers.LoansCentsScale[t] <= (BankTiers.DepositsCentsScale[t] * (1000 - OwnBankCatalogue.ReserveShare) / 1000) + BankTiers.CapitalCentsScale[t];
        }
        Check(deposits, "bank levels: each level's book fits in the savings it may hold, with their reserve kept");
        foreach (int level in new[] { PriceRules.Start, 1370, PriceRules.Most })
        {
            foreach (int share in new[] { 0, 500, EndScaleRules.Full })
            {
                long climbed = OwnBankRules.TierLicence(BankTiers.Community, level, share) + OwnBankRules.MoveUpFee(BankTiers.Regional, level, share)
                               + OwnBankRules.MoveUpFee(BankTiers.National, level, share);
                Check(climbed == OwnBankRules.TierLicence(BankTiers.National, level, share) && OwnBankRules.TierLicence(BankTiers.National, level, share) == OwnBankRules.Licence(level, share),
                    "bank levels: a bank climbed to the top has paid exactly a national licence, what founding cost before levels (price level " + level + ", scale " + share + ")");
            }
        }
        long before = OwnBankRules.TierLicence(BankTiers.Community, PriceRules.Start, 0) + OwnBankRules.TierCapital(BankTiers.Community, PriceRules.Start, 0, 0);
        long scale = OwnBankRules.TierLicence(BankTiers.Community, PriceRules.Start, EndScaleRules.Full) + OwnBankRules.TierCapital(BankTiers.Community, PriceRules.Start, 0, EndScaleRules.Full);
        long oldBefore = OwnBankRules.Licence(PriceRules.Start, 0) + OwnBankRules.CapitalLeast(PriceRules.Start, 0, 0);
        long oldScale = OwnBankRules.Licence(PriceRules.Start, EndScaleRules.Full) + OwnBankRules.CapitalLeast(PriceRules.Start, 0, EndScaleRules.Full);
        Console.WriteLine("    founding a community bank: " + Dollars(before) + " (a bank was " + Dollars(oldBefore) + ") without the end game's scale, " + Dollars(scale) + " (was "
                          + Dollars(oldScale) + ") with it");
        Check(before <= FoundMostBefore && scale <= FoundMostScale, "bank levels: a community bank is founded for at most " + Dollars(FoundMostBefore) + " without the scale and "
                                                                    + Dollars(FoundMostScale) + " with it, within reach in the middle of a game (" + Dollars(before) + ", " + Dollars(scale) + ")");
        Check(before * 1000 <= oldBefore * FoundShareMost && scale * 1000 <= oldScale * FoundShareMost,
            "bank levels: founding costs at most a tenth of what a bank cost before levels, with the scale and without");
        Check(OwnBankRules.TierCapital(BankTiers.Community, PriceRules.Start, 1, 0) == OwnBankRules.TierCapital(BankTiers.Community, PriceRules.Start, 0, 0) * 3 / 2
              && OwnBankRules.TierCapital(BankTiers.Community, PriceRules.Start, 2, 0) == OwnBankRules.TierCapital(BankTiers.Community, PriceRules.Start, 0, 0) * 2,
            "bank levels: a bank founded after a closure pays in half as much capital again for each, at the first level too");
        string director = System.IO.File.ReadAllText(System.IO.Path.Combine(DesignScripts, "Economy", "OwnBank", "OwnBankDirector.cs"));
        string tiers = System.IO.File.ReadAllText(System.IO.Path.Combine(DesignScripts, "Economy", "OwnBank", "Tiers", "OwnBankDirector.Tiers.cs"));
        string found = MemberText(director, "public bool Found(");
        Check(found.Contains("FoundLicence(level, share)") && found.Contains("FoundCapital(level, _bank.Closures, share)") && found.Contains("BankTiers.Community)")
              && tiers.Contains("OwnBankRules.TierLicence(BankTiers.Community, level, share)") && tiers.Contains("OwnBankRules.TierCapital(BankTiers.Community, level, closures, share)")
              && MemberText(director, "private void StartUp()").Contains("Fit(bank);"),
            "bank levels: the director founds a community bank at its licence and capital, and fits an older save's bank as it reads it");
        var five = new OwnBank();
        OwnBankRules.Found(five, 1, 3, OwnBankRules.CapitalLeast(PriceRules.Start), "Five");
        var six = new OwnBank();
        OwnBankRules.Found(six, 1, 3, OwnBankRules.CapitalLeast(PriceRules.Start), "Five", BankTiers.Community);
        Check(five.Tier == BankTiers.National && six.Tier == BankTiers.Community && six.TierFrom == 1 && six.PayShare == 0 && six.PaidAll == 0 && six.TierTold == BankTiers.Unknown,
            "bank levels: a bank founded as before levels is a national one; the director founds a community bank, its record from today and nothing paid out");
        Check(BankTiers.PayShareMost % BankTiers.PayStep == 0 && OwnBankRules.PayShareStep(37) == 0 && OwnBankRules.PayShareStep(349) == 300 && OwnBankRules.PayShareStep(1200) == 1000
              && OwnBankRules.PayShareStep(-5) == 0, "bank levels: the owner's share moves in steps of " + BankTiers.PayStep / 10 + "% from nothing to all of the profit");
    }

    private static string Dollars(long cents) => "$" + (cents / 100).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------- the limits in the day

    private static void TierLimitChecks()
    {
        foreach (int share in new[] { 0, EndScaleRules.Full })
        {
            string at = share > 0 ? " (at scale)" : " (before the scale)";
            BankCity c = TierCity(share);
            BankCaps caps = OwnBankRules.CapsOf(BankTiers.Community, c.Level, share);

            // Savers: a trusted bank paying two points over the rivals wants far more than its level may hold.
            OwnBank s = TierBank(c, BankTiers.Community, 11, OwnBankRules.TierCapital(BankTiers.Community, c.Level, 0, share) * 4);
            s.SaverOffset[0] = s.SaverOffset[1] = 200;
            var day = new BankDay();
            bool held = true, balanced = true;
            long most = 0;
            for (int d = 2; d < 2 + 3 * TierYear && s.Running; d++)
            {
                OwnBankRules.Day(s, c, d, null, day);
                held &= s.Savings <= caps.Deposits || day.Deposited == 0;
                balanced &= OwnBankRules.Balanced(s);
                most = Math.Max(most, s.Savings);
            }
            Check(held && balanced && s.Running, "bank levels: a community bank takes no saver's money past its level's savings; its interest is still the savers'" + at);
            Check(most >= caps.Deposits * 98 / 100 && OwnBankRules.SaversWanted(c, s, 0, 1000) + OwnBankRules.SaversWanted(c, s, 1, 1000) > caps.Deposits * 3 / 2,
                "bank levels: the savings limit binds: the city would bring far more and the bank fills its level (" + Dollars(most) + " of " + Dollars(caps.Deposits) + ")" + at);

            // Loans: capital to spare, so the ratio never binds first, and borrowers who want more than its book.
            OwnBank l = TierBank(c, BankTiers.Community, 13, OwnBankRules.TierCapital(BankTiers.Community, c.Level, 0, share) * 20);
            l.LoanOffset[OwnBankCatalogue.Good] = l.LoanOffset[OwnBankCatalogue.Companies] = -200;
            bool booked = true;
            long book = 0;
            for (int d = 2; d < 2 + 3 * TierYear && l.Running; d++)
            {
                OwnBankRules.Day(l, c, d, null, day);
                booked &= day.Lent == 0 || l.Lent + l.InArrears <= caps.Loans;
                book = Math.Max(book, l.Lent + l.InArrears);
            }
            long wanted = 0;
            for (int j = 0; j < OwnBankCatalogue.Segments; j++)
            {
                wanted += OwnBankRules.LoansWanted(c, l, j);
            }
            Check(booked && l.Running && OwnBankRules.Balanced(l), "bank levels: a community bank lends no further than its level's book, its capital to spare" + at);
            Check(book >= caps.Loans * 97 / 100 && wanted > caps.Loans * 3 / 2 && OwnBankRules.Ratio(l) > OwnBankCatalogue.LendRatio,
                "bank levels: the book binds before the ratio: borrowers want more and the bank holds its level's most (" + Dollars(book) + " of " + Dollars(caps.Loans) + ")" + at);

            // A bank over its limits keeps all of it, and takes no more.
            OwnBank over = TierBank(c, BankTiers.Community, 17, OwnBankRules.TierCapital(BankTiers.Community, c.Level, 0, share) * 20);
            over.Deposits[OwnBankCatalogue.People] = caps.Deposits * 2;
            over.Loans[OwnBankCatalogue.Good] = caps.Loans * 2;
            over.Reserves = over.Capital + over.Savings - over.Lent;
            long lentBefore = over.Lent;
            bool none = true;
            for (int d = 2; d < 2 + TierYear / 6 && over.Running; d++)
            {
                OwnBankRules.Day(over, c, d, null, day);
                none &= day.Lent == 0 && day.Deposited == 0;
            }
            Check(none && over.Running && OwnBankRules.Balanced(over) && over.Lent > caps.Loans && over.Lent < lentBefore && over.Savings > caps.Deposits,
                "bank levels: a bank over its level's limits keeps its loans and savers, repaid and withdrawn as ever, and takes no new loan or saver's money" + at);
        }

        // Branches: the level's count, and no more.
        var b = new OwnBank();
        OwnBankRules.Found(b, 1, 3, OwnBankRules.TierCapital(BankTiers.Community, PriceRules.Start, 0, 0), "Branches", BankTiers.Community);
        bool room = OwnBankRules.BranchRoom(b);
        for (int i = 0; i < BankTiers.BranchesMost[BankTiers.Community]; i++)
        {
            b.Branches.Add(new BankBranch { Place = "Count~" + i });
        }
        bool full = !OwnBankRules.BranchRoom(b);
        b.Tier = BankTiers.Regional;
        bool more = OwnBankRules.BranchRoom(b);
        b.Tier = BankTiers.National;
        for (int i = b.Branches.Count; i < BranchCatalogue.BranchesMost; i++)
        {
            b.Branches.Add(new BankBranch { Place = "Count~" + i });
        }
        Check(room && full && more && !OwnBankRules.BranchRoom(b) && OwnBankRules.Caps(b, PriceRules.Start, 0).Branches == BranchCatalogue.BranchesMost,
            "bank levels: a community bank takes in " + BankTiers.BranchesMost[0] + " branches and no more, a regional one more, a national one the sixteen a bank keeps");
        string reads = MemberText(BranchSource("OwnBankDirector.Branches.cs"), "private void ReadBranches()");
        Check(reads.Contains("OwnBankRules.BranchRoom(_bank)") && reads.IndexOf("RemoveAt", StringComparison.Ordinal) < reads.IndexOf("BranchRoom", StringComparison.Ordinal)
              && reads.Contains("TellWaiting"),
            "bank levels: the director takes a branch in only while its level has room, lets a closed branch go first, and tells the player of one that waits");
        string second = MemberText(BranchSource("OwnBankDirector.Branches.cs"), "private void BranchSecond()");
        Check(second.Contains("_bank.Branch(place) != null ? reg : null"), "bank levels: a branch waiting for a larger licence has no counter to work");

        // A national bank founded at its level runs exactly as one founded as before levels.
        BankCity q = TierCity(0);
        var one = new OwnBank();
        OwnBankRules.Found(one, 1, 9, OwnBankRules.CapitalLeast(q.Level), "Same");
        var two = new OwnBank();
        OwnBankRules.Found(two, 1, 9, OwnBankRules.CapitalLeast(q.Level), "Same", BankTiers.National);
        RunBank(one, q, 2, 2 * TierYear, null, new BankDay());
        RunBank(two, q, 2, 2 * TierYear, null, new BankDay());
        Check(OwnBankStore.Write(one) == OwnBankStore.Write(two) && OwnBankRules.LoanRoom(one, OwnBankRules.Caps(one, q.Level, 0)) == BankTiers.NoLimit
              && OwnBankRules.DepositRoom(one, OwnBankRules.Caps(one, q.Level, EndScaleRules.Full)) == BankTiers.NoLimit,
            "bank levels: a national bank has no limit on its savings or loans, and runs as every bank ran before levels");
    }

    // ---------------------------------------------------------------- the limits at the counter

    private static void TierCounterChecks()
    {
        BankCity c = TierCity(0);
        int level = c.Level;
        OwnBank b = TierBank(c, BankTiers.Community, 19, OwnBankRules.TierCapital(BankTiers.Community, level, 0, 0) * 20);
        BankCaps caps = OwnBankRules.Caps(b, level, 0);
        b.Deposits[OwnBankCatalogue.People] = caps.Deposits - 30000;   // $300 of room left
        b.Reserves = b.Capital + b.Savings - b.Lent;
        long savings = b.Savings, reserves = b.Reserves;
        VisitResult r = OwnBankRules.Visit(b, BranchCatalogue.Deposit, OwnBankCatalogue.Fair, 20000, level, caps, out long fee, out long moved);
        Check(r == VisitResult.PaidIn && moved == 20000 && fee > 0 && b.Savings == savings + 20000, "bank levels: a deposit the room still takes is paid in at the counter");
        savings = b.Savings;
        reserves = b.Reserves;
        long fees = b.CounterFees;
        r = OwnBankRules.Visit(b, BranchCatalogue.Open, OwnBankCatalogue.Fair, 40000, level, caps, out fee, out moved);
        Check(r == VisitResult.Full && moved == 0 && fee == 0 && b.Savings == savings && b.Reserves == reserves && b.CounterFees == fees && OwnBankRules.Balanced(b),
            "bank levels: at its level's savings, a customer who comes to open an account or pay in is turned away: nothing moves, and no fee");
        r = OwnBankRules.Visit(b, BranchCatalogue.Withdraw, OwnBankCatalogue.Fair, 30000, level, caps, out fee, out moved);
        Check(r == VisitResult.TakenOut && moved == 30000, "bank levels: a full bank still pays its savers out at the counter");

        OwnBank l = TierBank(c, BankTiers.Community, 23, OwnBankRules.TierCapital(BankTiers.Community, level, 0, 0) * 20);
        l.Deposits[OwnBankCatalogue.People] = caps.Deposits / 2;
        l.Loans[OwnBankCatalogue.Good] = caps.Loans - 100000;   // $1,000 of room left
        l.Reserves = l.Capital + l.Savings - l.Lent;
        Check(OwnBankRules.CounterLends(l, OwnBankCatalogue.Good, 90000, caps) && !OwnBankRules.CounterLends(l, OwnBankCatalogue.Good, 600000, caps),
            "bank levels: a loan at the counter only within the room its level's book leaves");
        r = OwnBankRules.Visit(l, BranchCatalogue.Loan, OwnBankCatalogue.Good, 600000, level, caps, out fee, out moved);
        Check(r == VisitResult.Refused && fee == 0 && l.Lent == caps.Loans - 100000, "bank levels: a loan past its level's book is turned down at the counter");
        BankCaps open = OwnBankRules.CapsOf(BankTiers.National, level, 0);
        Check(OwnBankRules.CounterLends(l, OwnBankCatalogue.Good, 600000, open), "bank levels: the same loan with no limit on the book is made");

        // An hour of a branch of a full bank: those who came to pay in are turned away, the rest seen to.
        OwnBank hourBank = TierBank(c, BankTiers.Community, 29, OwnBankRules.TierCapital(BankTiers.Community, level, 0, 0) * 20);
        hourBank.Deposits[OwnBankCatalogue.People] = caps.Deposits;
        hourBank.Reserves = hourBank.Capital + hourBank.Savings;
        var br = new BankBranch { Place = "Full~1", MonthFrom = 1 };
        hourBank.Branches.Add(br);
        int turned = 0;
        bool under = true;
        for (int h = 9; h < 18; h++)
        {
            BranchHourOut o = BranchRules.Hour(hourBank, br, MidBranch(), 2 * 24 + h, level, 5, 0);
            turned += o.Full;
            under &= hourBank.Savings <= caps.Deposits;
        }
        Check(turned > 0 && under && OwnBankRules.Balanced(hourBank),
            "bank levels: a day at a full bank's branch turns away " + turned + " who came to pay in, takes no more than its room, and pays its savers out");
    }

    // ---------------------------------------------------------------- moving up

    private static void TierMoveUpChecks()
    {
        BankCity c = TierCity(0);
        int level = c.Level, m = c.MonthDays, buy = 0;
        OwnBank b = TierBank(c, BankTiers.Community, 31);
        int months = BankTiers.RecordMonths[BankTiers.Regional];
        Check(OwnBankRules.MoveUpWhy(b, 1, m, level, buy) == OwnBankRules.UpRecordKey && OwnBankRules.MoveUpWhy(b, 1 + months * m - 1, m, level, buy) == OwnBankRules.UpRecordKey,
            "bank levels: a new community bank waits " + months + " months at its level before it may move up");
        Check(OwnBankRules.MoveUpWhy(b, 1 + months * m, m, level, buy) == OwnBankRules.UpCapitalKey, "bank levels: its record met, it still needs the next level's least capital");
        long least = OwnBankRules.TierCapital(BankTiers.Regional, level, 0, buy);
        OwnBankRules.PutIn(b, OwnBankRules.ToStep(least - OwnBankRules.CapitalNow(b) + OwnBankCatalogue.CapitalStepCents));
        Check(OwnBankRules.CapitalNow(b) >= least && OwnBankRules.MoveUpWhy(b, 1 + months * m, m, level, buy) == null, "bank levels: the record and the capital met, it may move up");
        b.Stage = Regulator.Watch;
        string watched = OwnBankRules.MoveUpWhy(b, 1 + months * m, m, level, buy);
        b.Stage = Regulator.Breach;
        string breached = OwnBankRules.MoveUpWhy(b, 1 + months * m, m, level, buy);
        b.Stage = Regulator.Clear;
        b.Run = true;
        string run = OwnBankRules.MoveUpWhy(b, 1 + months * m, m, level, buy);
        b.Run = false;
        Check(watched == OwnBankRules.UpRegulatorKey && breached == OwnBankRules.UpRegulatorKey && run == OwnBankRules.UpRegulatorKey,
            "bank levels: no move up while the regulator watches the bank, it is in breach or its savers run");
        Check(OwnBankRules.MoveUpWhy(b, 1 + months * m, m, level, EndScaleRules.Full) == OwnBankRules.UpCapitalKey,
            "bank levels: at the end game's scale the next level asks the scale's capital");
        long fee = OwnBankRules.MoveUpFee(BankTiers.Regional, level, buy);
        Check(fee == OwnBankRules.TierLicence(BankTiers.Regional, level, buy) - OwnBankRules.TierLicence(BankTiers.Community, level, buy) && fee > 0,
            "bank levels: moving up costs the next level's licence less the one the bank holds (" + Dollars(fee) + ")");
        int day = 1 + months * m;
        OwnBankRules.MoveUp(b, day, buy);
        Check(b.Tier == BankTiers.Regional && b.TierFrom == day && b.FoundedLeast == OwnBankRules.TierLeastAt(BankTiers.Regional, buy, 0)
              && OwnBankRules.MoveUpWhy(b, day, m, level, buy) == OwnBankRules.UpRecordKey && OwnBankRules.MostOut(b, level) <= OwnBankRules.CapitalNow(b) - least,
            "bank levels: moved up, it is a regional bank, its record begins again and it keeps the regional least capital from now");
        int national = BankTiers.RecordMonths[BankTiers.National];
        Check(OwnBankRules.MoveUpWhy(b, day + national * m, m, level, buy) == OwnBankRules.UpCapitalKey && OwnBankRules.MoveUpWhy(b, day + national * m - 1, m, level, buy) == OwnBankRules.UpRecordKey,
            "bank levels: a national licence asks " + national + " months as a regional bank, and a national bank's capital");
        b.Tier = BankTiers.National;
        Check(OwnBankRules.MoveUpWhy(b, day + 100 * m, m, level, buy) == OwnBankRules.UpTopKey && OwnBankRules.NextTier(b) == BankTiers.National,
            "bank levels: a national bank has no level above it");
        Check(OwnBankRules.MoveUpWhy(new OwnBank(), 5, m, level, buy) == "reallife_ob_why_none", "bank levels: no bank, nothing to move up");

        // A breach begins the record again.
        OwnBank r = TierBank(c, BankTiers.Community, 37);
        r.Loans[OwnBankCatalogue.Fair] = r.Capital * 12;    // a ratio of about 8%: in breach, not closed
        r.Deposits[OwnBankCatalogue.People] = r.Capital * 14;
        r.Reserves = r.Capital + r.Savings - r.Lent;
        for (int j = 0; j < OwnBankCatalogue.Segments; j++)
        {
            r.Lending[j] = false;
        }
        var bd = new BankDay();
        int breachDay = 1 + 8 * m;
        OwnBankRules.Day(r, c, breachDay, null, bd);
        Check(bd.Breached && r.TierFrom == breachDay && OwnBankRules.MonthsAtTier(r, breachDay + months * m - 1, m) == months - 1,
            "bank levels: a breach begins the bank's record at its level again: " + months + " months from its last day in breach");

        // The save's own months: a year of 365 days asks as many of its months.
        BankCity year = TierCity(0, 365);
        OwnBank longYear = TierBank(year, BankTiers.Community, 41);
        Check(OwnBankRules.MoveUpWhy(longYear, 1 + months * year.MonthDays - 1, year.MonthDays, year.Level, 0) == OwnBankRules.UpRecordKey
              && OwnBankRules.MonthsAtTier(longYear, 1 + months * year.MonthDays, year.MonthDays) == months,
            "bank levels: the record is in the save's own months, whatever its year");

        // Moved up, a bank at its old level's book lends again.
        BankCity q = TierCity(0);
        OwnBank grow = TierBank(q, BankTiers.Community, 43, OwnBankRules.TierCapital(BankTiers.Regional, q.Level, 0, 0) * 2);
        grow.LoanOffset[OwnBankCatalogue.Good] = -200;
        RunBank(grow, q, 2, 2 * TierYear, null, bd);
        BankCaps small = OwnBankRules.Caps(grow, q.Level, 0);
        long held = grow.Lent + grow.InArrears;
        OwnBankRules.Day(grow, q, 2 + 2 * TierYear, null, bd);
        long lentAt = bd.Lent;
        OwnBankRules.MoveUp(grow, 3 + 2 * TierYear, 0);
        OwnBankRules.Day(grow, q, 3 + 2 * TierYear, null, bd);
        Check(held >= small.Loans * 97 / 100 && lentAt < bd.Lent && bd.Lent > 0 && grow.Lent + grow.InArrears > held,
            "bank levels: a community bank at its book lends again the day after it moves up");
    }

    // ---------------------------------------------------------------- an older save's bank

    private static void TierOldSaveChecks()
    {
        BankCity c = TierCity(0);
        int level = c.Level;
        var day = new BankDay();

        // A bank founded before levels, its capital as it was: a national bank, everything as it was.
        var old = new OwnBank();
        OwnBankRules.Found(old, 1, 47, OwnBankRules.CapitalLeast(level), "Old Savings");
        RunBank(old, c, 2, TierYear, null, day);
        for (int i = 0; i < 9; i++)
        {
            old.Branches.Add(new BankBranch { Place = "Old~" + i, Name = "Old " + i, MonthFrom = 2 });
        }
        string line = HeadCut(OwnBankStore.Write(old), 35);   // a line as 0.47 writes it: no level, no pay
        OwnBank read = OwnBankStore.Read(line, out _);
        Check(read != null && read.Tier == BankTiers.Unknown, "bank levels: a line written before levels reads with its level not yet known");
        int fit = OwnBankRules.FitTier(read, level);
        Check(fit == BankTiers.National && read.Tier == BankTiers.National && read.FoundedLeast == old.FoundedLeast && read.TierFrom == old.FoundedDay && read.PayShare == 0
              && read.Branches.Count == 9 && read.Lent == old.Lent && read.Savings == old.Savings && OwnBankRules.Balanced(read),
            "bank levels: an older save's bank that still holds the capital it was founded with is a national bank, as it paid for, and keeps every branch, loan and saver");

        // One whose capital has fallen under its least: the highest level it meets, all it holds kept, nothing more taken in.
        BankCaps regional = OwnBankRules.CapsOf(BankTiers.Regional, level, 0);
        long regionalLeast = OwnBankRules.TierCapital(BankTiers.Regional, level, 0, 0);
        var fallen = new OwnBank();
        long fallenCapital = (regionalLeast + OwnBankRules.CapitalLeast(level)) / 2;   // founded before levels, its capital since fallen under its least
        OwnBankRules.Found(fallen, 1, 53, fallenCapital, "Fallen Savings");
        fallen.Deposits[OwnBankCatalogue.People] = regional.Deposits * 3 / 2;   // more savings than a regional bank may hold
        fallen.Loans[OwnBankCatalogue.Good] = regional.Loans * 3 / 2;           // and a larger book, its ratio still sound
        fallen.Reserves = fallen.Capital + fallen.Savings - fallen.Lent;
        for (int i = 0; i < 10; i++)
        {
            fallen.Branches.Add(new BankBranch { Place = "Fallen~" + i, MonthFrom = 2 });
        }
        OwnBank back = OwnBankStore.Read(HeadCut(OwnBankStore.Write(fallen), 35), out _);
        int fitted = OwnBankRules.FitTier(back, level);
        BankCaps caps = OwnBankRules.Caps(back, level, 0);
        Check(fitted == BankTiers.Regional && back.FoundedLeast == OwnBankRules.TierLeastAt(BankTiers.Regional, 0, 0) && back.Branches.Count == 10 && back.Savings > caps.Deposits
              && back.Lent > caps.Loans && OwnBankRules.Balanced(back),
            "bank levels: an older bank whose capital fell under its least is put at the highest level it meets, and keeps its ten branches, its savers and its loans past that level's limits");
        bool grandfathered = true;
        long lent = back.Lent;
        for (int d = TierYear + 2; d < TierYear + 2 + 2 * c.MonthDays && back.Running; d++)
        {
            OwnBankRules.Day(back, c, d, null, day);
            grandfathered &= day.Lent == 0 && day.Deposited == 0;
        }
        Check(grandfathered && back.Running && back.Lent < lent && back.Branches.Count == 10 && !OwnBankRules.BranchRoom(back),
            "bank levels: over its limits it makes no new loan, takes no saver's money and opens no branch, while what it holds is repaid and kept");
        var poor = new OwnBank();
        OwnBankRules.Found(poor, 1, 59, OwnBankRules.CapitalLeast(level), "Poor Savings");
        poor.Capital = OwnBankRules.TierCapital(BankTiers.Community, level, 0, 0) / 2;
        poor.Reserves = poor.Capital;
        OwnBank poorBack = OwnBankStore.Read(HeadCut(OwnBankStore.Write(poor), 35), out _);
        Check(OwnBankRules.FitTier(poorBack, level) == BankTiers.Community && poorBack.FoundedLeast == OwnBankRules.TierLeastAt(BankTiers.Community, 0, 0),
            "bank levels: one under even a community bank's least is a community bank");

        // Founded at the end game's scale, or after a closure: fitted on the terms it was founded on.
        BankCity s = TierCity(EndScaleRules.Full);
        var scaled = new OwnBank();
        OwnBankRules.Found(scaled, 1, 61, OwnBankRules.CapitalLeast(s.Level, 0, EndScaleRules.Full), "Scale Savings");
        scaled.FoundedLeast = OwnBankRules.LeastAt(EndScaleRules.Full);
        OwnBank scaledBack = OwnBankStore.Read(HeadCut(OwnBankStore.Write(scaled), 35), out _);
        Check(OwnBankRules.FoundedShare(scaled.FoundedLeast, 0) == EndScaleRules.Full && OwnBankRules.FitTier(scaledBack, s.Level) == BankTiers.National,
            "bank levels: a bank founded at the end game's scale is held to the scale's least, and with it is a national bank");
        scaledBack.Capital = OwnBankRules.TierCapital(BankTiers.Regional, s.Level, 0, EndScaleRules.Full) + OwnBankCatalogue.CapitalStepCents;
        scaledBack.Reserves = scaledBack.Capital;
        scaledBack.Tier = BankTiers.Unknown;
        Check(OwnBankRules.FitTier(scaledBack, s.Level) == BankTiers.Regional && scaledBack.FoundedLeast == OwnBankRules.TierLeastAt(BankTiers.Regional, EndScaleRules.Full, 0),
            "bank levels: fallen under the scale's national least, it is fitted by the scale's figures");
        var again = new OwnBank { Closures = 2 };
        OwnBankRules.Found(again, 1, 67, OwnBankRules.CapitalLeast(level, 2, 0), "Again Savings");
        again.FoundedLeast = OwnBankRules.LeastAt(0, 2);
        Check(OwnBankRules.FoundedShare(again.FoundedLeast, 2) == 0 && OwnBankRules.FitTier(again, level) == BankTiers.National,
            "bank levels: a bank founded after closures, on their stricter terms, is fitted on them");
        var sold = new OwnBank();
        OwnBankRules.Found(sold, 1, 71, OwnBankRules.CapitalLeast(level), "Sold Savings");
        OwnBankRules.Sell(sold, 30);
        OwnBank soldBack = OwnBankStore.Read(HeadCut(OwnBankStore.Write(sold), 35), out _);
        Check(OwnBankRules.FitTier(soldBack, level) == BankTiers.Community && !soldBack.Running, "bank levels: an older save's bank sold or closed: the next one is founded at the first level");

        // A line of this version keeps its level as written, and one out of the table is fitted again.
        OwnBank now = TierBank(c, BankTiers.Regional, 73);
        OwnBank nowBack = OwnBankStore.Read(OwnBankStore.Write(now), out _);
        OwnBank odd = OwnBankStore.Read(SetHead(OwnBankStore.Write(now), 35, "7"), out _);
        Check(nowBack.Tier == BankTiers.Regional && odd.Tier == BankTiers.Unknown, "bank levels: a level read back is the one written; one out of the table is fitted again");
    }

    // A line with one of its head record's fields written as given.
    private static string SetHead(string line, int field, string value)
    {
        int from = line.IndexOf("^s:", StringComparison.Ordinal) + 3, to = line.IndexOf('^', from);
        string[] head = line.Substring(from, to - from).Split('|');
        head[field] = value;
        return line.Substring(0, from) + string.Join("|", head) + line.Substring(to);
    }

    // ---------------------------------------------------------------- the save

    private static void TierStoreChecks()
    {
        BankCity c = TierCity(0);
        OwnBank b = TierBank(c, BankTiers.Regional, 79);
        b.TierFrom = 33;
        b.TierTold = BankTiers.National;
        b.PayMode = BankTiers.PayFixedMode;
        b.PayShare = 350;
        b.PayFixed = 1234500;
        b.PaidMonth = 111;
        b.PaidLast = 222;
        b.PaidAll = 3333;
        string line = OwnBankStore.Write(b);
        OwnBank back = OwnBankStore.Read(line, out _);
        Check(back != null && back.Tier == BankTiers.Regional && back.TierFrom == 33 && back.TierTold == BankTiers.National && back.PayMode == BankTiers.PayFixedMode && back.PayShare == 350
              && back.PayFixed == 1234500 && back.PaidMonth == 111 && back.PaidLast == 222 && back.PaidAll == 3333 && OwnBankStore.Write(back) == line,
            "bank levels: the save keeps the level, its record's day, the pay chosen and what was paid");
        string cut = HeadCut(line, 35);
        OwnBank older = OwnBankStore.Read(cut, out _);
        string[] head = line.Substring(line.IndexOf("^s:", StringComparison.Ordinal) + 3).Split('^')[0].Split('|');
        string[] oldHead = cut.Substring(cut.IndexOf("^s:", StringComparison.Ordinal) + 3).Split('^')[0].Split('|');
        bool same = oldHead.Length == 35;
        for (int i = 0; same && i < 35; i++)
        {
            same = head[i] == oldHead[i];
        }
        Check(same && head.Length > 35 && older != null && older.Capital == b.Capital && older.Name == b.Name && older.Tier == BankTiers.Unknown && older.PaidAll == 0,
            "bank levels: the new fields come after every field an older version reads, so it reads the rest as before and leaves them");
        OwnBank bounds = OwnBankStore.Read(SetHead(SetHead(SetHead(line, 38, "9"), 39, "377"), 40, "-50"), out _);
        Check(bounds.PayMode == BankTiers.PayFixedMode && bounds.PayShare == 350 && bounds.PayFixed == 0, "bank levels: a pay read back is put inside its bounds and on its step");
    }

    // ---------------------------------------------------------------- the owner's pay

    private static void TierPayChecks()
    {
        BankCity c = TierCity(0);
        var day = new BankDay();
        foreach (int share in new[] { 0, 50, 350, 1000 })
        {
            OwnBank b = TierBank(c, BankTiers.Community, 83);
            b.PayShare = share;
            bool exact = true, balanced = true;
            long sum = 0, profitDays = 0, whole = 0;
            for (int d = 2; d < 2 + 2 * TierYear && b.Running; d++)
            {
                long capital = b.Capital;
                OwnBankRules.Day(b, c, d, null, day);
                long wanted = day.Profit > 0 ? day.Profit * share / 1000 : 0;
                exact &= day.Pay >= 0 && day.Pay <= wanted && b.Capital - capital == day.Profit - day.Pay;
                balanced &= OwnBankRules.Balanced(b);
                sum += day.Pay;
                profitDays += day.Profit > 0 ? 1 : 0;
                whole += day.Profit > 0 && day.Pay == wanted ? 1 : 0;
            }
            Check(exact && balanced && b.PaidAll == sum && whole * 10 >= profitDays * 9 && (share == 0 ? sum == 0 : sum > 0),
                "the owner's pay: " + share / 10 + "% of each day's profit is paid out and the rest kept, to the cent, the books balanced every day (" + Dollars(sum) + " in two years)");
        }

        // The split, a day at a time.
        OwnBank p = TierBank(c, BankTiers.Community, 89, OwnBankRules.TierCapital(BankTiers.Community, c.Level, 0, 0));
        RunBank(p, c, 2, TierYear, null, day);
        p.PayShare = 350;
        Check(OwnBankRules.OwnerPay(p, 1000001, c.Level) == 350000 && OwnBankRules.OwnerPay(p, 0, c.Level) == 0 && OwnBankRules.OwnerPay(p, -5000, c.Level) == 0,
            "the owner's pay: a share of a day's profit, rounded down to the cent, and nothing on a day without one");
        p.PayMode = BankTiers.PayFixedMode;
        p.PayFixed = 500000;
        Check(OwnBankRules.OwnerPay(p, 800000, c.Level) == 500000 && OwnBankRules.OwnerPay(p, 300000, c.Level) == 300000 && OwnBankRules.OwnerPay(p, -1, c.Level) == 0,
            "the owner's pay: a fixed sum a day, never more than the day's profit");
        p.Stage = Regulator.Breach;
        long breach = OwnBankRules.OwnerPay(p, 800000, c.Level);
        p.Stage = Regulator.Clear;
        p.Run = true;
        long run = OwnBankRules.OwnerPay(p, 800000, c.Level);
        p.Run = false;
        p.Test = true;
        long test = OwnBankRules.OwnerPay(p, 800000, c.Level);
        p.Test = false;
        Check(breach == 0 && run == 0 && test == 0, "the owner's pay: nothing in a breach, a run, or from a test's bank");
        OwnBank thin = TierBank(c, BankTiers.Community, 97);
        thin.PayShare = 1000;
        thin.Loans[OwnBankCatalogue.Good] = thin.Capital * 7;
        thin.Deposits[OwnBankCatalogue.People] = thin.Capital * 8;
        thin.Reserves = thin.Capital + thin.Savings - thin.Lent;
        long spare = Math.Min(thin.Reserves - OwnBankRules.ReserveNeed(thin), OwnBankRules.CapitalNow(thin) - OwnBankRules.Keep(thin, c.Level));
        Check(OwnBankRules.OwnerPay(thin, 100000000, c.Level) == Math.Max(0, spare) && spare < 100000000,
            "the owner's pay: never past what the bank can spare over its reserve, its least capital and its lending line");

        // The same days, the same pay, on every run; a month's pay rolled with its books.
        OwnBank one = TierBank(c, BankTiers.Community, 101), two = TierBank(c, BankTiers.Community, 101);
        one.PayShare = two.PayShare = 450;
        RunBank(one, c, 2, 3 * TierYear, null, day);
        RunBank(two, c, 2, 3 * TierYear, null, new BankDay());
        Check(OwnBankStore.Write(one) == OwnBankStore.Write(two) && one.PaidAll > 0, "the owner's pay: the same bank and days pay the same to the cent");
        OwnBank month = TierBank(c, BankTiers.Community, 103);
        month.PayShare = 500;
        long inMonth = 0;
        bool rolled = false;
        for (int d = 2; d < 2 + 4 * c.MonthDays && month.Running; d++)
        {
            int was = month.Month;
            OwnBankRules.Day(month, c, d, null, day);
            inMonth += day.Pay;
            if (month.Month != was)
            {
                rolled |= month.PaidLast == inMonth && month.PaidMonth == 0;
                inMonth = 0;
            }
        }
        Check(rolled, "the owner's pay: a month's pay is kept as last month's when the month turns");

        // Kept, the profit grows the bank; paid out, it does not.
        long[] grown = new long[3];
        int[] shares = { 0, 500, 1000 };
        for (int i = 0; i < shares.Length; i++)
        {
            OwnBank g = TierBank(c, BankTiers.Community, 107);
            g.PayShare = shares[i];
            RunBank(g, c, 2, 2 * TierYear, null, day);
            grown[i] = g.Capital;
        }
        long founded = OwnBankRules.TierCapital(BankTiers.Community, c.Level, 0, 0);
        Check(grown[0] > grown[1] && grown[1] > grown[2] && (grown[2] - founded) * 10 < grown[0] - founded,
            "the owner's pay: profit kept grows the bank's capital, paid out it does not (" + Dollars(grown[0]) + ", " + Dollars(grown[1]) + ", " + Dollars(grown[2]) + ")");
        OwnBank zero = TierBank(c, BankTiers.Community, 109), none = TierBank(c, BankTiers.Community, 109);
        zero.PayMode = BankTiers.PayFixedMode;
        RunBank(zero, c, 2, TierYear, null, day);
        RunBank(none, c, 2, TierYear, null, new BankDay());
        zero.PayMode = none.PayMode;
        Check(OwnBankStore.Write(zero) == OwnBankStore.Write(none) && none.PaidAll == 0, "the owner's pay: with no pay chosen the bank keeps all of its profit, as before");
        string book = MemberText(System.IO.File.ReadAllText(System.IO.Path.Combine(DesignScripts, "Economy", "OwnBank", "OwnBankGame.cs")), "public static void BookDay(");
        Check(book.Contains("d.Pay") && book.Contains("OwnBankCatalogue.PayKey") && CashCategory.For(OwnBankCatalogue.PayKey) == CashCategory.Investments,
            "the owner's pay: booked into the player's cash as a dividend is, among the Finance app's investments");
    }

    // ---------------------------------------------------------------- the balance figures

    // A bank's climb to a level: founded a community bank, a year at each level below it (its record within it), its capital
    // put up to the next level's least where its profit has not brought it there, then moved up, and ten years at the level
    // it climbs to; nothing paid out to its owner.
    private sealed class TierClimb
    {
        public readonly long[] Fee = new long[BankTiers.Count];        // the licence (founding), or the move up to the level
        public readonly long[] Capital = new long[BankTiers.Count];    // the level's least capital
        public readonly long[] Settled = new long[BankTiers.Count];    // the day's profit six months after reaching the level
        public readonly long[] Last = new long[BankTiers.Count];       // the day's profit on its last day at the level below the top
        public readonly long[] TopUp = new long[BankTiers.Count];      // the capital put in to move up to the level
        public readonly int[] Back = { -1, -1, -1 };                   // days until the level's licence was paid back: by the kept profit, or by what the move added
    }

    private static TierClimb Climb(int share, int top)
    {
        BankCity c = TierCity(share);
        int buy = EndScaleRules.Buying(share > 0), m = c.MonthDays, settle = 6 * m;
        var run = new TierClimb();
        for (int t = 0; t < BankTiers.Count; t++)
        {
            run.Fee[t] = t == BankTiers.Community ? OwnBankRules.TierLicence(t, c.Level, buy) : OwnBankRules.MoveUpFee(t, c.Level, buy);
            run.Capital[t] = OwnBankRules.TierCapital(t, c.Level, 0, buy);
        }
        OwnBank b = TierBank(c, BankTiers.Community, 113);
        long put = b.Capital;
        var day = new BankDay();
        int d = 1;
        for (int t = 0; t <= top && b.Running; t++)
        {
            int from = d, stay = t < top ? Math.Max(12 * m, BankTiers.RecordMonths[t + 1] * m) : 120 * m;
            long added = 0;
            for (; d < from + stay && b.Running; d++)
            {
                OwnBankRules.Day(b, c, d, null, day);
                if (t == BankTiers.Community)
                {
                    if (run.Back[t] < 0 && b.Capital - put >= run.Fee[t])
                    {
                        run.Back[t] = d;
                    }
                }
                else
                {
                    added += day.Profit - run.Last[t - 1];
                    if (run.Back[t] < 0 && added >= run.Fee[t])
                    {
                        run.Back[t] = d - from + 1;
                    }
                }
                if (d == from + settle)
                {
                    run.Settled[t] = day.Profit;
                }
            }
            run.Last[t] = day.Profit;
            if (t < top && b.Running)
            {
                long short_ = run.Capital[t + 1] - OwnBankRules.CapitalNow(b);
                if (short_ > 0)
                {
                    run.TopUp[t + 1] = OwnBankRules.ToStep(short_ + OwnBankCatalogue.CapitalStepCents - 1);
                    OwnBankRules.PutIn(b, run.TopUp[t + 1]);
                }
                OwnBankRules.MoveUp(b, d, buy);
            }
        }
        return run;
    }

    // Days a bank founded at a level takes to bring its capital to the next level's least by its kept profit alone; -1 not within the days.
    private static int ByProfit(int share, int tier, int days)
    {
        BankCity c = TierCity(share);
        int buy = EndScaleRules.Buying(share > 0);
        OwnBank b = TierBank(c, tier, 131);
        long next = OwnBankRules.TierCapital(tier + 1, c.Level, 0, buy);
        var day = new BankDay();
        for (int d = 1; d <= days && b.Running; d++)
        {
            OwnBankRules.Day(b, c, d, null, day);
            if (OwnBankRules.CapitalNow(b) >= next)
            {
                return d;
            }
        }
        return -1;
    }

    private static void TierBalanceChecks()
    {
        Console.WriteLine("BANK LEVELS: level | licence or move up, least capital, capital put in to reach it | profit a day six months on, on its last day below the top | licence paid back");
        foreach (int share in new[] { 0, EndScaleRules.Full })
        {
            string at = share > 0 ? "at the end game's scale" : "before the scale";
            int m = TierCity(share).MonthDays;
            TierClimb run = Climb(share, BankTiers.National), regional = Climb(share, BankTiers.Regional);
            run.Back[BankTiers.Regional] = regional.Back[BankTiers.Regional];   // the move up to a regional bank measured over ten years at it
            for (int t = 0; t < BankTiers.Count; t++)
            {
                Console.WriteLine("  " + at + ", " + TierWords(t) + " | " + Dollars(run.Fee[t]) + ", " + Dollars(run.Capital[t]) + ", " + Dollars(run.TopUp[t]) + " | "
                                  + Dollars(run.Settled[t]) + ", " + (t + 1 < BankTiers.Count ? Dollars(run.Last[t]) : "-") + " | " + Months(run.Back[t], m));
            }
            int back = run.Back[BankTiers.Community] < 0 ? int.MaxValue : (run.Back[BankTiers.Community] + m - 1) / m;
            Check(run.Back[BankTiers.Community] > 0 && back <= BankTiers.PaybackMonthsMost && back >= PaybackMonthsLeast,
                "bank levels: a community bank pays its licence back from its profit in " + PaybackMonthsLeast + " to " + BankTiers.PaybackMonthsMost + " months, " + at + " (" + back + ")");
            int grow = ByProfit(share, BankTiers.Community, 60 * m), growMonths = grow < 0 ? int.MaxValue : (grow + m - 1) / m;
            Console.WriteLine("    a community bank " + at + " keeping all its profit has a regional bank's capital in " + Months(grow, m));
            Check(grow > 0 && growMonths <= GrowMonthsMost, "bank levels: a community bank keeping its profit grows to a regional bank's capital within " + GrowMonthsMost + " months, "
                                                             + at + " (" + (grow > 0 ? growMonths.ToString(System.Globalization.CultureInfo.InvariantCulture) : "not within five years") + ")");
            for (int t = BankTiers.Regional; t < BankTiers.Count; t++)
            {
                int most = t == BankTiers.National && share > 0 ? EndScaleRules.PaybackYearsMost * 12 : UpPaybackMonthsMost;
                int months = run.Back[t] < 0 ? int.MaxValue : (run.Back[t] + m - 1) / m;
                Check(run.Back[t] > 0 && months <= most, "bank levels: moving up to a " + TierWords(t) + " pays its licence back by what the larger bank adds within " + most
                                                         + " months, " + at + " (" + (run.Back[t] > 0 ? months.ToString(System.Globalization.CultureInfo.InvariantCulture) : "never") + ")");
            }
            Check(run.Last[BankTiers.Regional] > run.Last[BankTiers.Community] && run.Settled[BankTiers.National] > run.Last[BankTiers.Regional],
                "bank levels: each level earns more than the one below it, " + at);
        }
    }

    private static string TierWords(int tier)
    {
        return tier == BankTiers.Community ? "community bank" : tier == BankTiers.Regional ? "regional bank" : "national bank";
    }

    private static string Months(int days, int monthDays)
    {
        return days < 0 ? "not within the run" : (days + monthDays - 1) / monthDays + " month(s) (" + days + " days)";
    }

    // ---------------------------------------------------------------- the texts and the slider

    private static void TierTextChecks()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Dictionary<string, string> pt = LoadWords(System.IO.Path.Combine(DesignLocales, "pt.json"));
        Dictionary<string, string> zh = LoadWords(System.IO.Path.Combine(DesignLocales, "zh-cn.json"));
        if (en == null || pt == null || zh == null)
        {
            Check(false, "bank levels: the three locales are where the checks look");
            return;
        }
        var keys = new List<string>(BankTiers.NameKeys)
        {
            OwnBankRules.UpTopKey, OwnBankRules.UpRegulatorKey, OwnBankRules.UpRecordKey, OwnBankRules.UpCapitalKey, OwnBankCatalogue.PayKey, "reallife_ob_why_pay_most",
            "reallife_ob_toast_ready", "reallife_ob_toast_moved_up", "reallife_ob_toast_moved_top", "reallife_ob_news_moved_up", "reallife_ob_toast_full_deposits", "reallife_ob_toast_full_loans",
            "reallife_branch_toast_waiting", "reallife_ob_row_level", "reallife_ob_note_level", "reallife_ob_note_level_top", "reallife_ob_row_pay", "reallife_ob_note_pay_fixed",
            "reallife_ob_note_pay_share", "reallife_ob_note_pay_none", "reallife_ob_level_title", "reallife_ob_meter_deposits", "reallife_ob_meter_loans", "reallife_ob_box_branches",
            "reallife_ob_figure_of", "reallife_ob_figure_no_limit", "reallife_ob_box_level_over", "reallife_ob_box_level_top", "reallife_ob_sec_level_next", "reallife_ob_check_record",
            "reallife_ob_check_capital", "reallife_ob_check_regulator", "reallife_ob_box_up_fee", "reallife_ob_box_up_holds", "reallife_ob_box_up_button", "reallife_ob_box_level_note",
            "reallife_ob_confirm_up_title", "reallife_ob_confirm_up_body", "reallife_ob_confirm_up_body_top", "reallife_ob_pay_title", "reallife_ob_pay_head", "reallife_ob_pay_mode_share",
            "reallife_ob_pay_mode_fixed", "reallife_ob_pay_fixed_row", "reallife_ob_pay_set", "reallife_ob_ask_pay", "reallife_ob_pay_share", "reallife_ob_pay_paid", "reallife_ob_pay_month",
            "reallife_ob_pay_last", "reallife_ob_pay_all", "reallife_ob_pay_note", "reallife_ob_box_level", "reallife_ob_box_intro_level", "reallife_ob_test_level"
        };
        int missing = 0;
        foreach (string key in keys)
        {
            missing += en.ContainsKey(key) && pt.ContainsKey(key) && zh.ContainsKey(key) ? 0 : 1;
        }
        Check(missing == 0, "bank levels: every text is in en, pt and zh-cn (" + missing + " missing)");
        string[][] names =
        {
            new[] { "reallife_ob_toast_ready", "bank", "level", "fee" }, new[] { "reallife_ob_toast_moved_up", "bank", "level", "branches", "loans", "deposits" },
            new[] { "reallife_ob_toast_moved_top", "bank", "level", "branches" }, new[] { "reallife_ob_toast_full_deposits", "bank", "level", "most" },
            new[] { "reallife_ob_toast_full_loans", "bank", "level", "most" }, new[] { "reallife_branch_toast_waiting", "branch", "level", "count" },
            new[] { "reallife_ob_confirm_up_body", "fee", "level", "branches", "loans", "deposits" }, new[] { "reallife_ob_check_capital", "least", "now" },
            new[] { "reallife_ob_check_record", "months", "now" }, new[] { "reallife_ob_note_level", "branches", "loans", "deposits" }
        };
        bool placeholders = true;
        foreach (string[] row in names)
        {
            for (int i = 1; i < row.Length; i++)
            {
                placeholders &= Has(en, row[0], "{" + row[i] + "}") && Has(pt, row[0], "{" + row[i] + "}") && Has(zh, row[0], "{" + row[i] + "}");
            }
        }
        Check(placeholders, "bank levels: every notice's and page's names are in every language");
        bool fits = true;
        foreach (string notice in new[] { "reallife_ob_toast_ready", "reallife_ob_toast_moved_up", "reallife_ob_toast_moved_top", "reallife_ob_toast_full_deposits", "reallife_ob_toast_full_loans",
                     "reallife_branch_toast_waiting" })
        {
            fits &= en.ContainsKey(notice) && en[notice].Length + 60 <= 240;
        }
        Check(fits, "bank levels: the notices fit the game's notification with their names filled");
        string page = en.TryGetValue("reallife_guide_economy_ownbank", out string g) ? g : string.Empty;
        bool facts = true;
        foreach (string fact in new[] { "ob_up_regional", "ob_up_national", "ob_capital_regional", "ob_capital_national",
                     "ob_record_regional", "ob_record_national", "ob_branches_community", "ob_branches_regional", "ob_loans_community", "ob_deposits_community", "ob_pay_step" })
        {
            facts &= page.Contains("{" + fact + "}");
        }
        Check(facts, "bank levels: the Guide's bank page takes the levels' and the pay's figures from the code");
        Check(Has(en, "reallife_guide_economy_bank_branch", "{ob_branches_community}") || Has(en, "reallife_guide_economy_bank_branch", "level"),
            "bank levels: the Guide's branches page says a bank's level sets how many branches it runs");
        Check(AnyChangesRow(en, "Levels for your own bank") && AnyChangesRow(pt, "Níveis para o seu próprio banco") && AnyChangesRow(zh, "你自己的银行有了等级"),
            "bank levels: What's new says so, in a row of its own or a version's");
        string pay = MemberText(System.IO.File.ReadAllText(System.IO.Path.Combine(DesignScripts, "Economy", "OwnBank", "Tiers", "OwnBankApp.Tiers.cs")), "private void WritePay(");
        string slider = System.IO.File.ReadAllText(System.IO.Path.Combine(DesignScripts, "Core", "GameUi", "InfoBox.Slider.cs"));
        Check(pay.Contains("box.Slider(") && slider.Contains("GetComponentInChildren<ModOptionsSliderControl>(true)") && slider.Contains("UiClone.Asleep("),
            "bank levels: the owner's share is set on the game's own slider, the one its settings menu has, as the Mod settings app has it");
    }

    // ---------------------------------------------------------------- time

    private static void TierTimingChecks()
    {
        BankCity c = TierCity(EndScaleRules.Full);
        OwnBank b = TierBank(c, BankTiers.Community, 127, OwnBankRules.TierCapital(BankTiers.Community, c.Level, 0, EndScaleRules.Full) * 6);
        b.SaverOffset[0] = b.SaverOffset[1] = 300;
        b.PayShare = 300;
        var day = new BankDay();
        TierDays(b, c, 2, 30, day);   // warmed
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        TierDays(b, c, 32, 600, day);
        double perDay = watch.Elapsed.TotalMilliseconds / 600.0;
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Check(perDay < 0.5 && allocated < 4096, "bank levels: a day of a bank at its level's limits, paying its owner, in under half a millisecond and allocating nothing ("
                                                + perDay.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " ms, " + allocated + " bytes over 600 days)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void TierDays(OwnBank b, BankCity c, int from, int days, BankDay day)
    {
        for (int d = from; d < from + days && b.Running; d++)
        {
            OwnBankRules.Day(b, c, d, null, day);
        }
    }
}
