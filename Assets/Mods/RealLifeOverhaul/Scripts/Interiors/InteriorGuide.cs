using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Interiors
{
    /// <summary>
    /// The feature's entries in the mod's feature list, and the figures its Guide pages print: every {name} in their
    /// text is filled from <see cref="InteriorRules"/> here, so a page never says anything the feature does not do.
    /// Called once, when the feature list is built (Meta/FeatureList); each figure is worked out when a page is opened.
    /// </summary>
    public static class InteriorGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static string N(int v) => v.ToString(Inv);

        private static string Share(int mille) => (mille / 10f).ToString("0.#", Inv);

        public static void Register()
        {
            Features.Add("interiors", "reallife_meta_title_interiors")
                .Group("reallife_interior_header")
                .About("reallife_guide_interiors_about").Help("reallife_guide_interiors")
                .Owns(Settings.InteriorsId, Settings.InteriorsPricesLevelId, Settings.InteriorsWearLevelId, Settings.InteriorsStandingId, Settings.InteriorsStaffId)
                .Preset(Settings.InteriorsPricesLevelId, 4, 3, 2)   // Relaxed Generous, Realistic Standard, Hardcore Conservative
                .Preset(Settings.InteriorsWearLevelId, 1, 2, 3)     // Relaxed Slow, Realistic Standard, Hardcore Fast
                .Data(InteriorStore.Key)
                .Test("reallife_interior_test_report_button", "reallife_interior_test_wear_button", "reallife_interior_test_news_button")
                .Fact("in_repeat", () => Share(InteriorRules.RepeatShare))
                .Fact("in_repeat_most", () => N(InteriorRules.RepeatMost))
                .Fact("in_cap_art", () => Share(InteriorRules.CapShare[(int)DecorCategory.Art]))
                .Fact("in_cap_greenery", () => Share(InteriorRules.CapShare[(int)DecorCategory.Greenery]))
                .Fact("in_cap_lighting", () => Share(InteriorRules.CapShare[(int)DecorCategory.Lighting]))
                .Fact("in_cap_audio", () => Share(InteriorRules.CapShare[(int)DecorCategory.Audio]))
                .Fact("in_cap_floor", () => Share(InteriorRules.CapShare[(int)DecorCategory.Floor]))
                .Fact("in_cap_furniture", () => Share(InteriorRules.CapShare[(int)DecorCategory.Furniture]))
                .Fact("in_cap_equipment", () => Share(InteriorRules.CapShare[(int)DecorCategory.Equipment]))
                .Fact("in_target_least", () => N(InteriorRules.TargetLeast))
                .Fact("in_target_from", () => N(InteriorRules.TargetFromSqm))
                .Fact("in_target_sqm", () => N(InteriorRules.TargetSqmPerPoint))
                .Fact("in_target_most", () => N(InteriorRules.TargetMost))
                .Fact("in_variety_from", () => N(InteriorRules.VarietyFrom))
                .Fact("in_variety_most", () => N(InteriorRules.VarietyMost))
                .Fact("in_interior_share", () => Share(InteriorRules.InteriorShare))
                .Fact("in_wear_day", () => Share(InteriorRules.WearDay))
                .Fact("in_wear_dirt", () => Share(InteriorRules.WearDirtMost))
                .Fact("in_wear_penalty", () => Share(InteriorRules.WearPenaltyMost))
                .Fact("in_dirt_from", () => N(InteriorRules.DirtFrom))
                .Fact("in_dirt_penalty", () => Share(InteriorRules.DirtPenaltyMost))
                .Fact("in_redecorate", () => N(InteriorRules.RedecorateRise))
                .Fact("in_refresh_sqm", () => Economy.FinanceText.Money(Prices.At((float)InteriorRules.RefreshPerSqm)))
                .Fact("in_refresh_least", () => Economy.FinanceText.Money(Prices.At(InteriorRules.RefreshLeast)))
                .Fact("in_refresh_most", () => Economy.FinanceText.Money(Prices.At(InteriorRules.RefreshMost)))
                .Fact("in_worn", () => Share(InteriorRules.WornFrom))
                .Fact("in_worn_points", () => N(InteriorRules.WornInspection))
                .Fact("in_bears_over", () => Share(InteriorRules.BearsOverMost))
                .Fact("in_bears_under", () => Share(InteriorRules.BearsUnderMost))
                .Fact("in_brand_per", () => N(InteriorRules.BrandPointsPer))
                .Fact("in_brand_most", () => N(InteriorRules.BrandMost))
                .Fact("in_standing_per", () => N(InteriorRules.StandingPointsPer))
                .Fact("in_standing_most", () => N(InteriorRules.StandingMost))
                .Fact("in_comfort_most", () => N(InteriorRules.ComfortMost))
                .Fact("in_plants_two", () => N(InteriorRules.PlantsForTwo))
                .Fact("in_morale", () => Share(InteriorRules.ComfortMoralePerPoint))
                .Fact("in_office_one", () => N(InteriorRules.OfficeOne))
                .Fact("in_office_two", () => N(InteriorRules.OfficeTwo))
                .Fact("in_bid_from", () => N(InteriorRules.BidFrom))
                .Fact("in_bid_most", () => Share(InteriorRules.BidMost))
                .Fact("in_striking", () => N(InteriorRules.StrikingFrom))
                .Fact("in_shabby_wear", () => Share(InteriorRules.ShabbyWear))
                .Fact("in_shabby_short", () => N(InteriorRules.ShabbyShort))
                .Fact("in_story_days", () => N(InteriorRules.StoryRestDays))
                .ResetData(() => InteriorDirector.Instance?.Forget());

            Features.Add("interiors_standard", "reallife_meta_title_interiors_standard")
                .About("reallife_guide_interiors_standard_about").Help("reallife_guide_interiors_standard")
                .Owns(Settings.InteriorsStandardLevelId, Settings.InteriorsRewardLevelId, Settings.InteriorsBasketId)
                .Preset(Settings.InteriorsStandardLevelId, 1, 2, 3)   // Relaxed Gentle, Realistic Standard, Hardcore Strict
                .Preset(Settings.InteriorsRewardLevelId, 4, 2, 1)     // Relaxed Generous, Realistic Conservative, Hardcore Low
                .Preset(Settings.InteriorsBasketId, 1, 1, 1)          // the basket on in every preset, as by default (its size follows the reward)
                .StandsAside(InteriorsSentence, Settings.InteriorsStandardLevelId, Settings.InteriorsRewardLevelId, Settings.InteriorsBasketId)
                .Fact("in_grace", () => N(InteriorRules.GraceDays))
                .Fact("in_standard_least", () => N(InteriorRules.StandardLeast))
                .Fact("in_standard_most", () => N(InteriorRules.StandardMost))
                .Fact("in_income_least", () => N(InteriorRules.IncomeLeast))
                .Fact("in_income_most", () => N(InteriorRules.IncomeMost))
                .Fact("in_short_points", () => N(InteriorRules.ShortPointsPerPercent))
                .Fact("in_short_most", () => N(InteriorRules.ShortFootfallMost))
                .Fact("in_over_points", () => N(InteriorRules.OverPointsPerPercent))
                .Fact("in_over_most", () => N(InteriorRules.OverFootfallMost))
                .Fact("in_footfall_most", () => N(InteriorRules.FootfallMost))
                .Fact("in_complaint_from", () => N(InteriorRules.ComplaintFrom))
                .Fact("in_complaint_per", () => Share(InteriorRules.ComplaintPerPoint))
                .Fact("in_complaint_most", () => Share(InteriorRules.ComplaintMost))
                .Fact("in_basket_full", () => N(InteriorRules.BasketFullAt))
                .Fact("in_basket_poor", () => Share(InteriorRules.BasketMostPoor))
                .Fact("in_basket_rich", () => Share(InteriorRules.BasketMostRich))
                .Fact("in_basket_middle", () => Share(InteriorRules.BasketShare(InteriorRules.BasketFullAt, InteriorRules.Standard(InteriorRules.IncomeMiddle, 0))));

            Features.Add("interiors_decor", "reallife_meta_title_interiors_decor")
                .About("reallife_guide_interiors_decor_about").Help("reallife_guide_interiors_decor")
                .Owns(Settings.InteriorsDecorLevelId)
                .Preset(Settings.InteriorsDecorLevelId, 3, 2, 2)      // Relaxed 15%, Realistic and Hardcore 10% (the default, as Meaningful Ambience's)
                .StandsAside(AmbienceSentence, Settings.InteriorsDecorLevelId)
                .Fact("in_decor_most", () => N(InteriorRules.DecorPercents[InteriorRules.DecorDefault]))
                .Fact("in_decor_least", () => N(InteriorRules.DecorPercents[1]))
                .Fact("in_decor_top", () => N(InteriorRules.DecorPercents[InteriorRules.DecorPercents.Length - 1]))
                .Fact("in_recent", () => N(InteriorRules.RecentOrders));
        }

        /// <summary>Why the district standard's rows do nothing just now (Meaningful Interiors is on), or null.</summary>
        public static string InteriorsSentence()
        {
            return OtherMods.MeaningfulInteriors.Loaded ? Economy.FinanceText.Fill("reallife_interior_aside", "mod", OtherMods.MeaningfulInteriors.Name) : null;
        }

        /// <summary>Why the decor footfall's row does nothing just now (Meaningful Ambience is on), or null.</summary>
        public static string AmbienceSentence()
        {
            return OtherMods.MeaningfulAmbience.Loaded ? Economy.FinanceText.Fill("reallife_interior_aside", "mod", OtherMods.MeaningfulAmbience.Name) : null;
        }
    }

    /// <summary>The feature's place on the options page, under the mod's own namespace so that it is one line there.</summary>
    public static class InteriorOptions
    {
        public static ModOptions AddInteriors(this ModOptions options)
        {
            return options
                .AddHeader("reallife_interior_header")
                .AddToggle(Settings.InteriorsId, "reallife_interior_enabled", Settings.Interiors, v => Settings.Interiors = v)
                .AddDropdown(Settings.InteriorsStandardLevelId, "reallife_interior_standard_option", InteriorText.StandardChoices, Settings.InteriorsStandardLevel, v => Settings.InteriorsStandardLevel = v)
                .AddDropdown(Settings.InteriorsRewardLevelId, "reallife_interior_reward_option", InteriorText.StrengthChoices, Settings.InteriorsRewardLevel, v => Settings.InteriorsRewardLevel = v)
                .AddDropdown(Settings.InteriorsDecorLevelId, "reallife_interior_decor_option", InteriorText.DecorChoices, Settings.InteriorsDecorLevel, v => Settings.InteriorsDecorLevel = v)
                .AddDropdown(Settings.InteriorsPricesLevelId, "reallife_interior_prices_option", InteriorText.StrengthChoices, Settings.InteriorsPricesLevel, v => Settings.InteriorsPricesLevel = v)
                .AddToggle(Settings.InteriorsBasketId, "reallife_interior_basket_option", Settings.InteriorsBasket, v => Settings.InteriorsBasket = v)
                .AddDropdown(Settings.InteriorsWearLevelId, "reallife_interior_wear_option", InteriorText.WearChoices, Settings.InteriorsWearLevel, v => Settings.InteriorsWearLevel = v)
                .AddToggle(Settings.InteriorsStandingId, "reallife_interior_standing_option", Settings.InteriorsStanding, v => Settings.InteriorsStanding = v)
                .AddToggle(Settings.InteriorsStaffId, "reallife_interior_staff_option", Settings.InteriorsStaff, v => Settings.InteriorsStaff = v)
                .AddButton("reallife_interior_test_report_button", () => InteriorDirector.Instance?.TestReport())
                .AddButton("reallife_interior_test_wear_button", () => InteriorDirector.Instance?.TestWear())
                .AddButton("reallife_interior_test_news_button", () => InteriorDirector.Instance?.TestNews())
                .AddSplitter();
        }
    }
}
