using System.Collections.Generic;
using Localizor;

namespace RealLifeOverhaul.Utilities
{
    public static partial class UtilitiesGame
    {
        /// <summary>The utility bills paid, by service. Read only during a capture, nothing per frame.</summary>
        public static List<object> CompanionOutside()
        {
            return CompanionTransactions.Rows(new Dictionary<string, string>
            {
                ["reallife_transaction_util_power"] = "utilities.power",
                ["reallife_transaction_util_water"] = "utilities.water",
                ["reallife_transaction_util_net_bill"] = "utilities.internet"
            });
        }

        /// <summary>The broadband company's deduction label, which recurs.</summary>
        public static object CompanionCompanyTaxLabel()
        {
            return CompanionSnapshot.Row("label", CompanyDeductKey.GetLocalization(), "kind", "recurring");
        }

        /// <summary>The bills' deduction label, which recurs.</summary>
        public static object CompanionTaxLabel()
        {
            return CompanionSnapshot.Row("label", BillsDeductKey.GetLocalization(), "kind", "recurring");
        }
    }
}
