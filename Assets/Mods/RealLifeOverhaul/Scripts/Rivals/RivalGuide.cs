using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Rivals
{
    /// <summary>
    /// The rival tycoons' entry in the feature list, and the Guide page's figures, each filled from
    /// <see cref="RivalCatalogue"/> when the page opens. Registered once, when the feature list is built.
    /// </summary>
    public static class RivalGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("rivals", "reallife_meta_title_rivals")
                .Group("reallife_rival_header")
                .About("reallife_guide_rivals_about").Help("reallife_guide_rivals")
                .Owns(Settings.RivalsId, Settings.RivalsStrengthId, Settings.RivalsFirstId)
                .Preset(Settings.RivalsStrengthId, RivalCatalogue.Gentle, RivalCatalogue.Hard, RivalCatalogue.Ruthless)   // (Normal is its default)
                .Data(RivalStore.Key, RivalStore.TestKey)
                .Test("reallife_rival_test_property", "reallife_rival_test_outlet", "reallife_rival_test_poach", "reallife_rival_test_stake", "reallife_rival_test_bid",
                    "reallife_rival_test_lobby", "reallife_rival_test_suit", "reallife_rival_test_press", "reallife_rival_test_next", "reallife_rival_test_forward",
                    "reallife_rival_test_list", "reallife_rival_test_takeover", "reallife_rival_test_back", "reallife_rival_test_first")
                .Fact("rival_tens", () => Money(RivalRules.FirstFrom(Settings.RivalsFirst)))   // (the option's amount: the Guide says when they come in this game)
                .Fact("rival_hundreds", () => Money(RivalCatalogue.TierFrom[2]))
                .Fact("rival_billions", () => Money(RivalCatalogue.TierFrom[3]))
                .Fact("rival_ten_billions", () => Money(RivalCatalogue.TierFrom[4]))
                .Fact("rival_gap_tens", () => Whole(RivalRules.Gap(1, RivalCatalogue.Normal)))
                .Fact("rival_gap_hundreds", () => Whole(RivalRules.Gap(2, RivalCatalogue.Normal)))
                .Fact("rival_gap_billions", () => Whole(RivalRules.Gap(3, RivalCatalogue.Normal)))
                .Fact("rival_gap_top", () => Whole(RivalRules.Gap(4, RivalCatalogue.Normal)))
                .Fact("rival_gap_ruthless", () => Whole(RivalRules.Gap(4, RivalCatalogue.Ruthless)))
                .Fact("rival_jitter", () => Whole(RivalCatalogue.JitterDays))
                .Fact("rival_telegraph", () => Whole(RivalCatalogue.TelegraphDays))
                .Fact("rival_property_days", () => Whole(RivalCatalogue.PropertyDays))
                .Fact("rival_property_premium", () => Permille(RivalCatalogue.PropertyPremium))
                .Fact("rival_property_most", () => Permille(RivalCatalogue.PropertyPremiumMost))
                .Fact("rival_rent_rise", () => Permille(RivalCatalogue.RentRise))
                .Fact("rival_rent_most", () => Permille(RivalCatalogue.RentRiseMost))
                .Fact("rival_outlet_days", () => Whole(RivalCatalogue.OutletDays))
                .Fact("rival_outlet_dip", () => Whole(RivalCatalogue.OutletDip))
                .Fact("rival_outlet_most", () => Whole(RivalCatalogue.OutletDipMost))
                .Fact("rival_outlets_most", () => Whole(RivalCatalogue.OutletsMost))
                .Fact("rival_poach_premium", () => Permille(RivalCatalogue.PoachPremium))
                .Fact("rival_poach_most", () => Permille(RivalCatalogue.PoachPremiumMost))
                .Fact("rival_lobby_days", () => Whole(RivalCatalogue.LobbyDays))
                .Fact("rival_lobby_weight", () => Permille(RivalCatalogue.LobbyWeight))
                .Fact("rival_lobby_most", () => Permille(RivalCatalogue.LobbyWeightMost))
                .Fact("rival_suit_share", () => Basis(RivalCatalogue.SuitShare))
                .Fact("rival_suit_most_share", () => Basis(RivalCatalogue.SuitShareMost))
                .Fact("rival_suit_most", () => Money(RivalCatalogue.SuitMost))
                .Fact("rival_bid_after", () => Whole(RivalCatalogue.BidAfterStakeDays))
                .Fact("rival_bid_rest", () => Whole(RivalCatalogue.BidRestDays))
                .Fact("rival_threshold", () => (Markets.ListingCatalogue.TakeoverThresholdBp / 100).ToString(Inv) + "%")
                .Fact("rival_held_most", () => (System.Math.Min(Markets.MarketTrading.HoldingBp, Markets.MarketCatalogue.HoldingFloatBp) / 100).ToString(Inv) + "%")
                .Fact("rival_outbid_share", () => Basis(RivalCatalogue.CounterShare[(int)RivalCounter.Outbid]))
                .Fact("rival_poach_share", () => Basis(RivalCatalogue.CounterShare[(int)RivalCounter.Poach]))
                .Fact("rival_press_share", () => Basis(RivalCatalogue.CounterShare[(int)RivalCounter.Press]))
                .Fact("rival_lobby_share", () => Basis(RivalCatalogue.CounterShare[(int)RivalCounter.Lobby]))
                .Fact("rival_poach_quiet", () => Whole(RivalCatalogue.PoachQuietDays))
                .Fact("rival_lobby_rest", () => Whole(RivalCatalogue.CounterRestDays[(int)RivalCounter.Lobby]))
                .Fact("rival_suit_least", () => Money(RivalCatalogue.SuitLeast))
                .Fact("rival_poach_gap", () => Whole(Economy.CompanyCatalogue.PoachCompanyRestDays))
                .Fact("rival_poach_slow", () => Whole(RivalCatalogue.PoachSlowDays))
                .Fact("rival_press_delay", () => Whole(RivalCatalogue.PressDelayDays))
                .Fact("rival_press_cut", () => Permille(RivalCatalogue.PressWorthCut))
                .Fact("rival_lobby_bar", () => Whole(RivalCatalogue.LobbyBarDays))
                .Fact("rival_poach_rest", () => Whole(RivalCatalogue.CounterRestDays[(int)RivalCounter.Poach]))
                .Fact("rival_outbid_rest", () => Whole(RivalCatalogue.CounterRestDays[(int)RivalCounter.Outbid]))
                .Fact("rival_press_rest", () => Whole(RivalCatalogue.CounterRestDays[(int)RivalCounter.Press]))
                .Fact("rival_takeover_premium", () => (RivalCatalogue.TakeoverPremiumBp / 100).ToString(Inv) + "%")
                .Fact("rival_breakup", () => Permille(RivalCatalogue.BreakUpPermille))
                .Fact("rival_breakup_days", () => Whole(RivalCatalogue.BreakUpDays))
                .ResetData(() => RivalDirector.Instance?.Forget());   // the director opens the rivals afresh from the (now empty) save on its next second
        }

        private static string Money(long dollars)
        {
            return Guide.Money(RivalRules.At(dollars, Prices.Level));
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }

        private static string Permille(int value)
        {
            return (value / 10.0).ToString("0.#", Inv) + "%";
        }

        private static string Basis(int perTenThousand)
        {
            return (perTenThousand / 100.0).ToString("0.##", Inv) + "%";
        }
    }
}
