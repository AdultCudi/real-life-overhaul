using System.Collections.Generic;
using Localizor;

namespace RealLifeOverhaul.Utilities
{
    public static partial class UtilitiesGame
    {
        public static List<object> CompanionOutside()
        {
            return CompanionTransactions.Rows(new Dictionary<string, string>
            {
                ["reallife_transaction_util_power"] = "utilities.power",
                ["reallife_transaction_util_water"] = "utilities.water",
                ["reallife_transaction_util_net_bill"] = "utilities.internet"
            });
        }

        public static object CompanionTaxLabel()
        {
            return CompanionSnapshot.Row("label", BillsDeductKey.GetLocalization(), "kind", "recurring");
        }
    }
}
