using RealLifeOverhaul.Economy;

// The Finance app fixed, each reproduced here:
//   1. an approval's interest was taken with no word of it before, and cancelling took it with no question
//      (the warning is here; the charge itself is the credit products', changed with them);
//   2. net worth counted an approval's money, which goes back, as the player's own;
//   3. what leaves tomorrow had the game's rent without the city's change to it (CityFixChecks.cs);
//   4. a bank loan's amounts offered 500, under the least a bank lends once prices have risen;
//   5. a game loan picked in Accounts was known by its place in the list, so another could show as picked;
//   6. during a payment plan, "the next miss brings" named the step of the count, not the one a broken plan takes;
//   7. a test account's next payment was in the table's total;
//   8. a Standing notification could open the page on the row picked last time;
//   9. the Overview read the game's wealth without the fallback net worth has.
internal static partial class Program
{
    private static readonly string[] FinanceFixSteps =
    {
        "reallife_tab_step_reminder", "reallife_tab_step_fee", "reallife_tab_step_penalty", "reallife_tab_step_collections", "reallife_tab_step_repossession", "reallife_tab_step_claim"
    };

    private static void FinanceFixes()
    {
        FinanceFixNextStep();
        FinanceFixSources();
    }

    private static LadderState FinanceFixPlan()
    {
        return new LadderState { PlanLeft = 3000, PlanPerDay = 300, PlanUsed = true, Overdue = 3000, Step = (int)LadderStep.Collections };
    }

    private static void FinanceFixNextStep()
    {
        bool same = true;
        string seen = string.Empty;
        for (int level = ProductCatalogue.Relaxed; level <= ProductCatalogue.Hardcore; level++)
        {
            foreach (bool secured in new[] { true, false })
            {
                LadderState s = FinanceFixPlan(), copy = FinanceFixPlan();
                LadderStep expect = LadderRules.Missed(copy, level, secured, copy.Overdue);
                bool has = FinanceTables.NextStep(s, level, secured, out string key, out int away);
                bool right = expect != LadderStep.None ? has && away == 1 && key == FinanceFixSteps[(int)expect - 1] : !has || away > 1;
                same &= right && s.PlanLeft == 3000 && s.Misses == 0;
                seen += right ? string.Empty : " level " + level + (secured ? " secured" : string.Empty) + ": " + key + " in " + away;
            }
        }
        Check(same, "finance fix: during a payment plan the next miss names the step a broken plan takes, and the plan is untouched" + seen);
        FinanceTables.NextStep(FinanceFixPlan(), ProductCatalogue.Standard, true, out string car, out int carAway);
        Check(car == "reallife_tab_step_repossession" && carAway == 1, "finance fix: car finance in a plan: the next miss takes the car (" + car + " in " + carAway + ")");
        bool relaxed = FinanceTables.NextStep(FinanceFixPlan(), ProductCatalogue.Relaxed, false, out string after, out int afterAway);
        Check(!relaxed || afterAway > 1, "finance fix: where a broken plan takes no step, the next one named is a miss further on");
        Check(FinanceTables.NextStep(new LadderState(), ProductCatalogue.Standard, false, out string first, out int firstAway) && first == "reallife_tab_step_reminder" && firstAway == 1,
            "finance fix: with no plan the count's next step is named as before");
    }

    private static void FinanceFixSources()
    {
        string books = EconomySource(@"Finance\Books.cs"), overview = EconomySource(@"Finance\FinanceApp.Overview.cs");
        string worth = MemberText(books, "public static long NetWorth(");
        Check(worth.Contains("file.Relief.HasApproval ? file.Relief.ApprovedAmount") && worth.Contains("Wealth(out PersonalWealthData wealth)"),
            "finance fix: net worth leaves out an approval's money, which goes back to the lender");
        Check(overview.Contains("FinanceText.Money(Books.NetWorth())") && overview.Contains("Books.Wealth(out PersonalWealthData wealth)") && !overview.Contains("PlayerHelper.GetPersonalWealth()")
              && MemberText(books, "public static bool Wealth(").Contains("catch (Exception)"),
            "finance fix: the Overview shows net worth as the market and the rich list read it, and the game's wealth with its fallback");
        Check(MemberText(EconomySource(@"Finance\FinanceApp.Borrow.cs"), "private void Amounts(").Contains("AmountSteps[i] >= CreditCatalogue.LoanLeast"),
            "finance fix: a bank loan's amounts start at the least a bank lends at today's prices");
        string accounts = EconomySource(@"Finance\FinanceApp.Accounts.cs");
        Check(accounts.Contains("Row(LoanRowId(m))") && !accounts.Contains("Row(\"loan:\" + i)") && MemberText(accounts, "internal static string LoanRowId(").Contains("m.FirstDay"),
            "finance fix: a game loan's row is known by the loan, not its place in the list");
        Check(accounts.Contains("PaymentCells(row, next, day, today, !a.Test)") && MemberText(accounts, "private static void PaymentCells(").Contains("inTotal ? amount : double.NaN"),
            "finance fix: a test account's next payment is in no total");
        Check(MemberText(EconomySource(@"Finance\FinanceApp.cs"), "public void Opened(").Contains("_standingPick = _standing ? null : _standingPick"),
            "finance fix: a Standing notification opens the page with nothing picked");
        string products = EconomySource(@"Finance\FinanceApp.Products.cs");
        string finance = MemberText(products, "private void WriteFinance("), approval = MemberText(products, "internal static void WriteApproval(");   // the page Finance and the bank apps share
        Check(finance.Contains("WriteApproval(box, desk, relief, After);") && approval.Contains("\"reallife_econ_approval_charge\"") && approval.Contains("\"reallife_econ_confirm_cancel_approval\".Localize(")
              && finance.Contains("per_day = FinanceText.Money(ProductTerms.AdvanceCharge(")
              && approval.IndexOf("desk.CancelApproval(false)", System.StringComparison.Ordinal) > approval.IndexOf("GameConfirm.Show(\"reallife_econ_approval_cancel\"", System.StringComparison.Ordinal),
            "finance fix: an approval's interest is named before it is approved, on its page, and in a question before a cancel takes it");
    }
}
