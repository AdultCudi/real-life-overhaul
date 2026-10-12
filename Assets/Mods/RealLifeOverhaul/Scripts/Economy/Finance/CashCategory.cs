using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What a movement of money was for, told from its transaction type: the game's own ("ba:transaction_rent") or
    /// the mod's ("reallife_transaction_shares_bought"). The game gives a category to only eleven of its types, so
    /// the type's own name is read instead: the words in it say what it is. A type is read once and remembered.
    /// Game-free, so the offline checks run it.
    /// </summary>
    public static class CashCategory
    {
        public const int Takings = 0, Wages = 1, Rent = 2, Stock = 3, Loans = 4, Tax = 5, Fees = 6, Marketing = 7, Investments = 8, Other = 9, Count = 10;

        public static readonly string[] Keys =
        {
            "reallife_tab_cash_takings", "reallife_tab_cash_wages", "reallife_tab_cash_rent", "reallife_tab_cash_stock", "reallife_tab_cash_loans", "reallife_tab_cash_tax",
            "reallife_tab_cash_fees", "reallife_tab_cash_marketing", "reallife_tab_cash_investments", "reallife_tab_cash_other"
        };

        // Looked for in this order; the first that is in the type's name says what it is. "taxi" is no tax and
        // "coffee" no fee, rent revenue is rent, a late fee belongs to the loan it is charged on, and what was sold
        // for good (a building, shares) is an investment, not a day's takings.
        private static readonly string[][] Words =
        {
            new[] { "investment", "shares_", "venture_", "buildingbought", "buildingsold", "boatbought", "boatsold", "vehiclebought" },
            new[] { "loan", "interest", "credit_", "overdraft", "finance_", "consolidation", "arrangement", "bankruptcy", "late_fee", "repossession", "diverted" },
            new[] { "wage", "salary", "bonus", "overtime", "co_sick", "co_raise", "training", "healthinsurance", "replacement", "recruitment", "strike", "_job_", "prison_work" },
            new[] { "rent", "util_" },   // rent and the bills of a place: power and water (Scripts/Utilities)
            new[] { "itempurchase", "deliverycontract", "importdelivery" },
            new[] { "revenue", "takings", "inventorysold", "itemsold", "exportedgoods", "bulkorder" },
            new[] { "marketing", "campaign", "voucher" },
            new[] { "fee", "fine", "ticket", "licens", "licenc", "penalty", "impound", "towing", "bail", "court", "legal", "lawyer", "civil" }
        };

        private static readonly int[] Of = { Investments, Loans, Wages, Rent, Stock, Takings, Marketing, Fees };
        private static readonly string[] TaxWords = { "tax", "levy" };
        private static readonly Dictionary<string, int> Known = new Dictionary<string, int>(128);

        /// <summary>The category of a transaction type.</summary>
        public static int For(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                return Other;
            }
            int category;
            if (!Known.TryGetValue(type, out category))
            {
                category = Read(type);
                Known[type] = category;
            }
            return category;
        }

        private static int Read(string type)
        {
            if (type.IndexOf("taxi", StringComparison.OrdinalIgnoreCase) < 0 && Has(type, TaxWords))
            {
                return Tax;
            }
            for (int i = 0; i < Words.Length; i++)
            {
                if (Has(type, Words[i]) && (Of[i] != Fees || type.IndexOf("coffee", StringComparison.OrdinalIgnoreCase) < 0))
                {
                    return Of[i];
                }
            }
            return Other;
        }

        private static bool Has(string type, string[] words)
        {
            for (int i = 0; i < words.Length; i++)
            {
                if (type.IndexOf(words[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
