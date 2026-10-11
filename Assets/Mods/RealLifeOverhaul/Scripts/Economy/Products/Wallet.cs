using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The accounts' side of the player's cash: the game's own transaction call (GameManager.ChangeMoneySafe), so
    /// everything the accounts take or pay is in the game's transaction history under its own name. A payment is
    /// booked under the game's loan expenses, as its own loan payments are. During the day's run the transactions
    /// are dated on the day that has ended (<see cref="BookDay"/>), as the game dates its own run.
    /// </summary>
    public sealed class Wallet : IWallet
    {
        private const string LoanExpenses = "ba:transactioncategory_loanexpenses";   // the game's own category

        /// <summary>The day transactions are dated on; null for now.</summary>
        public int? BookDay;

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

        /// <summary>Takes what there is of an amount, and says how much that was.</summary>
        public int TakeUpTo(int amount, string why, string name)
        {
            int take = Mathf.Min(amount, Cash);
            return take > 0 && Take(take, why, name) ? take : 0;
        }

        // (a dictionary each: the game keeps hold of a transaction's data for as long as it lists the transaction)
        private static TransactionInfo Info(string why, string name, bool expense)
        {
            var data = new Dictionary<string, string>(1) { { "name", name ?? string.Empty } };
            return expense && (why == Money.Payment || why == Money.LateFee) ? new TransactionInfo(why, LoanExpenses, data) : new TransactionInfo(why, data);
        }
    }
}
