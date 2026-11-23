using System;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The bank risk in its director (BankRisk): the tools that meet a run (a sale of loans, money from other banks, repaying it), and what a day of the risk
    /// tells the owner (warning signs once a month, a coming shock once, a loan rolled over, a bank taken with what the waterfall left). Nothing per frame.
    /// </summary>
    public sealed partial class OwnBankDirector
    {
        /// <summary>The risk level in force for the bank (the option's, as the city reads it).</summary>
        public int RiskNow => BankRisk.Level(Settings.EconomyBank ? Settings.EconomyBankRisk : BankRisk.Off);

        /// <summary>
        /// A sale of performing loans of a kind to other banks, for their face less the discount of the day, to meet a run or raise cash. False with a reason key if
        /// it cannot be: the risk is off, the sum is over what one sale may take.
        /// </summary>
        public bool SellLoans(int segment, long faceCents, out string why)
        {
            why = RiskReady();
            if (why != null)
            {
                return false;
            }
            BankCity city = OwnBankGame.City();
            if (!OwnBankRules.SellLoans(_bank, city, segment, faceCents, out long cash, out long loss))
            {
                why = "reallife_ob_why_sale_too_much";
                return false;
            }
            Tell(NotificationType.Success, "reallife_ob_toast_loans_sold", TabBank, "face", OwnBankText.Money(faceCents), "cash", OwnBankText.Money(cash), "loss", OwnBankText.Money(loss));
            Logger?.Info("Own bank: " + faceCents / 100 + " of " + OwnBankCatalogue.SegmentKeys[segment] + " loans sold to other banks for " + cash / 100 + " on day " + Save.Day + " (" + loss / 100
                         + " lost); the ratio is " + OwnBankRules.Ratio(_bank) + " thousandths" + (OwnBankRules.Balanced(_bank) ? string.Empty : "; the books do not balance: FAIL") + ".");
            Changed();
            return true;
        }

        /// <summary>Money borrowed from other banks for a month, at the price of the day; false with a reason key if it is over what the bank may borrow.</summary>
        public bool Borrow(long cents, out string why)
        {
            why = RiskReady();
            if (why != null)
            {
                return false;
            }
            BankCity city = OwnBankGame.City();
            cents = OwnBankRules.ToStep(cents);
            if (cents <= 0 || !OwnBankRules.Borrow(_bank, city, cents, Save.Day, out long fee))
            {
                why = "reallife_ob_why_borrow_too_much";
                return false;
            }
            Tell(NotificationType.Info, "reallife_ob_toast_borrowed", TabBank, "amount", OwnBankText.Money(cents), "rate", OwnBankText.Rate(_bank.InterbankBp), "day",
                FinanceText.Day(_bank.InterbankDue),
                "fee", OwnBankText.Money(fee));
            Logger?.Info("Own bank: " + cents / 100 + " borrowed from other banks on day " + Save.Day + " at " + _bank.InterbankBp + " hundredths of a per cent, due day " + _bank.InterbankDue + ".");
            Changed();
            return true;
        }

        /// <summary>Money repaid to other banks early, out of the cash over the reserve; false with a reason key if there is nothing to repay or no cash for it.</summary>
        public bool Repay(long cents, out string why)
        {
            why = RiskReady();
            if (why != null)
            {
                return false;
            }
            long paid = OwnBankRules.Repay(_bank, OwnBankRules.ToStep(cents));
            if (paid <= 0)
            {
                why = _bank.Interbank <= 0 ? "reallife_ob_why_nothing_borrowed" : "reallife_ob_why_repay_cash";
                return false;
            }
            Tell(NotificationType.Success, "reallife_ob_toast_repaid", TabBank, "amount", OwnBankText.Money(paid), "left", OwnBankText.Money(_bank.Interbank));
            Changed();
            return true;
        }

        // Why the risk tools cannot be used now; null if they can.
        private string RiskReady()
        {
            string why = Ready() ?? (!_bank.Running ? "reallife_ob_why_none" : null);
            if (why == null && OwnBankRules.RiskOf(_bank) == BankRisk.Off)
            {
                why = "reallife_ob_why_risk_off";
            }
            return why;
        }

        // What a day of the risk told: the warning signs once a month, a shock coming for the next month, a loan rolled over, a desk cut.
        private void TellRisk(int day, BankCity city)
        {
            if (!_bank.Running || _bank.Risk == BankRisk.Off)
            {
                return;
            }
            if (_day.RiskTold)
            {
                int signs = OwnBankRules.Signs(_bank, city, day, OwnBankRules.Confidence(city, _bank, OwnBankRules.Ratio(_bank)));
                Tell(NotificationType.Warning, "reallife_ob_toast_signs", TabBank, "bank", _bank.Name, "signs", OwnBankReader.SignWords(signs, OwnBankText.T));
                Logger?.Info("Own bank: warning signs on day " + day + " (flags " + signs + "), the ratio " + OwnBankRules.Ratio(_bank) + " thousandths, cash for "
                    + OwnBankRules.CoverDays(_bank) / 10.0
                             + " days of a run.");
            }
            if (_day.ShockNew >= 0)
            {
                Tell(NotificationType.Warning, "reallife_ob_toast_shock", TabLending, "kind", OwnBankText.Segment(_day.ShockNew));
            }
            if (_day.Rolled)
            {
                Tell(NotificationType.Warning, "reallife_ob_toast_rolled", TabBank, "bank", _bank.Name, "rate", OwnBankText.Rate(_bank.InterbankBp));
            }
        }

        // The bank taken by the regulator told with how its assets were shared out: savers, the banks it borrowed from, and what was left to the owner.
        private void TellTaken(string name, string why)
        {
            Waterfall w = _day.Waterfall;
            Tell(NotificationType.Error, "reallife_ob_toast_taken", TabBank, "bank", name, "why", why, "savers", OwnBankText.Money(w.Savers), "banks", OwnBankText.Money(w.Banks),
                "left", OwnBankText.Money(w.Owner));
            Logger?.Info("Own bank: the waterfall of the closing: assets " + w.Assets / 100 + ", savers paid " + w.Savers / 100 + " (lost " + w.SaversLoss / 100 + "), banks paid " + w.Banks / 100
                         + " (lost " + w.BanksLoss / 100 + "), the regulator's charge " + w.Fee / 100 + ", the owner's " + w.Owner / 100 + ": "
                         + (w.Savers + w.Banks + w.Fee + w.Owner == Math.Max(0, w.Assets) ? "PASS" : "FAIL") + ".");
        }

        /// <summary>What the Bank tab's risk page says it would take to be clear of the regulator's warning line, in cents.</summary>
        public long CapitalToClear() => _bank != null && _bank.Running ? OwnBankRules.CapitalToClear(_bank) : 0;

        // The bank's figures as a line for the log, for the day's check.
        private static string RiskWords(OwnBank b)
        {
            return "risk " + b.Risk + ", borrowed " + (b.Interbank / 100).ToString(CultureInfo.InvariantCulture) + ", cash for " + (OwnBankRules.CoverDays(b) / 10.0).ToString("0.#",
                CultureInfo.InvariantCulture)
                   + " days of a run";
        }
    }
}
