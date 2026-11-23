using System.Collections.Generic;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>
    /// What paying several bills at once comes to, with no game types (checked offline): the bills of the player's accounts that are owed, their total against the cash,
    /// and, for a player who chooses it, the bills the cash covers taken in due-date order.
    /// </summary>
    public readonly struct BillQuote
    {
        public readonly int Count;        // bills owed among those asked about
        public readonly long Total;       // money, all of them
        public readonly long Cash;        // money in hand when it was asked
        public readonly int FitCount;     // bills the cash covers taken in due-date order, one that does not fit passed over
        public readonly long FitTotal;    // money, those

        public BillQuote(int count, long total, long cash, int fitCount, long fitTotal)
        {
            Count = count;
            Total = total;
            Cash = cash;
            FitCount = fitCount;
            FitTotal = fitTotal;
        }

        /// <summary>Money the cash falls short of paying every one, 0 when it covers them.</summary>
        public long Short => Total > Cash ? Total - Cash : 0;

        /// <summary>True if the cash pays every bill.</summary>
        public bool Covered => Count > 0 && Short == 0;
    }

    /// <summary>
    /// The rules of "Pay all now" and of paying the bills the player has ticked, in the Utilities app of both interfaces: which accounts owe, the order they are paid in, and how many the cash
    /// covers. Paying itself stays what paying one bill is (UtilitiesDirector.Pay: one bank line each, the account cleared so the auto debit finds nothing to take). Cost: a pass over the
    /// accounts and an insertion sort of the few that owe, when the player presses the button.
    /// </summary>
    public static class BillPayRules
    {
        /// <summary>Every account of every place with a bill owed, in the order the state keeps them.</summary>
        public static void Owing(UtilitiesState state, List<Account> into)
        {
            into.Clear();
            for (int i = 0; state != null && i < state.Accounts.Count; i++)
            {
                Account a = state.Accounts[i];
                if (a != null && a.Owed > 0)
                {
                    into.Add(a);
                }
            }
        }

        /// <summary>Those of the accounts that owe, the bills in the order they are paid in: the earliest due day first, a bill with no due day last, ties as they stood (a stable sort).</summary>
        public static void DueOrder(List<Account> bills)
        {
            for (int i = bills.Count - 1; i >= 0; i--)
            {
                if (bills[i] == null || bills[i].Owed <= 0)
                {
                    bills.RemoveAt(i);
                }
            }
            for (int i = 1; i < bills.Count; i++)
            {
                Account a = bills[i];
                int j = i - 1;
                while (j >= 0 && Later(bills[j], a))
                {
                    bills[j + 1] = bills[j];
                    j--;
                }
                bills[j + 1] = a;
            }
        }

        // True if the bill a is paid after the bill b.
        private static bool Later(Account a, Account b)
        {
            int da = a.DueDay > 0 ? a.DueDay : int.MaxValue, db = b.DueDay > 0 ? b.DueDay : int.MaxValue;
            return da > db;
        }

        /// <summary>What the bills come to against the cash. <paramref name="bills"/> are put in due-date order.</summary>
        public static BillQuote Quote(List<Account> bills, long cash)
        {
            DueOrder(bills);
            long total = 0, fit = 0, left = cash < 0 ? 0 : cash;
            int fitCount = 0;
            for (int i = 0; i < bills.Count; i++)
            {
                long owed = bills[i].Owed;
                total += owed;
                if (owed <= left)
                {
                    left -= owed;
                    fit += owed;
                    fitCount++;
                }
            }
            return new BillQuote(bills.Count, total, cash < 0 ? 0 : cash, fitCount, fit);
        }

        /// <summary>
        /// The bills that are paid, taken from <paramref name="bills"/> in due-date order into <paramref name="pay"/>: all of them, or with <paramref name="whatCashCovers"/> only those the cash
        /// covers, one that does not fit passed over so a smaller one after it still is paid.
        /// </summary>
        public static void Choose(List<Account> bills, long cash, bool whatCashCovers, List<Account> pay)
        {
            pay.Clear();
            DueOrder(bills);
            long left = cash < 0 ? 0 : cash;
            for (int i = 0; i < bills.Count; i++)
            {
                if (!whatCashCovers)
                {
                    pay.Add(bills[i]);
                }
                else if (bills[i].Owed <= left)
                {
                    left -= bills[i].Owed;
                    pay.Add(bills[i]);
                }
            }
        }
    }
}
