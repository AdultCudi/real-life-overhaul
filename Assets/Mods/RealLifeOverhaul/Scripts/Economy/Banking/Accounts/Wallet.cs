using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The accounts' cash through the game's own transaction call, so every movement is in the game's history; a payment is
    /// booked as a loan expense, as the game's own are.
    /// </summary>
    public sealed class Wallet : IWallet
    {
        private const string LoanExpenses = "ba:transactioncategory_loanexpenses";   // the game's own category

        public int? BookDay;   // the day transactions are dated on (the day's run books on the day that ended); null for now

        public int Cash => SaveGameManager.Current != null ? Mathf.FloorToInt(Mathf.Max(0f, SaveGameManager.Current.Money)) : 0;

        public bool Take(int amount, string why, string name)
        {
            return amount <= 0 || (SaveGameManager.Current != null && GameManager.ChangeMoneySafe(-amount, Info(why, name, true), BookDay, null, false));
        }

        public void Give(int amount, string why, string name)
        {
            if (amount > 0 && SaveGameManager.Current != null)
            {
                GameManager.ChangeMoneySafe(amount, Info(why, name, false), BookDay, null, true);
            }
        }

        /// <summary>Takes an amount whether or not it is there: the game lets cash go under nothing, as its own loan interest does.</summary>
        public void Force(int amount, string why, string name)
        {
            if (amount > 0 && SaveGameManager.Current != null)
            {
                GameManager.ChangeMoneySafe(-amount, Info(why, name, true), BookDay, null, true);
            }
        }

        /// <summary>Takes what there is of an amount. Returns how much that was.</summary>
        public int TakeUpTo(int amount, string why, string name)
        {
            int take = Mathf.Min(amount, Cash);
            return take > 0 && Take(take, why, name) ? take : 0;
        }

        // A dictionary each: the game keeps a transaction's data for as long as it lists the transaction.
        private static TransactionInfo Info(string why, string name, bool expense)
        {
            var data = new Dictionary<string, string>(1) { { "name", name ?? string.Empty } };
            return expense && (why == Money.Payment || why == Money.LateFee) ? new TransactionInfo(why, LoanExpenses, data) : new TransactionInfo(why, data);
        }
    }
}
