using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Loyalty
{
    /// <summary>
    /// The loyalty cards' entry in the feature list and the figures its Guide page prints, each {name} filled from
    /// <see cref="LoyaltyRules"/> when the page opens, so the page never says what the feature does not do.
    /// </summary>
    public static class LoyaltyGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static string N(int v) => v.ToString(Inv);

        private static string Share(int mille) => (mille / 10f).ToString("0.#", Inv);

        public static void Register()
        {
            Features.Add("loyalty", "reallife_meta_title_loyalty")
                .Group("reallife_loyalty_header")
                .About("reallife_guide_loyalty_about").Help("reallife_guide_loyalty")
                .Owns(Settings.LoyaltyId)
                .Data(LoyaltyStore.Key)
                .Test("reallife_loyalty_test_report_button", "reallife_loyalty_test_week_button", "reallife_loyalty_test_rivals_button")
                .Fact("loy_rate_1", () => Share(LoyaltyRules.RateMille[0]))
                .Fact("loy_rate_2", () => Share(LoyaltyRules.RateMille[1]))
                .Fact("loy_rate_3", () => Share(LoyaltyRules.RateMille[2]))
                .Fact("loy_rate_4", () => Share(LoyaltyRules.RateMille[3]))
                .Fact("loy_even_1", () => Share(LoyaltyRules.BreakEven(0, false, 0)))
                .Fact("loy_even_2", () => Share(LoyaltyRules.BreakEven(1, false, 0)))
                .Fact("loy_even_3", () => Share(LoyaltyRules.BreakEven(2, false, 0)))
                .Fact("loy_even_4", () => Share(LoyaltyRules.BreakEven(3, false, 0)))
                .Fact("loy_base_days", () => N(LoyaltyRules.BaseDays))
                .Fact("loy_pool", () => Share(LoyaltyRules.PoolMost))
                .Fact("loy_lift_most", () => N(LoyaltyRules.LiftMost))
                .Fact("loy_join", () => Share(LoyaltyRules.JoinBase))
                .Fact("loy_pleased_from", () => N(LoyaltyRules.PleasedFrom))
                .Fact("loy_pleased_full", () => N(LoyaltyRules.PleasedFull))
                .Fact("loy_lapse", () => Share(LoyaltyRules.LapseBase))
                .Fact("loy_lapse_shut", () => Share(LoyaltyRules.LapseShutMost))
                .Fact("loy_lapse_dirty", () => Share(LoyaltyRules.LapseDirtyMost))
                .Fact("loy_lapse_dear", () => Share(LoyaltyRules.LapseDearMost))
                .Fact("loy_lapse_off", () => Share(LoyaltyRules.LapseOff))
                .Fact("loy_clean_from", () => N(LoyaltyRules.CleanFrom))
                .Fact("loy_prices_from", () => N(LoyaltyRules.PricesFrom))
                .Fact("loy_cut", () => Share(LoyaltyRules.CutLapse))
                .Fact("loy_setup", () => Guide.Money(Prices.At(LoyaltyRules.SetupFee)))
                .Fact("loy_setup_chain", () => Guide.Money(Prices.At(LoyaltyRules.SetupCost(1))))
                .Fact("loy_change", () => Guide.Money(Prices.At(LoyaltyRules.ChangeCost())))
                .Fact("loy_card_day", () => Guide.Money(Prices.At(LoyaltyRules.CardDay)))
                .Fact("loy_push_staff", () => Guide.Money(Prices.At(LoyaltyRules.PushCost[1] / 100f)))
                .Fact("loy_push_bonus", () => Guide.Money(Prices.At(LoyaltyRules.PushCost[2] / 100f)))
                .Fact("loy_offer_points", () => N(LoyaltyRules.OfferPoints))
                .Fact("loy_offer_notice", () => N(LoyaltyRules.OfferNotice))
                .Fact("loy_reward_most", () => Share(LoyaltyRules.RewardMost))
                .Fact("loy_chain_reach", () => Share(LoyaltyRules.ChainReach))
                .Fact("loy_chain_most", () => Share(LoyaltyRules.ChainReachMost))
                .Fact("loy_rival_from", () => N(LoyaltyRules.RivalFrom))
                .Fact("loy_rival_days", () => N(LoyaltyRules.RivalSetupDays))
                .Fact("loy_rival_floor", () => Share(LoyaltyRules.RivalFloor))
                .ResetData(() => LoyaltyDirector.Instance?.Forget());
        }
    }

    /// <summary>The feature's place on the options page, under the mod's own namespace so that it is one line there.</summary>
    public static class LoyaltyOptions
    {
        public static ModOptions AddLoyalty(this ModOptions options)
        {
            return options
                .AddHeader("reallife_loyalty_header")
                .AddToggle(Settings.LoyaltyId, "reallife_loyalty_enabled", Settings.Loyalty, v => Settings.Loyalty = v)
                .AddButton("reallife_loyalty_test_report_button", () => LoyaltyDirector.Instance?.TestReport())
                .AddButton("reallife_loyalty_test_week_button", () => LoyaltyDirector.Instance?.TestWeek())
                .AddButton("reallife_loyalty_test_rivals_button", () => LoyaltyDirector.Instance?.TestRivals())
                .AddSplitter();
        }
    }
}
