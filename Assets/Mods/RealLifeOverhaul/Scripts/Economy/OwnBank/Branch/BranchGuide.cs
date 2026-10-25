using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The bank branches' feature entry, and their Guide page's figures, filled from the catalogue when the page opens.</summary>
    public static class BranchGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("economy_bank_branch", "reallife_meta_title_economy_bank_branch")
                .About("reallife_guide_economy_bank_branch_about").Help("reallife_guide_economy_bank_branch")
                .Owns(Settings.EconomyBranchId)
                .Test("reallife_branch_test_hour", "reallife_branch_test_floor")
                .Fact("branch_serve", () => Whole(BranchCatalogue.ServePerTeller))
                .Fact("branch_skill_least", () => OwnBankText.Ratio(BranchCatalogue.SkillLeast))
                .Fact("branch_skill_most", () => OwnBankText.Ratio(BranchCatalogue.SkillMost))
                .Fact("branch_open_fee", () => Money(OwnBankRules.At(BranchCatalogue.OpenFeeCents, Prices.Level)))
                .Fact("branch_counter_fee", () => Money(OwnBankRules.At(BranchCatalogue.CounterFeeCents, Prices.Level)))
                .Fact("branch_loan_fee", () => OwnBankText.Ratio(BranchCatalogue.LoanFee))
                .Fact("branch_loan_fee_least", () => Money(OwnBankRules.At(BranchCatalogue.LoanFeeLeastCents, Prices.Level)))
                .Fact("branch_loan", () => Money(OwnBankRules.At(BranchCatalogue.KindCents[BranchCatalogue.Loan], Prices.Level)))
                .Fact("branch_reach_most", () => OwnBankText.Ratio(BranchCatalogue.ReachMost))
                .Fact("branch_reach_half", () => Whole(BranchCatalogue.ReachHalf))
                .Fact("branch_firms", () => OwnBankText.Ratio(BranchCatalogue.FirmsReach))
                .Fact("branch_designs_most", () => Whole(Designs.DesignCatalogue.BranchMost))
                .Fact("branch_shown", () => Whole(BranchCatalogue.ShownMost))
                .Fact("branch_at_once", () => Whole(BranchCatalogue.OnFloorMost));
        }

        private static string Money(long cents)
        {
            return Guide.Money(cents / 100f);
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }
    }
}
