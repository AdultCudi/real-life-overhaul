using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Landmarks
{
    /// <summary>
    /// The landmarks' entry in the feature list, its keys written out whole as the options checks read them: its option,
    /// its lines in the save, its Guide page's figures, its test entries and its clean-up. It registers itself
    /// (<see cref="DescribesFeatureAttribute"/>). Each test works when pressed or says why not in a notice.
    /// </summary>
    [DescribesFeature]
    public static class LandmarksGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static void DescribeFeature()
        {
            Features.Add("landmarks", "reallife_meta_title_landmarks")
                .About("reallife_guide_landmarks_about").Help("reallife_guide_landmarks")
                .Row(new ToggleOption(Settings.LandmarksId, "reallife_lmk_enabled", true, on => Settings.Landmarks = on))
                .Owns(Settings.LandmarksId)
                .Data(LandmarkStore.Key, LandmarkStore.TestKey)
                .ResetData(LandmarksDirector.Forget)
                .Objects("RealLifeOverhaul Landmark tower", "RealLifeOverhaul Landmark mall", "RealLifeOverhaul Landmark stadium")
                .Fact("lmk_tower_jobs", () => LandmarkRules.JobsOf(LandmarkKind.Tower).ToString(Inv))
                .Fact("lmk_mall_jobs", () => LandmarkRules.JobsOf(LandmarkKind.Mall).ToString(Inv))
                .Fact("lmk_stadium_jobs", () => LandmarkRules.JobsOf(LandmarkKind.Stadium).ToString(Inv))
                .Fact("lmk_match_jobs", () => LandmarkRules.MatchJobs.ToString(Inv))
                .Fact("lmk_capacity", () => LandmarkRules.Capacity.ToString(Inv))
                .Fact("lmk_kick_off", () => LandmarkRules.KickOff.ToString("00", Inv) + ":00")
                .Fact("lmk_lift_cap", () => LandmarkRules.LiftCap.ToString(Inv))
                .Fact("lmk_match_lift", () => LandmarkRules.MatchLift.ToString(Inv))
                .Fact("lmk_tender_days", () => (Construction.BuildCatalogue.LandmarkTenderHours / 24).ToString(Inv))
                .Test("reallife_lmk_test_census", LandmarksDirector.TestCensus)
                .Test("reallife_lmk_test_announce", LandmarksDirector.TestAnnounce)
                .Test("reallife_lmk_test_award", LandmarksDirector.TestAward)
                .Test("reallife_lmk_test_tower", LandmarksDirector.TestTowerNow)
                .Test("reallife_lmk_test_mall", LandmarksDirector.TestMallNow)
                .Test("reallife_lmk_test_stadium", LandmarksDirector.TestStadiumNow)
                .Test("reallife_lmk_test_opening", LandmarksDirector.TestOpening)
                .Test("reallife_lmk_test_match", LandmarksDirector.TestMatch)
                .Test("reallife_lmk_test_take_stadium", LandmarksDirector.TestTakeStadium)
                .Test("reallife_lmk_test_take_tower", LandmarksDirector.TestTakeTower)
                .Test("reallife_lmk_test_take_mall", LandmarksDirector.TestTakeMall)
                .Test("reallife_lmk_test_back", LandmarksDirector.TestBack)
                .Cleanup(() => LandmarksDirector.Instance?.StandDown("its things were put away"));
        }
    }
}
