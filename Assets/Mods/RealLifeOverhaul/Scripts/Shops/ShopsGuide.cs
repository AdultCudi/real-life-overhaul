using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Shops
{
    /// <summary>The new kinds of shop's entry in the feature list, its keys written out whole as the options checks read them.</summary>
    public static class ShopsGuide
    {
        public static void Register()
        {
            Features.Add("newshops", "reallife_meta_title_shops")
                .Group("reallife_shops_header")
                .About("reallife_guide_shops_about").Help("reallife_guide_shops")
                .Owns(Settings.NewShopsId)
                .Fact("shops_kinds", () => ShopsCatalogue.Kinds.Length.ToString(CultureInfo.InvariantCulture))
                .Test("reallife_shops_test_say", "reallife_shops_test_today", "reallife_shops_test_help");
        }
    }

    /// <summary>The new kinds of shop's place on the options page.</summary>
    public static class ShopsOptions
    {
        public static ModOptions AddShops(this ModOptions options)
        {
            return options
                .AddHeader("reallife_shops_header")
                .AddToggle(Settings.NewShopsId, "reallife_shops_enabled", Settings.NewShops, ShopsDirector.Switch)
                .AddButton("reallife_shops_test_say", ShopsDirector.TestSay)
                .AddButton("reallife_shops_test_today", ShopsDirector.TestToday)
                .AddButton("reallife_shops_test_help", ShopsDirector.TestHelp)
                .AddSplitter();
        }
    }
}
