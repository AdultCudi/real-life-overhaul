using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>Projects explicitly owner-classified transaction rows. Native statement amounts are never rewritten.</summary>
    internal static class CompanionTransactions
    {
        public static List<object> Rows(Dictionary<string, string> categories)
        {
            var rows = new List<object>();
            foreach (Transaction transaction in SaveGameManager.Current.Transactions)
            {
                if (transaction == null || transaction.timestamp == null
                    || !categories.TryGetValue(transaction.transactionType, out string category))
                {
                    continue;
                }
                rows.Add(CompanionSnapshot.Row("scope", transaction.address == null ? "company" : "place",
                    "address", transaction.address == null ? null
                        : CompanionValues.Address(transaction.address.streetName, transaction.address.streetNumber),
                    "category", category, "basis", "paid", "fromDay", transaction.timestamp.Day,
                    "toDay", transaction.timestamp.Day, "cents", -CompanionValues.Cents(transaction.amount)));
            }
            return rows;
        }
    }
}
