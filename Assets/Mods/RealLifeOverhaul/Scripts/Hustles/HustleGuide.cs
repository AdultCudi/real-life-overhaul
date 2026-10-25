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
                .Fact("hustle_haggle_least", () => Share(HustleCatalogue.HaggleLeast))
                .Fact("hustle_negotiation", () => Share(HustleCatalogue.NegotiationLever))
                .Fact("hustle_overstate", () => Share(HustleCatalogue.OverstateChance))
                .Fact("hustle_owned_most", () => Guide.Number(HustleCatalogue.OwnedMost))
                .Fact("hustle_delivery", () => Money(HustleCatalogue.DeliveryLeast))
                .Fact("hustle_delivery_share", () => Share(HustleCatalogue.DeliveryShare))
                .Fact("hustle_delivery_hour", () => Hour(HustleCatalogue.DeliveryHour))
                .Fact("hustle_parts", () => Share(HustleCatalogue.PartsShare))
                .Fact("hustle_fix_base", () => Share(HustleCatalogue.FixChanceBase))
                .Fact("hustle_fix_xp", () => Share(HustleCatalogue.FixChancePerXp))
                .Fact("hustle_fix_least", () => Share(HustleCatalogue.FixChanceLeast))
                .Fact("hustle_fix_most", () => Share(HustleCatalogue.FixChanceMost))
                .Fact("hustle_fix_wreck", () => Share(HustleCatalogue.FixWreckCut))
                .Fact("hustle_wreck_below", () => Guide.Number(HustleCatalogue.WreckBelow))
                .Fact("hustle_xp_worked", () => Guide.Number(HustleCatalogue.XpWorked))
                .Fact("hustle_xp_failed", () => Guide.Number(HustleCatalogue.XpFailed))
                .Fact("hustle_shop_share", () => Share(HustleCatalogue.ShopShare))
                .Fact("hustle_shop_least", () => Money(HustleCatalogue.ShopLeast))
                .Fact("hustle_shop_minutes", () => Guide.Number(HustleCatalogue.ShopMinutes))
                .Fact("hustle_clean_minutes", () => Guide.Number(HustleCatalogue.CleanMinutes))
                .Fact("hustle_clean_cost", () => Money(HustleCatalogue.CleanSupplies))
                .Fact("hustle_clean_most", () => Guide.Number(HustleCatalogue.CleanMost))
                .Fact("hustle_energy", () => Guide.Number(HustleCatalogue.EnergyAnHour))
                .Fact("hustle_ask_least", () => Share(HustleCatalogue.AskLeastShare))
                .Fact("hustle_ask_most", () => Share(HustleCatalogue.AskMostShare))
                .Fact("hustle_buyers_from", () => Hour(HustleCatalogue.BuyersFrom))
                .Fact("hustle_buyers_until", () => Hour(HustleCatalogue.BuyersUntil))
                .Fact("hustle_offers_most", () => Guide.Number(HustleCatalogue.OffersMost))
                .Fact("hustle_counters", () => Guide.Number(HustleCatalogue.CountersMost))
                .Fact("hustle_gigs", () => Guide.Number(HustleCatalogue.GigsADay))
                .Fact("hustle_good_rating", () => Guide.Number(HustleCatalogue.GoodRating / 1000f))
                .Fact("hustle_poor_rating", () => Guide.Number(HustleCatalogue.PoorRating / 1000f))
                .Fact("hustle_good_pay", () => Share(HustleCatalogue.GoodRatingPay - 1000))
                .Fact("hustle_client_least", () => Share(HustleCatalogue.ClientLeast))
                .Fact("hustle_client_most", () => Share(HustleCatalogue.ClientMost))
                .Fact("hustle_gig_from", () => Hour(HustleCatalogue.GigFromLeast))
                .Fact("hustle_gig_to", () => Hour(HustleCatalogue.GigFromMost))
                .Fact("hustle_tip", () => Share(HustleCatalogue.TipShare))
                .Fact("hustle_cancel_rating", () => Guide.Number(HustleCatalogue.CancelRating))
                .Fact("hustle_missed_rating", () => Guide.Number(HustleCatalogue.MissedRating))
                .Fact("hustle_ratings_kept", () => Guide.Number(HustleCatalogue.RatingsKept))
                .Fact("hustle_van_reach", () => Guide.Number(HustleCatalogue.VanReach))
                .Fact("hustle_pay_detailing", () => GigPay(GigKind.Detailing))
                .Fact("hustle_pay_handyman", () => GigPay(GigKind.Handyman))
                .Fact("hustle_pay_junk", () => GigPay(GigKind.Junk))
                .Fact("hustle_pay_moving", () => GigPay(GigKind.Moving))
                .Fact("hustle_minutes_detailing", () => GigMinutes(GigKind.Detailing))
                .Fact("hustle_minutes_handyman", () => GigMinutes(GigKind.Handyman))
                .Fact("hustle_minutes_junk", () => GigMinutes(GigKind.Junk))
                .Fact("hustle_minutes_moving", () => GigMinutes(GigKind.Moving))
                .Cleanup(() => HustleDirector.Instance?.StopAll())
                .ResetData(() => HustleDirector.Instance?.Forget());   // the director opens the feature afresh from the (now empty) save on its next second
        }

        private static string Share(int thousandths) => (thousandths / 10f).ToString("0.#", CultureInfo.InvariantCulture) + "%";

        private static string Hour(int hour) => hour.ToString("00", CultureInfo.InvariantCulture) + ":00";

        /// <summary>A sum written at the start's prices, at today's.</summary>
        private static string Money(int amount) => Guide.Money(HustleRules.AtPrices(amount, Prices.Level));

        /// <summary>A visit's pay at today's prices, before the client's own figure and the rating.</summary>
        private static string GigPay(GigKind kind)
        {
            GigDef g = Gig(kind);
            return g != null ? Money(g.Pay) : "?";
        }

        /// <summary>A visit's game minutes of work at each address.</summary>
        private static string GigMinutes(GigKind kind)
        {
            GigDef g = Gig(kind);
            return g != null ? Guide.Number(g.Minutes) : "?";
        }

        private static GigDef Gig(GigKind kind)
        {
            for (int i = 0; i < HustleCatalogue.Gigs.Length; i++)
            {
                if (HustleCatalogue.Gigs[i].Kind == kind)
                {
                    return HustleCatalogue.Gigs[i];
                }
            }
            return null;
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
