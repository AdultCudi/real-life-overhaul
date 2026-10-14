using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The bonds' words (Markets/Bonds): a bond's name, its term, how often it pays, why something cannot be done. Every
    /// text is a key of en.json; written when a page is written, a notification goes out or a transaction is made, never
    /// per frame.
    /// </summary>
    public static class BondText
    {
        private static readonly string[] KindKeys = { "reallife_mkt_bond_kind_treasury", "reallife_mkt_bond_kind_city", "reallife_mkt_bond_kind_deposit" };
        private static readonly string[] NameKeys = { "reallife_mkt_bond_name_treasury", "reallife_mkt_bond_name_city", "reallife_mkt_bond_name_deposit" };
        private static readonly string[] WhyKeys =
        {
            "reallife_mkt_bond_why_none", "reallife_mkt_bond_why_closed", "reallife_mkt_bond_why_held", "reallife_mkt_bond_why_behind", "reallife_mkt_bond_why_off", "reallife_mkt_bond_why_too_little",
            "reallife_mkt_bond_why_too_much", "reallife_mkt_bond_why_no_cash", "reallife_mkt_bond_why_full", "reallife_mkt_bond_why_not_held", "reallife_mkt_bond_why_no_bank"
        };

        /// <summary>A bond's name: who issues it and its term ("Treasury bond, 2 years", "Deposit at a bank, 3 months").</summary>
        public static string Name(BondDef d, string issuer)
        {
            if (d == null)
            {
                return string.Empty;
            }
            return MarketText.Fill(NameKeys[Kind(d)], "term", Term(d.TermQuarters), "bank", issuer ?? string.Empty);
        }

        /// <summary>A bond held, by its name (for the game's transactions, in the language of the moment).</summary>
        public static string Name(BondHolding h)
        {
            return h != null ? Name(BondCatalogue.Find(h.Def), h.Issuer) : string.Empty;
        }

        public static string KindWords(BondDef d)
        {
            return d != null ? MarketText.T(KindKeys[Kind(d)]) : string.Empty;
        }

        private static int Kind(BondDef d)
        {
            int k = (int)d.Kind;
            return k < 0 || k >= KindKeys.Length ? 0 : k;
        }

        /// <summary>A term in words: "3 months", "1 year", "5 years".</summary>
        public static string Term(int quarters)
        {
            if (quarters % 4 != 0)
            {
                return MarketText.Fill("reallife_mkt_bond_term_months", "count", (quarters * 3).ToString());
            }
            return quarters == 4 ? MarketText.T("reallife_mkt_bond_term_year") : MarketText.Fill("reallife_mkt_bond_term_years", "count", (quarters / 4).ToString());
        }

        /// <summary>How often a bond pays: every month, every quarter, or once at its end.</summary>
        public static string Pays(BondDef d)
        {
            if (d == null || d.Payments <= 1)
            {
                return MarketText.T("reallife_mkt_bond_pays_end");
            }
            return MarketText.T(d.PaysPerYear >= 12 ? "reallife_mkt_bond_pays_month" : "reallife_mkt_bond_pays_quarter");
        }

        /// <summary>A rate a year in basis points: "3.25%".</summary>
        public static string Rate(int bp)
        {
            return (bp / 100f).ToString("0.00") + "%";
        }

        /// <summary>Why something cannot be done with a bond now, as a sentence.</summary>
        public static string Why(BondWhy why)
        {
            int i = (int)why;
            return MarketText.T(i >= 0 && i < WhyKeys.Length ? WhyKeys[i] : WhyKeys[0]);
        }

        /// <summary>Cents as a sum of money in whole units.</summary>
        public static string Money(long cents)
        {
            return MarketText.Money(cents / 100);
        }

        /// <summary>A price for each 100 it repays: "98.75".</summary>
        public static string Price(double per100)
        {
            return per100.ToString("0.00");
        }
    }
}
