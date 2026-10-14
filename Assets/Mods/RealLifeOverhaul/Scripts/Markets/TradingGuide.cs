using System.Globalization;
using System.Text;
using Localizor;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Orders that wait (stop losses, take profits, limits), worked orders and trading bots in the mod's feature list, and
    /// the figures their Guide page prints: every {name} in its text is filled from <see cref="MarketBots"/> and
    /// <see cref="MarketCatalogue"/> here, so the page can never say anything the rules do not do. With them, the market's
    /// own entry (Meta/FeatureList, registered first) takes a large investor's options, tests and figures: worked orders and
    /// larger bond purchases, dividends put back, a bond's rollover (InvestRules, BondCatalogue). Called once, when the
    /// feature list is built; each figure is worked out when the page is opened. Their data is the market's own line
    /// (MarketStore), which the market's entry already names.
    /// </summary>
    public static class TradingGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("markets", "reallife_meta_title_markets")   // (the market's entry, made just before: its large investor's parts)
                .Owns(Settings.MarketsLargeId, Settings.MarketsReinvestId)
                .Test("reallife_mkt_inv_test_back", "reallife_mkt_inv_test_roll")
                .Fact("bond_most_share", () => Whole(BondCatalogue.MostWorthPct) + "%")
                .Fact("bond_most_cap", () => Guide.Money(Prices.At((float)BondCatalogue.MostCapAtStart)))
                .Fact("bond_ladder_terms", () => Whole(BondRules.LadderTerms(new System.Collections.Generic.List<BondDef>(8))));
            Features.Add("markets_bots", "reallife_meta_title_markets_bots")
                .Group("reallife_mkt_header")
                .About("reallife_guide_markets_bots_about").Help("reallife_guide_markets_bots")
                .Owns(Settings.MarketsBotsId)
                .Test("reallife_mkt_bot_test_month", "reallife_mkt_inv_test_work")
                .Fact("bot_capital_least", () => Guide.Money(Prices.At((float)MarketBots.CapitalLeastAtStart)))
                .Fact("bot_fee_least", () => Guide.Money(Prices.At((float)MarketBots.FeeLeastAtStart)))
                .Fact("bot_trade_least", () => Guide.Money(Prices.At((float)MarketBots.TradeLeastAtStart)))
                .Fact("bot_listed_days", () => Whole(MarketBots.ListedDaysLeast))
                .Fact("bot_sizes", () => List(MarketBots.Sizes))
                .Fact("bot_stops", () => List(MarketBots.Stops, 1))
                .Fact("bot_losses", () => List(MarketBots.Losses))
                .Fact("bot_trend_fast", () => Whole(MarketBots.TrendFast))
                .Fact("bot_trend_slow", () => Whole(MarketBots.TrendSlow))
                .Fact("bot_rsi_days", () => Whole(MarketBots.RsiDays))
                .Fact("bot_rsi_in", () => Whole(MarketBots.RsiIn))
                .Fact("bot_rsi_out", () => Whole(MarketBots.RsiOut))
                .Fact("bot_reversion_filter", () => Whole(MarketBots.ReversionFilter))
                .Fact("bot_reversion_days", () => Whole(MarketBots.ReversionDaysMost))
                .Fact("bot_momentum_days", () => Whole(MarketBots.MomentumDays))
                .Fact("bot_momentum_skip", () => Whole(MarketBots.MomentumSkip))
                .Fact("bot_income_quarters", () => Whole(MarketBots.IncomeQuarters))
                .Fact("bot_trend", () => MarketBots.All[0].Name)
                .Fact("bot_reversion", () => MarketBots.All[1].Name)
                .Fact("bot_momentum", () => MarketBots.All[2].Name)
                .Fact("bot_income", () => MarketBots.All[3].Name)
                .Fact("bot_trend_fee", () => Bp(MarketBots.All[0].FeeBp))
                .Fact("bot_reversion_fee", () => Bp(MarketBots.All[1].FeeBp))
                .Fact("bot_momentum_fee", () => Bp(MarketBots.All[2].FeeBp))
                .Fact("bot_income_fee", () => Bp(MarketBots.All[3].FeeBp))
                .Fact("bot_trend_share", () => Bp(MarketBots.All[0].ShareBp))
                .Fact("bot_reversion_share", () => Bp(MarketBots.All[1].ShareBp))
                .Fact("bot_momentum_share", () => Bp(MarketBots.All[2].ShareBp))
                .Fact("bot_income_share", () => Bp(MarketBots.All[3].ShareBp))
                .Fact("bot_participation", () => Whole(MarketCatalogue.ParticipationPct) + "%")
                .Fact("bot_picks_most", () => Whole(MarketBots.PicksMost))
                .Fact("bot_own_volume", () => Whole(MarketBots.OwnVolumePct) + "%")
                .Fact("bot_own_swing", () => Whole(MarketBots.OwnSwingDays(System.Math.Max(8, GameCalendar.DaysPerYear))))
                .Fact("bot_deal_year", () => Bp(ListingCatalogue.FounderDealMostBpYear))
                .Fact("bot_founder_most", () => Bp(ListingCatalogue.FounderMostBp));
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }

        private static string Bp(int bp)
        {
            return Guide.Percent(bp / 10000f);
        }

        // "10%, 20%, 25%, 50% or 100%" in the language's words; from 'skip' on (a 0 that means none is left out).
        private static string List(int[] values, int skip = 0)
        {
            var text = new StringBuilder(48);
            for (int i = skip; i < values.Length; i++)
            {
                if (i > skip)
                {
                    text.Append(i == values.Length - 1 ? " " + "reallife_mkt_bot_list_or".GetLocalization() + " " : ", ");
                }
                text.Append(values[i].ToString(Inv)).Append('%');
            }
            return text.ToString();
        }
    }
}
