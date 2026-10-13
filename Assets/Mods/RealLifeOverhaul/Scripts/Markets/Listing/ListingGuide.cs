using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The player's company on the market in the mod's feature list, and the figures its Guide page prints: every {name}
    /// in its text is filled from <see cref="ListingCatalogue"/> here, so the page can never say anything the rules do
    /// not do. Called once, when the feature list is built (Meta/FeatureList); each figure is worked out when the page is
    /// opened. Its data is the market's own line (MarketStore), which the market's entry already names.
    /// </summary>
    public static class ListingGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("markets_listing", "reallife_meta_title_markets_listing")
                .Group("reallife_mkt_header")
                .About("reallife_guide_markets_listing_about").Help("reallife_guide_markets_listing")
                .Owns(Settings.MarketsListingId)
                .Test("reallife_lst_test_list", "reallife_lst_test_results", "reallife_lst_test_meeting", "reallife_lst_test_activist", "reallife_lst_test_bid", "reallife_lst_test_off")
                .Fact("lst_years", () => Whole(ListingCatalogue.BooksYears))
                .Fact("lst_quarters", () => Whole(ListingCatalogue.QuartersLeast))
                .Fact("lst_turnover", () => Guide.Money(Prices.At((float)ListingCatalogue.TurnoverLeastAtStart)))
                .Fact("lst_profit", () => Guide.Money(Prices.At((float)ListingCatalogue.ProfitLeastAtStart)))
                .Fact("lst_profitable", () => Whole(ListingCatalogue.ProfitableQuartersLeast))
                .Fact("lst_audit_days", () => Whole(ListingCatalogue.AuditDays))
                .Fact("lst_retainer", () => Bp(ListingCatalogue.RetainerBp))
                .Fact("lst_retainer_least", () => Guide.Money(Prices.At((float)ListingCatalogue.RetainerLeastAtStart)))
                .Fact("lst_retainer_most", () => Guide.Money(Prices.At((float)ListingCatalogue.RetainerMostAtStart)))
                .Fact("lst_audit", () => Bp(ListingCatalogue.AuditBp))
                .Fact("lst_audit_least", () => Guide.Money(Prices.At((float)ListingCatalogue.AuditLeastAtStart)))
                .Fact("lst_audit_most", () => Guide.Money(Prices.At((float)ListingCatalogue.AuditMostAtStart)))
                .Fact("lst_underwriting", () => Bp(ListingCatalogue.UnderwritingBp))
                .Fact("lst_underwriting_large", () => Bp(ListingCatalogue.UnderwritingLargeBp))
                .Fact("lst_large", () => Guide.Money(Prices.At((float)ListingCatalogue.UnderwritingLargeAtStart)))
                .Fact("lst_exchange", () => Bp(ListingCatalogue.ListingFeeBp))
                .Fact("lst_yearly", () => Bp(ListingCatalogue.YearlyFeeBp))
                .Fact("lst_float_least", () => Bp(ListingCatalogue.FloatSteps[0]))
                .Fact("lst_float_most", () => Bp(ListingCatalogue.FloatSteps[ListingCatalogue.FloatSteps.Length - 1]))
                .Fact("lst_discount", () => Guide.Percent((float)ListingCatalogue.IpoDiscount))
                .Fact("lst_range", () => Guide.Percent((float)ListingCatalogue.RangeSpread))
                .Fact("lst_beyond", () => Guide.Percent((float)ListingCatalogue.PriceBeyondRange))
                .Fact("lst_book_days", () => Whole(ListingCatalogue.BookDays))
                .Fact("lst_pay_fair", () => Bp(ListingCatalogue.PayRefBp))
                .Fact("lst_pay_most", () => Bp(ListingCatalogue.PaySteps[ListingCatalogue.PaySteps.Length - 1]))
                .Fact("lst_lockup", () => Whole(Year() / ListingCatalogue.LockUpYearShare))
                .Fact("lst_swing", () => Whole(Year() / ListingCatalogue.ShortSwingYearShare))
                .Fact("lst_founder_most", () => Bp(ListingCatalogue.FounderMostBp))
                .Fact("lst_deal_year", () => Bp(ListingCatalogue.FounderDealMostBpYear))
                .Fact("lst_private_premium", () => Bp(ListingCatalogue.PrivatePremiumBp))
                .Fact("lst_private_least", () => Bp(ListingCatalogue.PrivateLeastBp))
                .Fact("lst_placing", () => Guide.Percent((float)ListingCatalogue.PlacingDiscount))
                .Fact("lst_issue_most", () => Bp(ListingCatalogue.IssueMostBpYear))
                .Fact("lst_buyback_most", () => Bp(ListingCatalogue.BuyBackMostBp))
                .Fact("lst_board_payout", () => Bp(ListingCatalogue.BoardPayoutBp))
                .Fact("lst_agm_days", () => Whole(ListingCatalogue.AgmNoticeDays))
                .Fact("lst_egm_days", () => Whole(ListingCatalogue.EgmNoticeDays))
                .Fact("lst_payout", () => Bp(ListingCatalogue.SpecialPayoutBp))
                .Fact("lst_threshold", () => Bp(ListingCatalogue.TakeoverThresholdBp))
                .Fact("lst_bid_least", () => Guide.Percent((float)ListingCatalogue.OwnTakeoverPremiumLeast))
                .Fact("lst_bid_most", () => Guide.Percent((float)ListingCatalogue.OwnTakeoverPremiumMost))
                .Fact("lst_handover_days", () => Whole(ListingCatalogue.HandOverDays))
                .Fact("lst_dividend_tax", () => Bp(ListingCatalogue.DividendTaxBp))
                .Fact("lst_dividend_tax_step", () => Bp(ListingCatalogue.DividendTaxStepBp));
        }

        private static int Year()
        {
            return System.Math.Max(8, GameCalendar.DaysPerYear);
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }

        private static string Bp(int bp)
        {
            return Guide.Percent(bp / 10000f);
        }
    }
}
