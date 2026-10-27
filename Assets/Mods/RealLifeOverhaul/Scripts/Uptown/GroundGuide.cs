using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's entry in the feature list, its keys written out whole as the options checks read them: its option (read
    /// at the next load), its ground's five tests (the map fog's in the city expansion's test run too) and its clean-up. It
    /// registers itself (<see cref="DescribesFeatureAttribute"/>); the district's other features add their own tests to the
    /// same entry.
    /// </summary>
    [DescribesFeature]
    public static class GroundGuide
    {
        private static void DescribeFeature()
        {
            Features.Add("uptown", "reallife_meta_title_uptown")
                .About("reallife_guide_uptown_about").Help("reallife_guide_uptown")
                .Row(new ToggleOption(Settings.UptownId, "reallife_uptown_ground_enabled", true, on => Settings.Uptown = on))
                .Owns(Settings.UptownId)
                .NeedsReload(Settings.UptownId)
                .Fact("uptown_classes", () => Classes(DistrictSplit.Uptown))
                .Fact("cedar_classes", () => Classes(DistrictSplit.CedarHeights))
                .Fact("uptown_big", () => DistrictSplit.LargeStoresOf(DistrictSplit.Uptown).ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Fact("cedar_big", () => DistrictSplit.LargeStoresOf(DistrictSplit.CedarHeights).ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Fact("uptown_height", () => Construction.BuildCatalogue.UptownHeightLimit.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Fact("uptown_grow_sites", () => Construction.GrowRules.SitesMost.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Test("reallife_uptown_ground_test_census", GroundDirector.TestCensus)
                .Test("reallife_uptown_ground_test_path", GroundDirector.TestPath)
                .Test("reallife_uptown_ground_test_traffic", GroundDirector.TestTraffic)
                .Check("reallife_uptown_ground_test_fog", GroundDirector.TestFog, "Uptown check (map fog)")
                .Check("reallife_uptown_ground_test_what", GroundDirector.TestWhatGround, "Ground check (what is this ground)")
                .Test("reallife_uptown_ground_test_surfaces", GroundDirector.TestSurfaces)
                .Cleanup(() => GroundDirector.Instance?.Close("its feature's clean-up (the options page, faults in a row, the save tools or another city)"));
        }

        /// <summary>A district's people by class, for its Guide page: "5% working class, 76% middle class and 19% upper class".</summary>
        private static string Classes(int district)
        {
            DistrictFigures f = DistrictSplit.Figures(district);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            return f.Working.ToString(inv) + "% working class, " + f.Middle.ToString(inv) + "% middle class and " + f.Upper.ToString(inv) + "% upper class";
        }
    }
}
