using System.Globalization;
using RealLifeOverhaul.Meta;
using RealLifeOverhaul.TestRun;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The stock market's extras in the mod's feature list, and the figures their Guide page prints, each filled from the
    /// rules when the page is opened so the page cannot say what the rules do not do.
    /// </summary>
    public static class ExtrasGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("markets_extras", "reallife_meta_title_markets_extras")
                .Group("reallife_mkt_header")
                .About("reallife_guide_markets_extras_about").Help("reallife_guide_markets_extras")
                .Owns(Settings.MarketsExtrasId)
                .Check("reallife_stx_test_split", MarketDirector.TestExtrasSplit, "Stock extras check (a split at the next open)", RunPlace.Here)
                .Check("reallife_stx_test_reverse", MarketDirector.TestExtrasReverse, "Stock extras check (shares put together at the next open)", RunPlace.Here)
                .Check("reallife_stx_test_fund", MarketDirector.TestExtrasFund, "Stock extras check (the fund against its parts)", RunPlace.Here)
                .Check("reallife_stx_test_block", MarketDirector.TestExtrasBlock, "Stock extras check (a block quoted)", RunPlace.Here)
                .Check("reallife_stx_test_rival", MarketDirector.TestExtrasRival, "Stock extras check (a rival looks at the market)", RunPlace.Here)
                .Check("reallife_stx_test_bank", MarketDirector.TestExtrasBank, "Stock extras check (the bank's own books)", RunPlace.Here)
                .Fact("stx_split_from", () => MarketText.Price(ExtrasCatalogue.SplitLevelLeastCents))
                .Fact("stx_split_to", () => MarketText.Price(MarketCatalogue.SplitAboveCents))
                .Fact("stx_notice", () => Whole(MarketCatalogue.SplitNoticeDays))
                .Fact("stx_reverse_days", () => Whole(ExtrasCatalogue.ReverseAfterDays))
                .Fact("stx_fund_fee", () => Guide.Percent(ExtrasCatalogue.FundFeeBp / 10000f))
                .Fact("stx_fund_most", () => Guide.Percent(ExtrasCatalogue.FundHoldingBp / 10000f))
                .Fact("stx_disclose", () => Guide.Percent(ExtrasCatalogue.DiscloseStepBp / 10000f))
                .Fact("stx_block_days", () => Whole(ExtrasCatalogue.BlockListedDays))
                .Fact("stx_bank_quarters", () => Whole(ExtrasCatalogue.BankQuartersLeast))
                .Fact("stx_bank_book", () => ExtrasCatalogue.BankBookMost.ToString("0.#", Inv));
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }
    }
}
