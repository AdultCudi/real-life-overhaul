using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    internal static partial class CompanyGame
    {
        /// <summary>
        /// The company's cover and tax rows outside the statements, by category. Read only during a capture, nothing
        /// per frame.
        /// </summary>
        public static List<object> CompanionOutside()
        {
            return CompanionTransactions.Rows(new Dictionary<string, string>
            {
                [PremiumKey] = "cover.premium", [AccountantKey] = "tax.accountant", [PenaltyKey] = "tax.penalty",
                [ErrorKey] = "tax.penalty", [RebateKey] = "tax.rebate", [BackTaxKey] = "tax.backtax"
            });
        }
    }
}
