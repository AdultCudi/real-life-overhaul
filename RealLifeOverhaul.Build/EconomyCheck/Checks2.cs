using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

// The second part of the economy's checks: the credit products, what happens when payments are missed,
// consolidation, the lender of last resort, the ways out of debt, the save's version 2 and what is held on one of
// the game's loans. Every figure expected here is worked out by hand from the catalogue's constants.
internal static partial class Program
{
    private const int Year = 60;   // the game's own days in a year

    /// <summary>Cash that only counts: the accounts' side of the game's transaction call.</summary>
    private sealed class Purse : IWallet
    {
        public int Money, Taken, Given;
        public int Cash => Math.Max(0, Money);

        public bool Take(int amount, string why, string name)
        {
            if (amount <= 0 || amount > Money)
            {
                return amount <= 0;
            }
            Money -= amount;
            Taken += amount;
            return true;
        }

        public void Give(int amount, string why, string name)
        {
            Money += amount;
            Given += amount;
        }
    }

    private static DayOutcome Day(CreditAccount a, int today, Purse purse, int level = ProductCatalogue.Standard, bool consequences = true)
    {
        return AccountRules.RunDay(a, today, purse, level, consequences, "test");
    }

    private static void Products()
    {
        AccountRules.DaysPerYear = Year;
        const float own = 100000f;
        bool shut = true, rises = true, rateFalls = true, least = true;
        int overdraft = 0, card = 0, car = 0, home = 0, business = 0, rate = int.MaxValue;
        for (int score = CreditCatalogue.ScoreMin; score <= CreditCatalogue.ScoreMax; score++)
        {
            int o = ProductTerms.OverdraftLimit(score, own), c = ProductTerms.CardLimit(score, own), v = ProductTerms.FinanceMost(ProductKind.CarFinance, score, own),
                h = ProductTerms.FinanceMost(ProductKind.Mortgage, score, own), b = ProductTerms.BusinessMost(score, 250f);
            if (score < CreditCatalogue.LendingThreshold)
            {
                shut &= o == 0 && c == 0 && v == 0 && h == 0 && b == 0;
                continue;
            }
            rises &= o >= overdraft && c >= card && v >= car && h >= home && b >= business;
            least &= o >= ProductCatalogue.OverdraftLeast && c >= ProductCatalogue.CardLeast && o % ProductCatalogue.OverdraftRound == 0 && c % ProductCatalogue.CardRound == 0;
            overdraft = o;
            card = c;
            car = v;
            home = h;
            business = b;
            for (int kind = 0; kind < (int)ProductKind.Arrears; kind++)
            {
                int bp = ProductTerms.RateBp(score, 8, ProductTerms.Points((ProductKind)kind), 1f);
                rateFalls &= bp >= CreditCatalogue.RateFloor * 100;
                if (kind == (int)ProductKind.CreditCard)
                {
                    rateFalls &= bp <= rate;
                    rate = bp;
                }
            }
        }
        Check(shut, "under the threshold no product lends anything, at every score");
        Check(rises, "no product's limit falls as the score rises");
        Check(least, "an overdraft and a card are never under their least, and are round figures");
        Check(rateFalls, "a product's rate never rises as the score rises and is never under the floor");
        Check(ProductTerms.OverdraftLimit(580, own) == 800 && ProductTerms.OverdraftLimit(800, own) == 10400 && ProductTerms.OverdraftLimit(850, own) == 10400,
            "the overdraft: 2% of what the banks lend at 580 (800), 8% from 800 (10,400)");
        Check(ProductTerms.CardLimit(580, own) == 1200 && ProductTerms.CardLimit(800, own) == 15600, "the card: 3% at 580 (1,200), 12% from 800 (15,600)");
        Check(ProductTerms.OverdraftLimit(580, 5000f) == 200 && ProductTerms.CardLimit(580, 5000f) == 300 && ProductTerms.OverdraftLimit(579, 5000f) == 0, "a small city's limits are the least, and still nothing under the threshold");
        Check(ProductTerms.RateBp(580, 8, ProductCatalogue.OverdraftRatePoints, 1f) == 2200 && ProductTerms.RateBp(800, 8, ProductCatalogue.OverdraftRatePoints, 1f) == 1400,
            "the overdraft's rate: the bank's rate for the rating plus 8 points (22% at 580, 14% from 800)");
        Check(ProductTerms.RateBp(580, 8, ProductCatalogue.CardRatePoints, 1f) == 2600 && ProductTerms.RateBp(580, 8, ProductCatalogue.CarRatePoints, 1f) == 1600
              && ProductTerms.RateBp(580, 8, ProductCatalogue.HomeRatePoints, 1f) == 1300, "the card plus 12, car finance plus 2, a mortgage less 1");
        Check(ProductTerms.RateBp(800, 8, ProductCatalogue.CardRatePoints, 0.5f) == 900 && ProductTerms.RateBp(800, 1, ProductCatalogue.HomeRatePoints, 1f) == 100,
            "the game's interest multiplier applies, and a rate stops at the floor");
        Check(ProductTerms.BusinessMost(700, 250f) == 7500 && ProductTerms.BusinessMost(700, 0f) == 0 && ProductTerms.BusinessMost(700, -40f) == 0 && ProductTerms.BusinessRate(580, 8) == 13,
            "a business loan: 30 days of the business's average profit, nothing against a loss, the bank's rate less a point");
        Check(ProductTerms.FinanceMost(ProductKind.CarFinance, 580, own) == 40000 && ProductTerms.FinanceMost(ProductKind.Mortgage, 580, own) == 200000, "car finance up to what the bank lends at the rating, a mortgage five times that");
        Check(ProductTerms.Deposit(20000, 10) == 2000 && ProductTerms.Financed(20000, 10, 50000) == 18000 && ProductTerms.Financed(20000, 10, 15000) == 15000 && ProductTerms.DepositFor(18000, 10) == 2000,
            "the lender never pays the buyer's deposit, and never more than it approved");
        Check(ProductTerms.Financed(999, 50, 100000) == 499 && ProductTerms.Financed(0, 10, 100) == 0, "a deposit with a fraction is rounded up");
        Check(ProductCatalogue.KindKeys.Length == (int)ProductKind.Count && ProductCatalogue.KindSources.Length == (int)ProductKind.Count && (int)ProductKind.Arrears == 4, "a name and a source for every kind of account; the kinds keep their numbers");
    }

    private static void Interest()
    {
        long carry = 0;
        int sum = 0;
        for (int day = 0; day < 600; day++)
        {
            sum += AccountRules.Interest(9999, 1200, Year, 1, ref carry);
        }
        Check(sum == 11998, "600 days of interest on a steady balance come to the exact figure, less than one unit short (" + sum + ")");
        carry = 0;
        Check(AccountRules.Interest(10000, 1200, Year, 1, ref carry) == 20 && carry == 0, "12% a year on 10,000 is 20 a day in a 60-day year");
        Check(AccountRules.Interest(0, 1200, Year, 1, ref carry) == 0 && AccountRules.Interest(-5, 1200, Year, 1, ref carry) == 0 && AccountRules.Interest(500, 0, Year, 1, ref carry) == 0, "nothing owed or no rate, no interest");
        long a = 0, b = 0;
        int week = AccountRules.Interest(978, 2600, Year, 7, ref a), days = 0;
        for (int day = 0; day < 7; day++)
        {
            days += AccountRules.Interest(978, 2600, Year, 1, ref b);
        }
        Check(week == 29 && days == week && a == b, "a week's interest in one sum is the seven days' added up");
        Check(AccountRules.InstalmentFor(21600, 240) == 90 && AccountRules.InstalmentFor(10001, 60) == 167 && AccountRules.InstalmentFor(0, 60) == 0, "the daily repayment is the amount over the days, rounded up");
    }

    private static void Instalments()
    {
        // A vehicle on finance, every payment made: paid off on the last day of its term, to the unit.
        var purse = new Purse { Money = 10000000 };
        FinanceQuote quote = ProductTerms.Quote(24000, 10, 50000, 240, 1000, Year);
        CreditAccount carAccount = AccountRules.Finance(ProductKind.CarFinance, quote.Financed, quote.Term, quote.RateBp, 1, "abc", "car", 24000);
        bool never = true;
        int interest = 0, closedOn = 0, firstInterest = 0;
        for (int day = 1; day <= 300 && !carAccount.Closed; day++)
        {
            DayOutcome o = Day(carAccount, day, purse);
            interest += o.Interest;
            firstInterest = day == 1 ? o.Interest : firstInterest;
            never &= carAccount.Owed >= 0 && o.Missed == 0;
            closedOn = carAccount.Closed ? day : 0;
        }
        Check(quote.Deposit == 2400 && quote.Financed == 21600 && quote.Instalment == 90, "24,000 with 10% down: 21,600 financed at 90 a day");
        Check(closedOn == 240 && carAccount.Owed == 0 && never, "paid off on day 240 exactly, never under nothing");
        Check(purse.Taken == 21600 + interest && interest == quote.Interest && firstInterest == quote.FirstInterest && firstInterest == 36, "what was paid is the amount and the interest the offer showed (" + interest + ")");

        // A term that does not divide, and a mortgage over its longest term.
        CreditAccount odd = AccountRules.Finance(ProductKind.CarFinance, 10001, 60, 1600, 1, "x", "x", 12000);
        CreditAccount homeAccount = AccountRules.Finance(ProductKind.Mortgage, 800000, 1440, 700, 1, "5th Ave#12", "5th Ave 12", 1000000);
        int oddDay = 0, homeDay = 0;
        purse = new Purse { Money = 100000000 };
        int before = purse.Money, homeInterest = 0;
        for (int day = 1; day <= 1500; day++)
        {
            Day(odd, day, purse);
            oddDay = odd.Closed && oddDay == 0 ? day : oddDay;
        }
        int oddPaid = before - purse.Money;
        before = purse.Money;
        for (int day = 1; day <= 1500; day++)
        {
            homeInterest += Day(homeAccount, day, purse).Interest;
            homeDay = homeAccount.Closed && homeDay == 0 ? day : homeDay;
        }
        Check(oddDay == 60 && odd.Owed == 0 && oddPaid == 10001 + ProductTerms.Quote(12000, 10, 10001, 60, 1600, Year).Interest, "a term that does not divide ends on its last day with a smaller payment");
        Check(homeDay > 0 && homeDay <= 1440 && homeAccount.Owed == 0 && before - purse.Money == 800000 + homeInterest, "a mortgage over 1,440 days is paid off within its term, to the unit (day " + homeDay + ")");

        // Days without the money: the interest is added, nothing is taken, and it still ends at nothing.
        CreditAccount late = AccountRules.Finance(ProductKind.CarFinance, 9000, 90, 1200, 1, "x", "x", 10000);
        purse = new Purse { Money = 0 };
        DayOutcome first = Day(late, 1, purse), second = Day(late, 2, purse);
        Check(first.Missed == 100 && first.Interest == 18 && late.Ladder.Misses == 2 && first.Step == LadderStep.Reminder && second.Step == LadderStep.LateFee && second.Fee == 15,
            "a missed instalment: nothing taken, a reminder, then a late fee of 15");
        Check(late.Owed == 9000 + 18 + 18 + 15 && late.Ladder.Overdue == 200 && purse.Taken == 0, "the interest and the fee go on what is owed (" + late.Owed + ")");
        purse.Money = 1000000;
        DayOutcome third = Day(late, 3, purse);
        Check(third.Missed == 0 && third.Paid == 118 && late.Owed == 8951 && late.Ladder.Misses == 0 && late.Ladder.Overdue == 0, "a payment in full puts the count back to nothing");
        bool ends = true;
        for (int day = 4; day <= 400 && !late.Closed; day++)
        {
            Day(late, day, purse);
            ends &= late.Owed >= 0;
        }
        Check(late.Closed && late.Owed == 0 && ends, "and the account is still paid off to the unit");

        // Early repayment: any amount, no fee, never more than is owed.
        CreditAccount early = AccountRules.Finance(ProductKind.CarFinance, 5000, 60, 1200, 1, "x", "x", 6000);
        Check(AccountRules.Pay(early, 1200) == 1200 && early.Owed == 3800 && !early.Closed, "part paid early comes straight off");
        Check(AccountRules.Pay(early, 999999) == 3800 && early.Owed == 0 && early.Closed && AccountRules.Pay(early, 50) == 0, "the rest paid early closes it, and nothing more can be taken");
        Check(early.PaymentDue == 0f && early.Balance == 0f, "a closed account asks for nothing");

        // With "Consequences of not paying" off a miss is only reported.
        CreditAccount mild = AccountRules.Finance(ProductKind.CarFinance, 9000, 90, 1200, 1, "x", "x", 10000);
        purse = new Purse();
        DayOutcome quiet = default(DayOutcome);
        for (int day = 1; day <= 12; day++)
        {
            quiet = Day(mild, day, purse, ProductCatalogue.Hardcore, false);
        }
        Check(quiet.Missed == 100 && quiet.Step == LadderStep.None && mild.Ladder.Misses == 0 && !mild.Ladder.Penalty && mild.RateNow == 1200, "with consequences off twelve misses take no step");

        // In a debt arrangement an account stands still.
        mild.Frozen = true;
        int owed = mild.Owed;
        Check(Day(mild, 13, purse).Interest == 0 && mild.Owed == owed && mild.PaymentDue == 0f, "a frozen account bears no interest and asks for nothing");
    }

    private static void OverdraftCover()
    {
        CreditAccount o = AccountRules.Open(ProductKind.Overdraft, 800, 2200, 1);
        Check(AccountRules.Cover(o, 300) == 300 && o.Owed == 300 && o.Free == 500, "the overdraft pays a shortfall of 300");
        Check(AccountRules.Cover(o, 1000) == 500 && o.Owed == 800 && o.Free == 0 && AccountRules.Cover(o, 50) == 0, "and no more than is free of its limit");
        Check(AccountRules.Cover(o, 0) == 0 && AccountRules.Cover(o, -20) == 0 && AccountRules.Cover(null, 100) == 0, "no shortfall, nothing paid");

        o = AccountRules.Open(ProductKind.Overdraft, 800, 2200, 1);
        AccountRules.Cover(o, 300);
        var purse = new Purse { Money = 700 };
        DayOutcome day = Day(o, 2, purse);
        Check(day.Interest == 1 && day.Paid == 200 && o.Owed == 101 && purse.Money == ProductCatalogue.OverdraftFloat && !day.WasDue, "a day's interest goes on, and cash above 500 pays it back");
        day = Day(o, 3, purse);
        Check(day.Paid == 0 && o.Owed == 101 && purse.Money == 500 && !day.WasDue, "at the float nothing more is taken");
        purse.Money = 5000;
        Day(o, 4, purse);
        Check(o.Owed == 0 && !o.Closed && o.Free == 800, "paid back in full it stays open");

        // Pinned at its limit with the interest unpaid: what is over the limit is due at once.
        o = AccountRules.Open(ProductKind.Overdraft, 800, 2200, 1);
        AccountRules.Cover(o, 800);
        purse = new Purse { Money = 100 };
        day = Day(o, 2, purse);
        Check(day.WasDue && day.Missed == 2 && day.Step == LadderStep.Reminder && o.Owed == 802, "interest over the limit is a missed payment");
        day = Day(o, 3, purse);
        Check(day.Missed == 5 && day.Step == LadderStep.LateFee && day.Fee == 15 && o.Owed == 820, "and the next day a late fee");
        purse.Money = 5000;
        day = Day(o, 4, purse);
        Check(day.WasDue && day.Missed == 0 && o.Owed == 0 && o.Ladder.Misses == 0 && o.Ladder.InOrder, "paid back, it is in order again");

        o.Frozen = true;
        Check(AccountRules.Cover(o, 100) == 0, "a frozen overdraft covers nothing");
    }

    private static void CardCycle()
    {
        Check(AccountRules.Minimum(1030) == 52 && AccountRules.Minimum(300) == 25 && AccountRules.Minimum(20) == 20 && AccountRules.Minimum(10000) == 500 && AccountRules.Minimum(0) == 0,
            "the minimum: 5% of the statement, at least 25, or the whole of a smaller balance");
        Check(AccountRules.CashFee(1000) == 30 && AccountRules.CashFee(100) == 5 && AccountRules.CashFee(0) == 0, "the cash fee: 3%, at least 5");

        var purse = new Purse { Money = 5000 };
        CreditAccount card = AccountRules.Open(ProductKind.CreditCard, 1200, 2600, 10);
        int fee;
        Check(AccountRules.Draw(card, 1000, out fee) && fee == 30 && card.Owed == 1030 && card.Free == 170, "1,000 drawn costs 30 and both go on the balance");
        Check(AccountRules.MostDraw(card) == 165 && !AccountRules.Draw(card, 166, out fee) && !AccountRules.Draw(card, 10, out fee) && card.Owed == 1030, "no more than fits the limit with its fee, and no less than the least");
        bool quietDays = true;
        for (int day = 11; day <= 16; day++)
        {
            DayOutcome o = Day(card, day, purse);
            quietDays &= !o.WasDue && !o.Statement && card.Owed == 1030;
        }
        DayOutcome statement = Day(card, 17, purse);
        Check(quietDays && statement.Statement && card.StatementLeft == 1030 && card.MinimumLeft == 52 && card.DueDay == 20 && card.NextDay == 24, "a statement on the seventh day, its minimum due three days on");
        Check(AccountRules.DueTomorrow(card, 18) == 0 && AccountRules.DueTomorrow(card, 19) == 52, "the minimum shows as leaving tomorrow on the day before it is due");
        Day(card, 18, purse);
        Day(card, 19, purse);
        DayOutcome due = Day(card, 20, purse);
        Check(due.WasDue && due.Paid == 52 && due.Missed == 0 && due.Interest == 29 && card.Owed == 1007 && card.DueDay == 0, "on the due day the minimum is taken, and what is carried bears a week's interest (29)");

        // Paid in full before the due day: no interest.
        var full = AccountRules.Open(ProductKind.CreditCard, 1200, 2600, 10);
        AccountRules.Draw(full, 1000, out fee);
        Day(full, 17, purse);
        Check(AccountRules.Pay(full, 1030) == 1030 && full.Owed == 0 && full.MinimumLeft == 0 && !full.Closed, "the statement paid in full");
        DayOutcome free = Day(full, 20, purse);
        Check(free.Interest == 0 && free.Paid == 0 && free.Missed == 0 && full.Owed == 0 && full.DueDay == 0, "bears no interest");

        // The minimum missed: a miss each day until it is paid, interest once.
        var missed = AccountRules.Open(ProductKind.CreditCard, 1200, 2600, 10);
        AccountRules.Draw(missed, 1000, out fee);
        var empty = new Purse();
        Day(missed, 17, empty);
        DayOutcome a = Day(missed, 20, empty), b = Day(missed, 21, empty);
        Check(a.Missed == 52 && a.Interest == 31 && a.Step == LadderStep.Reminder && b.Missed == 52 && b.Interest == 0 && b.Step == LadderStep.LateFee && missed.Owed == 1030 + 31 + 15,
            "a missed minimum is a missed payment, tried again the next day; the interest is charged once");
        empty.Money = 5000;
        DayOutcome c = Day(missed, 22, empty);
        Check(c.Paid == 52 && missed.DueDay == 0 && missed.Ladder.Misses == 0 && missed.Owed == 1024, "paid, the card is in order");
        DayOutcome next = Day(missed, 24, empty);
        Check(next.Statement && missed.MinimumLeft == 52 && missed.DueDay == 27, "and the next statement comes on its day");

        // Paid by hand while overdue.
        var hand = AccountRules.Open(ProductKind.CreditCard, 1200, 2600, 10);
        AccountRules.Draw(hand, 1000, out fee);
        empty = new Purse();
        Day(hand, 17, empty);
        Day(hand, 20, empty);
        Check(hand.Ladder.Misses == 1 && hand.Ladder.Overdue == 52 && AccountRules.Pay(hand, 52) == 52 && hand.Ladder.Misses == 0 && hand.DueDay == 0, "paying what is overdue by hand puts the card in order");
    }

    private static List<LadderStep> Run(int level, bool secured, int misses)
    {
        var state = new LadderState();
        var steps = new List<LadderStep>();
        for (int i = 1; i <= misses; i++)
        {
            steps.Add(LadderRules.Missed(state, level, secured, i * 100));
        }
        return steps;
    }

    private static string Path(List<LadderStep> steps)
    {
        var text = new System.Text.StringBuilder();
        foreach (LadderStep step in steps)
        {
            text.Append((int)step);
        }
        return text.ToString();
    }

    private static void LadderSteps()
    {
        Check(ProductCatalogue.StepCounts.Length == 6 && ProductCatalogue.StepCountsHardcore.Length == 6 && ProductCatalogue.LastStep.Length == 4, "a count for every step and a last step for every level");
        Check(Path(Run(ProductCatalogue.Standard, true, 14)) == "12304005000000", "Standard, secured: reminder, fee, rate, the call at 5, the security at 8, and no claim");
        Check(Path(Run(ProductCatalogue.Standard, false, 14)) == "12304000000000", "Standard, unsecured: it ends with the call");
        Check(Path(Run(ProductCatalogue.Relaxed, true, 14)) == "12304000000000", "Relaxed stops after the call, secured or not");
        Check(Path(Run(ProductCatalogue.Realistic, true, 14)) == "12304005000600", "Realistic, secured: the security at 8, the claim at 12");
        Check(Path(Run(ProductCatalogue.Realistic, false, 14)) == "12304000000600", "Realistic, unsecured: the claim at 12");
        Check(Path(Run(ProductCatalogue.Hardcore, true, 10)) == "1234050060", "Hardcore: 1, 2, 3, 4, 6 and 9");
        Check(Path(Run(ProductCatalogue.Hardcore, false, 10)) == "1234000060", "Hardcore, unsecured: no repossession, the claim at 9");
        Check(Path(Run(-3, true, 6)) == Path(Run(ProductCatalogue.Relaxed, true, 6)) && Path(Run(99, true, 10)) == Path(Run(ProductCatalogue.Hardcore, true, 10)), "a level outside the four is the nearest of them");

        // A payment puts the count back; the penalty rate ends after three in a row.
        var s = new LadderState();
        LadderRules.Missed(s, ProductCatalogue.Standard, false, 100);
        LadderRules.Missed(s, ProductCatalogue.Standard, false, 200);
        Check(!LadderRules.Paid(s) && s.Misses == 0 && s.Overdue == 0 && s.Step == 0, "a payment in full puts the count back to nothing");
        Check(LadderRules.Missed(s, ProductCatalogue.Standard, false, 100) == LadderStep.Reminder, "and the next miss is a first miss again");
        LadderRules.Missed(s, ProductCatalogue.Standard, false, 200);
        Check(LadderRules.Missed(s, ProductCatalogue.Standard, false, 300) == LadderStep.PenaltyRate && s.Penalty, "the third miss brings the penalty rate");
        Check(!LadderRules.Paid(s) && !LadderRules.Paid(s) && s.Penalty, "two payments do not end it");
        LadderRules.Missed(s, ProductCatalogue.Standard, false, 100);
        Check(!LadderRules.Paid(s) && !LadderRules.Paid(s) && s.Penalty && LadderRules.Paid(s) && !s.Penalty, "a miss starts the three again; three in a row end it");
        Check(!LadderRules.Paid(s) && s.InOrder, "and then it is in order");

        Check(AccountRules.LateFee(100, ProductCatalogue.Standard) == 15 && AccountRules.LateFee(2000, ProductCatalogue.Standard) == 40 && AccountRules.LateFee(2000, ProductCatalogue.Hardcore) == 40,
            "the late fee: 2% of the instalment, at least 15");
        Check(AccountRules.LateFee(100, ProductCatalogue.Relaxed) == 8 && AccountRules.LateFee(2000, ProductCatalogue.Relaxed) == 20, "halved on Relaxed");
        Check(LadderRules.Takes(LadderStep.Claim, ProductCatalogue.Realistic, false) && !LadderRules.Takes(LadderStep.Claim, ProductCatalogue.Standard, true)
              && !LadderRules.Takes(LadderStep.Repossession, ProductCatalogue.Hardcore, false) && !LadderRules.Takes(LadderStep.Repossession, ProductCatalogue.Relaxed, true),
            "which level takes which step");

        // The penalty rate on an account of the mod's.
        CreditAccount a = AccountRules.Finance(ProductKind.CarFinance, 9000, 90, 1200, 1, "x", "x", 10000);
        var purse = new Purse();
        for (int day = 1; day <= 3; day++)
        {
            Day(a, day, purse);
        }
        Check(a.Ladder.Penalty && a.RateNow == 1700 && a.RateBp == 1200, "from the third miss an account's rate is 5 points dearer; the agreed rate is kept");
        purse.Money = 1000000;
        DayOutcome last = default(DayOutcome);
        for (int day = 4; day <= 6; day++)
        {
            last = Day(a, day, purse);
        }
        Check(last.PenaltyEnded && !a.Ladder.Penalty && a.RateNow == 1200, "three payments in a row and it is the agreed rate again");
    }

    private static CreditAccount Behind(int level, bool secured, out Purse purse)
    {
        CreditAccount a = AccountRules.Finance(ProductKind.CarFinance, 9000, 90, 1200, 1, secured ? "x" : null, "x", 10000);
        purse = new Purse();
        int[] counts = ProductCatalogue.Counts(level);
        for (int day = 1; day <= counts[(int)LadderStep.Collections - 1]; day++)
        {
            Day(a, day, purse, level);
        }
        return a;
    }

    private static void PaymentPlan()
    {
        Purse purse;
        CreditAccount a = Behind(ProductCatalogue.Standard, true, out purse);
        Check(a.Ladder.Misses == 5 && a.Ladder.CallDue && a.Ladder.Step == (int)LadderStep.Collections && a.Ladder.Overdue == 500, "five misses: the collections call is due, 500 overdue");
        Check(LadderRules.PlanPerDay(500) == 50 && LadderRules.PlanPerDay(501) == 51 && LadderRules.PlanPerDay(0) == 0, "a plan spreads what is overdue over ten days, rounded up");
        Check(AccountRules.AgreePlan(a) && a.Ladder.PlanLeft == 500 && a.Ladder.PlanPerDay == 50 && a.Ladder.Misses == 0 && !a.Ladder.CallDue && !AccountRules.AgreePlan(a), "one plan per account");
        purse.Money = 1000000;
        bool onTop = true;
        for (int day = 6; day <= 15; day++)
        {
            DayOutcome o = Day(a, day, purse);
            onTop &= o.Missed == 0 && o.Paid == 100 + 50 + o.Interest;
        }
        Check(onTop && a.Ladder.PlanLeft == 0 && a.Ladder.Overdue == 0 && a.Ladder.Step == 0 && a.Ladder.InOrder && a.Ladder.PlanUsed, "ten days of the payment and the plan on top, and the account is in order");
        DayOutcome after = Day(a, 16, purse);
        Check(after.Paid == 100 + after.Interest, "after the plan the payment is the normal one");

        // A plan that is broken goes straight to the next step.
        a = Behind(ProductCatalogue.Standard, true, out purse);
        AccountRules.AgreePlan(a);
        DayOutcome broken = Day(a, 6, purse);
        Check(broken.Step == LadderStep.Repossession && a.Ladder.PlanLeft == 0 && a.Ladder.Misses == 8, "Standard, secured: a broken plan is the security taken");
        a = Behind(ProductCatalogue.Realistic, false, out purse);
        AccountRules.AgreePlan(a);
        broken = Day(a, 6, purse, ProductCatalogue.Realistic);
        Check(broken.Step == LadderStep.Claim && a.Ladder.Misses == 12, "Realistic, unsecured: a broken plan is a claim");
        a = Behind(ProductCatalogue.Relaxed, true, out purse);
        AccountRules.AgreePlan(a);
        broken = Day(a, 6, purse, ProductCatalogue.Relaxed);
        Check(broken.Step == LadderStep.None && a.Ladder.PlanLeft == 0 && a.Ladder.Misses == 1, "Relaxed: a broken plan takes no further step");

        // What is overdue paid at once on the call.
        a = Behind(ProductCatalogue.Standard, true, out purse);
        int owed = a.Owed;
        Check(AccountRules.Pay(a, a.Ladder.Overdue) == 500 && a.Owed == owed - 500 && a.Ladder.Misses == 0 && !a.Ladder.CallDue && a.Ladder.Step == 0, "paying what is overdue ends the matter");

        // A plan on a card: a payment of its own each day.
        var card = AccountRules.Open(ProductKind.CreditCard, 1200, 2600, 10);
        int fee;
        AccountRules.Draw(card, 1000, out fee);
        purse = new Purse();
        for (int day = 17; day <= 24; day++)
        {
            Day(card, day, purse);
        }
        Check(card.Ladder.Misses == 5 && card.Ladder.CallDue && card.Ladder.Overdue == 52 && AccountRules.AgreePlan(card) && card.MinimumLeft == 0 && card.DueDay == 0 && card.Ladder.PlanPerDay == 6,
            "a card's plan stands in for the minimum that was overdue");
        purse.Money = 100000;
        int cardOwed = card.Owed;
        DayOutcome planDay = Day(card, 25, purse);
        Check(planDay.Paid == 6 && card.Owed == cardOwed - 6 && card.Ladder.PlanLeft == 46 && planDay.Missed == 0, "and takes its part each day");
    }

    private static void Repossession()
    {
        Check(LadderRules.RepossessionValue(20000) == 12000 && LadderRules.RepossessionValue(0) == 0 && LadderRules.RepossessionValue(999) == 599, "a repossessed vehicle fetches 60% of its price");
        CreditAccount a = AccountRules.Finance(ProductKind.CarFinance, 15000, 150, 1200, 1, "abc", "car", 20000);
        int surplus;
        Check(AccountRules.Repossess(a, 40, out surplus) == 12000 && surplus == 0 && a.Owed == 3000 && !a.Secured && !a.Closed && a.Ladder.RepoDay == 40, "it comes off the debt; the rest stays owed with nothing behind it");
        Check(!LadderRules.UnsecuredClaimDue(a.Ladder, a.Owed, 49, ProductCatalogue.Realistic) && LadderRules.UnsecuredClaimDue(a.Ladder, a.Owed, 50, ProductCatalogue.Realistic),
            "ten days later what is left goes to court");
        Check(!LadderRules.UnsecuredClaimDue(a.Ladder, a.Owed, 90, ProductCatalogue.Standard) && !LadderRules.UnsecuredClaimDue(a.Ladder, 0, 90, ProductCatalogue.Hardcore), "not on Standard, and not once it is paid");
        a.Ladder.ClaimNumber = 7;
        Check(!LadderRules.UnsecuredClaimDue(a.Ladder, a.Owed, 90, ProductCatalogue.Hardcore), "and only once");
        Check(LadderRules.ClaimCosts(3000) == 300 && LadderRules.ClaimCosts(200) == 50 && LadderRules.ClaimCosts(0) == 0, "the lender's costs on a claim: 10%, at least 50");

        CreditAccount b = AccountRules.Finance(ProductKind.CarFinance, 9000, 90, 1200, 1, "abc", "car", 20000);
        Check(AccountRules.Repossess(b, 40, out surplus) == 9000 && surplus == 3000 && b.Owed == 0 && b.Closed, "a vehicle worth more than the debt: the debt is cleared and the rest is the borrower's");

        CreditAccount sold = AccountRules.Finance(ProductKind.CarFinance, 9000, 90, 1200, 1, "abc", "car", 20000);
        AccountRules.Unsecure(sold);
        var purse = new Purse { Money = 1000000 };
        DayOutcome o = Day(sold, 2, purse);
        Check(!sold.Secured && o.Paid == 100 + 18 && sold.Ladder.RepoDay == 0, "a vehicle sold: the balance stays owed on the same payments, unsecured");
        Check(Path(Run(ProductCatalogue.Hardcore, sold.Secured, 10)) == "1234000060", "and no repossession step is left for it");
    }

    private static void Consolidation()
    {
        var debts = new List<DebtItem>
        {
            new DebtItem { Owed = 4000, DailyInterest = 8, DailyPayment = 50 },
            new DebtItem { Owed = 2500, DailyInterest = 5, DailyPayment = 40 },
            new DebtItem { Owed = 1000, DailyInterest = 4, DailyPayment = 0 },
            new DebtItem { Owed = 0, DailyInterest = 9, DailyPayment = 9 }
        };
        Check(ProductTerms.ConsolidationFee(7500) == 75 && ProductTerms.ConsolidationFee(1000) == 25 && ProductTerms.ConsolidationFee(0) == 0, "the fee: 1%, at least 25");
        DebtTotals before = ProductTerms.Before(debts, 100);
        Check(before.Owed == 7500 && before.Daily == 107 && before.Days == 80 && before.Cost == 640 + 315 + 400, "before: 7,500 owed, 107 a day, 80 days, 1,355 of interest");
        DebtTotals after = ProductTerms.After(7575, 15, 76, 75);
        Check(after.Owed == 7575 && after.Daily == 91 && after.Days == 100 && after.Cost == 1575, "after: one loan of 7,575 with the fee in it, 91 a day for 100 days");
        Check(ProductTerms.Consolidate(579, 7500, 1e9f) == ProductAnswer.BelowThreshold && ProductTerms.Consolidate(600, 7500, 7000f) == ProductAnswer.TooMuch
              && ProductTerms.Consolidate(600, 7500, 7575f) == ProductAnswer.Yes, "under the threshold never; otherwise it has to fit what the bank lends at the rating");
        Check(ProductTerms.Consolidate(600, 300, 1e9f) == ProductAnswer.TooLittle && ProductTerms.Consolidate(600, 0, 1e9f) == ProductAnswer.NothingOwed, "too little, or nothing, to consolidate");
        Check(ProductTerms.Before(new List<DebtItem> { new DebtItem { Owed = 30, DailyInterest = 1, DailyPayment = 50 } }, 10).Daily == 31, "a last instalment is what is left");
    }

    private static void Shark()
    {
        Check(SharkRules.Most(0f) == 5000 && SharkRules.Most(-9f) == 5000 && SharkRules.Most(800f) == 8000 && SharkRules.Most(730f) == 7000, "500 to 5,000, or ten days of income if that is more");
        var debt = new SharkDebt { Known = true };
        Check(SharkRules.CanLend(debt, 400, 0f) == ProductAnswer.TooLittle && SharkRules.CanLend(debt, 5500, 0f) == ProductAnswer.TooMuch && SharkRules.CanLend(debt, 2000, 0f) == ProductAnswer.Yes, "what it will lend");
        SharkRules.Lend(debt, 2000, 10);
        Check(debt.Active && debt.DueDay == 20 && SharkRules.CanLend(debt, 500, 0f) == ProductAnswer.AlreadyOpen, "one debt at a time, due in ten days");
        Check(SharkRules.Grow(debt, 10) == 0 && SharkRules.Grow(debt, 11) == 60 && debt.Owed == 2060 && SharkRules.Grow(debt, 11) == 0, "3% of the sum a day, added once a day");
        SharkRules.Grow(debt, 20);
        Check(debt.Owed == 2600 && SharkRules.OwedOnDueDay(2000) == 2600, "2,600 on the due day");
        Check(SharkRules.ExtendFee(debt) == 390 && SharkRules.Extend(debt, 20) && debt.Owed == 2990 && debt.DueDay == 30 && !SharkRules.Extend(debt, 21), "one extension for 15% of what is owed");
        bool capped = true;
        for (int day = 21; day <= 300; day++)
        {
            SharkRules.Grow(debt, day);
            capped &= debt.Owed <= SharkRules.Cap(2000);
        }
        Check(capped && debt.Owed == 6000 && SharkRules.Grow(debt, 500) == 0, "it grows to three times the sum and stops there");
        Check(SharkRules.Pay(debt, 1000) == 1000 && debt.Owed == 5000 && debt.Active, "part paid comes off");
        Check(SharkRules.Pay(debt, 99999) == 5000 && !debt.Active && debt.Borrowed == 0 && debt.Known && !debt.Extended, "paid in full it is over at once");

        Check(SharkRules.ActOn(1, true) == SharkAct.Warn && SharkRules.ActOn(2, true) == SharkAct.None && SharkRules.ActOn(3, true) == SharkAct.Assault && SharkRules.ActOn(6, true) == SharkAct.Damage,
            "a day late a warning, three an assault, six damage");
        Check(SharkRules.ActOn(7, true) == SharkAct.None && SharkRules.ActOn(9, true) == SharkAct.Assault && SharkRules.ActOn(12, true) == SharkAct.Damage && SharkRules.ActOn(15, true) == SharkAct.Assault,
            "then one or the other every three days");
        Check(SharkRules.ActOn(3, false) == SharkAct.Warn && SharkRules.ActOn(6, false) == SharkAct.Warn && SharkRules.ActOn(2, false) == SharkAct.None, "on the mildest level only warnings");
        SharkRules.Lend(debt, 1000, 10);
        Check(SharkRules.Today(debt, 20, true) == SharkAct.None && SharkRules.Today(debt, 21, true) == SharkAct.Warn && SharkRules.Today(debt, 21, true) == SharkAct.None && SharkRules.Today(debt, 23, true) == SharkAct.Assault,
            "nothing on the due day itself, and each day's act once");
        Check(SharkRules.DamageBill(1000) == 150 && SharkRules.DamageBill(8000) == 800 && SharkRules.DamageBill(50000) == 1500, "a repair bill: 10% of the debt, 150 to 1,500");
    }

    private static void WaysOut()
    {
        Check(ReliefRules.ArrangementPayment(400f) == 80 && ReliefRules.ArrangementPayment(0f) == 25 && ReliefRules.ArrangementPayment(-50f) == 25, "an arrangement's payment: 20% of the average daily income, at least 25");
        ArrangementPlan plan = ReliefRules.Arrangement(5000, 400f);
        Check(plan.Total == 5000 && plan.PerDay == 80 && plan.Days == 63 && ReliefRules.Arrangement(10, 400f).PerDay == 10 && ReliefRules.Arrangement(0, 400f).Days == 0, "5,000 at 80 a day is 63 days");

        Begin(200);
        CreditLedger.TestSet(800, 200);
        Relief r = CreditLedger.File.Relief;
        CreditRefusal reason;
        Check(Score() == 800 && CreditLedger.CanBorrow(out reason) && !CreditLedger.Score.Held, "with nothing running the rating is its own");
        ReliefRules.BeginArrangement(r, plan, 200);
        Check(Score() == ProductCatalogue.ArrangementScoreCap && !CreditLedger.CanBorrow(out reason) && reason == CreditRefusal.BelowThreshold && CreditLedger.Score.Held, "an arrangement holds it at 560: nobody lends");
        Check(!ReliefRules.ArrangementMissed(r) && !ReliefRules.ArrangementMissed(r) && ReliefRules.ArrangementMissed(r), "three missed payments end an arrangement");
        _day = 230;
        ReliefRules.EndArrangement(r, 230);
        Check(Score() == 560, "and for a while after it has ended");
        _day = 249;
        int held = Score();
        _day = 250;
        Check(held == 560 && Score() == 800, "for 20 days");

        Begin(100);
        CreditLedger.TestSet(800, 100);
        r = CreditLedger.File.Relief;
        BankruptcyPlan b = ReliefRules.Bankruptcy(12000, 3000, 2300, 100);
        Check(b.WrittenOff == 12000 && b.Kept == 3000 && b.CashTaken == 1800 && b.CashLeft == 500 && b.NoCreditUntil == 159 && b.NextAllowedDay == 280, "bankruptcy: the debts go, cash above 500 goes, nobody lends until day 159");
        Check(ReliefRules.Bankruptcy(100, 0, 320, 5).CashTaken == 0 && ReliefRules.Bankruptcy(100, 0, -40, 5).CashLeft == 0, "cash under 500 is left alone");
        Check(ReliefRules.CanBankrupt(r, 100), "a first bankruptcy can be asked for");
        r.BankruptDay = 100;
        Check(Score() == 300 && !CreditLedger.CanBorrow(out reason) && reason == CreditRefusal.Bankrupt, "the rating goes to 300 and the refusal says why");
        _day = 159;
        Check(Score() == 300 && ReliefRules.NoCredit(r, 159) && !CreditLedger.CanBorrow(out reason) && reason == CreditRefusal.Bankrupt, "for 60 days");
        _day = 160;
        Check(Score() == 450 && !ReliefRules.NoCredit(r, 160) && !CreditLedger.CanBorrow(out reason) && reason == CreditRefusal.BelowThreshold, "then it rebuilds from 450");
        _day = 224;
        int under = Score();
        _day = 225;
        Check(under == 578 && Score() == 580 && CreditLedger.CanBorrow(out reason), "by 2 points a day: lenders lend again 65 days on");
        _day = 400;
        Check(Score() == 800 && ReliefRules.Ceiling(r, 400) == 850, "and in the end the ceiling is gone");
        Check(!ReliefRules.CanBankrupt(r, 279) && ReliefRules.CanBankrupt(r, 280), "one bankruptcy in 180 days");

        var advice = new Relief();
        bool early = false;
        for (int day = 5; day < 15; day++)
        {
            early |= ReliefRules.AdviceDue(advice, true, false, day);
        }
        Check(!early && advice.OverdueSince == 5 && ReliefRules.AdviceDue(advice, true, false, 15) && !ReliefRules.AdviceDue(advice, true, false, 16), "debt advice is offered once, after ten days overdue");
        Check(!ReliefRules.AdviceDue(advice, false, false, 17) && advice.OverdueSince == 0 && !advice.AdviceTold, "back in order, the count starts again");
        Check(ReliefRules.AdviceDue(advice, false, true, 18) && !ReliefRules.AdviceDue(advice, false, true, 19), "a claim brings it at once");

        var approval = new Relief { ApprovedKind = (int)ProductKind.CarFinance, ApprovedAmount = 9000, ApprovedDay = 30 };
        Check(approval.HasApproval && !ReliefRules.ApprovalOver(approval, 31) && ReliefRules.ApprovalOver(approval, 32), "an approval stands to the end of the day after it is given");
        approval.ClearApproval();
        Check(!approval.HasApproval && !ReliefRules.ApprovalOver(approval, 99), "and then it is gone");
    }

    private static void SaveVersionTwo()
    {
        var file = new CreditFile { StartDay = 3 };
        CreditAccount card = file.Add(AccountRules.Open(ProductKind.CreditCard, 1200, 2600, 10));
        int fee;
        AccountRules.Draw(card, 1000, out fee);
        card.Street = "Odd|Name; St~";
        card.Number = 7;
        card.Carry = 123456789012L;
        card.Ladder.Misses = 4;
        card.Ladder.Overdue = 52;
        card.Ladder.Penalty = true;
        card.Ladder.PlanUsed = true;
        CreditAccount carAccount = file.Add(AccountRules.Finance(ProductKind.CarFinance, 21600, 240, 1000, 12, "id+/=abc", "Honza Mimic", 24000));
        carAccount.Ladder.RepoPending = true;
        carAccount.Ladder.ClaimNumber = 31;
        carAccount.Test = true;
        CreditAccount gone = file.Add(AccountRules.Open(ProductKind.Overdraft, 800, 2200, 14));
        gone.Closed = true;
        SharkRules.Lend(file.Shark, 2000, 15);
        file.Shark.Known = true;
        file.Shark.Waiting = SharkAct.Assault;
        file.Relief.ApprovedKind = (int)ProductKind.Mortgage;
        file.Relief.ApprovedAmount = 500000;
        file.Relief.ApprovedDeposit = 20;
        file.Relief.ApprovedTerm = 720;
        file.Relief.ApprovedRateBp = 700;
        file.Relief.ApprovedDay = 16;
        file.Relief.ApprovedStreet = "W 34th St";
        file.Relief.ApprovedNumber = 120;
        file.Relief.BankruptDay = 2;
        file.Relief.Arranged = true;
        file.Relief.ArrangedPerDay = 80;
        var mark = new LoanMark { Street = "W 34th St", Number = 120, Total = 25000f, FirstDay = 9, Remaining = 20000f, Payment = 208, Interest = 34, Purpose = LoanPurpose.Business, Security = "5th Ave#12" };
        mark.Ladder.Misses = 3;
        mark.Ladder.Penalty = true;
        int heldInterest = 34, heldPayment = 208;
        LoanHolds.Begin(mark, LoanHold.Penalty, ref heldInterest, ref heldPayment, 1f, Year);
        file.Loans.Add(mark);

        string line = CreditStore.Write(file);
        int version;
        CreditFile back = CreditStore.Read(line, out version);
        Check(version == 2 && back != null && CreditStore.Write(back) == line, "version 2 round trip: the same line is written again");
        if (back != null)
        {
            CreditAccount c = back.AccountById(card.Id), v = back.AccountById(carAccount.Id);
            Check(back.Accounts.Count == 3 && back.NextAccountId == 4 && c != null && v != null && back.Account(ProductKind.Overdraft) == null, "the accounts come back with their ids; a closed one is not an open one");
            Check(c != null && c.Kind == ProductKind.CreditCard && c.Owed == 1030 && c.Agreed == 1200 && c.RateBp == 2600 && c.Carry == 123456789012L && c.NextDay == 17 && c.Street == "Odd Name  St " && c.Number == 7,
                "a card's figures, its lender's name with the dividers taken out");
            Check(c != null && c.Ladder.Misses == 4 && c.Ladder.Overdue == 52 && c.Ladder.Penalty && c.Ladder.PlanUsed && !c.Ladder.CallDue, "its standing with the lender");
            Check(v != null && v.Security == "id+/=abc" && v.SecurityName == "Honza Mimic" && v.SecurityValue == 24000 && v.Secured && v.Term == 240 && v.Instalment == 90 && v.Test && v.Ladder.RepoPending && v.Ladder.ClaimNumber == 31,
                "a vehicle on finance with its security");
            Check(back.Shark.Active && back.Shark.Known && back.Shark.Owed == 2000 && back.Shark.DueDay == 25 && back.Shark.Waiting == SharkAct.Assault, "the lender of last resort");
            Check(back.Relief.HasApproval && back.Relief.ApprovedKind == (int)ProductKind.Mortgage && back.Relief.ApprovedAmount == 500000 && back.Relief.ApprovedStreet == "W 34th St" && back.Relief.ApprovedNumber == 120
                  && back.Relief.BankruptDay == 2 && back.Relief.Arranged && back.Relief.ArrangedPerDay == 80, "an approval, an arrangement and the day of a bankruptcy");
            LoanMark m = back.Loans.Count == 1 ? back.Loans[0] : new LoanMark();
            Check(m.Held == LoanHold.Penalty && m.OwnInterest == 34 && m.OwnPayment == 208 && m.Purpose == LoanPurpose.Business && m.Security == "5th Ave#12" && m.Ladder.Misses == 3 && m.Ladder.Penalty && LoanHolds.Sound(m),
                "what is held on one of the game's loans, with the loan's own figures");
        }

        // A version 1 file, as Phase 1 wrote it: five parts, seven fields to a loan.
        const string one = "v=1~h:5|3|10|1|0|0|655|2|9|2|1|8|1|95|0|-1|0|1|0|120|0~d:8|1|0|0|0|0;9|0|1|0|0|0~e:3|3|8000|1|0|0|phone;1|9|95|1|-21|0|loans~f:0|0;1|95~l:5th Ave|12|10000|3|7000|100|12";
        var data = new Dictionary<string, string> { { CreditStore.Key, one } };
        bool fresh;
        CreditFile old = CreditStore.Load(data, out fresh, out version);
        Check(old != null && !fresh && version == 1, "a version 1 file loads");
        if (old != null)
        {
            Check(old.StartDay == 5 && old.FirstCreditDay == 3 && old.OldOnTime == 10 && old.UnseenMissed == 2 && old.LegalSeen.Judgments == 1 && old.Days.Count == 2 && old.Events.Count == 2 && old.FoldedCount[1] == 1,
                "everything version 1 kept is read as it was");
            Check(old.Accounts.Count == 0 && old.NextAccountId == 1 && !old.Shark.Active && !old.Shark.Known && !old.Relief.HasApproval && old.Relief.ApprovedKind == -1 && old.Relief.BankruptDay == 0 && !old.Relief.Arranged,
                "what version 1 did not have is empty: no accounts, nothing owed elsewhere, no ceiling");
            Check(old.Loans.Count == 1 && old.Loans[0].Remaining == 7000f && old.Loans[0].Ladder.InOrder && old.Loans[0].Held == LoanHold.None && old.Loans[0].Security == string.Empty && old.Loans[0].Purpose == LoanPurpose.Plain,
                "its loan starts in order, with nothing held on it");
            var s = new CreditScore();
            CreditScoring.Compute(old, default(CreditInputs), 12, s);
            Check(s.Ceiling == CreditCatalogue.ScoreMax && s.Value >= 300 && s.Value <= 850, "and it scores without a ceiling");
            CreditStore.Save(data, old);
            int again;
            CreditFile upgraded = CreditStore.Read(data[CreditStore.Key], out again);
            Check(data[CreditStore.Key].StartsWith("v=2~h:") && again == 2 && upgraded != null && upgraded.Loans.Count == 1 && upgraded.Events.Count == 2 && upgraded.OldOnTime == 10, "it is written back as version 2 with nothing lost");
        }

        // Damage in the new parts costs that part, never the load.
        string damaged = line.Substring(0, line.IndexOf("~a:", StringComparison.Ordinal)) + "~a:x|y|z;1|99|5|5|5|0|1|1|1|1|1|1|1|0|0|0|0|0|0|0;|||~k:a|b|c~r:q|-5|z|" + new string('|', 40);
        CreditFile hurt = CreditStore.Read(damaged, out version);
        Check(hurt != null && hurt.Accounts.Count == 0 && !hurt.Shark.Active && !hurt.Relief.HasApproval && !hurt.Relief.Arranged && hurt.Loans.Count == 1, "an account of no known kind, and parts that do not read, are left out");
        var many = new CreditFile();
        for (int i = 0; i < 60; i++)
        {
            CreditAccount a = many.Add(AccountRules.Finance(ProductKind.CarFinance, 1000 + i, 60, 1000, 1, "v" + i, "car", 2000));
            a.Closed = i % 2 == 0;
        }
        Check(many.Accounts.Count <= 30 + 1 && many.Account(ProductKind.CarFinance) != null && many.NextAccountId == 61, "closed accounts are dropped when there are too many; an id is never used twice");
        many.Trim(true);
        bool allOpen = true;
        foreach (CreditAccount a in many.Accounts)
        {
            allOpen &= !a.Closed;
        }
        Check(allOpen && many.Accounts.Count == 30, "and all of them when asked");
    }

    // The game's own loan arithmetic (LoanHelper.CalculateDailyInterestPayment), written out independently in doubles.
    private static int Model(double amount, double rate, double multiplier, int years, int daysPerYear)
    {
        return (int)Math.Floor(amount * rate * multiplier / 100.0 * years / (daysPerYear * (double)years) + 1e-9);
    }

    private static void LoanHoldsOnTheGamesFormula()
    {
        bool same = true, near = true, dearer = true;
        int[] amounts = { 500, 1000, 2500, 7575, 10000, 25000, 123456, 1000000 };
        float[] multipliers = { 0.5f, 1f, 1.5f, 2f };
        foreach (int amount in amounts)
        {
            foreach (float multiplier in multipliers)
            {
                for (int rate = 1; rate <= 20; rate++)
                {
                    int own = GameLoanMath.DailyInterest(amount, rate, multiplier, 3, Year);
                    same &= Math.Abs(own - Model(amount, rate, multiplier, 3, Year)) <= 1;
                    int penalty = GameLoanMath.PenaltyInterest(own, amount, ProductCatalogue.PenaltyPoints, multiplier, Year);
                    near &= Math.Abs(penalty - Model(amount, rate + ProductCatalogue.PenaltyPoints, multiplier, 3, Year)) <= 2;
                    dearer &= penalty >= own;
                }
            }
        }
        Check(same, "a loan's daily interest is the game's own formula");
        Check(near && dearer, "the penalty interest is that formula at the loan's rate plus 5 points, to within the rounding");
        Check(GameLoanMath.DailyInterest(10000f, 8f, 1f, 3, Year) == 13 && GameLoanMath.DailyInterest(10000f, 7f, 1f, 3, Year) == 11 && GameLoanMath.PenaltyInterest(13, 10000f, 5, 1f, Year) == 21,
            "10,000 at 8%: 13 a day; a point less, 11; with the penalty, 21");

        // The loan in memory: its two figures, as the game keeps them.
        int interest = 13, payment = 27;
        var mark = new LoanMark { Total = 10000f };
        Check(!LoanHolds.Apply(mark, ref interest, ref payment, 1f, Year) && interest == 13, "nothing held, nothing written");
        LoanHolds.Begin(mark, LoanHold.Penalty, ref interest, ref payment, 1f, Year);
        Check(LoanHolds.Apply(mark, ref interest, ref payment, 1f, Year) && interest == 21 && payment == 27 && mark.OwnInterest == 13 && mark.OwnPayment == 27, "the penalty rate is written; the loan's own interest is remembered");
        Check(!LoanHolds.Apply(mark, ref interest, ref payment, 1f, Year) && interest == 21, "asking again writes nothing");
        bool exact = true;
        for (int i = 0; i < 100; i++)
        {
            LoanHolds.Restore(mark, ref interest, ref payment, 1f, Year);
            exact &= interest == 13 && payment == 27 && mark.Held == LoanHold.Penalty;
            LoanHolds.Apply(mark, ref interest, ref payment, 1f, Year);
            exact &= interest == 21 && payment == 27;
        }
        Check(exact, "put back for a save and written again a hundred times: exact each time");

        // A debt arrangement on top: nothing is taken; ending it leaves the penalty.
        LoanHolds.Begin(mark, LoanHold.Frozen, ref interest, ref payment, 1f, Year);
        LoanHolds.Apply(mark, ref interest, ref payment, 1f, Year);
        Check(interest == 0 && payment == 0 && mark.OwnInterest == 13 && mark.OwnPayment == 27, "frozen: no interest and no payment, the loan's own figures still the first ones");
        LoanHolds.End(mark, LoanHold.Frozen, ref interest, ref payment, 1f, Year);
        Check(interest == 21 && payment == 27 && mark.Held == LoanHold.Penalty, "the arrangement ends: the penalty rate is still on");

        // The game recalculates the loan (its interest multiplier was changed), and the player changes the payment.
        interest = 26;
        payment = 40;
        LoanHolds.Apply(mark, ref interest, ref payment, 2f, Year);
        Check(mark.OwnInterest == 26 && mark.OwnPayment == 40 && interest == 26 + 16 && payment == 40, "a figure changed from outside is the loan's own from then on");
        LoanHolds.End(mark, LoanHold.Penalty, ref interest, ref payment, 2f, Year);
        Check(interest == 26 && payment == 40 && mark.Held == LoanHold.None && mark.OwnInterest == 0 && LoanHolds.Sound(mark), "the penalty ends: the loan's own figures exactly, and nothing kept");
        LoanHolds.End(mark, LoanHold.Penalty, ref interest, ref payment, 2f, Year);
        Check(interest == 26 && payment == 40, "ending what is not held changes nothing");
        Check(!LoanHolds.Sound(new LoanMark { Held = LoanHold.Frozen }), "a hold with no own figures remembered is caught by the self-check");
    }
}
