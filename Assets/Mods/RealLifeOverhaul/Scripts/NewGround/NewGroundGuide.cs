using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.NewGround
{
    /// <summary>
    /// New ground's entry in the feature list: its developer option (shown with the test tools) and its tests. Players
    /// see nothing of it yet: the district that will lay new ground with it is not built, so it has no Guide page.
    /// </summary>
    [DescribesFeature]
    public static class NewGroundGuide
    {
        private static void DescribeFeature()
        {
            Features.Add("newground", "reallife_meta_title_newground")
                .About("reallife_guide_newground_about")
                .Row(new ToggleOption(Settings.NewGroundTestRoadId, "reallife_newground_trial_road", false, on => Settings.NewGroundTestRoad = on))
                .Owns(Settings.NewGroundTestRoadId)
                .Present(Settings.NewGroundTestRoadId, () => Settings.ShowTestTools)
                .NeedsReload(Settings.NewGroundTestRoadId)
                .Test("reallife_newground_test_under", GroundDirector.TestUnder)
                .Test("reallife_newground_test_patch", GroundDirector.TestPatch)
                .Test("reallife_newground_test_grow", GroundDirector.TestGrow)
                .Test("reallife_newground_test_link", GroundDirector.TestLink)
                .Test("reallife_newground_test_census", GroundDirector.TestCensus)
                .Test("reallife_newground_test_road", GroundDirector.TestRoad)
                .Test("reallife_newground_test_parking", GroundDirector.TestParking)
                .Objects(RoadsGame.LampsName, ParkingGame.HolderName, PeopleGame.ZoneName, PeopleGame.CrossingName, ParkingGame.CopyName)
                .Cleanup(() => GroundDirector.Instance?.GiveBackAll("the options page's cleanup"));
        }
    }
}
