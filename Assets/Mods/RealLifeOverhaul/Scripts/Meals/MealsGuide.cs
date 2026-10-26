using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Meals
{
    /// <summary>
    /// Meals and music's entry in the feature list, its keys written out whole as the options checks read them. Its Guide
    /// page's numbers are facts filled from <see cref="MealsRules"/>.
    /// </summary>
    public static class MealsGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("meals", "reallife_meta_title_meals")
                .Group("reallife_meals_header")
                .About("reallife_guide_meals_about").Help("reallife_guide_meals")
                .Owns(Settings.MealsSeatedId, Settings.MealsCookedId, Settings.MealsDanceId, Settings.MealsTakeoutId)
                .Fact("meals_reach", () => MealsRules.Reach.ToString("0", Inv))
                .Fact("meals_least", () => MealsRules.LeastSeconds.ToString("0", Inv))
                .Fact("meals_most", () => MealsRules.MostSeconds.ToString("0", Inv))
                .Fact("meals_dance_minutes", () => MealsRules.DanceAtLeast.ToString(Inv))
                .Fact("meals_dance_hours", () => MealsRules.DanceGapHours.ToString(Inv))
                .Fact("meals_takeout_most", () => HomeBalance.TakeoutMost.ToString(Inv))
                .Test("reallife_meals_test_seated", "reallife_meals_test_cooked", "reallife_meals_test_dance", "reallife_meals_test_takeout")
                .Cleanup(() => MealsDirector.Instance?.StopAll("the options page's clean-up"));
        }
    }

    /// <summary>Meals and music's place on the options page.</summary>
    public static class MealsOptions
    {
        public static ModOptions AddMeals(this ModOptions options)
        {
            return options
                .AddHeader("reallife_meals_header")
                .AddToggle(Settings.MealsSeatedId, "reallife_meals_seated", Settings.MealsSeated,
                    v => { Settings.MealsSeated = v; if (!v) { MealsDirector.Instance?.End(false, "its option was switched off", true); } })
                .AddToggle(Settings.MealsCookedId, "reallife_meals_cooked", Settings.MealsCooked,
                    v => { Settings.MealsCooked = v; if (!v) { MealsDirector.Instance?.End(false, "its option was switched off", true); } })
                .AddToggle(Settings.MealsDanceId, "reallife_meals_dance", Settings.MealsDance,
                    v => { Settings.MealsDance = v; if (!v) { MealsDirector.Instance?.StopDancing("its option was switched off", true); } })
                .AddToggle(Settings.MealsTakeoutId, "reallife_meals_takeout", Settings.MealsTakeout,
                    v => { Settings.MealsTakeout = v; TakeoutButton.Refresh(); })   // a meal at a seat on now ends at its next frame (MealsDirector.Wanted)
                .AddButton("reallife_meals_test_seated", () => MealsDirector.Instance?.TestSeated())
                .AddButton("reallife_meals_test_cooked", () => MealsDirector.Instance?.TestCooked())
                .AddButton("reallife_meals_test_dance", () => MealsDirector.Instance?.TestDance())
                .AddButton("reallife_meals_test_takeout", () => MealsDirector.Instance?.TestTakeout())
                .AddSplitter();
        }
    }
}
