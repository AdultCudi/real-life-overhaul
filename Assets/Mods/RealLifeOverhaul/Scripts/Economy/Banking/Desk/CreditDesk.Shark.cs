using System.Collections.Generic;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The credit desk's private lender: making itself known, lending, being paid, the one extension, and what it does past the due day.</summary>
    public sealed partial class CreditDesk
    {
        private readonly List<BuildingRegistration> _shops = new List<BuildingRegistration>(8);

        private static float Income => Mathf.Max(0f, CreditLedger.Income != null ? CreditLedger.Income() : 0f);

        public SharkDebt Shark => _file != null ? _file.Shark : null;

        public int SharkMost => SharkRules.Most(Income);

        /// <summary>A bank has just refused the player, or the rating went under the threshold: the private lender makes itself known, once.</summary>
        public void Refused()
        {
            if (_file == null || !Settings.EconomyShark || _file.Shark.Known)
            {
                return;
            }
            _file.Shark.Known = true;
            DebtPhone.Setup(this);
            DebtPhone.Text(DebtPhone.Lender, "reallife_text_shark_intro", Data("least", FinanceText.Money(ProductCatalogue.SharkLeast), "most", FinanceText.Money(SharkMost)), _openLender);
            if (Verbose)
            {
                Logger?.Info("Economy check (private lender): made itself known by a text at rating " + CreditLedger.Value + " (lenders want " + CreditCatalogue.LendingThreshold + "): PASS");
            }
        }

        public ProductAnswer SharkBorrow(int amount)
        {
            if (_file == null || !Settings.EconomyShark || !_file.Shark.Known)
            {
                return ProductAnswer.Off;
            }
            SharkDebt debt = _file.Shark;
            ProductAnswer answer = SharkRules.CanLend(debt, amount, Income);
            if (answer != ProductAnswer.Yes)
            {
                return answer;
            }
            int day = Today;
            SharkRules.Lend(debt, amount, day);
            _wallet.Give(amount, Money.SharkLent, string.Empty);
            if (Verbose)
            {
                Logger?.Info("Economy check (private lender): " + amount + " lent on day " + day + ", " + SharkRules.ADay(amount) + " a day on top, " + SharkRules.OwedOnDueDay(amount)
                             + " due on day " + debt.DueDay
                             + ", never more than " + SharkRules.Cap(amount) + "; nothing goes on the credit file: PASS");
            }
            return ProductAnswer.Yes;
        }

        public ProductAnswer SharkPay(int amount, out int used)
        {
            used = 0;
            if (_file == null || !_file.Shark.Active)
            {
                return ProductAnswer.NothingOwed;
            }
            SharkDebt debt = _file.Shark;
            SharkRules.Grow(debt, Today);
            amount = Mathf.Min(amount, debt.Owed);
            if (amount <= 0 || !_wallet.Take(amount, Money.SharkPaid, string.Empty))
            {
                return ProductAnswer.NoCash;
            }
            used = SharkRules.Pay(debt, amount);
            if (Verbose)
            {
                Logger?.Info("Economy check (private lender): " + used + " paid, " + debt.Owed + " left" + (debt.Active ? string.Empty : ": it is over, and nothing went on the credit file")
                             + ": PASS");
            }
            return ProductAnswer.Yes;
        }

        public ProductAnswer SharkExtend(out int fee)
        {
            fee = 0;
            if (_file == null || !_file.Shark.Active)
            {
                return ProductAnswer.NothingOwed;
            }
            SharkDebt debt = _file.Shark;
            int day = Today;
            SharkRules.Grow(debt, day);
            fee = SharkRules.ExtendFee(debt);
            if (!SharkRules.Extend(debt, day))
            {
                return ProductAnswer.AlreadyOpen;
            }
            if (Verbose)
            {
                Logger?.Info("Economy check (private lender): extended once for a fee of " + fee + "; " + debt.Owed + " owed, due on day " + debt.DueDay + ": PASS");
            }
            return ProductAnswer.Yes;
        }

        // Each day: the lender shows itself if nobody else lends; the debt grows; past its day, today's act is decided.
        private void SharkDay(int day, int level)
        {
            SharkDebt debt = _file.Shark;
            if (!debt.Known && !CreditLedger.Score.Lends)
            {
                Refused();
            }
            if (!debt.Active)
            {
                return;
            }
            if (!Settings.EconomyShark)
            {
                debt.GrownDay = day;   // switched off: the debt stands as it is and can be paid; it does not grow and nothing is done about it
                return;
            }
            int added = SharkRules.Grow(debt, day);
            if (day == debt.DueDay)
            {
                DebtPhone.Text(DebtPhone.Lender, "reallife_text_shark_due", Data("amount", FinanceText.Money(debt.Owed)), _openLender);
            }
            SharkAct act = SharkRules.Today(debt, day, level > ProductCatalogue.Relaxed);
            if (act == SharkAct.Warn)
            {
                DebtPhone.Text(DebtPhone.Lender, "reallife_text_shark_warn", Data("amount", FinanceText.Money(debt.Owed), "days", (day - debt.DueDay).ToString()), _openLender);
            }
            else if (act != SharkAct.None)
            {
                debt.Waiting = act;
            }
            if (Verbose)
            {
                Logger?.Info("Economy check (private lender, day " + day + "): " + added + " added, " + debt.Owed + " owed of at most " + SharkRules.Cap(debt.Borrowed) + ", due day " + debt.DueDay
                             + (day > debt.DueDay ? ", " + (day - debt.DueDay) + " day(s) past: " + act
                                + (level > ProductCatalogue.Relaxed ? string.Empty : " (no violence on Relaxed)") : string.Empty)
                             + ": " + (debt.Owed <= SharkRules.Cap(debt.Borrowed) ? "PASS" : "FAIL"));
            }
        }

        // Each hour: an act that is waiting is carried out when nothing has hold of the player.
        private void SharkHour(int day)
        {
            SharkDebt debt = _file.Shark;
            if (debt.Waiting == SharkAct.None)
            {
                return;
            }
            if (!debt.Active || !Settings.EconomyShark || Level <= ProductCatalogue.Relaxed)
            {
                debt.Waiting = SharkAct.None;
                return;
            }
            if (EconomyBridges.Held != null && EconomyBridges.Held())
            {
                return;   // in custody, in hospital, on a trip, in a scene: not now
            }
            if (debt.Waiting == SharkAct.Assault)
            {
                if (PlayerHelper.IsUsingVehicle || BuildingManager.IsInsideBuilding)
                {
                    return;   // only in the street, on foot
                }
                int taken = _wallet.TakeUpTo(debt.Owed, Money.SharkTaken, string.Empty);
                bool hurt = EconomyBridges.Assault != null && EconomyBridges.Assault(ProductCatalogue.SharkInjuryHours, ProductCatalogue.SharkInjuryEnergy, "reallife_econ_note_shark_assault");
                SharkRules.Pay(debt, taken);
                debt.Waiting = SharkAct.None;
                Notifications.Show(NotificationType.Error, "reallife_econ_note_shark_taken", Data("amount", FinanceText.Money(taken), "left", FinanceText.Money(debt.Owed)), 14f, null,
                                   Guard.Wrap(_openLender), true, false);
                if (Verbose)
                {
                    Logger?.Info("Economy check (private lender, day " + day + "): the borrower was set upon in the street (" + (hurt ? "hurt as a mugging hurts" : "no injury could be given") + "); "
                                 + taken + " in cash taken against the debt, " + debt.Owed + " left: PASS");
                }
                return;
            }
            Books.Businesses(_shops);
            if (_shops.Count == 0)
            {
                debt.Waiting = SharkAct.None;
                DebtPhone.Text(DebtPhone.Lender, "reallife_text_shark_warn", Data("amount", FinanceText.Money(debt.Owed), "days", (day - debt.DueDay).ToString()), _openLender);
                return;
            }
            BuildingRegistration shop = _shops[day % _shops.Count];
            _shops.Clear();
            int bill = SharkRules.DamageBill(debt.Owed);
            _wallet.Force(bill, Money.SharkDamage, shop.BusinessName);
            debt.Waiting = SharkAct.None;
            Notifications.Show(NotificationType.Error, "reallife_econ_note_shark_damage", Data("business", shop.BusinessName, "amount", FinanceText.Money(bill)), 14f, null, Guard.Wrap(_openLender),
                               true, false);
            if (Verbose)
            {
                Logger?.Info("Economy check (private lender, day " + day + "): " + shop.BusinessName + " was damaged: a repair bill of " + bill + " (" + debt.Owed + " still owed): PASS");
            }
        }
    }
}
