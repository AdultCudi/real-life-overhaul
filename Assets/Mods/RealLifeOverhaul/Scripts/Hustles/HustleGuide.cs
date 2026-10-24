using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>The Marketplace's entry in the feature list; its Guide page's figures come from the catalogue.</summary>
    public static class HustleGuide
    {
        public static void Register()
        {
            Features.Add("hustles", "reallife_meta_title_hustles")
                .Group("reallife_hustle_header")
                .About("reallife_guide_hustles_about").Help("reallife_guide_hustles")
                .Owns(Settings.HustlesId)
                .Test("reallife_hustle_test_collect", "reallife_hustle_test_car", "reallife_hustle_test_fix", "reallife_hustle_test_buyer", "reallife_hustle_test_gig",
                    "reallife_hustle_test_helper")
                .Data(HustleStore.Key)
                .Fact("hustle_goods", () => Guide.Number(HustleCatalogue.GoodsADay))
                .Fact("hustle_cars", () => Guide.Number(HustleCatalogue.CarsADay))
                .Fact("hustle_haggles", () => Guide.Number(HustleCatalogue.HagglesMost))
                .Fact("hustle_shop", () => HustleCatalogue.ShopCondition.ToString(CultureInfo.InvariantCulture))
                .Fact("hustle_window", () => Guide.Number(HustleCatalogue.GigWindow))
                .Fact("hustle_helper", () => (HustleCatalogue.HelperShare / 10).ToString(CultureInfo.InvariantCulture))
                .Fact("hustle_offer_hours", () => Guide.Number(HustleCatalogue.OfferHours))
                .Cleanup(() => HustleDirector.Instance?.StopAll())
                .ResetData(() => HustleDirector.Instance?.Forget());   // the director opens the feature afresh from the (now empty) save on its next second
        }
    }

    /// <summary>The Marketplace's place on the options page.</summary>
    public static class HustleOptions
    {
        public static ModOptions AddHustles(this ModOptions options)
        {
            return options
                .AddHeader("reallife_hustle_header")
                .AddToggle(Settings.HustlesId, "reallife_hustle_enabled", Settings.Hustles,
                    v => { Settings.Hustles = v; if (!v) { HustleDirector.Instance?.StopAll(); } })
                .AddButton("reallife_hustle_test_collect", () => HustleDirector.Instance?.TestCollect())
                .AddButton("reallife_hustle_test_car", () => HustleDirector.Instance?.TestCar())
                .AddButton("reallife_hustle_test_fix", () => HustleDirector.Instance?.TestFix())
                .AddButton("reallife_hustle_test_buyer", () => HustleDirector.Instance?.TestBuyer())
                .AddButton("reallife_hustle_test_gig", () => HustleDirector.Instance?.TestGig())
                .AddButton("reallife_hustle_test_helper", () => HustleDirector.Instance?.TestHelper())
                .AddSplitter();
        }
    }
}
