using System.Collections.Generic;
using Helpers;

namespace RealLifeOverhaul.Economy
{
    public static partial class BankTerms
    {
        /// <summary>Native bank terms currently in memory. Additional credit accounts are outside native eligibility.</summary>
        public static CompanionModule CompanionBankTerms()
        {
            if (Banks.Count == 0)
            {
                return new CompanionModule("unavailable");
            }
            var rows = new List<object>();
            foreach (Lender lender in Banks)
            {
                if (lender.Settings == null)
                {
                    continue;
                }
                rows.Add(CompanionSnapshot.Row("bankId", lender.Name,
                    "address", CompanionValues.Address(lender.Street, lender.Number),
                    "capCents", CompanionValues.Cents(lender.Settings.maxTotalLoanAmount),
                    "annualRatePercent", CompanionValues.Decimal(LoanHelper.CalculateEffectiveAnnualInterestRate(lender.Settings)),
                    "termDays", LoanHelper.CalculatePayBackDays(lender.Settings),
                    "debtBasis", "native-loans", "wealthBasis", "native-wealth-before-loans",
                    "profitBasis", "native-statement-daily-income", "incomeTermShareThousandths", 250,
                    "securedAccountRulesApply", Applied && Secured != null,
                    "securedCapTreatment", "included-in-effective-cap",
                    "tutorialMinimumCents", TutorialHelper.IsTutorialEnabled()
                        && !SaveGameManager.Current.CompletedQuestEntries.Contains(TutorialLoanObjective) ? 1550000 : 0,
                    "nativeDebtAtBankCents", CompanionValues.Cents(GameLoans.OwedTo(lender.Address))));
            }
            return new CompanionModule("ready", CompanionSnapshot.Row("model", "rlo-native-bank-terms-v1",
                "banks", rows, "additionalCreditIncluded", false, "principalIsExpense", false));
        }
    }
}
