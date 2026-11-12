using System;
using Dialogs;
using Entities;
using Localizor;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The bank's own loan talk, past its loan form: asking for a point off the rate, which a bank gives a rating of
    /// <see cref="LoanTalkRules.LowerRateFrom"/> or more once in <see cref="LoanTalkRules.LowerRateEveryDays"/> days (the day
    /// asked is saved on the credit file), and a credit line at the same bank in place of a loan (the credit desk's overdraft,
    /// or its card where an overdraft is open), opened through the desk as the Finance app opens one. The RLO interface's
    /// conversation offers both as answers past the game's three; the bank's replies are lines of the game's own dialogue.
    /// Nothing here runs on a timer. Rivals and the city's firms borrow through the city's economy, not this desk.
    /// </summary>
    public static class LoanTalk
    {
        /// <summary>True while the rating runs: the economy on and a credit file loaded.</summary>
        public static bool Running => CreditLedger.Ready && EconomyDirector.Instance != null && EconomyDirector.Instance.Running;

        /// <summary>What asking now would come to at a bank lending at <paramref name="rate"/> per cent; <paramref name="given"/> once agreed in this talk.</summary>
        public static LowerRateAnswer CanLower(int rate, bool given, out int score, out int waitDays)
        {
            score = CreditLedger.Value;
            int asked = CreditLedger.File != null ? CreditLedger.File.RateAskedDay : 0;
            return LoanTalkRules.CanLower(Running, score, CreditLedger.Today, asked, rate, given, out waitDays);
        }

        /// <summary>Asked and agreed: the day kept on the credit file, so the next ask waits its days. False when there is no file.</summary>
        public static bool Lowered(string bank, int rate)
        {
            if (CreditLedger.File == null)
            {
                return false;
            }
            CreditLedger.File.RateAskedDay = Math.Max(1, CreditLedger.Today);
            EconomyDirector.Logger?.Info("Economy: " + (bank ?? "a bank") + " took " + LoanTalkRules.LowerRatePoints + " point off its loan rate (" + rate
                                         + "% a year now) for a rating of " + CreditLedger.Value + " on day " + CreditLedger.Today + ".");
            return true;
        }

        /// <summary>The city's bank a branch at an address belongs to, as the credit desk names it; null where there is none.</summary>
        public static Lender BankAt(Address address)
        {
            return address == null ? null : BankTerms.BankOf(CreditStore.Clean(address.streetName), address.streetNumber);
        }

        /// <summary>The credit line a bank offers in place of a loan: its overdraft, or its card where an overdraft is open already.</summary>
        public static ProductOffer Line(Lender bank, out ProductKind kind)
        {
            kind = ProductKind.Overdraft;
            CreditDesk desk = EconomyDirector.Instance != null ? EconomyDirector.Instance.Desk : null;
            if (desk == null || bank == null || !Running)
            {
                return new ProductOffer { Answer = bank == null ? ProductAnswer.NoBank : ProductAnswer.Off, Bank = bank };
            }
            ProductOffer offer = desk.Offer(ProductKind.Overdraft, bank);
            if (offer.Answer == ProductAnswer.AlreadyOpen)
            {
                ProductOffer card = desk.Offer(ProductKind.CreditCard, bank);
                if (card.Answer != ProductAnswer.AlreadyOpen)
                {
                    kind = ProductKind.CreditCard;
                    return card;
                }
            }
            return offer;
        }

        /// <summary>The bank's reply to the credit line asked for: the offer, opening it, or back to the loan.</summary>
        public static DialogEntry LineReply(Lender bank, Func<DialogEntry> back)
        {
            ProductOffer offer = Line(bank, out ProductKind kind);
            if (offer.Answer != ProductAnswer.Yes)
            {
                return Reply("reallife_loan_talk_line_failed", new { why = FinanceApp.Why(offer.Answer) }, back);
            }
            string thing = FinanceText.T(kind == ProductKind.CreditCard ? "reallife_econ_kind_card" : "reallife_econ_kind_overdraft");
            string limit = FinanceText.Money(offer.Most), rate = FinanceApp.Rate(offer.RateBp);
            var entry = new DialogEntry
            {
                messageData = "reallife_loan_talk_line_offer".Localize(new { thing, limit, rate }),
                ConfirmTextOverride = "reallife_loan_talk_line_open".Localize(),
                OnConfirm = () => Open(kind, bank, thing, limit),
                SecondOptionTextOverride = "reallife_loan_talk_back",
                OnSecondOption = back,
                OnCancel = DialogController.current.CancelDialog
            };
            // The same figures the offer above says: what the line gives and what it costs, so the answer and the words agree.
            entry.Cost(DialogButton.Confirm, FinanceText.T("reallife_loan_talk_line_limit", "limit", limit), 1)
                 .Cost(DialogButton.Confirm, FinanceText.T("reallife_loan_talk_line_cost", "rate", rate), -1);
            return entry;
        }

        private static DialogEntry Open(ProductKind kind, Lender bank, string thing, string limit)
        {
            CreditDesk desk = EconomyDirector.Instance != null ? EconomyDirector.Instance.Desk : null;
            ProductAnswer answer = desk != null ? desk.OpenAccount(kind, bank, out CreditAccount _) : ProductAnswer.Off;
            EconomyDirector.Logger?.Info("Economy: " + thing + " of up to " + limit + " asked for in the bank's loan talk at " + (bank != null ? bank.Name : "a bank") + ": " + answer + ".");
            return answer == ProductAnswer.Yes
                ? Closing("reallife_loan_talk_line_opened", new { thing })
                : Closing("reallife_loan_talk_line_failed", new { why = FinanceApp.Why(answer) });
        }

        private static DialogEntry Reply(string key, object data, Func<DialogEntry> back)
        {
            return new DialogEntry
            {
                messageData = key.Localize(data),
                ConfirmTextOverride = "reallife_loan_talk_back".Localize(),
                OnConfirm = back,
                OnCancel = DialogController.current.CancelDialog
            };
        }

        private static DialogEntry Closing(string key, object data)
        {
            bool phone = DialogController.current.dialogType == DialogType.PhoneCall;
            return new DialogEntry
            {
                messageData = key.Localize(data),
                OnVisible = phone ? DialogController.current.FinishDialog : (Action)null,
                OnCancel = phone ? (Action)null : DialogController.current.FinishDialog
            };
        }
    }
}
