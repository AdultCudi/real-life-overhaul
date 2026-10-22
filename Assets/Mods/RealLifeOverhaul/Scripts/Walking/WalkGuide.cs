using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Walking
{
    /// <summary>Walking's entry in the feature list. Its Guide page names the player's own key through the fact {key_walk}.</summary>
    public static class WalkGuide
    {
        public static void Register()
        {
            Features.Add("walking", "reallife_meta_title_walking")
                .Group("reallife_walk_header")
                .About("reallife_guide_walking_about").Help("reallife_guide_walking")
                .Owns(Settings.WalkToggleId)
                .Test("reallife_walk_test_say")
                .Cleanup(() => WalkDirector.Instance?.Stop(true, "the options page's cleanup"));
        }
    }

    /// <summary>Walking's place on the options page; its key is under Controls.</summary>
    public static class WalkOptions
    {
        public static ModOptions AddWalking(this ModOptions options)
        {
            return options
                .AddHeader("reallife_walk_header")
                .AddToggle(Settings.WalkToggleId, "reallife_walk_toggle", Settings.WalkToggle,
                    v => { Settings.WalkToggle = v; if (!v) { WalkDirector.Instance?.Stop(true, "its option was switched off"); } })
                .AddButton("reallife_walk_test_say", () => WalkDirector.Instance?.TestSay())
                .AddSplitter();
        }
    }
}
