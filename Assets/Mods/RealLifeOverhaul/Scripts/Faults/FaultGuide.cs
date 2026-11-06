using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Faults
{
    /// <summary>
    /// The city faults' entry in the feature list, and the Guide page's figures, each filled from
    /// <see cref="FaultRules"/> when the page opens. Registered once, when the feature list is built.
    /// </summary>
    public static class FaultGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("faults", "reallife_meta_title_faults")
                .Group("reallife_faults_header")
                .About("reallife_guide_faults_about").Help("reallife_guide_faults")
                .Fact("faults_litter_days_cut", () => FaultRules.LitterDaysCut.ToString(Inv))
                .Owns(Settings.CityFaultsId, Settings.CityFaultsCountId, Settings.CityFaultsFalseCostId)
                .Preset(Settings.CityFaultsCountId, 0, 2, 2)
                .Data(FaultStore.Key)
                .Test("reallife_faults_test_near", "reallife_faults_test_reports", "reallife_faults_test_mend")
                .Fact("faults_few", () => FaultRules.Most[0].ToString(Inv))
                .Fact("faults_some", () => FaultRules.Most[1].ToString(Inv))
                .Fact("faults_many", () => FaultRules.Most[2].ToString(Inv))
                .Fact("faults_match", () => ((int)FaultRules.MatchMetres).ToString(Inv))
                .Fact("faults_warn_at", () => FaultRules.WarnAt.ToString(Inv))
                .Fact("faults_bar_at", () => FaultRules.BarAt.ToString(Inv))
                .Fact("faults_bar_days", () => FaultRules.BarDays.ToString(Inv))
                .Fact("faults_strike_days", () => FaultRules.StrikeDays.ToString(Inv))
                .Fact("faults_reputation", () => Flare.FlareLiveRules.FalseReportCost.ToString(Inv))
                .Fact("faults_hotspot_days", () => FaultRules.HotspotDays.ToString(Inv))
                .Fact("faults_litter_days", () => FaultRules.LitterDays.ToString(Inv))
                .Fact("faults_litter_percent", () => (-FaultRules.LitterPercent).ToString(Inv))
                .Fact("faults_litter_most", () => (-FaultRules.LitterMostPercent).ToString(Inv))
                .Fact("faults_dark_crime", () => ((int)System.Math.Round((FaultRules.DarkCrime - 1f) * 100f)).ToString(Inv))
                .Fact("faults_slow_kmh", () => FaultRules.PotholeSlowKmh.ToString(Inv))
                .Cleanup(() => FaultDirector.Instance?.StopAll())
                .ResetData(() => FaultDirector.Instance?.Forget());
        }
    }

    /// <summary>The city's faults' place on the options page, under the mod's own namespace so that it is one line there.</summary>
    public static class FaultOptions
    {
        /// <summary>The "City faults" block of the options page: its switch, how many, the cost of false reports, and the tests.</summary>
        public static ModOptions AddCityFaults(this ModOptions options)
        {
            return options
                .AddHeader("reallife_faults_header")
                .AddToggle(Settings.CityFaultsId, "reallife_faults_enabled", Settings.CityFaults,
                    v => Settings.CityFaults = v)
                .AddDropdown(Settings.CityFaultsCountId, "reallife_faults_opt_count",
                    new[] { "reallife_faults_count_0", "reallife_faults_count_1", "reallife_faults_count_2" }, Settings.CityFaultsCount,
                    v => Settings.CityFaultsCount = v)
                .AddToggle(Settings.CityFaultsFalseCostId, "reallife_faults_opt_false_cost", Settings.CityFaultsFalseCost,
                    v => Settings.CityFaultsFalseCost = v)
                .AddButton("reallife_faults_test_near", () => FaultDirector.Instance?.TestNear())
                .AddButton("reallife_faults_test_reports", () => FaultDirector.Instance?.TestReports())
                .AddButton("reallife_faults_test_mend", () => FaultDirector.Instance?.TestMend())
                .AddSplitter();
        }
    }
}
