using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Read-only observations of additional credit. Principal never becomes a statement adjustment.</summary>
    public static class CreditCompanion
    {
        /// <summary>One row per open account that no test made; a read, so no payment, interest or step is taken.</summary>
        public static List<object> Accounts(List<CreditAccount> accounts, int today)
        {
            var rows = new List<object>();
            foreach (CreditAccount account in accounts)
            {
                if (account.Closed || account.Test)
                {
                    continue;
                }
                rows.Add(CompanionSnapshot.Row("id", account.Id.ToString(CultureInfo.InvariantCulture),
                    "kind", account.Key, "balanceCents", (long)account.Owed * 100,
                    "paymentTomorrowCents", (long)AccountRules.DueTomorrow(account, today) * 100,
                    "annualRateBasisPoints", account.RateNow, "frozen", account.Frozen,
                    "includedInGameLoans", false));
            }
            return rows;
        }
    }
}
