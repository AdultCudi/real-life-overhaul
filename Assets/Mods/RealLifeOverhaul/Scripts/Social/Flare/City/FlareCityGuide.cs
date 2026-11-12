using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare in the city's entry in the feature list (under Living population and Flare): its option, its save line, its four tests, its Guide page
    /// and the figures the page names, each {name} filled from the catalogue when the page opens.
    /// </summary>
    public static class FlareCityGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("flare_city", "reallife_meta_title_flare_city")
                .About("reallife_guide_flare_city_about").Help("reallife_guide_flare_city")
                .Owns(Settings.LivingFlareId)
                .Data(FlareCityStore.Key)
                .Test("reallife_flare_city_test_day", () => FlareDirector.Instance?.TestResidentsDay())
                .Test("reallife_flare_city_test_post", () => FlareDirector.Instance?.TestResidentsPost())
                .Test("reallife_flare_city_test_ad", () => FlareDirector.Instance?.TestResidentsAd())
                .Test("reallife_flare_city_test_page", () => FlareDirector.Instance?.TestResidentsPage())
                .Test("reallife_flare_city_test_offer", () => FlareDirector.Instance?.TestResidentsOffer())
                .Test("reallife_flare_city_test_trend", () => FlareDirector.Instance?.TestResidentsTrend())
                .Test("reallife_flare_city_test_plan", () => FlareDirector.Instance?.TestResidentsPlan())
                .Fact("flarecity_adult_age", () => Whole(FlareCityCatalogue.AdultAge))
                .Fact("flarecity_share_newcomer", () => Whole(FlareCityCatalogue.CityShare[0] / 10) + "%")
                .Fact("flarecity_share_celebrity", () => Whole(FlareCityCatalogue.CityShare[FlareCityCatalogue.CityShare.Length - 1] / 10) + "%")
                .Fact("flarecity_seen", () => Whole(FlareCityCatalogue.SeenShare / 10) + "%")
                .Fact("flarecity_fade", () => Whole((1000 - FlareCityCatalogue.HeardKeep) / 10) + "%")
                .Fact("flarecity_scandal_days", () => Whole(FlareCityCatalogue.ScandalDays))
                .Fact("flarecity_visits_review", () => Whole(FlareCityCatalogue.VisitsPerReview))
                .Fact("flarecity_ad_cpm", () => Guide.Money(Prices.At(FlareCityCatalogue.AdCpm)))
                .Fact("flarecity_offer_from", () => Whole(FlareRules.TierFrom[1]))
                .Fact("flarecity_offer_fee", () => Guide.Money(Prices.At(FlareCityRules.OfferFeeThousand / 100)))
                .Fact("flarecity_offer_days", () => Whole(FlareCityRules.OfferDays))
                .Fact("flarecity_sponsor_posts", () => Whole(FlareCityRules.SponsorPosts))
                .Fact("flarecity_sponsor_gap", () => Whole(FlareCityRules.SponsorGap))
                .Fact("flarecity_creator_from", () => Whole(FlareRules.TierFrom[FlareCityRules.CreatorFrom]))
                .Fact("flarecity_creator_days", () => Whole(FlareCityRules.CreatorDays))
                .Fact("flarecity_creator_rate", () => Guide.Money(Prices.At(FlareCityRules.CreatorThousandViews / 100)))
                .Fact("flarecity_devlog_most", () => Whole(FlareCityRules.DevlogHypeMost / 10) + "%")
                .Fact("flarecity_investors", () => Whole(FlareCityCatalogue.InvestorsPerKick))
                .Fact("flarecity_kicks_most", () => Whole(FlareCityCatalogue.MarketKicksMost))
                .Fact("flarecity_trend_rise", () => Whole(FlareCityCatalogue.TrendRiseDays))
                .Fact("flarecity_trend_days", () => Whole(FlareCityCatalogue.TrendDays))
                .Fact("flarecity_trend_reach", () => Whole(FlareCityCatalogue.TrendReach / 10) + "%")
                .Fact("flarecity_trend_district", () => Whole(FlareCityCatalogue.TrendDistrictReach / 10) + "%")
                .Fact("flarecity_trend_stick", () => Whole(FlareCityCatalogue.TrendStick / 10) + "%")
                .Fact("flarecity_plan_hour", () => Whole(FlareCityRules.PlanHour) + ":00")
                .ResetData(() => FlareDirector.Instance?.ForgetCity());
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }
    }

    /// <summary>Flare in the city's option and tests on the options page, after Flare's own.</summary>
    public static class FlareCityOptions
    {
        public static ModOptions AddFlareCity(this ModOptions options)
        {
            return options
                .AddToggle(Settings.LivingFlareId, "reallife_flare_city_enabled", Settings.LivingFlare, v => Settings.LivingFlare = v)
                .AddButton("reallife_flare_city_test_day", () => FlareDirector.Instance?.TestResidentsDay())
                .AddButton("reallife_flare_city_test_post", () => FlareDirector.Instance?.TestResidentsPost())
                .AddButton("reallife_flare_city_test_ad", () => FlareDirector.Instance?.TestResidentsAd())
                .AddButton("reallife_flare_city_test_page", () => FlareDirector.Instance?.TestResidentsPage())
                .AddButton("reallife_flare_city_test_offer", () => FlareDirector.Instance?.TestResidentsOffer())
                .AddButton("reallife_flare_city_test_trend", () => FlareDirector.Instance?.TestResidentsTrend())
                .AddButton("reallife_flare_city_test_plan", () => FlareDirector.Instance?.TestResidentsPlan());
        }
    }
}
