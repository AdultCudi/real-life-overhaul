using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Designs
{
    /// <summary>The designs' entry in the feature list, with the figure its Guide page prints from <see cref="DesignCatalogue"/>.</summary>
    public static class DesignsGuide
    {
        public static void Register()
        {
            Features.Add("designs", "reallife_meta_title_designs")
                .Group("reallife_designs_header")
                .About("reallife_guide_designs_about").Help("reallife_guide_designs")
                .Fact("designs_shop_most", () => DesignCatalogue.ShopMost.ToString(CultureInfo.InvariantCulture))
                .Fact("designs_branch_most", () => DesignCatalogue.BranchMost.ToString(CultureInfo.InvariantCulture))
                .Owns(Settings.DesignsId)
                .Test("reallife_designs_test_say", "reallife_designs_test_install")
                .Fact("designs_showroom_most", () => DesignCatalogue.ShowroomMost.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>The designs' place on the options page.</summary>
    public static class DesignsOptions
    {
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
