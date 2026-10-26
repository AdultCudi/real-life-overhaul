using System.Globalization;
using RealLifeOverhaul.Meta;
using RealLifeOverhaul.TestRun;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The city's builders on the stock market and a project's hedge in the mod's feature list, and the figures their Guide
    /// page prints, each filled from the rules when the page is opened so the page cannot say what the rules do not do.
    /// </summary>
    public static class BuildersGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("markets_builders", "reallife_meta_title_markets_builders")
                .Group("reallife_mkt_header")
                .About("reallife_guide_markets_builders_about").Help("reallife_guide_markets_builders")
                .Owns(Settings.MarketsBuildersId)
                .Check("reallife_mkt_builders_test_win", MarketDirector.TestBuilderWins, "Builders on the market check (a rival builder wins a tender now)", RunPlace.Here)
                .Check("reallife_mkt_builders_test_scandal", MarketDirector.TestBuilderScandal, "Builders on the market check (a bribery scandal at a rival builder now)", RunPlace.Here)
                .Check("reallife_mkt_builders_test_listing", MarketDirector.TestListingBuilder, "Listing check (a company holding a construction business)", RunPlace.Here)
                .Check("reallife_mkt_hedge_test_hedge", Construction.ConstructionDirector.TestHedge, "Hedging check (hedge the test project)", RunPlace.Here)
                .Check("reallife_mkt_hedge_test_end", Construction.ConstructionDirector.TestHedgeEnd, "Hedging check (the test project's end)", RunPlace.Here)
                .Check("reallife_mkt_builders_test_back", MarketDirector.TestBuildersBack, "Builders on the market check (put the builders back)", RunPlace.Here)
                .Check("reallife_mkt_hedge_test_back", Construction.ConstructionDirector.TestHedgeBack, "Hedging check (put the hedge tests back)", RunPlace.Here)
                .Fact("bm_listed", () => Whole(Listed()))
                .Fact("bm_win", () => Bp(BuilderMarket.WinBp))
                .Fact("bm_late", () => Bp(-BuilderMarket.SiteLateBp))
                .Fact("bm_bribery", () => Bp(-BuilderMarket.BriberyBp))
                .Fact("bm_bribery_margin", () => Guide.Percent(-BuilderMarket.BriberyMarginPermille / 1000f))
                .Fact("bm_contracts", () => Whole(DerivCatalogue.ContractsMost))
                .Fact("bm_units", () => DerivCatalogue.MaterialUnitsMost.ToString("N0"));
        }

        private static int Listed()
        {
            int n = 0;
            for (int r = 0; r < BuilderMarket.RivalStock.Length; r++)
            {
                n += BuilderMarket.RivalStock[r] > 0 ? 1 : 0;
            }
            return n;
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
