using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's entry in the feature list, its keys written out whole as the options checks read them: its option (read
    /// at the next load), its ground's three tests and its clean-up. It registers itself (<see cref="DescribesFeatureAttribute"/>);
    /// the district's other features add their own tests to the same entry.
    /// </summary>
    [DescribesFeature]
    public static class GroundGuide
    {
        private static void DescribeFeature()
        {
            Features.Add("uptown", "reallife_meta_title_uptown")
                .Row(new ToggleOption(Settings.UptownId, "reallife_uptown_ground_enabled", true, on => Settings.Uptown = on))
                .Owns(Settings.UptownId)
                .NeedsReload(Settings.UptownId)
                .Test("reallife_uptown_ground_test_census", GroundDirector.TestCensus)
                .Test("reallife_uptown_ground_test_path", GroundDirector.TestPath)
                .Test("reallife_uptown_ground_test_traffic", GroundDirector.TestTraffic)
                .Cleanup(() => GroundDirector.Instance?.Close("its feature's clean-up (the options page, faults in a row, the save tools or another city)"));
        }
    }
}
