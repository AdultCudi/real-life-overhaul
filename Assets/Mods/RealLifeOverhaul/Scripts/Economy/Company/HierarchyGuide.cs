using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The figures of the Guide page "Company hierarchy", from the policy's catalogue, as the page opens.</summary>
    public static class HierarchyGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void AddFacts(Feature f)
        {
            f.Fact("hier_gap_days", () => Start(PolicyField.GapDays))
                .Fact("hier_budget_share", () => CompanyText.Share(StartValue(PolicyField.BudgetShare)))
                .Fact("hier_unmet_days", () => Start(PolicyField.UnmetDays))
                .Fact("hier_mood_floor", () => Start(PolicyField.MoodFloor))
                .Fact("hier_mood_days", () => Whole(PolicyCatalogue.MoodDays))
                .Fact("hier_wave_count", () => Start(PolicyField.WaveCount))
                .Fact("hier_wave_days", () => Whole(PolicyCatalogue.WaveWindow))
                .Fact("hier_kpi_days", () => Start(PolicyField.KpiDays))
                .Fact("hier_tolerance", () => CompanyText.Share(StartValue(PolicyField.Tolerance)))
                .Fact("hier_templates_most", () => Whole(PolicyCatalogue.TemplatesMost))
                .Fact("hier_regions_most", () => Whole(HierarchyRules.CustomMost))
                .Fact("hier_replace_score", () => Whole(RegionCatalogue.ReplaceScore))
                .Fact("hier_exec_premium", () => CompanyText.Share(HierarchyRules.ExecutivePremium))
                .Fact("hier_staff_hour", () => ManagerCatalogue.StaffHour.ToString("00", Inv));
        }

        // A rule's starting value, as a whole number.
        private static string Start(PolicyField field)
        {
            return Whole(StartValue(field));
        }

        private static int StartValue(PolicyField field)
        {
            return PolicyCatalogue.Value(field, PolicyCatalogue.Def(field).Default);
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }
    }
}
