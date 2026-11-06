using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The figures of the Guide page "Managers", from the catalogue, as the page opens.</summary>
    public static class ManagerGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void AddFacts(Feature f)
        {
            f.Fact("mgr_mistake_most", () => CompanyText.Share(ManagerCatalogue.MistakeMost))
                .Fact("mgr_review_cut", () => CompanyText.Share(ManagerCatalogue.ReviewCut))
                .Fact("mgr_far", () => Whole(ManagerCatalogue.RegionalFar))
                .Fact("mgr_allowance_place", () => CompanyText.Money(ManagerCatalogue.AllowanceBase[ManagerCatalogue.RolePlace]))
                .Fact("mgr_allowance_place_skill", () => CompanyText.Money(ManagerCatalogue.AllowancePerSkill[ManagerCatalogue.RolePlace]))
                .Fact("mgr_allowance_region", () => CompanyText.Money(ManagerCatalogue.AllowanceBase[ManagerCatalogue.RoleRegion]))
                .Fact("mgr_allowance_region_skill", () => CompanyText.Money(ManagerCatalogue.AllowancePerSkill[ManagerCatalogue.RoleRegion]))
                .Fact("mgr_price_hour", () => ManagerCatalogue.PriceHour.ToString("00", Inv))
                .Fact("mgr_staff_hour", () => ManagerCatalogue.StaffHour.ToString("00", Inv))
                .Fact("mgr_hire_hour", () => ManagerCatalogue.HireHour.ToString("00", Inv))
                .Fact("mgr_stock_hour", () => ManagerCatalogue.StockHour.ToString("00", Inv))
                .Fact("mgr_rota_hour", () => ManagerCatalogue.RotaHour.ToString("00", Inv))
                .Fact("mgr_digest_hour", () => ManagerCatalogue.DigestHour.ToString("00", Inv))
                .Fact("mgr_undo_days", () => Whole(ManagerCatalogue.UndoDays))
                .Fact("mgr_hire_undo_days", () => Whole(ManagerCatalogue.HireUndoDays))
                .Fact("mgr_hire_hours", () => Whole(ManagerCatalogue.HireWeekHours))
                .Fact("mgr_slip_most", () => CompanyText.Share(ManagerCatalogue.StockSlipMost))
                .Fact("mgr_learn_hours", () => Whole(ManagerCatalogue.LearnHours))
                .Fact("mgr_kept", () => Whole(ManagerCatalogue.DecisionsKept))
                .Fact("mgr_items", () => Whole(ManagerCatalogue.ItemsInDecision))
                .Fact("mgr_price_step", () => CompanyText.Share(ManagerCatalogue.PriceStep))
                .Fact("mgr_stock_step", () => CompanyText.Share(ManagerCatalogue.StockStep))
                .Fact("mgr_budget_near", () => CompanyText.Share(ManagerDirectoryRules.BudgetNearShare))
                .Fact("mgr_most", () => Whole(ManagerCatalogue.ManagersMost))
                .Fact("mgr_band_least", () => CompanyText.Share(ManagerCatalogue.WageBands[0]))
                .Fact("mgr_band_most", () => CompanyText.Share(ManagerCatalogue.WageBands[ManagerCatalogue.WageBands.Length - 1]))
                .Fact("mgr_band_default", () => CompanyText.Share(ManagerCatalogue.WageBands[ManagerCatalogue.WageBandDefault]))
                .Fact("mgr_budget_most", () => CompanyText.Money(ManagerCatalogue.Budgets[ManagerCatalogue.Budgets.Length - 1]))
                .Fact("mgr_budget_default", () => CompanyText.Money(ManagerCatalogue.Budgets[ManagerCatalogue.BudgetDefault]))
                .Fact("mgr_keep_most", () => Whole(ManagerCatalogue.KeepMost))
                .Fact("mgr_stock_default", () => Whole(ManagerCatalogue.StockDays[ManagerCatalogue.StockDaysDefault]))
                .Fact("mgr_price_low_default", () => CompanyText.Share(ManagerCatalogue.PriceLows[ManagerCatalogue.PriceLowDefault]))
                .Fact("mgr_price_high_default", () => CompanyText.Share(ManagerCatalogue.PriceHighs[ManagerCatalogue.PriceHighDefault]));
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }
    }
}
