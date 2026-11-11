namespace RealLifeOverhaul.Markets
{
    /// <summary>The extras' words: why something cannot be done, and a split as the charts and the pages say it. Keys only; the texts are the locales'.</summary>
    public static class ExtrasText
    {
        private static string Of(string[] keys, int at)
        {
            return MarketText.T(keys[at >= 0 && at < keys.Length ? at : 0]);
        }

        public static string Why(SplitWhy why) => Of(ExtrasKeys.SplitWhyKeys, (int)why);

        public static string Why(FundWhy why) => Of(ExtrasKeys.FundWhyKeys, (int)why);

        public static string Why(BlockWhy why) => Of(ExtrasKeys.BlockWhyKeys, (int)why);

        public static string Why(BankWhy why) => Of(ExtrasKeys.BankWhyKeys, (int)why);

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
