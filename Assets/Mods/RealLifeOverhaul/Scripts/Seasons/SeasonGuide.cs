using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>The seasons' entry in the feature list; its Guide page's figures ({season_snow_chance} and the rest) come from SeasonRules.</summary>
    public static class SeasonGuide
    {
        public static void Register()
        {
            Features.Add("seasons", "reallife_meta_title_seasons")
                .Group("reallife_season_header")
                .About("reallife_guide_seasons_about").Help("reallife_guide_seasons")
                .Owns(Settings.SeasonsId, Settings.SnowLookId, Settings.SeasonTreesId, Settings.SeasonSkyId, Settings.SeasonRainId, Settings.SeasonLeavesId,
                    Settings.SeasonBarId, Settings.SeasonCoatsId, Settings.SeasonBesideAlcwareId)
                .Test("reallife_season_test_goods", "reallife_season_test_snow", "reallife_season_test_spring", "reallife_season_test_summer",
                    "reallife_season_test_autumn", "reallife_season_test_winter")
                .Fact("season_snow_chance", () => (SeasonRules.SnowChance / 10).ToString(CultureInfo.InvariantCulture) + "%")
                .Fact("season_forecast_days", () => SeasonRules.ForecastDays.ToString(CultureInfo.InvariantCulture))
                .Fact("season_melt_hours", () => ((int)SeasonRules.MeltHours).ToString(CultureInfo.InvariantCulture))
                .Fact("season_forecast_hour", () => Hour(SeasonDirector.EveningAt))
                .Fact("season_coffee_winter", () => Figure("ba:itemname_cupofcoffee", CalendarRules.Winter))
                .Fact("season_tea_winter", () => Figure("ba:itemname_cupoftea", CalendarRules.Winter))
                .Fact("season_gifts_winter", () => Figure("ba:itemname_expensivegift", CalendarRules.Winter))
                .Fact("season_flowers_winter", () => Figure("ba:itemname_expensiveflower", CalendarRules.Winter))
                .Fact("season_whisky_winter", () => Figure("ba:itemname_whisky", CalendarRules.Winter))
                .Fact("season_icecream_summer", () => Figure("ba:itemname_icecream", CalendarRules.Summer))
                .Fact("season_icecream_winter", () => Figure("ba:itemname_icecream", CalendarRules.Winter))
                .Fact("season_beer_summer", () => Figure("ba:itemname_beer", CalendarRules.Summer))
                .Fact("season_umbrella_autumn", () => Figure("ba:itemname_umbrella", CalendarRules.Autumn))
                .Fact("season_slushie_winter", () => Figure("ba:itemname_slushi", CalendarRules.Winter))
                .Fact("season_salad_winter", () => Figure("ba:itemname_salad", CalendarRules.Winter))
                .Fact("season_clothes_winter", () => Figure("ba:itemname_classiccheapmaleclothing", CalendarRules.Winter))
                .Fact("season_flowers_spring", () => Figure("ba:itemname_cheapflower", CalendarRules.Spring))
                .Fact("season_umbrella_spring", () => Figure("ba:itemname_umbrella", CalendarRules.Spring))
                .Fact("season_salad_summer", () => Figure("ba:itemname_salad", CalendarRules.Summer))
                .Fact("season_kebab_summer", () => Figure("ba:itemname_kabob", CalendarRules.Summer))
                .Fact("season_umbrella_summer", () => Figure("ba:itemname_umbrella", CalendarRules.Summer))
                .Fact("season_clothes_summer", () => Figure("ba:itemname_classiccheapmaleclothing", CalendarRules.Summer))
                .Fact("season_whisky_summer", () => Figure("ba:itemname_whisky", CalendarRules.Summer))
                .Fact("season_apple_autumn", () => Figure("ba:itemname_apple", CalendarRules.Autumn))
                .Fact("season_flowers_autumn", () => Figure("ba:itemname_expensiveflower", CalendarRules.Autumn))
                .Fact("season_heavy_chance", () => Share(SeasonRules.HeavyChance))
                .Fact("season_snow_least", () => Whole(SeasonRules.LeastHours))
                .Fact("season_snow_most", () => Whole(SeasonRules.MostHours))
                .Fact("season_snow_latest", () => Hour(SeasonRules.LatestStart))
                .Fact("season_shower_chance", () => Share(SeasonLookRules.ShowerChance))
                .Fact("season_shower_hours", () => Whole(SeasonLookRules.ShowerHours))
                .Fact("season_autumn_rain_chance", () => Share(SeasonLookRules.AutumnRainChance))
                .Fact("season_autumn_rain_hours", () => Whole(SeasonLookRules.AutumnRainHours))
                .Fact("season_storm_chance", () => Share(SeasonLookRules.StormChance))
                .Fact("season_storm_hours", () => Whole(SeasonLookRules.StormHours))
                .Fact("season_rain_from", () => Hour(SeasonLookRules.RainFirstHour))
                .Fact("season_rain_to", () => Hour(SeasonLookRules.RainLastHour))
                .Cleanup(() => SeasonDirector.Instance?.StopAll());   // on again at the next second, as the option says
        }

        private static string Figure(string item, int season)
        {
            return SeasonRules.Change(SeasonRules.DemandOf(SeasonRules.IndexOf(item), season));
        }

        private static string Share(int thousandths) => (thousandths / 10f).ToString("0.#", CultureInfo.InvariantCulture) + "%";

        private static string Whole(int n) => n.ToString(CultureInfo.InvariantCulture);

        private static string Hour(int hour) => hour.ToString("00", CultureInfo.InvariantCulture) + ":00";
    }

    /// <summary>The seasons' place on the options page.</summary>
    public static class SeasonOptions
    {
        public static ModOptions AddSeasons(this ModOptions options)
        {
            return options
                .AddHeader("reallife_season_header")
                .AddToggle(Settings.SeasonsId, "reallife_season_enabled", Settings.Seasons,
                    v => { Settings.Seasons = v; if (!v) { SeasonDirector.Instance?.StopAll(); } })
                .AddToggle(Settings.SnowLookId, "reallife_season_snow_look", Settings.SnowLook,
                    v => Settings.SnowLook = v)
                .AddToggle(Settings.SeasonTreesId, "reallife_season_trees", Settings.SeasonTrees, v => Settings.SeasonTrees = v)
                .AddToggle(Settings.SeasonSkyId, "reallife_season_sky", Settings.SeasonSky, v => Settings.SeasonSky = v)
                .AddToggle(Settings.SeasonRainId, "reallife_season_rain", Settings.SeasonRain, v => Settings.SeasonRain = v)
                .AddToggle(Settings.SeasonLeavesId, "reallife_season_leaves", Settings.SeasonLeaves, v => Settings.SeasonLeaves = v)
                .AddToggle(Settings.SeasonBarId, "reallife_season_bar_option", Settings.SeasonBar, v => Settings.SeasonBar = v)
                .AddToggle(Settings.SeasonCoatsId, "reallife_season_coats", Settings.SeasonCoats, v => Settings.SeasonCoats = v)
                .AddToggle(Settings.SeasonBesideAlcwareId, "reallife_season_beside_alcware", Settings.SeasonBesideAlcware, v => Settings.SeasonBesideAlcware = v)
                .AddButton("reallife_season_test_goods", () => SeasonDirector.Instance?.TestGoods())
                .AddButton("reallife_season_test_snow", () => SeasonDirector.Instance?.TestSnow())
                .AddButton("reallife_season_test_spring", () => SeasonDirector.Instance?.TestShow(CalendarRules.Spring))
                .AddButton("reallife_season_test_summer", () => SeasonDirector.Instance?.TestShow(CalendarRules.Summer))
                .AddButton("reallife_season_test_autumn", () => SeasonDirector.Instance?.TestShow(CalendarRules.Autumn))
                .AddButton("reallife_season_test_winter", () => SeasonDirector.Instance?.TestShow(CalendarRules.Winter))
                .AddSplitter();
        }
    }
}
