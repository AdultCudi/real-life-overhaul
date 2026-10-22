using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Designs
{
    /// <summary>
    /// The feature's entry in the mod's feature list, and the figures its Guide page prints from <see cref="DesignCatalogue"/>.
    /// Called once, when the feature list is built (Meta/FeatureList); each figure is worked out when the page is opened.
    /// </summary>
    public static class DesignsGuide
    {
        public static void Register()
        {
            Features.Add("designs", "reallife_meta_title_designs")
                .Group("reallife_designs_header")
                .About("reallife_guide_designs_about").Help("reallife_guide_designs")
                .Owns(Settings.DesignsId)
                .Test("reallife_designs_test_say", "reallife_designs_test_install")
                .Fact("designs_showroom_most", () => DesignCatalogue.ShowroomMost.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>The ready-made designs' place on the options page, under the mod's own namespace so that it is one line there.</summary>
    public static class DesignsOptions
    {
        /// <summary>The "Ready-made designs" block of the options page: its switch and its tests.</summary>
        public static ModOptions AddDesigns(this ModOptions options)
        {
            return options
                .AddHeader("reallife_designs_header")
                .AddToggle(Settings.DesignsId, "reallife_designs_enabled", Settings.Designs, v => { Settings.Designs = v; DesignsDirector.Ask(v); })
                .AddButton("reallife_designs_test_say", DesignsDirector.TestSay)
                .AddButton("reallife_designs_test_install", DesignsDirector.TestInstall)
                .AddSplitter();
        }
    }
}
