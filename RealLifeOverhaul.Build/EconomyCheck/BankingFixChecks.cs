using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul.Economy;

// The player's banking fixed (Scripts/Economy/Banking), each reproduced here:
//   4. the student loan dropped out of the rating once the credit side had stood down and started again in a session;
//   5. a purchase on finance paid the approval's interest and the new account's for the same day, and on the part of the
//      advance that went back unused; a bank app's own Cancel took the charge with no question, and its approval question
//      showed "{per_day}" unfilled; the Guide said nothing of the interest;
//   6. a card's minimum paid by hand before its due day earned no payment on time;
//   7. consolidation could pay off the game's bankruptcy-quest emergency loan;
//   8. the game's interest multiplier changed mid-game priced every loan again at the rating's rate of the day, and a
//      business loan lost its point off;
//   9. a bank app's picked loan was known by its place in the list;
//   10. the private lender's fee for more time was named only after it was taken;
// and the interest multiplier and the loan-paid event read in one place only.
internal static partial class Program
{
    private static void BankingFixes()
    {
        BankingFixStudentLoan();
        BankingFixApproval();
        BankingFixCardEarly();
        BankingFixConsolidation();
        BankingFixRepricedRate();
        BankingFixRepricedHolds();
        BankingFixRowId();
        BankingFixLender();
        BankingFixOneDefinition();
        BankingFixTexts();
    }

    private static string BankingSource(string file)
    {
        return EconomySource(@"Banking\" + file);
    }

    // 4: the ledger forgets every account when it closes; the start registers the other features' again through the bridge.
    private static void BankingFixStudentLoan()
    {
        var student = new Account { Owed = 3000f, Cap = 20000f };
        Begin(10);
        CreditLedger.Register(student);
        CreditLedger.Close();
        Check(!CreditLedger.Accounts.Contains(student), "banking fix: the ledger lets go of every account when the credit side stands down (why the start must register them again)");
        string director = BankingSource("EconomyDirector.cs"), start = MemberText(director, "private void StartUp(");
        int open = start.IndexOf("CreditLedger.Open(file);", StringComparison.Ordinal), others = start.IndexOf("EconomyBridges.OtherAccounts?.Invoke(_others);", StringComparison.Ordinal);
        int refresh = start.IndexOf("CreditLedger.Refresh();", StringComparison.Ordinal);
        Check(open > 0 && others > open && refresh > others && start.Contains("CreditLedger.Register(_others[i]);"),
            "banking fix: each start of the credit side registers the other features' credit (the student loan) before the rating is first worked out");
        string links = EconomySource(@"..\Wiring\CareersLinks.cs");
        Check(MemberText(links, "internal static void Connect(").Contains("Economy.EconomyBridges.OtherAccounts = StudentCredit;")
              && MemberText(links, "internal static void Disconnect(").Contains("Economy.EconomyBridges.OtherAccounts = null;")
              && MemberText(links, "private static void StudentCredit(").Contains("into.Add(Loan);"),
            "banking fix: Careers gives the student loan through the bridge while it is connected");
        Check(MemberText(EconomySource("EconomyBridges.cs"), "public static void Reset(").Contains("OtherAccounts = null;"), "banking fix: the bridge is put back with the others");
    }

    // 5: the approval's interest on a purchase is on the financed part only and to the day before; a cancel stays as it was.
    private static void BankingFixApproval()
    {
        AccountRules.DaysPerYear = Year;
        var r = new Relief { ApprovedKind = (int)ProductKind.CarFinance, ApprovedAmount = 10000, ApprovedDeposit = 10, ApprovedTerm = 60, ApprovedRateBp = 1000, ApprovedDay = 10 };
        int charge = ReliefRules.PurchaseCharge(r, 9000, 12, Year);
        CreditAccount account = AccountRules.Finance(ProductKind.CarFinance, 9000, 60, 1000, 12, "v1", "Car", 10000);
        DayOutcome first = Day(account, 13, new Purse { Money = 100000 });
        Check(charge == 30 && first.Interest == 15 && charge + first.Interest == ProductTerms.AdvanceCharge(9000, 1000, 3, Year),
            "banking fix: bought on the third day held, the 9,000 financed pays 30 for the two days before and the account 15 for the day of purchase, each day once (" + charge + " and "
            + first.Interest + "; it was 50 on the whole 10,000 for three days)");
        var larger = new Relief { ApprovedKind = r.ApprovedKind, ApprovedAmount = 20000, ApprovedRateBp = 1000, ApprovedDay = 10 };
        Check(ReliefRules.PurchaseCharge(larger, 9000, 12, Year) == charge, "banking fix: what of the approval goes back unused costs nothing (the same 30 with 20,000 approved)");
        Check(ReliefRules.PurchaseCharge(r, 9000, 10, Year) == 0 && ReliefRules.PurchaseCharge(r, 0, 12, Year) == 0,
            "banking fix: bought on the day it was approved, or nothing financed, the approval costs nothing");
        Check(ReliefRules.PurchaseCharge(r, 15000, 12, Year) == ProductTerms.AdvanceCharge(10000, 1000, 2, Year), "banking fix: never more than the approved amount is charged for");
        Check(ReliefRules.CancelCharge(r, 12, Year) == 50 && ProductTerms.AdvanceCharge(10000, 1000, 3, Year) == 50,
            "banking fix: an approval ended with nothing bought still costs interest on all of it for every day held (50)");
        string finance = BankingSource(@"Desk\CreditDesk.Finance.cs");
        Check(MemberText(finance, "private void Financed(").Contains("ReliefRules.PurchaseCharge(r, financed, day, AccountRules.DaysPerYear)")
              && MemberText(finance, "public void CancelApproval(").Contains("ReliefRules.CancelCharge(r, Today, AccountRules.DaysPerYear)") && !finance.Contains("ProductTerms.AdvanceCharge("),
            "banking fix: the desk charges a purchase and a cancel by the one rule each");
        string products = EconomySource(@"Finance\FinanceApp.Products.cs"), approval = MemberText(products, "internal static void WriteApproval(");
        Check(approval.Contains("ReliefRules.CancelCharge(relief, today, AccountRules.DaysPerYear)") && approval.Contains("\"reallife_econ_approval_charge\"")
              && approval.IndexOf("desk.CancelApproval(false)", StringComparison.Ordinal) > approval.IndexOf("GameConfirm.Show(\"reallife_econ_approval_cancel\"", StringComparison.Ordinal),
            "banking fix: an approval's page names its cost so far and asks before a cancel takes it");
        string apply = MemberText(BankingSource(@"BankApps\BankApp.Apply.cs"), "private void WriteFinance(");
        Check(apply.Contains("FinanceApp.WriteApproval(box, desk, relief, After);") && !apply.Contains("desk.CancelApproval(false)")
              && apply.Contains("per_day = FinanceText.Money(ProductTerms.AdvanceCharge(amount, offer.RateBp, 1, AccountRules.DaysPerYear))"),
            "banking fix: a bank app's approval asks before a cancel, as Finance does, and its question fills in the interest a day");
    }

    // 6: a card's minimum paid by hand before its due day is a payment on time on that day.
    private static void BankingFixCardEarly()
    {
        CreditAccount card = CardFixCard();
        var purse = new Purse { Money = 5000 };
        DayOutcome statement = Day(card, 17, purse);
        int minimum = card.MinimumLeft;
        Check(statement.Statement && minimum > 0 && card.DueDay == 20 && AccountRules.Pay(card, minimum) == minimum && card.MinimumLeft == 0, "banking fix: the minimum is paid by hand two days early");
        DayOutcome due = Day(card, 20, purse);
        Check(due.WasDue && due.Paid == 0 && due.Missed == 0 && due.PaidEarly && due.OnTime && card.Ladder.InOrder,
            "banking fix: on its due day a minimum paid early counts as a payment on time, with nothing taken");
        CreditAccount whole = CardFixCard();
        Day(whole, 17, purse);
        AccountRules.Pay(whole, whole.Owed);
        DayOutcome cleared = Day(whole, 20, purse);
        Check(cleared.OnTime && whole.Owed == 0 && !whole.Closed, "banking fix: so does a statement paid off in full before its day");
        CreditAccount taken = CardFixCard();
        Day(taken, 17, purse);
        DayOutcome onDay = Day(taken, 20, purse);
        Check(onDay.OnTime && !onDay.PaidEarly && onDay.Paid > 0, "banking fix: a minimum taken on its day is on time as before");
        CreditAccount none = CardFixCard();
        var empty = new Purse();
        Day(none, 17, empty);
        DayOutcome missed = Day(none, 20, empty);
        Check(!missed.OnTime && missed.Missed > 0 && !missed.PaidEarly, "banking fix: a minimum not paid is missed as before");
        CreditAccount overdraft = AccountRules.Open(ProductKind.Overdraft, 500, 1000, 1);
        overdraft.Owed = 400;
        overdraft.Ladder.Misses = 1;
        DayOutcome back = Day(overdraft, 5, new Purse());
        Check(back.WasDue && !back.OnTime, "banking fix: an overdraft back within its limit with nothing paid is in order but no payment on time, as before");
        CreditAccount car = AccountRules.Finance(ProductKind.CarFinance, 9000, 60, 1000, 12, "v1", "Car", 10000);
        Check(Day(car, 13, new Purse { Money = 5000 }).OnTime, "banking fix: an instalment taken is on time as before");
        Check(MemberText(BankingSource(@"Desk\CreditDesk.Day.cs"), "private void AccountsDay(").Contains("else if (o.OnTime)"), "banking fix: the desk counts payments on time by the day's outcome");
    }

    // 7: refused while the game's bankruptcy quest runs, at whichever bank, and the page says why.
    private static void BankingFixConsolidation()
    {
        string consolidation = MemberText(BankingSource(@"Desk\CreditDesk.Loans.cs"), "public ProductAnswer Consolidation(");
        int busy = consolidation.IndexOf("BankTerms.QuestHold(bank.Settings) || GameBankruptcy", StringComparison.Ordinal), loans = consolidation.IndexOf("_debts.Clear();", StringComparison.Ordinal);
        Check(busy > 0 && loans > busy && consolidation.Contains("return ProductAnswer.Busy;"), "banking fix: consolidation is refused while the game's bankruptcy quest runs, before any loan is counted");
        string products = EconomySource(@"Finance\FinanceApp.Products.cs");
        Check(MemberText(products, "internal static string NotConsolidated(").Contains("answer == ProductAnswer.Busy && CreditDesk.GameBankruptcy ? FinanceText.T(\"reallife_advice_game_quest\")")
              && MemberText(products, "private void WriteConsolidate(").Contains("Refused(box, NotConsolidated(answer, Why(answer)))")
              && MemberText(BankingSource(@"BankApps\BankApp.Apply.cs"), "private void WriteConsolidate(").Contains("FinanceApp.NotConsolidated(answer, Why(answer))"),
            "banking fix: both consolidation pages say the game's own quest is the way out");
    }

    // 8: a loan priced again for a new multiplier keeps the rate it was made at.
    private static void BankingFixRepricedRate()
    {
        float[] multipliers = { 0.5f, 1f, 1.5f, 2f, 3f };
        int exact = 0, unique = 0, near = 0, all = 0;
        string seen = string.Empty;
        foreach (float amount in new[] { 800f, 5000f, 20000f, 60000f, 150000f })
        {
            foreach (int years in new[] { 1, 3, 5 })
            {
                for (int rate = 1; rate <= 30; rate++)
                {
                    foreach (float was in multipliers)
                    {
                        int interest = GameLoanMath.DailyInterest(amount, rate, was, years, Year);
                        bool one = GameLoanMath.DailyInterest(amount, rate - 1, was, years, Year) != interest && GameLoanMath.DailyInterest(amount, rate + 1, was, years, Year) != interest;
                        foreach (float now in multipliers)
                        {
                            int want = GameLoanMath.DailyInterest(amount, rate, now, years, Year), got = GameLoanMath.Repriced(interest, amount, was, now, years, Year);
                            all++;
                            unique += one ? 1 : 0;
                            exact += one && got == want ? 1 : 0;
                            bool close = Math.Abs(got - want) <= Math.Ceiling(now / was) + 1;
                            near += close ? 1 : 0;
                            if ((one && got != want) || !close)
                            {
                                seen = " " + amount + " at " + rate + "% from " + was + " to " + now + ": " + got + " for " + want;
                            }
                        }
                    }
                }
            }
        }
        Check(unique > 0 && exact == unique && near == all, "banking fix: a loan priced again keeps its own whole rate: exact wherever its interest tells the rate (" + exact + " of " + unique
                                                           + "), within a unit or so on the smallest loans (" + near + " of " + all + ")" + seen);
        // The report's case: a business loan at 7% (the rating's 8 less a point) when the bank's rate for the rating is 9 today.
        int own = GameLoanMath.DailyInterest(50000f, 7, 1f, 2, Year), game = GameLoanMath.DailyInterest(50000f, 9, 2f, 2, Year);
        int kept = GameLoanMath.Repriced(own, 50000f, 1f, 2f, 2, Year);
        Check(kept == GameLoanMath.DailyInterest(50000f, 7, 2f, 2, Year) && kept != game, "banking fix: a business loan keeps its point off at the new multiplier (" + kept + " a day, not the game's " + game + ")");
        Check(GameLoanMath.Repriced(own, 50000f, 0f, 2f, 2, Year) == -1, "banking fix: with no multiplier before no rate can be read, and the game's figure stands");
        Check(GameLoanMath.Repriced(13, 6000f, 1f, 1f, 1, Year) == 13, "banking fix: the same multiplier changes nothing");
    }

    // 8: what is held on a loan stays held, on its own figures at the new multiplier, and the game's figure is not taken for its own.
    private static void BankingFixRepricedHolds()
    {
        int own7 = GameLoanMath.DailyInterest(50000f, 7, 1f, 2, Year), now7 = GameLoanMath.DailyInterest(50000f, 7, 2f, 2, Year);
        int gameInterest = GameLoanMath.DailyInterest(50000f, 9, 2f, 2, Year), gamePayment = 416;
        var plain = new LoanMark { Total = 50000f, Interest = own7, Payment = 400 };
        int interest = gameInterest, payment = gamePayment;
        LoanHolds.Repriced(plain, ref interest, ref payment, 1f, 2f, 2, Year, true);
        Check(interest == now7 && payment == 400 && plain.Interest == now7 && plain.Payment == 400,
            "banking fix: a loan priced again by the game gets back its own rate at the new multiplier and its own payment, and the mark reads it so (no payment change on file)");
        var penalty = new LoanMark { Total = 50000f, Held = LoanHold.Penalty, OwnInterest = own7, OwnPayment = 400 };
        interest = gameInterest;
        payment = gamePayment;
        LoanHolds.Repriced(penalty, ref interest, ref payment, 1f, 2f, 2, Year, true);
        int wanted = GameLoanMath.PenaltyInterest(now7, 50000f, ProductCatalogue.PenaltyPoints, 2f, Year);
        int again = interest, againPayment = payment;
        bool rewrite = LoanHolds.Apply(penalty, ref again, ref againPayment, 2f, Year);
        Check(penalty.OwnInterest == now7 && penalty.OwnPayment == 400 && interest == wanted && payment == 400 && !rewrite && penalty.OwnInterest == now7,
            "banking fix: at the penalty rate, the loan's own interest moves to the new multiplier, the penalty stays on it, and the hour's holds take nothing of the game's figure");
        var frozen = new LoanMark { Total = 50000f, Held = LoanHold.Frozen, OwnInterest = own7, OwnPayment = 400 };
        interest = gameInterest;
        payment = gamePayment;
        LoanHolds.Repriced(frozen, ref interest, ref payment, 1f, 2f, 2, Year, true);
        Check(interest == 0 && payment == 0 && frozen.OwnInterest == now7 && frozen.OwnPayment == 400, "banking fix: a loan in an arrangement stays still, its own figures moved for when it ends");
        var off = new LoanMark { Total = 50000f, Held = LoanHold.Frozen, OwnInterest = own7, OwnPayment = 400 };
        interest = gameInterest;
        payment = gamePayment;
        LoanHolds.Repriced(off, ref interest, ref payment, 1f, 2f, 2, Year, false);
        Check(interest == now7 && payment == 400, "banking fix: with the desk off, a held loan carries its own figures, as the desk left it");
        string day = BankingSource("EconomyDirector.Day.cs"), director = BankingSource("EconomyDirector.cs");
        string hour = MemberText(day, "private void OnHour("), loanEvent = MemberText(day, "private void OnGameEvent("), settle = MemberText(day, "private void Settle(");
        string slow = MemberText(director, "private void Slow("), save = MemberText(director, "private void OnSave(");
        bool Ahead(string body, string first, string then) => body.IndexOf(first, StringComparison.Ordinal) >= 0 && body.IndexOf(first, StringComparison.Ordinal) < body.IndexOf(then, StringComparison.Ordinal);
        Check(Ahead(hour, "KeepLoanRates();", "DeskDo(\"the hour\", _deskKeep);") && Ahead(loanEvent, "KeepLoanRates();", "SyncLoans(Save.Day);")
              && Ahead(settle, "KeepLoanRates();", "GameLoans.Read(_live);") && Ahead(slow, "KeepLoanRates();", "DeskDo(\"the timer\", _deskSlow);")
              && Ahead(save, "KeepLoanRates();", "DeskDo(\"the save\", _deskSave);") && MemberText(director, "private void StartUp(").Contains("_multiplier = GameLoans.Multiplier;"),
            "banking fix: the multiplier is compared before anything takes a loan's figures as its own: the second, the hour, a loan paid off, the day and the save");
        Check(MemberText(day, "private void KeepLoanRates(").Contains("LoanHolds.Repriced(m, ref interest, ref payment, was, now, years, AccountRules.DaysPerYear, holds);"),
            "banking fix: every loan of the file is priced again by the rule");
    }

    // 9: a bank app knows a picked loan by the loan.
    private static void BankingFixRowId()
    {
        string loans = MemberText(BankingSource(@"BankApps\BankApp.Loans.cs"), "private string WriteLoans(");
        Check(loans.Contains("Row(FinanceApp.LoanRowId(m))") && !loans.Contains("IndexOf(m)") && MemberText(EconomySource(@"Finance\FinanceApp.Accounts.cs"), "internal static string LoanRowId(").Contains("m.FirstDay"),
            "banking fix: a bank app's loan row is known by the loan, as Finance's is, not its place in the list");
    }

    // 10: the fee and the new day are named in a question, and they are what the extension then does.
    private static void BankingFixLender()
    {
        foreach (int today in new[] { 14, 25 })
        {
            var debt = new SharkDebt();
            SharkRules.Lend(debt, 1000, 10);
            SharkRules.Grow(debt, today);
            int fee = SharkRules.ExtendFee(debt), day = SharkRules.ExtendedDay(debt, today), owed = debt.Owed + fee;
            bool done = SharkRules.Extend(debt, today);
            Check(done && fee > 0 && debt.Owed == owed && debt.DueDay == day && day == Math.Max(20, today) + ProductCatalogue.SharkDueDays,
                "banking fix: the extension asked for on day " + today + " costs the fee named (" + fee + ") and moves the day to the one named (" + day + ")");
        }
        string phone = BankingSource(@"Debt\DebtPhone.cs"), ask = MemberText(phone, "private static DialogEntry AskExtend(");
        Check(MemberText(phone, "private static DialogEntry Status(").Contains("entry.OnSecondOption = () => DebtPhone.Safe(AskExtend);")
              && ask.Contains("\"reallife_lender_extend_terms\".Localize(") && ask.Contains("SharkRules.ExtendFee(debt)") && ask.Contains("SharkRules.ExtendedDay(debt, today)")
              && ask.Contains("\"reallife_lender_extend_agree\".Localize(new { fee = DebtPhone.Money(fee) })") && ask.Contains("OnConfirm = () => DebtPhone.Safe(Extend)"),
            "banking fix: asking the private lender for more time names its fee and day in a question before it is agreed");
    }

    // The interest multiplier and the loan-paid event are written once, in GameLoans.
    private static void BankingFixOneDefinition()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), @"..\Scripts"));
        var multiplier = new List<string>();
        var paidEvent = new List<string>();
        foreach (string path in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string text = System.IO.File.ReadAllText(path), name = System.IO.Path.GetFileName(path);
            if (text.Contains("bankInterestMultiplier"))
            {
                multiplier.Add(name);
            }
            if (text.Contains("\"ba:gameevent_loanpaid\""))
            {
                paidEvent.Add(name);
            }
        }
        Check(multiplier.Count == 1 && multiplier[0] == "GameLoans.cs" && paidEvent.Count == 1 && paidEvent[0] == "GameLoans.cs",
            "banking fix: the game's interest multiplier and its loan-paid event are read in GameLoans only (" + string.Join(", ", multiplier) + "; " + string.Join(", ", paidEvent) + ")");
    }

    private static void BankingFixTexts()
    {
        string folder = LocalesFolder();
        var languages = new List<(string code, Dictionary<string, string> words)>();
        foreach (string code in new[] { "en", "pt", "zh-cn", "de", "fr", "es", "ru" })
        {
            languages.Add((code, LoadWords(System.IO.Path.Combine(folder, code + ".json"))));
        }
        if (languages.Any(l => l.words == null))
        {
            Check(false, "banking fixes: the texts are at " + folder);
            return;
        }
        var missing = new List<string>();
        foreach ((string key, string[] names) in new[] { ("reallife_lender_extend_terms", new[] { "{fee}", "{amount}", "{day}" }), ("reallife_lender_extend_agree", new[] { "{fee}" }) })
        {
            for (int l = 0; l < 3; l++)
            {
                if (!languages[l].words.TryGetValue(key, out string text) || Array.Exists(names, n => text.IndexOf(n, StringComparison.Ordinal) < 0))
                {
                    missing.Add(languages[l].code + ":" + key);
                }
            }
        }
        Check(missing.Count == 0, "banking fixes: the private lender's question is in en, pt and zh-cn with its names" + (missing.Count > 0 ? ": " + string.Join(", ", missing) : string.Empty));
        // The Guide's car finance line names the approval's interest in every language.
        string[] interest = { "costs interest", "juros", "利息", "Zinsen", "intérêts", "intereses", "процент" };
        var silent = new List<string>();
        for (int l = 0; l < languages.Count; l++)
        {
            string guide = languages[l].words.TryGetValue("reallife_guide_economy", out string g) ? g : string.Empty;
            string car = guide.Split('\n').FirstOrDefault(line => line.Contains("{econ_approval_days}")) ?? string.Empty;
            if (car.IndexOf(interest[l], StringComparison.Ordinal) < 0)
            {
                silent.Add(languages[l].code);
            }
        }
        Check(silent.Count == 0, "banking fixes: the Guide says what holding an approval costs, in every language" + (silent.Count > 0 ? ": not in " + string.Join(", ", silent) : string.Empty));
        Dictionary<string, string> en = languages[0].words;
        Check(en["reallife_guide_economy"].Contains("only on the part the lender finances") && en["reallife_econ_confirm_car"].Contains("only on the part the lender finances")
              && en["reallife_econ_confirm_home"].Contains("only on the part the lender finances") && en["reallife_econ_approval_charge"].Contains("only the part financed is charged"),
            "banking fixes: the Guide, the approval's question and its page say a purchase is charged on the financed part only");
        // The fixes stay in the next row until a version numbers them, so every What's new row is read.
        string rows = string.Join(" ", en.Where(kv => kv.Key.StartsWith("reallife_changes_", StringComparison.Ordinal)).Select(kv => kv.Value));
        Check(rows.Contains("Fixed: buying on finance charges the approval's interest only on the part the lender finances")
              && rows.Contains("Fixed: a card's minimum paid before its due day counts as a payment on time") && rows.Contains("Fixed: changing the game's interest multiplier keeps each loan")
              && rows.Contains("Fixed: switching Investors off no longer stops the deals you have"),
            "banking fixes: What's new names the banking fixes and the investors' deals that carry on");
    }
}
