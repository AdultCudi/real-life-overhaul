using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ledger;
using RealLifeOverhaul.Outlook;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Fix your loan's rate: the loan found from the game's own, the question asked in the game's box with the old rate, the new rate, the saving and the fee, and
    /// the fixed term written onto the loan only when the answer is yes. The same rule (NewsRules.Offer, WillFix) fixes a rival's debt when a rise is set
    /// (Markets/MarketEngine.Session FixRivalDebt).
    /// </summary>
    public sealed partial class NewsLive
    {
        private static readonly LoanFixDesk FixDesk = new LoanFixDesk();

        // What a month saves on the loan the fix helps most, or 0 for none.
        private static int LoanSaving()
        {
            return FixDesk.Find(out LoanFixCase found) ? found.Saving : 0;
        }

        private sealed class LoanFixDesk : ILoanFixDesk
        {
            private readonly List<LoanView> _live = new List<LoanView>(4);

            public bool Find(out LoanFixCase found)
            {
                found = default(LoanFixCase);
                _live.Clear();
                GameLoans.Read(_live);
                int best = 0, at = -1;
                for (int i = 0; i < _live.Count; i++)
                {
                    if (_live[i].Remaining > best && _live[i].Interest > 0)
                    {
                        best = (int)_live[i].Remaining;
                        at = i;
                    }
                }
                return at >= 0 && Offer(at, out found);
            }

            // The loan asked about, found again among the game's by its street, number and total; a loan repaid or a new one taken meanwhile does not move it.
            public bool Again(LoanFixCase asked, out LoanFixCase now)
            {
                now = default(LoanFixCase);
                _live.Clear();
                GameLoans.Read(_live);
                for (int i = 0; i < _live.Count; i++)
                {
                    if (_live[i].Street == asked.Street && _live[i].Number == asked.Number && (int)_live[i].Total == asked.Total && _live[i].Remaining > 0 && _live[i].Interest > 0)
                    {
                        return Offer(i, out now);
                    }
                }
                return false;
            }

            public void Gone() => Notifications.Show(NotificationType.Info, "reallife_news_fix_gone", (Dictionary<string, string>)null, 6f);

            private bool Offer(int at, out LoanFixCase made)
            {
                bool rising = Look.Pending && Look.PendingMove > 0;
                LoanView v = _live[at];
                return NewsLoanFix.Offer(at, v.Street, v.Number, (int)v.Total, (int)v.Remaining, v.Interest, CreditLedger.Value, Look.RateBp, AccountRules.DaysPerYear, rising, out made);
            }

            public void Ask(LoanFixCase asked, Action yes)
            {
                string fee = asked.Fee > 0 ? OutlookText.T("reallife_news_fix_fee", "money", LedgerText.Money(asked.Fee)) : OutlookText.T("reallife_news_fix_nofee");
                string body = LedgerText.Fill("reallife_news_fix_ask_body", "amount", LedgerText.Money(asked.Remaining), "old", Rate(asked.OldRateBp), "new", Rate(asked.NewRateBp),
                    "money", LedgerText.Money(asked.Saving), "fee", fee);
                GameConfirm.Show(OutlookText.T("reallife_news_fix_ask_title"), body, Guard.Wrap(() =>
                {
                    yes();
                    Economy._local++;   // the Insights are read again: the saving has gone from the list
                }), null, "reallife_news_fix_ask_yes", "common_cancel", false, false);
            }

            public void Fix(LoanFixCase fixing)
            {
                _live.Clear();
                GameLoans.Read(_live);
                if (CreditLedger.File == null || fixing.Index < 0 || fixing.Index >= _live.Count)
                {
                    return;
                }
                LoanView view = _live[fixing.Index];
                LoanMark mark = null;
                for (int i = 0; i < CreditLedger.File.Loans.Count; i++)
                {
                    LoanMark one = CreditLedger.File.Loans[i];
                    if (one.Street == view.Street && one.Number == view.Number)
                    {
                        mark = one;
                        break;
                    }
                }
                if (mark == null)
                {
                    return;
                }
                int interest = view.Interest;
                int payment = view.Payment;
                LoanHolds.Begin(mark, LoanHold.Fixed, ref interest, ref payment, GameLoans.Multiplier, AccountRules.DaysPerYear);
                mark.OwnInterest = fixing.NewDaily;
                mark.OwnPayment = payment;
                LoanHolds.Apply(mark, ref interest, ref payment, GameLoans.Multiplier, AccountRules.DaysPerYear);
                GameLoans.WriteTerms(view, interest, payment);
                CreditLedger.Touch();
            }

            public bool OpenApp() => PhoneApp.OpenById(EconomyDirector.AppId);

            private static string Rate(int bp) => (bp / 100f).ToString("0.0#", Inv);
        }
    }
}
