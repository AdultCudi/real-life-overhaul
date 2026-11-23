using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI.Notification;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>
    /// "Pay all now" and paying the bills the player has ticked, for the Utilities app of both interfaces (BillPayRules says which bills, in what order and how many the cash covers). The player is
    /// asked first in the game's own question box with the total and the cash in it. A cash that falls short pays nothing and says by how much; the bills the cash does cover are paid, earliest
    /// due first, only when the player says yes to that. Each bill is paid as one is paid by hand (<see cref="Pay"/>): its own bank line, the account cleared on the spot, so the auto debit on its
    /// due day finds nothing owed and takes nothing twice. Cost: a pass over the accounts when a button is pressed; nothing per frame.
    /// </summary>
    public sealed partial class UtilitiesDirector
    {
        private readonly List<Account> _owing = new List<Account>(16);
        private readonly List<Account> _toPay = new List<Account>(16);

        /// <summary>Every account of every place with a bill owed, into <paramref name="into"/> (the cleared list is the caller's), and what they come to.</summary>
        public long Owing(List<Account> into)
        {
            BillPayRules.Owing(_state, into);
            long total = 0;
            for (int i = 0; i < into.Count; i++)
            {
                total += into[i].Owed;
            }
            return total;
        }

        /// <summary>
        /// Pay what is owed on every account now. Asks first. <paramref name="done"/> is told when something was paid (the kit's page reads again).
        /// </summary>
        public void AskPayAll(Action done)
        {
            Owing(_owing);
            Ask(_owing, done);
        }

        /// <summary>Pay the bills of these accounts now (those with nothing owed are left out). Asks first.</summary>
        public void AskPay(List<Account> bills, Action done)
        {
            Ask(bills, done);
        }

        private void Ask(List<Account> bills, Action done)
        {
            if (_state == null || bills == null)
            {
                return;
            }
            BillQuote q = BillPayRules.Quote(bills, UtilitiesGame.Cash);
            if (q.Count == 0)
            {
                UtilitiesText.Toast(NotificationType.Info, UtilitiesText.T("reallife_util_pay_none"), 6f);
                return;
            }
            var list = new List<Account>(bills);   // the caller's list is its own to change while the box is up
            string count = q.Count.ToString(), total = UtilitiesText.Money(q.Total), cash = UtilitiesText.Money(q.Cash);
            if (q.Covered)
            {
                GameConfirm.Show("reallife_util_pay_title".Localize(), "reallife_util_pay_body".Localize(new { count, total, cash }),
                    () => UiClone.Safe(() => PayBills(list, false, done)), null, null, null, true, false);
                return;
            }
            string shortBy = UtilitiesText.Money(q.Short);
            if (q.FitCount == 0)
            {
                UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_util_pay_short", "count", count, "total", total, "cash", cash, "lack", shortBy), 10f);
                return;
            }
            GameConfirm.Show("reallife_util_pay_part_title".Localize(), "reallife_util_pay_part_body".Localize(new
                {
                    count, total, cash, lack = shortBy, fit = q.FitCount.ToString(), fit_total = UtilitiesText.Money(q.FitTotal)
                }),
                () => UiClone.Safe(() => PayBills(list, true, done)), null, null, null, true, false);
        }

        /// <summary>
        /// The bills paid, in due-date order, each with its own bank line and each cleared as it is paid; with <paramref name="whatCashCovers"/> only those the cash covers. The cash is read again here:
        /// the box was up for as long as the player took. A bill the game refuses stops the run and what was paid stands. One notice at the end says how many and how much.
        /// </summary>
        public void PayBills(List<Account> bills, bool whatCashCovers, Action done)
        {
            if (_state == null)
            {
                return;
            }
            int today = UtilitiesGame.Today;
            BillPayRules.Choose(bills, UtilitiesGame.Cash, whatCashCovers, _toPay);
            if (!whatCashCovers && _toPay.Count > 0 && BillPayRules.Quote(_toPay, UtilitiesGame.Cash).Short > 0)
            {
                _toPay.Clear();   // the cash fell while the box was up: nothing is paid, the player asks again
            }
            int paid = 0, wanted = _toPay.Count;
            long sum = 0;
            _paying = true;
            try
            {
                for (int i = 0; i < _toPay.Count; i++)
                {
                    int owed = _toPay[i].Owed;
                    if (!Pay(_toPay[i], today, true, false))
                    {
                        break;
                    }
                    paid++;
                    sum += owed;
                }
            }
            finally
            {
                _paying = false;
                _toPay.Clear();
            }
            Logger?.Info("Utilities check (pay together): " + paid + " of " + wanted + " bill(s) paid, " + sum + " in all" + (whatCashCovers ? " (those the cash covers, earliest due first)" : string.Empty) + ": "
                         + (paid == wanted ? "PASS" : "STOPPED"));
            if (paid == 0)
            {
                UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_util_desk_no_cash"), 8f);
            }
            else
            {
                UtilitiesText.Toast(NotificationType.Success, UtilitiesText.T("reallife_util_toast_paid_many", "paid", paid.ToString(), "amount", UtilitiesText.Money(sum)), 8f);
            }
            _view?.Changed();
            Revision++;
            done?.Invoke();
        }
    }
}
