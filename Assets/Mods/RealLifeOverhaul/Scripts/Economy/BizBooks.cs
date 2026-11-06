using System;
using System.Collections.Generic;
using Entities;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The one call every money line of the mod goes through (the game's GameManager.ChangeMoneySafe is called nowhere else; EconomyCheck
    /// reads every source for it). The money is booked once, as the game books it: the bank statement, the tax's deductions and the
    /// balance. What BizMan shows of a business, its average daily income and the Finance app's pages, is the game's daily statement,
    /// which the game builds from its own tills; the mod's lines reach it by their address and their key (<see cref="BizLines"/>,
    /// <see cref="GameBooks.Refresh"/>, once a day, as the day's books are brought up to date). This call sees that every line at a
    /// business has a key the table knows, and says so once in the log when one has not. COST: one dictionary look-up per payment, no allocation.
    /// </summary>
    public static class BizBooks
    {
        private static readonly HashSet<string> Told = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The log of the mod, for the one line a key of the table's features that no list holds. Set by the wiring; null before.</summary>
        public static Action<string> Warn;

        /// <summary>Keys booked at a business that the table has in no list, this session.</summary>
        public static int Unlisted { get; private set; }

        /// <summary>
        /// Books a money line as the game's own call does (the same arguments and result): a payment or a taking, at a business when
        /// <paramref name="address"/> is given, dated <paramref name="dayOfTransaction"/> or today.
        /// </summary>
        public static bool Book(float amount, TransactionInfo transactionInfo, int? dayOfTransaction = null, Address address = null, bool force = false, bool showNotification = false)
        {
            if (address != null && transactionInfo != null && BizLines.Of(transactionInfo.Type) == BizLine.Unlisted)
            {
                Note(transactionInfo.Type);
            }
            return GameManager.ChangeMoneySafe(amount, transactionInfo, dayOfTransaction, address, force, showNotification);
        }

        private static void Note(string type)
        {
            if (Told.Add(type))
            {
                Unlisted++;
                Warn?.Invoke("BizMan check (income): the key " + type + " is booked at a business and is in none of BizLines' lists, so BizMan does not count it: FAIL");
            }
        }

        /// <summary>Forgets what was told, for a new save.</summary>
        public static void Reset()
        {
            Told.Clear();
            Unlisted = 0;
        }
    }
}
