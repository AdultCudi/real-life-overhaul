using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// The mod's own buildings' entry in the feature list, its keys written out whole as the options checks read them. It
    /// registers itself (<see cref="DescribesFeatureAttribute"/>). No Guide page yet: a player sees nothing of it until a
    /// feature puts one of its places in a made building; the test building is a developer's.
    /// </summary>
    [DescribesFeature]
    public static class NewBuildingsGuide
    {
        private static void DescribeFeature()
        {
            Features.Add("newbuildings", "reallife_meta_title_newbuildings")
                .Group("reallife_newbuildings_header")
                .Owns(Settings.NewBuildingsId)
                .Test("reallife_newbuildings_test_make", "reallife_newbuildings_test_walk", "reallife_newbuildings_test_save", "reallife_newbuildings_test_copy",
                    "reallife_newbuildings_test_cost", "reallife_newbuildings_test_off")
                .Cleanup(() => NewBuildingsDirector.Instance?.StopAll("its feature's clean-up (the options page, or a city that is not New York)"));
        }
    }

    /// <summary>The mod's own buildings' place on the options page: the option, and the test entries (shown with the test tools).</summary>
    public static class NewBuildingsOptions
    {
        public static ModOptions AddNewBuildings(this ModOptions options)
        {
            return options
                .AddHeader("reallife_newbuildings_header")
                .AddToggle(Settings.NewBuildingsId, "reallife_newbuildings_enabled", Settings.NewBuildings,
                    v => Settings.NewBuildings = v)
                .AddButton("reallife_newbuildings_test_make", NewBuildingsDirector.TestMake)
                .AddButton("reallife_newbuildings_test_walk", () => NewBuildingsDirector.Instance?.TestWalk())
                .AddButton("reallife_newbuildings_test_save", () => NewBuildingsDirector.Instance?.TestSave())
                .AddButton("reallife_newbuildings_test_copy", () => NewBuildingsDirector.Instance?.TestCopy())
                .AddButton("reallife_newbuildings_test_cost", () => NewBuildingsDirector.Instance?.TestCost())
                .AddButton("reallife_newbuildings_test_off", () => NewBuildingsDirector.Instance?.TestOff())
                .AddSplitter();
        }
    }
}
