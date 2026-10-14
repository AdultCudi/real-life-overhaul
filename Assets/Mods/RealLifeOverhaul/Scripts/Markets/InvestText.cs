namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// A large investor's words (InvestRules): what each holding is, the income of each kind, a share put back, a bond that
    /// rolls over. Every text is a key of en.json; written when a page is written or a notification goes out, never per frame.
    /// </summary>
    public static class InvestText
    {
        /// <summary>What each kind of holding is called (HoldingKind's order), for the Portfolio tab's column and its filter.</summary>
        public static readonly string[] KindKeys =
        {
            "reallife_mkt_inv_kind_stock", "reallife_mkt_inv_kind_trust", "reallife_mkt_inv_kind_treasury", "reallife_mkt_inv_kind_city", "reallife_mkt_inv_kind_deposit"
        };

        // What each kind pays, by the same order: dividends, a trust's distributions, a bond's interest, a deposit's.
        private static readonly string[] IncomeKeys =
        {
            "reallife_mkt_inv_income_dividends", "reallife_mkt_inv_income_trust", "reallife_mkt_inv_income_treasury", "reallife_mkt_inv_income_city", "reallife_mkt_inv_income_deposit"
        };

        public static string Kind(HoldingKind kind)
        {
            int i = (int)kind;
            return MarketText.T(KindKeys[i >= 0 && i < KindKeys.Length ? i : 0]);
        }

        public static string Income(HoldingKind kind)
        {
            int i = (int)kind;
            return MarketText.T(IncomeKeys[i >= 0 && i < IncomeKeys.Length ? i : 0]);
        }

        /// <summary>The share of each dividend a holding puts back, as a choice: "Pay each dividend to me", "Put 25% of each dividend back".</summary>
        public static string Reinvest(int pct)
        {
            return pct <= 0 ? MarketText.T("reallife_mkt_inv_set_back_none") : MarketText.Fill("reallife_mkt_inv_set_back", "pct", pct.ToString());
        }

        /// <summary>A bond's end as a choice: repaid into the cash, or rolled over into a new one.</summary>
        public static string Rollover(bool on)
        {
            return MarketText.T(on ? "reallife_mkt_bond_set_roll_yes" : "reallife_mkt_bond_set_roll_no");
        }

        /// <summary>A small mark after a name: "  25% of dividends put back" in the smaller size the app's marks have.</summary>
        public static string Mark(string words)
        {
            return string.IsNullOrEmpty(words) ? string.Empty : "  <size=80%>" + words + "</size>";
        }
    }
}
