using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Walking
{
    /// <summary>
    /// The feature's entry in the mod's feature list and its place on the options page. Its Guide page names the key
    /// through the key's own fact ({key_walk}, filled by the Guide from Core/Keys), so the page always says the key the
    /// player has. Called once, when the feature list and the options page are built.
    /// </summary>
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

    /// <summary>Walking's place on the options page, under the mod's own namespace so that it is one line there.</summary>
    public static class WalkOptions
    {
        /// <summary>The "Walking" block of the options page: its switch and its test. Its key is under Controls (Core/Keys).</summary>
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
