using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Subway
{
    /// <summary>
    /// The districts' subway stations in the feature list, their keys written out whole as the options checks read them:
    /// the option (it acts at once, in play), the Guide page with its figures filled from the code, the four tests and the
    /// clean-up. It registers itself (<see cref="DescribesFeatureAttribute"/>). Nothing is saved, so there is no data to reset.
    /// </summary>
    [DescribesFeature]
    public static class StationsGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static void DescribeFeature()
        {
            Features.Add("uptown_subway", "reallife_meta_title_uptown_subway")
                .About("reallife_guide_uptown_subway_about").Help("reallife_guide_uptown_subway")
                .Row(new ToggleOption(Settings.UptownSubwayId, "reallife_uptown_subway_enabled", true, on => Settings.UptownSubway = on))
                .Owns(Settings.UptownSubwayId)
                .Objects(StationsDirector.RootName)
                .Fact("uptown_subway_stations", () => StationsRules.Sites.Count.ToString(Inv))
                .Fact("uptown_subway_fare", () => Guide.Money(SubwayStation.PricePerRide))
                .Check("reallife_uptown_subway_test_check", StationsDirector.TestStations, "Subway check (stations)")
                .Test("reallife_uptown_subway_test_go", StationsDirector.TestGo)
                .Test("reallife_uptown_subway_test_ride", StationsDirector.TestRide)
                .Test("reallife_uptown_subway_test_rider", StationsDirector.TestRider)
                .Cleanup(() => StationsDirector.Instance?.Close("its feature's clean-up (the options page, faults in a row, the save tools or another city)"));
        }
    }
}
