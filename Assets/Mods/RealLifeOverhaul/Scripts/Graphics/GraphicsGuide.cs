using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Gfx
{
    /// <summary>
    /// Realistic graphics' entry in the feature list; its Guide page's figures (each {name}) come from
    /// <see cref="GraphicsRules"/> when the page opens, so the page never promises a cost the rules do not hold to.
    /// </summary>
    public static class GraphicsGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("graphics", "reallife_meta_title_graphics")
                .Group("reallife_gfx_header")
                .About("reallife_guide_graphics_about").Help("reallife_guide_graphics")
                .Owns(Settings.GraphicsLookId, Settings.GraphicsCostId)
                .Test("reallife_gfx_test_cost", "reallife_gfx_test_hours", "reallife_gfx_test_say")
                .Fact("gfx_free_ms", () => Ms(GraphicsRules.FreeBudgetMs))
                .Fact("gfx_balanced_ms", () => Ms(GraphicsRules.BalancedBudgetMs))
                .Fact("gfx_high_ms", () => Ms(GraphicsRules.HighBudgetMs))
                .Fact("gfx_window_seconds", () => GraphicsRules.CostWindowSeconds.ToString("0.#", Inv))
                .Fact("gfx_pairs", () => GraphicsRules.CostPairs.ToString(Inv))
                .Fact("gfx_hour_seconds", () => GraphicsRules.HourShownSeconds.ToString("0.#", Inv))
                .Fact("gfx_grey_percent", () => GraphicsRules.GreyTolerance.ToString("0", Inv))
                .Cleanup(() => GraphicsDirector.Instance?.PutAway());   // on again at the next second, as the option says
        }

        private static string Ms(float ms) => ms.ToString("0.0#", Inv);
    }

    /// <summary>The feature's place on the options page, under the mod's own namespace so that it is one line there.</summary>
    public static class GraphicsOptions
    {
        public static ModOptions AddGraphics(this ModOptions options)
        {
            return options
                .AddHeader("reallife_gfx_header")
                .AddDropdown(Settings.GraphicsLookId, "reallife_gfx_look",
                    new[] { "reallife_gfx_look_off", "reallife_gfx_look_natural", "reallife_gfx_look_filmic", "reallife_gfx_look_vivid" },
                    Settings.GraphicsLook, v => { Settings.GraphicsLook = v; GraphicsDirector.OptionChanged(); })   // on the picture at once, also in the menu's pause
                .AddDropdown(Settings.GraphicsCostId, "reallife_gfx_cost",
                    new[] { "reallife_gfx_cost_free", "reallife_gfx_cost_balanced", "reallife_gfx_cost_high" },
                    Settings.GraphicsCost, v => { Settings.GraphicsCost = v; GraphicsDirector.OptionChanged(); })
                .AddButton("reallife_gfx_test_cost", () => GraphicsDirector.Instance?.TestCost())
                .AddButton("reallife_gfx_test_hours", () => GraphicsDirector.Instance?.TestHours())
                .AddButton("reallife_gfx_test_say", () => GraphicsDirector.Instance?.TestSay())
                .AddSplitter();
        }
    }
}
