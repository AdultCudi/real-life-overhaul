using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The bank's automatic dividends in its director: the page's choices, and a dividend's day told. Nothing per frame; a notice on the day one falls due.</summary>
    public sealed partial class OwnBankDirector
    {
        /// <summary>How often the bank pays a dividend on its own (BankDividends) and the share of the profit it kept; a change of how often begins the time today.</summary>
        public void SetDividends(int every, int share)
        {
            if (Running && Save != null && _bank.Running)
            {
                int was = _bank.DivEvery;
                OwnBankRules.SetDividends(_bank, every, share, Save.Day);
                if (was != _bank.DivEvery)
                {
                    Logger?.Info("Own bank: automatic dividends set to " + _bank.DivEvery + " (0 off, 1 every " + BankDividends.WeekDays + " days, 2 monthly, 3 yearly) at "
                                 + _bank.DivShare + " thousandths of the profit kept, from day " + Save.Day + ".");
                }
            }
        }

        /// <summary>The automatic dividends' share, thousandths, put on the slider's step.</summary>
        public void SetDividendShare(int share)
        {
            if (Running && Save != null && _bank.Running)
            {
                OwnBankRules.SetDividends(_bank, _bank.DivEvery, share, Save.Day);
            }
        }

        // A dividend's day after the day's books: paid, paid short or skipped, each told with why; a test's bank only in the log.
        private void TellDividend(int day)
        {
            if (!_day.DividendDue)
            {
                return;
            }
            string name = _bank.Name, why = OwnBankApp.DividendWhy(_bank);
            if (_bank.DivWhy == BankDividends.Paid)
            {
                Tell(NotificationType.Success, "reallife_ob_toast_div_paid", TabBank, "bank", name, "amount", OwnBankText.Money(_bank.DivPaid), "share", OwnBankText.Ratio(_bank.DivShare));
            }
            else if (_bank.DivWhy != BankDividends.Test && _bank.DivPaid > 0)
            {
                Tell(NotificationType.Warning, "reallife_ob_toast_div_short", TabBank, "bank", name, "amount", OwnBankText.Money(_bank.DivPaid), "wanted", OwnBankText.Money(_bank.DivWanted),
                    "why", why);
            }
            else if (_bank.DivWhy != BankDividends.Test)
            {
                Tell(NotificationType.Warning, "reallife_ob_toast_div_none", TabBank, "bank", name, "why", why);
            }
            if (Verbose)
            {
                bool full = _bank.DivWanted > 0 && _bank.DivPaid == _bank.DivWanted;
                bool right = _bank.DivPaid >= 0 && _bank.DivPaid <= _bank.DivWanted && full == (_bank.DivWhy == BankDividends.Paid) && _day.PaidOut == _bank.DivPaid
                             && OwnBankRules.Balanced(_bank) && (_bank.DivPaid == 0 || OwnBankRules.Ratio(_bank) >= OwnBankCatalogue.WatchRatio);
                Logger?.Info("Own bank check (dividend, day " + day + "): " + _bank.DivPaid + " of " + _bank.DivWanted + " cents paid (" + _bank.DivShare + " thousandths of the profit kept), why "
                             + _bank.DivWhy + ", the ratio " + OwnBankRules.Ratio(_bank).ToString(CultureInfo.InvariantCulture) + ", the next on day "
                             + OwnBankRules.DividendDay(_bank, GameCalendar.MonthDays, GameCalendar.DaysPerYear) + ": " + (right ? "PASS" : "FAIL") + ".");
            }
        }
    }
}
