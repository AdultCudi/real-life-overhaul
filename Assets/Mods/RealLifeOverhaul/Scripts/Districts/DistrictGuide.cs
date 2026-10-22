using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Districts
{
    /// <summary>
    /// The district view's entry in the feature list, and the Guide page's figures, each filled from
    /// <see cref="DistrictRules"/> when the page opens. Registered once, when the feature list is built.
    /// </summary>
    public static class DistrictGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("districtview", "reallife_meta_title_districtview")
                .Group("reallife_dv_header")
                .About("reallife_guide_districtview_about").Help("reallife_guide_districtview")
                .Owns(Settings.DistrictViewId)
                .Data(DistrictLog.Key)
                .Objects(DistrictPanel.ObjectName)
                .Test("reallife_dv_test_open", "reallife_dv_test_crime")
                .Fact("dv_crime_days", () => DistrictRules.CrimeDays.ToString(Inv))
                .Fact("dv_wealth_low", () => DistrictRules.WealthEdges[0].ToString(Inv))
                .Fact("dv_wealth_high", () => DistrictRules.WealthEdges[DistrictRules.WealthEdges.Length - 1].ToString(Inv))
                .Fact("dv_crime_many", () => DistrictRules.CrimeEdges[DistrictRules.CrimeEdges.Length - 1].ToString(Inv))
                .Fact("dv_standing_low", () => DistrictRules.StandingEdges[0].ToString(Inv))
                .Fact("dv_standing_high", () => DistrictRules.StandingEdges[DistrictRules.StandingEdges.Length - 1].ToString(Inv))
                .Fact("dv_bands", () => DistrictRules.Bands.ToString(Inv))
                .ResetData(() => DistrictView.Instance?.Forget());
        }
    }

    /// <summary>The district view's place on the options page, under the mod's own namespace so that it is one line there.</summary>
    public static class DistrictOptions
    {
        /// <summary>The "District view" block of the options page: its one switch and the test buttons.</summary>
        public static ModOptions AddDistrictView(this ModOptions options)
        {
            return options
                .AddHeader("reallife_dv_header")
                .AddToggle(Settings.DistrictViewId, "reallife_dv_enabled", Settings.DistrictView,
                    v => { Settings.DistrictView = v; MapPlaces.Refresh(); })
                .AddButton("reallife_dv_test_open", () => DistrictView.Instance?.TestOpen())
                .AddButton("reallife_dv_test_crime", () => DistrictView.Instance?.TestCrime())
                .AddSplitter();
        }
    }
}
