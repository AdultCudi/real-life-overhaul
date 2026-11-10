namespace RealLifeOverhaul.Markets
{
    /// <summary>The extras' words: why something cannot be done, and a split as the charts and the pages say it. Keys only; the texts are the locales'.</summary>
    public static class ExtrasText
    {
        /// <summary>A sentence for each <see cref="SplitWhy"/>, in its order; the first is never shown.</summary>
        public static readonly string[] SplitWhyKeys =
        {
            "reallife_stx_why_none", "reallife_stx_why_off", "reallife_stx_split_why_not_mine", "reallife_stx_split_why_firm", "reallife_stx_split_why_board",
            "reallife_stx_split_why_pending", "reallife_stx_split_why_bid", "reallife_stx_split_why_ratio", "reallife_stx_split_why_low", "reallife_stx_split_why_high"
        };

        /// <summary>A sentence for each <see cref="FundWhy"/>.</summary>
        public static readonly string[] FundWhyKeys =
        {
            "reallife_stx_why_none", "reallife_stx_why_off", "reallife_stx_why_closed", "reallife_stx_why_held", "reallife_stx_fund_why_bad", "reallife_stx_why_money",
            "reallife_stx_fund_why_units", "reallife_stx_fund_why_large", "reallife_stx_fund_why_limit"
        };

        /// <summary>A sentence for each <see cref="BlockWhy"/>.</summary>
        public static readonly string[] BlockWhyKeys =
        {
            "reallife_stx_why_none", "reallife_stx_why_off", "reallife_stx_why_closed", "reallife_stx_why_held", "reallife_stx_block_why_listed", "reallife_stx_block_why_founders",
            "reallife_stx_block_why_bid", "reallife_stx_block_why_young", "reallife_stx_block_why_small", "reallife_stx_block_why_limit", "reallife_stx_block_why_shares",
            "reallife_stx_why_money", "reallife_stx_block_why_margin"
        };

        /// <summary>A sentence for each <see cref="BankWhy"/>.</summary>
        public static readonly string[] BankWhyKeys =
        {
            "reallife_stx_why_none", "reallife_stx_why_off", "reallife_stx_bank_why_none", "reallife_stx_bank_why_test", "reallife_stx_bank_why_young", "reallife_stx_bank_why_books",
            "reallife_stx_bank_why_profit", "reallife_stx_bank_why_regulator", "reallife_stx_bank_why_company", "reallife_stx_bank_why_way", "reallife_stx_bank_why_listed",
            "reallife_stx_why_money", "reallife_stx_bank_why_float", "reallife_stx_bank_why_full"
        };

        private static string Of(string[] keys, int at)
        {
            return MarketText.T(keys[at >= 0 && at < keys.Length ? at : 0]);
        }

        public static string Why(SplitWhy why) => Of(SplitWhyKeys, (int)why);

        public static string Why(FundWhy why) => Of(FundWhyKeys, (int)why);

        public static string Why(BlockWhy why) => Of(BlockWhyKeys, (int)why);

        public static string Why(BankWhy why) => Of(BankWhyKeys, (int)why);

        /// <summary>A ratio as the founder chooses it: "2 for 1", or "1 for 10" for shares put together.</summary>
        public static string Ratio(int ratio)
        {
            return ratio > 0 ? MarketText.Fill("reallife_stx_ratio", "new", ratio.ToString(), "old", "1") : MarketText.Fill("reallife_stx_ratio", "new", "1", "old", (-ratio).ToString());
        }

        /// <summary>A split that took effect, as the chart's reading says it.</summary>
        public static string Note(in SplitNote n)
        {
            return MarketText.Fill(n.Reverse ? "reallife_stx_chart_reverse" : "reallife_stx_chart_split", "new", n.New.ToString(), "old", n.Old.ToString(), "day", n.Day.ToString());
        }

        /// <summary>A company's split to come, in the few words its status has: the old key for a split, its own for shares put together.</summary>
        public static string Status(Stock k)
        {
            return k.SplitRatio < 0 ? MarketText.Fill("reallife_stx_status_reverse", "ratio", (-k.SplitRatio).ToString(), "day", k.SplitDay.ToString())
                : MarketText.Fill("reallife_mkt_status_split", "ratio", k.SplitRatio.ToString(), "day", k.SplitDay.ToString());
        }
    }
}
