using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;

// The player's own bank (Scripts/Economy/OwnBank): the catalogue, the rival banks' rates, how the city's savers and
// borrowers choose, defaults and what moves them, the arrears, the regulator and its ratio, a run, the fund, a sale, a
// bank wound up, the balance sheet after every day, and the save.
internal static partial class Program
{
    private const int BankYear = 120;   // a game year of 120 days: a month of ten

    private static void PlayerBank()
    {
        int before = _run, failedBefore = _failed;
        var watch = Stopwatch.StartNew();
        BankCatalogueChecks();
        BankRateChecks();
        BankMarketChecks();
        BankDefaultChecks();
        BankDayChecks();
        BankRegulatorChecks();
        BankRunChecks();
        BankFundChecks();
        BankEndChecks();
        BankAgainChecks();
        BankStoreChecks();
        BankTimingChecks();
        Console.WriteLine("  Own bank: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static OwnBank NewBank(BankCity c, int day = 1, int seed = 7, long capital = -1)
    {
        var b = new OwnBank();
        OwnBankRules.Found(b, day, seed, capital < 0 ? OwnBankRules.CapitalLeast(c.Level) : capital, "Test Savings");
        return b;
    }

    // A run of days in a city; false if the books failed to balance on any of them, or the bank closed when it should not.
    private static bool RunBank(OwnBank b, BankCity c, int from, int days, IList<FundQuote> quotes, BankDay day, Action<int> each = null)
    {
        bool balanced = true;
        for (int d = from; d < from + days && b.Running; d++)
        {
            OwnBankRules.Day(b, c, d, quotes, day);
            balanced &= !b.Running || OwnBankRules.Balanced(b);
            each?.Invoke(d);
        }
        return balanced;
    }

    // ---------------------------------------------------------------- the catalogue

    private static void BankCatalogueChecks()
    {
        int s = OwnBankCatalogue.Segments, k = OwnBankCatalogue.Savers;
        Check(OwnBankCatalogue.SegmentKeys.Length == s && OwnBankCatalogue.DemandCents.Length == s && OwnBankCatalogue.TypicalScore.Length == s && OwnBankCatalogue.OwnLoanHold.Length == s
              && OwnBankCatalogue.LoanPull.Length == s && OwnBankCatalogue.TermYears.Length == s && OwnBankCatalogue.DefaultBase.Length == s && OwnBankCatalogue.CureShare.Length == s
              && OwnBankCatalogue.RecoverShare.Length == s && OwnBankCatalogue.LoanWeight.Length == s,
            "own bank: a figure of every table for every kind of borrower");
        Check(OwnBankCatalogue.SaverKeys.Length == k && OwnBankCatalogue.SavingsCents.Length == k && OwnBankCatalogue.OwnSaverHold.Length == k && OwnBankCatalogue.SaverPull.Length == k
              && OwnBankCatalogue.SaverFollow.Length == k && OwnBankCatalogue.RunShare.Length == k,
            "own bank: a figure of every table for every kind of saver");
        Check(OwnBankCatalogue.PeopleCycle.Length == MacroCatalogue.StateCount && OwnBankCatalogue.CycleTrust.Length == MacroCatalogue.StateCount && OwnBankCatalogue.DemandCycle.Length == MacroCatalogue.StateCount,
            "own bank: a figure for every state of the cycle");
        Check(OwnBankCatalogue.StrategyKeys.Length == OwnBankCatalogue.Strategies && OwnBankCatalogue.FundCompanies.Length == OwnBankCatalogue.Strategies && OwnBankCatalogue.FundDistress.Length == OwnBankCatalogue.Strategies,
            "own bank: a figure for every strategy of the fund");
        bool safer = true;
        for (int j = 1; j < s; j++)
        {
            safer &= OwnBankCatalogue.DefaultBase[OwnBankCatalogue.Poor] > OwnBankCatalogue.DefaultBase[j];
        }
        Check(safer && OwnBankCatalogue.DefaultBase[OwnBankCatalogue.Fair] > OwnBankCatalogue.DefaultBase[OwnBankCatalogue.Good]
              && OwnBankCatalogue.DefaultBase[OwnBankCatalogue.Shops] > OwnBankCatalogue.DefaultBase[OwnBankCatalogue.Companies],
            "own bank: the poor default most, a good rating less than a fair one, an established firm less than a shop");
        Check(OwnBankCatalogue.PeopleCycle[0] < OwnBankCatalogue.PeopleCycle[1] && OwnBankCatalogue.PeopleCycle[1] < OwnBankCatalogue.PeopleCycle[2] && OwnBankCatalogue.PeopleCycle[2] < OwnBankCatalogue.PeopleCycle[3],
            "own bank: people default more the worse the cycle");
        Check(OwnBankCatalogue.LendRatio > OwnBankCatalogue.WatchRatio && OwnBankCatalogue.WatchRatio > OwnBankCatalogue.MinRatio && OwnBankCatalogue.RunEnd > OwnBankCatalogue.RunStart,
            "own bank: the lending line over the watch line over the law's; a run ends higher than it starts");
        Check(OwnBankRules.Licence(PriceRules.Start) == 300000000L && OwnBankRules.Licence(PriceRules.Most) == 1200000000L && OwnBankRules.CapitalLeast(1250) == 625000000L
              && OwnBankRules.At(OwnBankCatalogue.SavingsCents[0], PriceRules.Most) == 160000000000L && OwnBankRules.At(100, 99999) == 400 && OwnBankRules.At(100, 1) == 50,
            "own bank: millions at today's prices without overflowing, within the price level's bounds");
        Check(OwnBankRules.ToStep(25000000L) == 20000000L && OwnBankRules.ToStep(-5) == 0 && OwnBankRules.ToStep(9999999L) == 0, "own bank: capital moves in whole steps");
        Check(OwnBankCatalogue.IsTakings(OwnBankCatalogue.TakingsKey) && !OwnBankCatalogue.IsTakings(OwnBankCatalogue.SaversKey) && OwnBankCatalogue.CostRow(OwnBankCatalogue.SaversKey) == OwnBankCatalogue.SaversRow
              && OwnBankCatalogue.CostRow(OwnBankCatalogue.LossesKey) == OwnBankCatalogue.LossesRow && OwnBankCatalogue.CostRow(OwnBankCatalogue.WagesKey) == null && OwnBankCatalogue.CostRow(null) == null,
            "own bank: its takings and its rows among the headquarters' goods, by transaction");
        Check(CashCategory.For(OwnBankCatalogue.TakingsKey) == CashCategory.Takings && CashCategory.For(OwnBankCatalogue.WagesKey) == CashCategory.Wages && CashCategory.For(OwnBankCatalogue.FineKey) == CashCategory.Fees
              && CashCategory.For(OwnBankCatalogue.LicenceKey) == CashCategory.Fees && CashCategory.For(OwnBankCatalogue.CapitalInKey) == CashCategory.Investments
              && CashCategory.For(OwnBankCatalogue.KeptKey) == CashCategory.Investments && CashCategory.For(OwnBankCatalogue.SaleKey) == CashCategory.Investments
              && CashCategory.For(OwnBankCatalogue.SaversKey) != CashCategory.Loans && CashCategory.For(OwnBankCatalogue.LossesKey) != CashCategory.Loans,
            "own bank: the Finance app files each of its transactions where it belongs (no saver's interest among the player's loans)");
    }

    // ---------------------------------------------------------------- the rival banks' rates

    private static void BankRateChecks()
    {
        BankCity c = BankCity.Quiet(BankYear);
        var none = new OwnBank();
        Check(OwnBankRules.RivalSaverBp(c, none, 0) == 150 && OwnBankRules.RivalSaverBp(c, null, 1) == 150, "own bank: the rivals pay savers the bank rate less a point and a half");
        BankCity up = c;
        up.CityRateBp = 400;
        up.BasePointsQuarter = 4;
        BankCity floor = c;
        floor.CityRateBp = 100;
        Check(OwnBankRules.RivalSaverBp(up, none, 0) == 250 && OwnBankRules.RivalSaverBp(floor, none, 0) == OwnBankCatalogue.RivalSaverLeast,
            "own bank: what savers are paid follows the central bank, never under a quarter point");
        int fair = OwnBankRules.RivalLoanBp(c, none, OwnBankCatalogue.Fair), good = OwnBankRules.RivalLoanBp(c, none, OwnBankCatalogue.Good);
        int shops = OwnBankRules.RivalLoanBp(c, none, OwnBankCatalogue.Shops), firms = OwnBankRules.RivalLoanBp(c, none, OwnBankCatalogue.Companies);
        int poor = OwnBankRules.RivalLoanBp(c, none, OwnBankCatalogue.Poor);
        Check(fair == 1700 && good == 1200 && shops == 1400 && firms == 1000 && poor == 2900,
            "own bank: the rivals price each kind of borrower as they price the player, from a bank's own 12% (17%, 12%, a shop 14%, a firm 10%; the poor at the fair rate and 12 points)");
        Check(OwnBankRules.RivalLoanBp(up, none, OwnBankCatalogue.Fair) == 1800 && OwnBankRules.RivalLoanBp(up, none, OwnBankCatalogue.Good) == 1300, "own bank: the central bank's point on every loan");
        BankCity hard = c;
        hard.Multiplier = 2000;
        Check(OwnBankRules.RivalLoanBp(hard, none, OwnBankCatalogue.Good) == 2400 && OwnBankRules.RivalSaverBp(hard, none, 0) == 150, "own bank: the game's interest multiplier on loans, not on savings");
        BankCity noHouse = c;
        noHouse.HouseRate = 0;
        Check(OwnBankRules.RivalLoanBp(noHouse, none, OwnBankCatalogue.Good) == good, "own bank: a city with no bank to read prices as one at the usual rate");

        OwnBank b = NewBank(c);
        b.SaverOffset[0] = 100;
        b.LoanOffset[OwnBankCatalogue.Good] = OwnBankCatalogue.LoanOffsetLeast;
        BankCity cheapCity = c;
        cheapCity.HouseRate = 3;
        Check(OwnBankRules.SaverBp(c, b, 0) == 250 && OwnBankRules.LoanBp(c, b, OwnBankCatalogue.Good) == 900 && OwnBankRules.LoanBp(cheapCity, b, OwnBankCatalogue.Good) == 100
              && OwnBankRules.LoanBp(c, b, OwnBankCatalogue.Fair) == 1700,
            "own bank: the player's points over the rivals; a loan never under a point");
        b.SaverOffset[0] = -400;
        Check(OwnBankRules.SaverBp(c, b, 0) == 0, "own bank: savers are never charged to save");
        b.Deposits[OwnBankCatalogue.People] = OwnBankRules.SavingsPool(c, 0) / 10;
        b.Loans[OwnBankCatalogue.Fair] = OwnBankRules.DemandPool(c, OwnBankCatalogue.Fair) / 5;
        Check(OwnBankRules.RivalSaverBp(c, b, 0) == 200 && OwnBankRules.RivalSaverBp(c, b, 1) == 150 && OwnBankRules.RivalLoanBp(c, b, OwnBankCatalogue.Fair) == 1600,
            "own bank: the rivals answer where the bank has taken their customers: half a point at a tenth, a point at most");
    }

    // ---------------------------------------------------------------- the city's savers and borrowers

    private static void BankMarketChecks()
    {
        BankCity c = BankCity.Quiet(BankYear);
        var none = new OwnBank();
        Check(OwnBankRules.SaversWanted(c, none, 0, 1000) == 0 && OwnBankRules.LoansWanted(c, none, OwnBankCatalogue.Good) == 0, "own bank: with no bank of the player's the city's savings and loans stay with the rivals");
        OwnBank b = NewBank(c);
        long people = OwnBankRules.SaversWanted(c, b, 0, 1000), pool = OwnBankRules.SavingsPool(c, 0);
        Check(people == (long)(pool * (70.0 / 1070.0)), "own bank: trusted fully and paying what the rivals pay, a bank holds its hold's share of the city's savings (6.5% of people's)");
        Check(OwnBankRules.SaversWanted(c, b, 0, 0) == 0 && OwnBankRules.SaversWanted(c, b, 0, 500) < people, "own bank: nobody saves with a bank the city does not trust; less with one it half trusts");
        b.SaverOffset[0] = 200;
        long dearer = OwnBankRules.SaversWanted(c, b, 0, 1000);
        b.SaverOffset[0] = -100;
        long meaner = OwnBankRules.SaversWanted(c, b, 0, 1000);
        b.SaverOffset[0] = 0;
        Check(dearer > people * 17 / 10 && meaner < people, "own bank: two points more for savers nearly doubles what they bring; less brings less");
        b.SaverOffset[1] = 200;
        long firms = OwnBankRules.SaversWanted(c, b, 1, 1000);
        b.SaverOffset[1] = 0;
        Check(firms * 1000 / OwnBankRules.SaversWanted(c, b, 1, 1000) > dearer * 1000 / people, "own bank: firms chase a rate harder than people do");
        long lent = OwnBankRules.LoansWanted(c, b, OwnBankCatalogue.Good);
        b.LoanOffset[OwnBankCatalogue.Good] = 200;
        long dear = OwnBankRules.LoansWanted(c, b, OwnBankCatalogue.Good);
        b.LoanOffset[OwnBankCatalogue.Good] = -100;
        long cheap = OwnBankRules.LoansWanted(c, b, OwnBankCatalogue.Good);
        b.LoanOffset[OwnBankCatalogue.Good] = 0;
        Check(lent > 0 && dear < lent && cheap > lent && OwnBankRules.LoansWanted(c, b, OwnBankCatalogue.Poor) == 0, "own bank: dearer loans fewer borrowers, cheaper more; none of a kind it does not lend to");
        BankCity slump = c;
        slump.Cycle = CycleState.Recession;
        Check(OwnBankRules.DemandPool(slump, OwnBankCatalogue.Shops) == OwnBankRules.DemandPool(c, OwnBankCatalogue.Shops) * 8 / 10, "own bank: the city borrows a fifth less in a recession");

        // Trust: a new name is earned over months.
        int conf = OwnBankRules.Confidence(c, b, OwnBankRules.Ratio(b));
        Check(conf == 700 && OwnBankRules.Trust(c, b, 1, conf) == 77 && OwnBankRules.Trust(c, b, 1 + 4 * c.MonthDays, conf) == 388 && OwnBankRules.Trust(c, b, 1 + 20 * c.MonthDays, conf) == 700,
            "own bank: the city's confidence with integrity at 50 is 700; a new bank has a ninth of it, all of it after eight months");
        BankCity famed = c;
        famed.Integrity = 100;
        BankCity shamed = c;
        shamed.Integrity = 0;
        Check(OwnBankRules.Confidence(famed, b, OwnBankRules.RatioMost) == 1000 && OwnBankRules.Confidence(shamed, b, OwnBankRules.RatioMost) == 400, "own bank: an owner's integrity is the bank's name");
        b.Scare = 600;
        Check(OwnBankRules.Confidence(c, b, OwnBankRules.RatioMost) == 490 && OwnBankRules.Confidence(c, b, 110) == 416 && OwnBankRules.Confidence(c, b, 90) == 245,
            "own bank: what the paper prints and a thin ratio take the city's confidence away");
        b.Scare = 0;

        // The market settles where the weights say, a part of the way each day.
        OwnBank s = NewBank(c);
        var day = new BankDay();
        long first = 0;
        bool small = true;
        RunBank(s, c, 1, 1, null, day);
        first = s.Savings;
        RunBank(s, c, 2, 30 * c.MonthDays, null, day, d => small &= day.Deposited <= OwnBankRules.SavingsPool(c, 1) / 100);
        long settledPeople = s.Deposits[0], wantNow = OwnBankRules.SaversWanted(c, s, 0, OwnBankRules.Trust(c, s, s.LastDay, OwnBankRules.Confidence(c, s, OwnBankRules.Ratio(s))));
        Check(first > 0 && first < OwnBankRules.SavingsPool(c, 0) / 1000 && small, "own bank: no saver in a day: the first day brings under a thousandth of the city's savings, no day more than a hundredth");
        Check(Math.Abs(settledPeople - wantNow) * 50 < wantNow, "own bank: after two and a half years the savings are within 2% of where the city's savers would have them");
    }

    // ---------------------------------------------------------------- defaults

    private static void BankDefaultChecks()
    {
        BankCity c = BankCity.Quiet(BankYear);
        OwnBank b = NewBank(c);
        int[] steady = new int[OwnBankCatalogue.Segments];
        bool noisy = true, same = true;
        for (int j = 0; j < OwnBankCatalogue.Segments; j++)
        {
            steady[j] = OwnBankRules.DefaultPerMille(c, b, j);
            int n = OwnBankRules.Noise(b.Seed, b.Month, j);
            noisy &= n >= OwnBankCatalogue.MonthNoiseLeast && n <= OwnBankCatalogue.MonthNoiseMost;
            same &= n == OwnBankRules.Noise(b.Seed, b.Month, j) && steady[j] == (int)Math.Round(OwnBankCatalogue.DefaultBase[j] * n / 1000.0);
        }
        Check(noisy && same, "own bank: each month's luck within its bounds, the same for the same bank, month and kind, and all that moves a steady city's defaults");
        int spread = 0;
        for (int m = 0; m < 24; m++)
        {
            spread = Math.Max(spread, Math.Abs(OwnBankRules.Noise(b.Seed, m, 1) - OwnBankRules.Noise(b.Seed, m + 1, 1)));
        }
        Check(spread > 100, "own bank: the months' luck is not the same month after month");
        BankCity slump = c;
        slump.Cycle = CycleState.Recession;
        slump.Trouble = CityModel.Trouble(CycleState.Recession, MacroCatalogue.LevelDefault);
        BankCity boom = c;
        boom.Cycle = CycleState.Boom;
        boom.Trouble = CityModel.Trouble(CycleState.Boom, MacroCatalogue.LevelDefault);
        Check(OwnBankRules.DefaultPerMille(slump, b, OwnBankCatalogue.Fair) > steady[OwnBankCatalogue.Fair] * 14 / 10 && OwnBankRules.DefaultPerMille(boom, b, OwnBankCatalogue.Fair) < steady[OwnBankCatalogue.Fair],
            "own bank: people default half as often again in a recession, less in a boom");
        Check(OwnBankRules.DefaultPerMille(slump, b, OwnBankCatalogue.Shops) > steady[OwnBankCatalogue.Shops] && OwnBankRules.DefaultPerMille(boom, b, OwnBankCatalogue.Shops) < steady[OwnBankCatalogue.Shops],
            "own bank: firms default as business pressure runs in the economy");
        BankCity dear = c;
        dear.InflationBp = 600;
        BankCity falling = c;
        falling.InflationBp = -100;
        Check(OwnBankRules.DefaultPerMille(dear, b, OwnBankCatalogue.Good) > steady[OwnBankCatalogue.Good] && OwnBankRules.DefaultPerMille(falling, b, OwnBankCatalogue.Good) > steady[OwnBankCatalogue.Good]
              && OwnBankRules.DefaultPerMille(dear, b, OwnBankCatalogue.Shops) == steady[OwnBankCatalogue.Shops],
            "own bank: prices running away from the target, either way, weigh on people's debts (not on the firms', which business pressure carries)");
        BankCity taxed = c;
        taxed.Tax = 1;
        BankCity granted = c;
        granted.Grants = 1;
        BankCity floorWage = c;
        floorWage.Wages = 1;
        BankCity rentFree = c;
        rentFree.Rent = 1;
        Check(OwnBankRules.Policies(taxed, true) == 1120 && OwnBankRules.Policies(granted, true) == 900 && OwnBankRules.Policies(floorWage, false) == 920 && OwnBankRules.Policies(floorWage, true) == 1060
              && OwnBankRules.Policies(rentFree, false) == 1080 && OwnBankRules.Policies(taxed, false) == 1000 && OwnBankRules.Policies(c, true) == 1000,
            "own bank: the council's business tax and grants weigh on firms, its wage floor and rents on people");
        BankCity worst = c;
        worst.Tax = 1;
        worst.Grants = -1;
        worst.Wages = 1;
        worst.Rent = 1;
        worst.Cycle = CycleState.Recession;
        worst.Trouble = 5000;
        Check(OwnBankRules.DefaultPerMille(worst, b, OwnBankCatalogue.Shops) <= OwnBankCatalogue.DefaultBase[OwnBankCatalogue.Shops] * OwnBankCatalogue.FactorMost / 1000,
            "own bank: everything at its worst, no more than three times the steady figure");
        b.LoanOffset[OwnBankCatalogue.Fair] = 400;
        int adverse = OwnBankRules.DefaultPerMille(c, b, OwnBankCatalogue.Fair);
        b.LoanOffset[OwnBankCatalogue.Fair] = -200;
        Check(adverse > steady[OwnBankCatalogue.Fair] * 15 / 10 && OwnBankRules.DefaultPerMille(c, b, OwnBankCatalogue.Fair) == steady[OwnBankCatalogue.Fair],
            "own bank: a lender four points dearer gets borrowers who default half as often again; a cheap one no better ones than the rivals");
    }

    // ---------------------------------------------------------------- a bank's days

    private static void BankDayChecks()
    {
        BankCity c = BankCity.Quiet(BankYear);
        OwnBank b = NewBank(c);
        var day = new BankDay();
        Check(b.Running && b.Capital == OwnBankRules.CapitalLeast(c.Level) && b.Reserves == b.Capital && OwnBankRules.Balanced(b) && !b.Lending[OwnBankCatalogue.Poor] && b.Lending[OwnBankCatalogue.Good],
            "own bank: founded with its capital in its reserves, lending to all but the poor");
        bool lineKept = true, profitable = false;
        long lostSeen = 0, backSeen = 0;
        bool balanced = RunBank(b, c, 2, 3 * BankYear, null, day, d =>
        {
            lineKept &= day.Lent == 0 || OwnBankRules.Ratio(b) >= OwnBankCatalogue.LendRatio - 2;
            lostSeen += day.WrittenOff;
            backSeen += day.Recovered;
        });
        Check(balanced && b.Running, "own bank: three years of a quiet city, and the books balance every day");
        Check(lineKept && OwnBankRules.Ratio(b) >= OwnBankCatalogue.MinRatio, "own bank: it lends no further than the lending line; the regulator never has cause");
        Check(b.Lent > 0 && b.Savings > 0 && b.Reserves >= OwnBankRules.ReserveNeed(b) * 9 / 10, "own bank: it takes savings and lends them, keeping its reserves");
        profitable = OwnBankRules.YearProfit(b) > 0 && b.HistoryCount == OwnBankCatalogue.HistoryMonths;
        Check(profitable && b.Capital > OwnBankRules.CapitalLeast(c.Level), "own bank: a bank run at the rivals' rates in a quiet city makes a profit, which its capital keeps");
        long year = OwnBankRules.YearProfit(b), capital = OwnBankRules.CapitalLeast(c.Level);
        Check(year < capital * 8 / 10 && year > capital / 50, "own bank: a year's profit is between 2% and 80% of the least capital: neither nothing nor a fortune (" + year / 100 + ")");
        Check(lostSeen > 0 && backSeen > 0 && backSeen < lostSeen && b.LostAll >= lostSeen - 1 && b.RecoveredAll == backSeen, "own bank: loans written off, and a part of them come back later");

        // A day's money, item by item.
        OwnBank t = NewBank(c);
        t.Loans[OwnBankCatalogue.Good] = 100000000L;
        t.Deposits[OwnBankCatalogue.People] = 100000000L;
        t.Reserves = t.Capital + t.Savings - t.Lent;
        int loanBp = OwnBankRules.LoanBp(c, t, OwnBankCatalogue.Good), saverBp = OwnBankRules.SaverBp(c, t, OwnBankCatalogue.People);
        OwnBankRules.Day(t, c, 2, null, day);
        Check(loanBp == 1198 && saverBp == 151 && day.Interest == 100000000L * loanBp / (10000L * BankYear) && day.Savers == 100000000L * saverBp / (10000L * BankYear)
              && day.Profit == day.Income - day.Costs && OwnBankRules.Balanced(t),
            "own bank: a day's interest on its loans and to its savers at their rates");
        Check(day.Staff >= OwnBankCatalogue.StaffDayCents && day.Defaulted > 0 && day.Repaid > 0, "own bank: a day's staff, defaults and repayments");

        // The arrears and the courts.
        OwnBank a = NewBank(c);
        a.Arrears[OwnBankCatalogue.Shops] = 100000000L;
        a.Reserves = a.Capital - a.InArrears;
        a.Lending[OwnBankCatalogue.Shops] = false;
        OwnBankRules.Day(a, c, 2, null, day);
        long settled = 100000000L / c.MonthDays, cured = settled * OwnBankCatalogue.CureShare[OwnBankCatalogue.Shops] / 1000;
        Check(day.Cured == cured && day.WrittenOff == settled - cured && a.Recovering[OwnBankCatalogue.Shops] > 0 && day.Court == 0, "own bank: a month's part of the arrears settles each day: a quarter put right, the rest written off");
        BankCity courts = c;
        courts.Courts = true;
        OwnBank ac = NewBank(c);
        ac.Arrears[OwnBankCatalogue.Shops] = 100000000L;
        ac.Reserves = ac.Capital - ac.InArrears;
        ac.Lending[OwnBankCatalogue.Shops] = false;
        OwnBankRules.Day(ac, courts, 2, null, day);
        Check(ac.Recovering[OwnBankCatalogue.Shops] > a.Recovering[OwnBankCatalogue.Shops] && day.Court == day.WrittenOff * OwnBankCatalogue.CourtCost / 1000 && ac.Recover[OwnBankCatalogue.Shops] > a.Recover[OwnBankCatalogue.Shops]
              && OwnBankRules.Balanced(ac),
            "own bank: through the courts more comes back, later, for their costs");
        long backA = 0, backC = 0;
        RunBank(a, c, 3, 2 * BankYear, null, day, d => backA += day.Recovered);
        RunBank(ac, courts, 3, 2 * BankYear, null, day, d => backC += day.Recovered);
        Check(backC > backA && a.Arrears[OwnBankCatalogue.Shops] < 1000000L, "own bank: over two years the courts bring more back; the arrears are settled");

        // Lending stops where it should.
        OwnBank thin = NewBank(c, 1, 7, OwnBankRules.CapitalLeast(c.Level) / 10);
        RunBank(thin, c, 2, BankYear, null, day);
        Check(thin.Running && OwnBankRules.RiskWeighted(thin) <= OwnBankRules.CapitalNow(thin) * 1000 / OwnBankCatalogue.LendRatio + 100000000L, "own bank: a small capital lends little: the ratio holds it back");
        OwnBank off = NewBank(c);
        for (int j = 0; j < OwnBankCatalogue.Segments; j++)
        {
            off.Lending[j] = false;
        }
        RunBank(off, c, 2, BankYear, null, day);
        Check(off.Lent == 0 && off.Savings > 0 && OwnBankRules.Ratio(off) == OwnBankRules.RatioMost, "own bank: a bank that lends to nobody only keeps savings");

        // Capital in and out.
        OwnBank k = NewBank(c);
        RunBank(k, c, 2, BankYear, null, day);
        long most = OwnBankRules.MostOut(k, c.Level);
        Check(most >= 0 && most % OwnBankCatalogue.CapitalStepCents == 0 && !OwnBankRules.TakeOut(k, most + OwnBankCatalogue.CapitalStepCents, c.Level),
            "own bank: no more taken out than leaves the reserves, the least capital and the lending line");
        OwnBankRules.PutIn(k, 200000000L);
        long more = OwnBankRules.MostOut(k, c.Level);
        Check(more >= most + 190000000L && OwnBankRules.TakeOut(k, more, c.Level) && OwnBankRules.Balanced(k) && OwnBankRules.CapitalNow(k) >= OwnBankRules.CapitalLeast(c.Level)
              && OwnBankRules.Ratio(k) >= OwnBankCatalogue.LendRatio - 1,
            "own bank: capital put in can be taken out again, as far as the lines allow");
    }

    // ---------------------------------------------------------------- the regulator

    private static void BankRegulatorChecks()
    {
        BankCity c = BankCity.Quiet(BankYear);
        var day = new BankDay();
        // A bank whose capital is thin against its loans: in breach the first day.
        OwnBank b = NewBank(c, 1, 7, 600000000L);
        b.Loans[OwnBankCatalogue.Good] = 10000000000L;
        b.Deposits[OwnBankCatalogue.People] = 20000000000L;
        b.Reserves = b.Capital + b.Savings - b.Lent;
        for (int j = 0; j < OwnBankCatalogue.Segments; j++)
        {
            b.Lending[j] = false;
        }
        OwnBankRules.Day(b, c, 2, null, day);
        int m = c.MonthDays;
        Check(day.Breached && b.Stage == Regulator.Breach && b.Deadline == 2 + m && b.Scare > 0 && OwnBankRules.Balanced(b), "own bank: a ratio under the law's is a breach, with a month to put it right");
        bool fined = false;
        long fine = 0;
        RunBank(b, c, 3, m, null, day, d => { fined |= day.Fined; fine += day.Fine; });
        Check(fined && fine >= OwnBankRules.At(OwnBankCatalogue.FineLeastCents, c.Level) && b.Running && b.Strikes == 1 && b.FinedCents == fine, "own bank: the deadline missed: a fine, and another month");
        bool closed = false;
        ClosedWhy why = ClosedWhy.None;
        RunBank(b, c, 3 + m, m + 1, null, day, d => { closed |= day.Closed; why = day.Closed ? day.Why : why; });
        Check(closed && why == ClosedWhy.Strikes && b.State == OwnBankState.Closed, "own bank: the second deadline missed closes the bank");

        // Put right in time.
        OwnBank r = NewBank(c, 1, 7, 600000000L);
        r.Loans[OwnBankCatalogue.Good] = 10000000000L;
        r.Deposits[OwnBankCatalogue.People] = 20000000000L;
        r.Reserves = r.Capital + r.Savings - r.Lent;
        OwnBankRules.Day(r, c, 2, null, day);
        OwnBankRules.PutIn(r, 200000000L);
        OwnBankRules.Day(r, c, 3, null, day);
        Check(day.Cured2 && r.Stage != Regulator.Breach && r.Strikes == 0, "own bank: capital put in puts a breach right");

        // Gone at once.
        OwnBank broke = NewBank(c, 1, 7, 9000000L);
        broke.Loans[OwnBankCatalogue.Fair] = 100000000L;
        broke.Deposits[OwnBankCatalogue.People] = 150000000L;
        broke.Capital = -20000000L;
        broke.Reserves = broke.Capital + broke.Savings - broke.Lent;
        OwnBankRules.Day(broke, c, 2, null, day);
        Check(day.Closed && day.Why == ClosedWhy.Insolvent && broke.State == OwnBankState.Closed && day.WoundUp == 0, "own bank: a bank whose capital is gone is closed, and leaves its owner nothing");
        OwnBank half = NewBank(c, 1, 7, 4000000L);
        half.Loans[OwnBankCatalogue.Fair] = 100000000L;
        half.Deposits[OwnBankCatalogue.People] = 100000000L;
        half.Reserves = half.Capital + half.Savings - half.Lent;
        OwnBankRules.Day(half, c, 2, null, day);
        Check(day.Closed && day.Why == ClosedWhy.Collapse, "own bank: a ratio under half the law's is closed at once");
        Check(OwnBankRules.Fine(NewBank(c), c.Level) == OwnBankRules.At(OwnBankCatalogue.FineLeastCents, c.Level), "own bank: a fine is never under the least");
    }

    // ---------------------------------------------------------------- a run

    private static void BankRunChecks()
    {
        BankCity c = BankCity.Quiet(BankYear);
        c.Integrity = 10;
        var day = new BankDay();
        OwnBank b = NewBank(c);
        b.Deposits[OwnBankCatalogue.People] = 300000000L;
        b.Deposits[OwnBankCatalogue.Firms] = 300000000L;
        b.Reserves = b.Capital + b.Savings;
        b.Scare = 500;
        int firmBp = OwnBankRules.SaverBp(c, b, 1);
        OwnBankRules.Day(b, c, 2, null, day);
        Check(day.RunStarted && b.Run && day.Withdrawn > 0 && day.Lent == 0, "own bank: a scared city with a poor name runs on the bank: savers leave and it lends nothing");
        long firms = 300000000L + 300000000L * firmBp / (10000L * BankYear);
        Check(b.Deposits[OwnBankCatalogue.Firms] == firms - firms * OwnBankCatalogue.RunShare[1] / 1000, "own bank: a run takes a day's share of the savings, not all of them");
        c.Integrity = 90;
        bool ended = false;
        RunBank(b, c, 3, 3 * c.MonthDays, null, day, d => ended |= day.RunEnded);
        Check(ended && !b.Run && b.Running, "own bank: as the scare fades and the name holds, the run ends");

        // A run the reserves cannot meet: the fund is sold, then the bank fails.
        BankCity poor = BankCity.Quiet(BankYear);
        poor.Integrity = 0;
        OwnBank f = NewBank(poor);
        f.Deposits[OwnBankCatalogue.Firms] = 2000000000L;
        f.Loans[OwnBankCatalogue.Companies] = 2400000000L;
        f.Capital = 500000000L;
        f.Reserves = f.Capital + f.Savings - f.Lent;
        f.Scare = 1000;
        bool failed = false;
        ClosedWhy why = ClosedWhy.None;
        RunBank(f, poor, 2, 10, null, day, d => { failed |= day.Closed; why = day.Closed ? day.Why : why; });
        Check(failed && why == ClosedWhy.Run && f.State == OwnBankState.Closed, "own bank: savers it cannot pay close the bank");
    }

    // ---------------------------------------------------------------- the fund

    private static List<FundQuote> Quotes(int count, int price)
    {
        var list = new List<FundQuote>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(new FundQuote
            {
                StockId = i + 1, Ticker = "T" + i, Name = "Company " + i, LastCents = price, DpsCents = price / 100, Shares = 1000000L * (count - i), Size = i % 3, Distress = i * 15, Listed = true
            });
        }
        return list;
    }

    private static void BankFundChecks()
    {
        BankCity c = BankCity.Quiet(BankYear);
        var day = new BankDay();
        List<FundQuote> q = Quotes(30, 5000);
        Check(OwnBankFund.Eligible(q[0], 1) && !OwnBankFund.Eligible(q[1], 1) && OwnBankFund.Eligible(q[1], 2) && !OwnBankFund.Eligible(q[2], 2) && OwnBankFund.Eligible(q[2], 3)
              && !OwnBankFund.Eligible(q[0], 0) && !OwnBankFund.Eligible(q[21], 3),
            "own bank: the strategies' rules: a cautious fund large companies only, a balanced one no small ones, a bold one any not near failing");
        FundQuote bid = q[0];
        bid.UnderBid = true;
        FundQuote gone = q[0];
        gone.Listed = false;
        Check(!OwnBankFund.Eligible(bid, 3) && !OwnBankFund.Eligible(gone, 3), "own bank: never a company under a bid, nor one gone");

        OwnBank b = NewBank(c);
        b.Deposits[OwnBankCatalogue.People] = 1000000000L;
        b.Reserves = b.Capital + b.Savings;
        for (int j = 0; j < OwnBankCatalogue.Segments; j++)
        {
            b.Lending[j] = false;
        }
        b.Strategy = 2;
        b.FundLimit = 100;
        b.Rebalance = true;
        OwnBankRules.Day(b, c, 3, q, day);
        int eligible = 0;
        for (int i = 0; i < q.Count; i++)
        {
            eligible += OwnBankFund.Eligible(q[i], 2) ? 1 : 0;
        }
        Check(b.Holdings.Count == Math.Min(eligible, OwnBankCatalogue.FundCompanies[2]) && day.Bought > 0 && day.Fees == OwnBankFund.Fee(day.Bought) || day.Fees > 0, "own bank: the fund buys its companies");
        bool even = true, ruled = true;
        long part = b.FundValue / Math.Max(1, b.Holdings.Count);
        for (int i = 0; i < b.Holdings.Count; i++)
        {
            even &= Math.Abs(b.Holdings[i].Qty * b.Holdings[i].LastCents - part) <= part / 50 + 5000;
            ruled &= OwnBankFund.Eligible(q[b.Holdings[i].StockId - 1], 2);
        }
        Check(even && ruled && b.FundValue <= b.Savings * 100 / 1000 && OwnBankRules.Balanced(b), "own bank: each an even part, all within the rules, no more than the limit's share of the deposits");

        // A day: dividends; a company gone pays out; one in distress is sold.
        long cost = b.FundCost;
        FundHolding first = b.Holdings[0];
        long qty = first.Qty;
        List<FundQuote> next = Quotes(30, 5000);
        FundQuote failing = next[first.StockId - 1];
        failing.Listed = false;
        failing.GoneCents = 0;
        next[first.StockId - 1] = failing;
        FundHolding second = b.Holdings[1];
        FundQuote shaky = next[second.StockId - 1];
        shaky.Distress = 900;
        next[second.StockId - 1] = shaky;
        OwnBankRules.Day(b, c, 4, next, day);
        Check(b.Find(first.StockId) == null && day.FundLost >= qty * 5000 * 99 / 100 && b.Find(second.StockId) == null && day.Dividends > 0 && OwnBankRules.Balanced(b) && b.FundCost < cost,
            "own bank: a company that failed is a loss, one in distress is sold, dividends come in a day at a time");

        // A rise is a gain the regulator counts before it is taken.
        List<FundQuote> rise = Quotes(30, 6000);
        OwnBankRules.Day(b, c, 5, rise, day);
        Check(OwnBankRules.CapitalNow(b) > b.Capital && OwnBankRules.Balanced(b), "own bank: the fund at the market's prices in the capital the regulator counts");
        b.FundLimit = 0;
        OwnBankRules.Day(b, c, 6, rise, day);
        Check(b.Holdings.Count == 0 && b.FundCost == 0 && day.Gains > 0 && OwnBankRules.Balanced(b), "own bank: a limit of nothing sells the fund, its gain taken");

        // No market: nothing moves.
        b.FundLimit = 100;
        b.Rebalance = true;
        OwnBankRules.Day(b, c, 7, null, day);
        Check(b.Holdings.Count == 0 && day.Bought == 0, "own bank: with the market closed the fund does nothing");

        // Breach: nothing bought.
        OwnBank br = NewBank(c);
        br.Deposits[OwnBankCatalogue.People] = 1000000000L;
        br.Reserves = br.Capital + br.Savings;
        br.Strategy = 3;
        br.FundLimit = 250;
        br.Rebalance = true;
        br.Stage = Regulator.Breach;
        br.Deadline = 1000;
        br.Lending[OwnBankCatalogue.Good] = false;
        OwnBankRules.Day(br, c, 3, q, day);
        Check(br.Holdings.Count == 0, "own bank: a bank in breach buys nothing for its fund");

        // A fire sale.
        OwnBank fs = NewBank(c);
        fs.Deposits[OwnBankCatalogue.People] = 1000000000L;
        fs.Reserves = fs.Capital + fs.Savings;
        fs.Strategy = 1;
        fs.FundLimit = 250;
        fs.Rebalance = true;
        for (int j = 0; j < OwnBankCatalogue.Segments; j++)
        {
            fs.Lending[j] = false;
        }
        OwnBankRules.Day(fs, c, 3, q, day);
        long held = fs.FundValue;
        fs.Reserves -= held / 2;
        fs.Capital -= held / 2;   // (a hole the size of half the fund, to be met)
        long need = held / 4;
        fs.Reserves = -need;
        fs.Capital = fs.Reserves + fs.Lent + fs.InArrears + fs.FundCost - fs.Savings;
        OwnBankFund.FireSale(fs, need, day);
        Check(fs.Reserves >= 0 && fs.FundValue < held && day.FireSold >= need && OwnBankRules.Balanced(fs), "own bank: a fire sale raises what the savers need, and no more than it must");
    }

    // ---------------------------------------------------------------- a sale and a winding up

    private static void BankEndChecks()
    {
        BankCity c = BankCity.Quiet(BankYear);
        var day = new BankDay();
        OwnBank b = NewBank(c);
        RunBank(b, c, 2, 2 * BankYear, null, day);
        long value = OwnBankRules.SaleValue(b);
        Check(value == OwnBankRules.CapitalNow(b) + OwnBankCatalogue.SaleYears * OwnBankRules.YearProfit(b) && OwnBankRules.Sellable(b), "own bank: a buyer pays the capital and three years of the last year's profit");
        long paid = OwnBankRules.Sell(b, 300);
        Check(paid == value && b.State == OwnBankState.Sold && b.EndDay == 300 && b.EndCents == paid && b.Savings == 0 && b.Lent == 0 && !b.Running, "own bank: sold, its books go with it");
        OwnBankRules.Day(b, c, 301, null, day);
        Check(day.Income == 0 && day.Costs == 0, "own bank: a bank that is no longer the player's has no day");
        OwnBank run = NewBank(c);
        run.Run = true;
        OwnBank breach = NewBank(c);
        breach.Stage = Regulator.Breach;
        Check(!OwnBankRules.Sellable(run) && !OwnBankRules.Sellable(breach), "own bank: no buyer for a bank its savers are running from, or the regulator is after");

        OwnBank w = NewBank(c);
        w.Loans[OwnBankCatalogue.Good] = 1000000000L;
        w.Deposits[OwnBankCatalogue.People] = 900000000L;
        w.Reserves = w.Capital + w.Savings - w.Lent;
        long left = OwnBankRules.WindUp(w, 50, ClosedWhy.Strikes);
        long assets = (w.EndCents > 0 ? 0 : 0) + (500000000L + 900000000L - 1000000000L) + 1000000000L * OwnBankCatalogue.WindPerforming / 1000;
        Check(left == (assets - 900000000L) * (1000 - OwnBankCatalogue.WindFee) / 1000 && w.State == OwnBankState.Closed && w.Why == ClosedWhy.Strikes, "own bank: wound up, its savers paid first and the regulator's charge taken, the rest the owner's");
    }

    // ---------------------------------------------------------------- a licence again, after a closure

    private static void BankAgainChecks()
    {
        BankCity c = BankCity.Quiet(BankYear);
        string wait = OwnBankRules.AgainWaitKey, mend = OwnBankRules.AgainIntegrityKey;
        int least = OwnBankCatalogue.AgainIntegrity;
        OwnBank b = NewBank(c);
        Check(OwnBankRules.AgainDay(b, BankYear) == 0 && OwnBankRules.AgainWhy(b, 5, BankYear, true, 0) == null && b.Closures == 0,
            "own bank, again: a bank that is running, or none closed, is held to no wait");
        OwnBank sold = NewBank(c);
        OwnBankRules.Sell(sold, 40);
        Check(OwnBankRules.AgainWhy(sold, 41, BankYear, true, 0) == null && sold.Closures == 0 && OwnBankRules.CapitalLeast(c.Level, sold.Closures) == OwnBankRules.CapitalLeast(c.Level),
            "own bank, again: a bank that was sold is no closure: a new one may be founded at once, on the first terms");

        OwnBankRules.WindUp(b, 50, ClosedWhy.Strikes);
        Check(b.State == OwnBankState.Closed && b.Closures == 1 && OwnBankRules.AgainDay(b, BankYear) == 50 + BankYear, "own bank, again: a closure is counted, and a new licence may be applied for a game year after it");
        Check(OwnBankRules.AgainDay(b, 365) == 415 && OwnBankRules.AgainDay(b, 60) == 110 && OwnBankRules.AgainDay(b, 0) == 51, "own bank, again: the wait is the save's own year, whatever its length");
        Check(OwnBankRules.AgainWhy(b, 51, BankYear, false, 100) == wait && OwnBankRules.AgainWhy(b, 50 + BankYear - 1, BankYear, false, 100) == wait
              && OwnBankRules.AgainWhy(b, 50 + BankYear - 1, BankYear, true, 100) == wait,
            "own bank, again: no licence on any day of the year after the closure, whatever the player's integrity");
        Check(OwnBankRules.AgainWhy(b, 50 + BankYear, BankYear, false, 0) == null && OwnBankRules.AgainWhy(b, 50 + BankYear, BankYear, false, 50) == null,
            "own bank, again: with Standing off, the year is all that is asked");
        Check(OwnBankRules.AgainWhy(b, 50 + BankYear, BankYear, true, least - 1) == mend && OwnBankRules.AgainWhy(b, 50 + 5 * BankYear, BankYear, true, 42) == mend
              && OwnBankRules.AgainWhy(b, 50 + BankYear, BankYear, true, least) == null && OwnBankRules.AgainWhy(b, 50 + BankYear, BankYear, true, 100) == null,
            "own bank, again: with Standing on, an integrity under " + least + " waits however long it has been, and one at " + least + " or over is licensed");
        Check(least > 50 && least - 8 < 50, "own bank, again: the integrity asked for is over a newcomer's, so a newcomer whose bank was closed must earn it back");

        Check(OwnBankRules.CapitalLeast(PriceRules.Start, 0) == OwnBankRules.CapitalLeast(PriceRules.Start) && OwnBankRules.CapitalLeast(PriceRules.Start, 1) == 750000000L
              && OwnBankRules.CapitalLeast(PriceRules.Start, 2) == 1000000000L && OwnBankRules.CapitalLeast(1250, 1) == 937500000L && OwnBankRules.CapitalLeast(PriceRules.Start, -3) == 500000000L,
            "own bank, again: the next bank pays in half as much capital again for each bank closed before ($7.5m after one, $10m after two), at today's prices");

        long capital = OwnBankRules.CapitalLeast(c.Level, b.Closures);
        OwnBankRules.Found(b, 50 + BankYear, 9, capital, "Second Savings");
        Check(b.Running && b.Closures == 1 && b.Capital == capital && b.EndDay == 0 && OwnBankRules.AgainWhy(b, 50 + BankYear, BankYear, true, 0) == null && OwnBankRules.Balanced(b),
            "own bank, again: a new bank founded after the year is a bank like any other, and the closure stays counted");
        OwnBankRules.WindUp(b, 400, ClosedWhy.Run);
        Check(b.Closures == 2 && OwnBankRules.AgainDay(b, BankYear) == 400 + BankYear && OwnBankRules.CapitalLeast(c.Level, b.Closures) > capital,
            "own bank, again: a second closure waits a year from its own day, and asks more capital again");
        OwnBank test = NewBank(c);
        test.Test = true;
        OwnBankRules.WindUp(test, 20, ClosedWhy.Insolvent);
        Check(test.Closures == 0 && OwnBankRules.AgainDay(test, BankYear) == 20 + BankYear, "own bank, again: a bank a test founded and closed raises no capital");

        // The save: the count is kept, and a line of a version before it, with a closed bank, counts that one.
        string line = OwnBankStore.Write(b);
        OwnBank back = OwnBankStore.Read(line, out _);
        Check(back != null && back.Closures == 2 && back.State == OwnBankState.Closed && back.EndDay == 400 && OwnBankStore.Write(back) == line, "own bank, again: the closures are kept in the save");
        string old = line.Replace("|2^d:", "^d:");
        OwnBank older = OwnBankStore.Read(old, out _);
        Check(old != line && older != null && older.Closures == 1 && OwnBankRules.AgainWhy(older, 400 + BankYear - 1, BankYear, false, 50) == wait
              && OwnBankRules.AgainWhy(older, 400 + BankYear, BankYear, false, 50) == null && OwnBankRules.CapitalLeast(c.Level, older.Closures) == OwnBankRules.CapitalLeast(c.Level, 1),
            "own bank, again: an older save with a closed bank follows the new rule: a year from its closure, then the stricter terms");
        OwnBank running = NewBank(c);
        string oldRunning = OwnBankStore.Write(running).Replace("|0^d:", "^d:");
        OwnBank stillRunning = OwnBankStore.Read(oldRunning, out _);
        Check(stillRunning != null && stillRunning.Running && stillRunning.Closures == 0, "own bank, again: an older save with a bank that is running counts no closure");
    }

    // ---------------------------------------------------------------- the save

    private static void BankStoreChecks()
    {
        BankCity c = BankCity.Quiet(BankYear);
        var day = new BankDay();
        OwnBank b = NewBank(c);
        b.Name = "Pipe|Caret^Semi;Per%cent";
        b.SaverOffset[1] = 75;
        b.LoanOffset[OwnBankCatalogue.Shops] = -125;
        b.Lending[OwnBankCatalogue.Poor] = true;
        b.Strategy = 2;
        b.FundLimit = 150;
        RunBank(b, c, 2, BankYear, Quotes(20, 4000), day);
        b.Scare = 123;
        string line = OwnBankStore.Write(b);
        OwnBank back = OwnBankStore.Read(line, out int version);
        Check(back != null && version == OwnBankStore.Version && OwnBankStore.Write(back) == line && back.Name == b.Name && back.Holdings.Count == b.Holdings.Count && back.Holdings.Count > 0
              && OwnBankRules.Balanced(back) && back.Lending[OwnBankCatalogue.Poor] && back.LoanOffset[OwnBankCatalogue.Shops] == -125,
            "own bank: the save's line read back is the bank as it was, name and fund and all");
        Check(OwnBankStore.Read(null, out _) == null && OwnBankStore.Read("", out _) == null && OwnBankStore.Read("x=1", out _) == null && OwnBankStore.Read("v=1^s:1|2", out _) == null
              && OwnBankStore.Read("v=0^s:", out _) == null,
            "own bank: no line, or one that cannot be read, gives no bank");
        string edited = line.Replace("^s:1|", "^s:9|");
        OwnBank odd = OwnBankStore.Read(edited, out _);
        Check(odd != null && odd.State == OwnBankState.Closed, "own bank: a figure out of its bounds is put inside them");
        OwnBank later = OwnBankStore.Read(line + "^z:what a later version keeps", out _);
        Check(later != null && OwnBankStore.Write(later) == line, "own bank: a part of a later version is skipped");
        OwnBank mend = OwnBankStore.Read(line, out _);
        mend.Reserves += 5;
        Check(OwnBankStore.Mend(mend) && OwnBankRules.Balanced(mend) && !OwnBankStore.Mend(mend), "own bank: a line that no longer balances is mended once");
        OwnBank off = OwnBankStore.Read(line.Replace("|100|", "|125|").Replace("^l:", "^l:").Replace("|75^l", "|80^l"), out _);
        Check(off != null && off.SaverOffset[1] % OwnBankCatalogue.SaverOffsetStep == 0, "own bank: a rate read back is on its step");
    }

    // ---------------------------------------------------------------- determinism and time

    private static void BankTimingChecks()
    {
        BankCity c = BankCity.Quiet(BankYear);
        c.Cycle = CycleState.Slowdown;
        c.Trouble = 1150;
        var day = new BankDay();
        OwnBank one = NewBank(c, 1, 99), two = NewBank(c, 1, 99);
        one.Strategy = two.Strategy = 3;
        one.FundLimit = two.FundLimit = 200;
        List<FundQuote> q = Quotes(150, 3000);
        var watch = Stopwatch.StartNew();
        RunBank(one, c, 2, 10 * BankYear, q, day);
        long ms = watch.ElapsedMilliseconds;
        RunBank(two, c, 2, 10 * BankYear, q, day);
        Check(OwnBankStore.Write(one) == OwnBankStore.Write(two), "own bank: the same bank in the same city comes to the same books");
        Check(ms < 1500, "own bank: ten years of days with a fund over 150 companies in under a second and a half (" + ms + " ms)");
        OwnBank other = NewBank(c, 1, 100);
        RunBank(other, c, 2, 2 * BankYear, null, day);
        OwnBank same = NewBank(c, 1, 99);
        RunBank(same, c, 2, 2 * BankYear, null, day);
        Check(other.LostAll != same.LostAll, "own bank: another bank has other luck");
    }
}
