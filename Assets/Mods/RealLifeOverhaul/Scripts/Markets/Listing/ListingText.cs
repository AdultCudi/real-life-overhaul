using System.Text;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The words of a player's company on the market (Markets/Listing): every text is a key of en.json; figures come
    /// from the catalogue. Built when a page is written or a notification goes out, never per frame.
    /// </summary>
    public static class ListingText
    {
        private const string P = "reallife";   // (apart, so that a key made from parts is not taken for a whole one)

        private static readonly string[] WhyKeys = Keys(P + "_lst_why_", "none", "not_listed", "shut", "held", "closed", "locked_up", "no_money", "no_shares", "too_much", "too_large",
            "board", "bid", "reserve", "control", "suspended", "off", "not_ready", "year_limit");

        private static readonly string[] DemandKeys = Keys(P + "_lst_demand_", "none", "payout", "pay", "board", "sale");

        private static readonly string[] StageKeys = Keys(P + "_lst_stage_", "none", "advised", "auditing", "audited", "building", "priced", "listed");

        private static readonly string[] NeedKeys = Keys(P + "_lst_need_", "years", "quarters", "turnover", "profit", "profitable", "audit");

        private static readonly int[] FindingBits = { ListingBook.BackTaxes, ListingBook.LateReturns, ListingBook.CashOffBooks, ListingBook.Bankrupt, ListingBook.BribesOut };
        private static readonly string[] FindingKeys = Keys(P + "_lst_found_", "back_taxes", "late", "cash", "bankrupt", "bribes");

        private static string[] Keys(string prefix, params string[] names)
        {
            var keys = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                keys[i] = prefix + names[i];
            }
            return keys;
        }

        private static string Of(string[] keys, int i)
        {
            return MarketText.T(i >= 0 && i < keys.Length ? keys[i] : keys[0]);
        }

        /// <summary>Why something cannot be done, as a sentence.</summary>
        public static string Why(OwnWhy why)
        {
            if (why == OwnWhy.TooMuch)
            {
                return MarketText.Fill("reallife_lst_why_too_much", "most", MarketText.Percent(ListingCatalogue.FounderMostBp));
            }
            if (why == OwnWhy.Control)
            {
                return MarketText.Fill("reallife_lst_why_control", "least", MarketText.Percent(ListingCatalogue.PrivateLeastBp));
            }
            if (why == OwnWhy.YearLimit)
            {
                return MarketText.Fill("reallife_lst_why_year_limit", "deals", MarketText.Percent(ListingCatalogue.FounderDealMostBpYear), "back", MarketText.Percent(ListingCatalogue.BuyBackMostBp));
            }
            return Of(WhyKeys, (int)why);
        }

        public static string Demand(ActivistDemand demand)
        {
            return Of(DemandKeys, (int)demand);
        }

        public static string Stage(ListingStage stage)
        {
            return Of(StageKeys, (int)stage);
        }

        public static string Need(int which)
        {
            return Of(NeedKeys, which);
        }

        public static string PrivateBuyer()
        {
            return MarketText.T("reallife_lst_private_buyer");
        }

        /// <summary>What the auditors found, as a list; "nothing to report" for nothing.</summary>
        public static string Findings(int bits)
        {
            var t = new StringBuilder(64);
            for (int i = 0; i < FindingBits.Length; i++)
            {
                if ((bits & FindingBits[i]) != 0)
                {
                    t.Append(t.Length > 0 ? ", " : string.Empty).Append(MarketText.T(FindingKeys[i]));
                }
            }
            return t.Length > 0 ? t.ToString() : MarketText.T("reallife_lst_found_none");
        }

        /// <summary>What came of a shareholders' meeting, as a list of what was decided.</summary>
        public static string Meeting(int bits)
        {
            var t = new StringBuilder(96);
            Add(t, bits, ListingVotes.PayLost, "reallife_lst_meeting_pay_lost");
            Add(t, bits, ListingVotes.PayoutVoted, "reallife_lst_meeting_payout");
            Add(t, bits, ListingVotes.BoardLost, "reallife_lst_meeting_board_lost");
            if ((bits & ListingVotes.HadDemand) != 0)
            {
                Add(t, bits, ListingVotes.HadDemand, (bits & ListingVotes.DemandPassed) != 0 ? "reallife_lst_meeting_demand_passed" : "reallife_lst_meeting_demand_failed");
            }
            return t.Length > 0 ? t.ToString() : MarketText.T("reallife_lst_meeting_carried");
        }

        private static void Add(StringBuilder t, int bits, int bit, string key)
        {
            if ((bits & bit) != 0)
            {
                t.Append(t.Length > 0 ? "; " : string.Empty).Append(MarketText.T(key));
            }
        }
    }
}
