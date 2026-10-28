using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a transaction of the mod's is to the business it is booked at, in the game's daily statement (BizMan's average
    /// daily income, the Finance app's business pages, the game's tax and bank read that statement).</summary>
    public enum BizLine
    {
        /// <summary>Not one of the businesses' lines this table keeps: another feature's own accounting (the company's, the city's, the estate agency's).</summary>
        Foreign = 0,
        /// <summary>The business's takings: sales in the statement.</summary>
        Takings = 1,
        /// <summary>A running cost: a row among the statement's goods (its own, or the running costs row); profit falls by it.</summary>
        Cost = 2,
        /// <summary>Capital, financing, a deposit or a sale of an asset: money that moves but is no income or running cost, so it stays out of the statement.</summary>
        Capital = 3,
        /// <summary>A key of the businesses this table keeps that is in no list: the self-check fails on it, and so does the offline check.</summary>
        Unlisted = 4
    }

    /// <summary>
    /// Every transaction key of the businesses the mod runs on its own (the tech firms and data centres, the broadband company, the
    /// player's bank, the construction company, the academies, and the fire and hold-up losses at a shop) is in exactly one list
    /// here: takings, a running cost, or capital. <see cref="GameBooks.Refresh"/> reads the day's statement from this table, so
    /// a business of the mod's counts its takings and costs in BizMan as the game's own shops do, once. Game-free; EconomyCheck
    /// reads the sources and fails on a key of these features that no list holds. Nothing here allocates once built.
    /// </summary>
    public static class BizLines
    {
        /// <summary>The row among a statement's goods a running cost with no row of its own goes in.</summary>
        public const string RunningRow = "reallife_books_running_costs";

        private const string P = "reallife_transaction_";

        // The features whose keys this table keeps: a key of theirs in no list is a mistake.
        private static readonly string[] Domain =
        {
            P + "tech_", P + "broadband_", P + "ownbank_", P + "bld_", P + "study_academy", P + "fire_repair", P + "holdup_till"
        };

        /// <summary>Takings: the sales of the business (a refund is the same key, negative).</summary>
        public static readonly string[] TakingsKeys =
        {
            P + "tech_takings", P + "tech_cloud_takings", P + "broadband_takings", P + "ownbank_takings",
            P + "study_academy", P + "bld_contract"
        };

        /// <summary>Running costs.</summary>
        public static readonly string[] CostKeys =
        {
            P + "tech_hosting", P + "tech_software", P + "tech_compute", P + "tech_util_power", P + "tech_util_water", P + "tech_marketing",
            P + "tech_wages", P + "tech_severance_wage", P + "tech_training", P + "tech_bonus_clause", P + "tech_crew_wages", P + "tech_upkeep",
            P + "tech_it_services", P + "tech_data_fine", P + "tech_register_fee", P + "tech_cloud_licence_fee",
            P + "tech_recruitment_ad", P + "tech_recruitment_checks", P + "tech_recruitment_search",
            P + "broadband_network", P + "broadband_installs", P + "broadband_marketing", P + "broadband_wages", P + "broadband_licence",
            P + "ownbank_savers", P + "ownbank_losses", P + "ownbank_wages", P + "ownbank_systems", P + "ownbank_fine",
            P + "bld_crews", P + "bld_plant", P + "bld_fees", P + "bld_interest", P + "bld_land_tax", P + "bld_vacant_tax", P + "bld_cover",
            P + "fire_repair", P + "holdup_till"
        };

        /// <summary>Capital, financing, deposits and sales of assets: no income and no running cost, so not in the statement.</summary>
        public static readonly string[] CapitalKeys =
        {
            P + "tech_investment_racks", P + "tech_investment_fitout", P + "tech_relief", P + "tech_write_down",
            P + "tech_venture_bridge_interest", P + "tech_venture_bridge_loan", P + "tech_venture_diligence_fee", P + "tech_venture_fund",
            P + "tech_venture_fund_back", P + "tech_venture_investment", P + "tech_venture_raise", P + "tech_venture_return", P + "tech_venture_share_sale",
            P + "broadband_investment", P + "broadband_investment_sale",
            P + "ownbank_licence", P + "ownbank_investment_capital", P + "ownbank_investment_covered", P + "ownbank_investment_dividend",
            P + "ownbank_investment_kept", P + "ownbank_investment_pay", P + "ownbank_investment_sale",
            P + "bld_builder", P + "bld_builder_back", P + "bld_buyout", P + "bld_cash", P + "bld_conversion", P + "bld_conversion_back",
            P + "bld_demolition", P + "bld_demolition_back", P + "bld_fees_back", P + "bld_insurance", P + "bld_investor_in", P + "bld_investor_out",
            P + "bld_land", P + "bld_land_back", P + "bld_land_rival", P + "bld_land_rival_sold", P + "bld_land_sold", P + "bld_loan",
            P + "bld_materials", P + "bld_plant_buy", P + "bld_plant_sale", P + "bld_refit", P + "bld_refit_back", P + "bld_repay",
            P + "bld_ring_cut", P + "bld_sales", P + "bld_show", P + "bld_show_fit", P + "bld_test_furnish", P + "bld_test_land"
        };

        private static readonly Dictionary<string, BizLine> Lines = Build();

        private static Dictionary<string, BizLine> Build()
        {
            var lines = new Dictionary<string, BizLine>(128, StringComparer.Ordinal);
            for (int i = 0; i < TakingsKeys.Length; i++)
            {
                lines[TakingsKeys[i]] = BizLine.Takings;
            }
            for (int i = 0; i < CostKeys.Length; i++)
            {
                lines[CostKeys[i]] = BizLine.Cost;
            }
            for (int i = 0; i < CapitalKeys.Length; i++)
            {
                lines[CapitalKeys[i]] = BizLine.Capital;
            }
            return lines;
        }

        /// <summary>True if the key is one of the features this table keeps (listed or not).</summary>
        public static bool InDomain(string type)
        {
            if (type == null)
            {
                return false;
            }
            for (int i = 0; i < Domain.Length; i++)
            {
                if (type.StartsWith(Domain[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>What a transaction type is to its business: <see cref="BizLine.Foreign"/> for another feature's, <see cref="BizLine.Unlisted"/> for a key of these features in no list.</summary>
        public static BizLine Of(string type)
        {
            if (type == null)
            {
                return BizLine.Foreign;
            }
            return Lines.TryGetValue(type, out BizLine line) ? line : InDomain(type) ? BizLine.Unlisted : BizLine.Foreign;
        }

        /// <summary>
        /// The self-check's line for a day: "BizMan check (income): N of N businesses run by the mod add their takings and costs to BizMan:
        /// PASS", or FAIL naming the businesses that do not and why.
        /// </summary>
        public static string Verdict(int day, int businesses, int good, string failures)
        {
            var text = new StringBuilder(160);
            text.Append("BizMan check (income, day ").Append(day).Append("): ").Append(good).Append(" of ").Append(businesses)
                .Append(" businesses run by the mod add their takings and costs to BizMan: ");
            if (good == businesses && string.IsNullOrEmpty(failures))
            {
                text.Append("PASS");
            }
            else
            {
                text.Append("FAIL (").Append(failures).Append(')');
            }
            return text.ToString();
        }

        /// <summary>The offline test of the table over a list of keys: every key of the features in a list, none in two. Returns what is wrong, or null.</summary>
        public static string Audit(IEnumerable<string> keys)
        {
            var wrong = new StringBuilder();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                if (InDomain(key) && Of(key) == BizLine.Unlisted && seen.Add(key))
                {
                    wrong.Append(key).Append(' ');
                }
            }
            var all = new List<string>(Lines.Count);
            all.AddRange(TakingsKeys);
            all.AddRange(CostKeys);
            all.AddRange(CapitalKeys);
            var once = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < all.Count; i++)
            {
                if (!once.Add(all[i]))
                {
                    wrong.Append("in two lists: ").Append(all[i]).Append(' ');
                }
            }
            return wrong.Length == 0 ? null : wrong.ToString().TrimEnd();
        }
    }
}
