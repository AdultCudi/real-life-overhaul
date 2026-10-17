using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

// The credit card fixed: everything else could be paid off, but the card had no way to be paid off and so the agreement
// never ended. Three faults, each reproduced here:
//   1. in a debt arrangement every account was frozen and its buttons hidden, so the card could only be paid by the
//      arrangement's daily payment; now a frozen debt can be paid by hand, that counts under the arrangement, and the
//      arrangement ends cleared the moment the last of it is paid, by whatever route, once;
//   2. a missed minimum was tried again each day and every failed day was a new miss (Banking/Accounts/CreditAccount.cs Card):
//      a player with no cash went from a reminder to the collections call in five days; now one miss a statement;
//   3. a new statement put its own minimum in place of one still unpaid, so what was overdue was due no longer; now it
//      is carried into the new minimum.
internal static partial class Program
{
    private static void CardFix()
    {
        CardFixArrangement();
        CardFixLoanRead();
        CardFixMissOnce();
        CardFixLateFeeOnce();
        CardFixLatePayment();
        CardFixCarried();
        CardFixCarriedPlan();
        CardFixSources();
        CardFixWords();
    }

    // A drawn card with its first statement on day 17, due on day 20.
    private static CreditAccount CardFixCard(int drawn = 1000)
    {
        AccountRules.DaysPerYear = Year;
        CreditAccount card = AccountRules.Open(ProductKind.CreditCard, 1200, 2600, 10);
        AccountRules.Draw(card, drawn, out int _);
        return card;
    }

    // The player's case: loans and a card in an arrangement, the loans paid off by hand, then the card by hand.
    private static void CardFixArrangement()
    {
        CreditAccount card = CardFixCard();
        var accounts = new List<CreditAccount> { card };
        var r = new Relief();
        ArrangementPlan plan = ReliefRules.Arrangement(2000 + card.Owed, 400f);
        ReliefRules.BeginArrangement(r, plan, 30);
        card.Frozen = true;   // as CreditDesk.Arrange does it
        card.MinimumLeft = 0;
        card.DueDay = 0;
        int owed = card.Owed;
        var purse = new Purse { Money = 100000 };
        bool still = true;
        for (int day = 31; day <= 45; day++)
        {
            DayOutcome o = Day(card, day, purse);
            still &= !o.WasDue && o.Paid == 0 && o.Interest == 0 && o.Step == LadderStep.None && card.Owed == owed;
        }
        Check(still && ReliefRules.FrozenOwed(accounts) == owed, "card fix: a card in an arrangement stands still (no statement, interest or step) and is what the arrangement still holds");

        // The loans are paid off by hand (EconoView, or a bank's app): the arrangement goes on, the card is left.
        bool endsEarly = ReliefRules.PaidByHand(r, 2000, ReliefRules.FrozenOwed(accounts));
        Check(!endsEarly && r.Arranged && r.ArrangedPaid == 2000, "card fix: paying the loans off by hand counts under the arrangement, which goes on while the card is owed");

        // Part of the card by hand, then the rest.
        Check(AccountRules.Pay(card, 300) == 300 && !ReliefRules.PaidByHand(r, 300, ReliefRules.FrozenOwed(accounts)) && r.ArrangedPaid == 2300 && card.Owed == owed - 300,
            "card fix: part of the card paid by hand: counted, and the arrangement goes on");
        int rest = card.Owed;
        int used = AccountRules.Pay(card, rest);
        bool ends = ReliefRules.PaidByHand(r, used, ReliefRules.FrozenOwed(accounts));
        Check(used == rest && card.Owed == 0 && ends, "card fix: the card paid off by hand is the last of it: the arrangement is to end now");
        ReliefRules.EndArrangement(r, 46);   // what CreditDesk.EndArrangement(day, true, 0) does to the record, on the day of the payment
        Check(!r.Arranged && r.ArrangedEndDay == 46 && r.ArrangedPaid == r.ArrangedTotal && r.ArrangedTotal == plan.Total,
            "card fix: it ends cleared on the day the card is paid off, not a day later, and Finance's paid of total is the whole of it");
        Check(!ReliefRules.PaidByHand(r, 50, 0) && r.ArrangedPaid == plan.Total && r.ArrangedEndDay == 46, "card fix: and it ends once: a payment after it has ended ends nothing again and counts under nothing");
        Check(ReliefRules.Ceiling(r, 46 + ProductCatalogue.ArrangementCapDaysAfter - 1) == ProductCatalogue.ArrangementScoreCap
              && ReliefRules.Ceiling(r, 46 + ProductCatalogue.ArrangementCapDaysAfter) == CreditCatalogue.ScoreMax,
            "card fix: the rating's hold after a cleared arrangement is as before, counted from the day it ended");
        Check(ReliefRules.FrozenOwed(null) == 0 && ReliefRules.FrozenOwed(new List<CreditAccount>()) == 0 && !ReliefRules.PaidByHand(null, 10, 0), "card fix: nothing held, nothing owed");

        // After it: the card is free again, with nothing owed it runs quietly and can be drawn on.
        card.Frozen = false;
        DayOutcome after = Day(card, 47, purse);
        Check(!after.WasDue && after.Missed == 0 && card.Owed == 0 && !card.Closed && card.Ladder.InOrder, "card fix: the card is out of the arrangement, in order, with nothing owed");

        // The daily payment's own way still ends it once (a frozen account, nothing paid by hand).
        var day2 = new Relief();
        ReliefRules.BeginArrangement(day2, ReliefRules.Arrangement(80, 400f), 10);
        Check(!ReliefRules.PaidByHand(day2, 0, 80) && day2.Arranged && day2.ArrangedPaid == 0, "card fix: nothing paid by hand changes nothing");
    }

    // A frozen loan paid in EconoView: the file's reading says it was held by the arrangement, with what was paid.
    private static void CardFixLoanRead()
    {
        object loan = new object(), other = new object();
        var marks = new List<LoanMark>
        {
            new LoanMark { Street = "Bank St", Number = 1, Total = 5000f, Remaining = 1200f, Payment = 0, Interest = 0, Ref = loan, Held = LoanHold.Frozen },
            new LoanMark { Street = "Bank St", Number = 1, Total = 3000f, Remaining = 900f, Payment = 0, Interest = 0, Ref = other, Held = LoanHold.Frozen }
        };
        var live = new List<LoanView>
        {
            new LoanView { Street = "Bank St", Number = 1, Total = 3000f, Remaining = 400f, Payment = 0, Interest = 0, Ref = other }
        };
        var findings = new List<LoanFinding>();
        LoanBook.Sync(marks, live, 40, findings);
        int gone = 0, part = 0;
        bool held = true;
        foreach (LoanFinding f in findings)
        {
            held &= (f.Mark.Held & LoanHold.Frozen) != 0;
            gone += f.Note == LoanNote.Gone ? f.Amount : 0;
            part += f.Note == LoanNote.ExtraPaid ? f.Amount : 0;
        }
        Check(findings.Count == 2 && held && gone == 1200 && part == 500 && marks.Count == 1,
            "card fix: a loan of the arrangement paid off in EconoView, and one paid in part, are read as paid by hand while it held them (1,200 and 500)");
    }

    // A minimum missed five days in a row with no cash: one miss, one notice's worth, no late fee.
    private static void CardFixMissOnce()
    {
        foreach (bool consequences in new[] { true, false })
        {
            CreditAccount card = CardFixCard();
            var empty = new Purse();
            Day(card, 17, empty, ProductCatalogue.Standard, consequences);
            int counted = 0, fees = 0, missedMoney = 0;
            LadderStep last = LadderStep.None;
            for (int day = 20; day <= 24; day++)
            {
                DayOutcome o = Day(card, day, empty, ProductCatalogue.Standard, consequences);
                counted += o.WasDue && o.Missed > 0 ? 1 : 0;   // what CreditDesk.AccountsDay posts to the credit file and tells the player
                missedMoney += o.WasDue ? o.Missed : 0;
                fees += o.Fee > 0 ? 1 : 0;
                last = o.Step != LadderStep.None ? o.Step : last;
            }
            Check(counted == 1 && missedMoney == 52 && fees == 0 && card.Ladder.Misses == (consequences ? 1 : 0) && last == (consequences ? LadderStep.Reminder : LadderStep.None)
                  && !card.Ladder.Penalty && !card.Ladder.CallDue,
                "card fix: a minimum missed five days in a row counts once (" + counted + " counted, " + fees + " late fee(s), consequences " + (consequences ? "on" : "off") + ")");
        }
        CreditAccount hard = CardFixCard();
        var none = new Purse();
        Day(hard, 17, none, ProductCatalogue.Hardcore);
        for (int day = 20; day <= 23; day++)
        {
            Day(hard, day, none, ProductCatalogue.Hardcore);
        }
        Check(hard.Ladder.Misses == 1 && hard.Ladder.Step == (int)LadderStep.Reminder && hard.Owed == 1030 + 31, "card fix: Hardcore too: one miss, no fee, interest once");
    }

    // A second statement missed: its late fee once, whatever the days after it.
    private static void CardFixLateFeeOnce()
    {
        CreditAccount card = CardFixCard();
        var empty = new Purse();
        int fees = 0, feeMoney = 0, counted = 0;
        for (int day = 17; day <= 30; day++)
        {
            DayOutcome o = Day(card, day, empty);
            fees += o.Fee > 0 ? 1 : 0;
            feeMoney += o.Fee;
            counted += o.WasDue && o.Missed > 0 ? 1 : 0;
        }
        Check(counted == 2 && card.Ladder.Misses == 2 && fees == 1 && feeMoney == AccountRules.LateFee(card.Ladder.Overdue, ProductCatalogue.Standard) && !card.Ladder.Penalty,
            "card fix: two statements missed over fourteen days are two misses and one late fee (" + counted + " missed, " + fees + " fee(s))");
    }

    // The money is taken the day it is there, and puts the card in order.
    private static void CardFixLatePayment()
    {
        CreditAccount card = CardFixCard();
        var purse = new Purse();
        Day(card, 17, purse);
        Day(card, 20, purse);
        Day(card, 21, purse);
        purse.Money = 60;
        DayOutcome o = Day(card, 22, purse);
        Check(o.Paid == 52 && !o.WasDue && o.Missed == 0 && purse.Money == 8 && card.MinimumLeft == 0 && card.DueDay == 0 && card.Ladder.InOrder && card.Ladder.Overdue == 0,
            "card fix: the overdue minimum is taken the day the cash is there, and the card is in order again");
        DayOutcome quiet = Day(card, 23, purse);
        Check(!quiet.WasDue && quiet.Paid == 0 && quiet.Interest == 0, "card fix: and nothing more is asked until the next statement");
    }

    // The overdue minimum is carried into the next statement's, on its own day, and paying it puts the card in order.
    private static void CardFixCarried()
    {
        CreditAccount card = CardFixCard();
        var empty = new Purse();
        Day(card, 17, empty);
        Day(card, 20, empty);
        for (int day = 21; day <= 23; day++)
        {
            Day(card, day, empty);
        }
        DayOutcome statement = Day(card, 24, empty);
        int want = Math.Min(card.Owed, AccountRules.Minimum(card.Owed) + 52);
        Check(statement.Statement && card.StatementLeft == card.Owed && card.MinimumLeft == want && want == 52 + AccountRules.Minimum(card.Owed) && card.DueDay == 20,
            "card fix: an unpaid minimum of 52 is carried into the next statement's minimum (" + card.MinimumLeft + "), and stays due from its own day");
        Check(AccountRules.DueTomorrow(card, 24) == want, "card fix: what leaves tomorrow shows the carried minimum");
        // Paying what is overdue by hand (the collections call's "pay what is overdue" takes this way too) puts it in order.
        Check(card.Ladder.Overdue == 52 && AccountRules.Pay(card, card.Ladder.Overdue) == 52 && card.Ladder.InOrder && card.Ladder.Overdue == 0 && card.MinimumLeft == want - 52 && card.DueDay == 20,
            "card fix: paying the overdue part puts the card in order; this statement's own minimum is still to pay");

        // Not paid: the next statement's miss counts once more, with all of it overdue.
        CreditAccount behind = CardFixCard();
        for (int day = 17; day <= 24; day++)
        {
            Day(behind, day, empty);
        }
        int carried = behind.MinimumLeft;
        DayOutcome second = Day(behind, 25, empty);
        Check(second.WasDue && second.Missed == carried && behind.Ladder.Misses == 2 && behind.Ladder.Overdue == carried && second.Step == LadderStep.LateFee,
            "card fix: the carried minimum missed again is the second statement's one miss, with all of it overdue");
        var purse = new Purse { Money = 100000 };
        DayOutcome paid = Day(behind, 26, purse);
        Check(paid.Paid == carried && behind.Ladder.InOrder && behind.MinimumLeft == 0 && behind.DueDay == 0, "card fix: taken the day the cash is there, in order again");

        // Capped at what is owed.
        CreditAccount small = CardFixCard(25);
        Day(small, 17, empty);
        Day(small, 20, empty);
        Day(small, 24, empty);
        Check(small.MinimumLeft == small.Owed && small.Owed < AccountRules.Minimum(small.Owed) + AccountRules.Minimum(small.StatementLeft), "card fix: the carried minimum is never more than is owed");
    }

    // The collections call's plan on a card with a carried minimum: it stands in for all of it; broken, all of it is carried.
    private static void CardFixCarriedPlan()
    {
        CreditAccount card = CardFixCard();
        var empty = new Purse();
        for (int day = 17; day <= 60 && !card.Ladder.CallDue; day++)
        {
            Day(card, day, empty);
        }
        int overdue = card.Ladder.Overdue;
        Check(AccountRules.AgreePlan(card) && card.Ladder.PlanLeft == overdue && card.MinimumLeft == 0 && card.DueDay == 0, "card fix: the plan stands in for the carried minimum");
        var purse = new Purse { Money = card.Ladder.PlanPerDay };
        DayOutcome kept = Day(card, 47, purse);
        int left = card.Ladder.PlanLeft;
        Check(kept.Paid > 0 && left == overdue - kept.Paid, "card fix: a plan day kept");
        DayOutcome broken = Day(card, 48, empty);
        Check(broken.Missed > 0 && card.Ladder.PlanLeft == 0 && card.Ladder.Overdue == left, "card fix: a broken plan leaves all of its money overdue (" + card.Ladder.Overdue + " of " + left + ")");
        int day2 = card.NextDay;
        for (int day = 49; day <= day2; day++)
        {
            Day(card, day, empty);
        }
        Check(card.MinimumLeft == Math.Min(card.Owed, AccountRules.Minimum(card.Owed) + left) && card.DueDay == day2 + ProductCatalogue.CardGraceDays,
            "card fix: and the next statement carries it into its minimum");
    }

    // The game side, read from its source: the pay control on a frozen debt, and every route ending the arrangement.
    private static void CardFixSources()
    {
        string economy = System.IO.Path.Combine(LocalesFolder(), @"..\Scripts\Economy");
        string Read(string file) => System.IO.File.ReadAllText(System.IO.Path.Combine(economy, file));
        string offers = Read(@"Banking\Desk\CreditDesk.Offers.cs"), relief = Read(@"Banking\Desk\CreditDesk.Relief.cs"), director = Read(@"Banking\EconomyDirector.cs"), banks = Read(@"Banking\Desk\CreditDesk.Banks.cs");
        string loans = Read(@"Banking\BankApps\BankApp.Loans.cs"), bank = Read(@"Banking\BankApps\BankApp.cs"), finance = Read(@"Finance\FinanceApp.Accounts.cs");
        Check(offers.Contains("if (a.Frozen)") && offers.Contains("PaidByHand(used,"), "card fix: a payment by hand into a frozen account counts under the arrangement (CreditDesk.Pay)");
        Check(director.Contains("_desk.PaidByHand(arranged,") && director.Contains("LoanPaidEvent") && director.Contains("EventHold.Keep(ref GameEvent.onGameEventTriggered"),
            "card fix: a frozen loan paid in EconoView or a bank's app is read at once and counts too (EconomyDirector)");
        Check(relief.Contains("int left = ArrangedLeft();") && relief.Contains("EndArrangement(day, true, 0);"), "card fix: the daily payment and a payment by hand share one reckoning of what is left");
        Check(!banks.Contains("(m.Held & LoanHold.Frozen) != 0 || Save == null"), "card fix: a bank's app pays a frozen loan, as EconoView does");
        Check(!loans.Contains("if (!card.Frozen)") && !loans.Contains("if (!frozen)   // (in a debt arrangement") && !bank.Contains("if (!a.Frozen)\n") && !bank.Contains("if (!a.Frozen)\r\n                {\r\n                    Transform buttons")
              && !finance.Contains("if (!a.Frozen)   // (in a debt arrangement"),
            "card fix: no page hides a frozen debt's pay buttons any more");
        Check(loans.Contains("if (card.Frozen)\r\n            {\r\n                return;") || loans.Contains("if (card.Frozen)\n            {\n                return;"), "card fix: a frozen card is not drawn on or closed");
    }

    private static void CardFixWords()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("card fix", langs, codes))
        {
            return;
        }
        string lacking = LanguagesLacking(langs, codes, "reallife_econ_account_arranged_pay");
        Check(lacking.Length == 0, "card fix: the arrangement's line on a frozen debt is in every language (" + lacking + ")");
        string guide = LanguagesLacking(langs, codes, "reallife_guide_economy");
        Check(guide.Length == 0 && langs[0]["reallife_guide_economy"].Contains("one missed payment for that statement") && langs[0]["reallife_guide_economy"].Contains("pay any of them off sooner"),
            "card fix: the Guide's credit page says a missed minimum counts once and is carried, and that an arrangement's debts can be paid sooner (" + guide + ")");
        bool line = false;
        foreach (KeyValuePair<string, string> row in langs[0])
        {
            line |= row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.Contains("Fixed: in a debt arrangement you can now pay the credit card off early");
        }
        Check(line, "card fix: What's new has the fix's line, in some reallife_changes_ row");
    }
}
