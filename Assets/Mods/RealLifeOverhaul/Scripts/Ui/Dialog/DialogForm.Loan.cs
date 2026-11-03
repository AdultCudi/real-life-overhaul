using System;
using System.Globalization;
using Entities;
using Helpers;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.GameUi;
using SpecialServices.Bank;
using UI.Dialog;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The bank's loan form (the game's BankLoanSettings) on the kit. Under the amount the loan's term, its rate a year,
    /// its payment a day and the whole sum to repay, worked out by the game's own formulas (LoanTalkRules) in place of the
    /// game's three daily figures, which hold for the bank's own term only. Under the line's confirm what taking the loan
    /// pays and costs; past the game's three answers the bank's own (LoanTalk): a point off the rate for a good rating, and
    /// a credit line in place of a loan. The game's loan keeps a daily payment and a daily interest, not a term, so the
    /// term chosen and a point agreed are written into the form's two figures as the game's confirm runs (the line's own
    /// answer, wrapped once), and the game's BankDialog makes the loan from them as always. While the economy is off, or a
    /// quest loan holds the bank to its own terms, the form shows the bank's own term and nothing past the game's answers.
    /// Costs nothing a frame; a little arithmetic when the form is read.
    /// </summary>
    internal sealed partial class DialogForm
    {
        private const int TermRow = 1;   // the kit's own row over the loan form: its term

        private readonly int[] _terms = new int[LoanTalkRules.TermsMost];
        private readonly string[] _termWords = new string[LoanTalkRules.TermsMost];
        private int _termCount, _term = -1;
        private object _loanTalk;                 // the conversation the term and a point off were agreed in
        private bool _lowered;                    // the bank took a point off in this talk
        private BankLoanSettings _loanForm;       // the bank's loan form while it is the one read
        private BankSettings _bank;
        private Lender _lender;
        private DialogEntry _lineFor;             // the line the credit line's answer was asked for
        private ProductOffer _line;
        private ProductKind _lineKind;

        /// <summary>The bank's loan form is the one on the kit now.</summary>
        public bool LoanShown => _loanForm != null;

        // After the walk: the bank's loan form gets its term, its figures and the bank's answers. Any other form is as read.
        private void Loan(Transform form, DialogEntry entry, DialogController talk, int heading)
        {
            BankLoanSettings settings = form != null ? form.GetComponent<BankLoanSettings>() : null;
            BankSettings bank = settings != null && talk != null && talk.contact != null && SaveGameManager.Current != null
                ? LoanHelper.GetBankSettings(talk.contact.Address) : null;
            if (bank == null || entry == null)
            {
                _loanForm = null;
                return;
            }
            if (!ReferenceEquals(_loanTalk, talk.dialog))
            {
                _loanTalk = talk.dialog;   // a new talk at the counter starts at the bank's own term, its rate its own
                _term = -1;
                _lowered = false;
                _lineFor = null;
            }
            _loanForm = settings;
            _bank = bank;
            _lender = LoanTalk.BankAt(talk.contact.Address);
            bool own = LoanTalk.Running && !BankTerms.QuestHold(bank);
            _termCount = LoanTalkRules.Terms(bank.yearsToPayLoan, _terms);
            if (_term < 0 || _term >= _termCount || !own)
            {
                _term = LoanTalkRules.OwnTerm(bank.yearsToPayLoan, _terms, _termCount);
            }
            int daysPerYear = SaveGameManager.Current.gameVariables.daysPerYear;
            for (int i = 0; i < _termCount; i++)
            {
                _termWords[i] = TermWords(_terms[i], daysPerYear);
            }

            // The game's three daily figures hold for the bank's own term: the kit's take their place.
            for (int i = _rows.Count - 1; i >= heading; i--)
            {
                if (_rows[i].Kind == UiFormKind.Figure || _rows[i].Kind == UiFormKind.Break)
                {
                    _rows.RemoveAt(i);
                    _widgets.RemoveAt(i);
                }
            }
            LoanFigures f = Figures(out float amount, out int rate);
            string termLabel = UiText.T("reallife_ui_dialog_loan_term");
            Add(own
                ? new UiFormRow { Kind = UiFormKind.Choice, Label = termLabel, Value = _termWords[_term] }
                : new UiFormRow { Kind = UiFormKind.Figure, Label = termLabel, Value = _termWords[_term] }, new Widget { Kit = own ? TermRow : 0 });
            Add(new UiFormRow { Kind = UiFormKind.Break }, default);
            Add(new UiFormRow { Kind = UiFormKind.Figure, Label = UiText.T("reallife_ui_dialog_loan_rate"), Value = RateWords(rate) }, default);
            Add(new UiFormRow { Kind = UiFormKind.Figure, Label = UiText.T("reallife_ui_dialog_loan_payment"), Value = Money(f.PerDay) }, default);
            Add(new UiFormRow { Kind = UiFormKind.Figure, Label = UiText.T("reallife_ui_dialog_loan_total"), Value = Money(f.Total), Strong = true }, default);

            // What taking it pays and costs, under the line's confirm; the bank's own answers past the game's three.
            entry.NoCosts(DialogButton.Confirm).NoExtras();
            if (amount > 0f)
            {
                entry.Cost(DialogButton.Confirm, UiText.Fill("reallife_ui_dialog_loan_cost_in", "amount", Money(amount)), 1, 1)
                     .Cost(DialogButton.Confirm, UiText.Fill("reallife_ui_dialog_loan_cost_out", "payment", Money(f.PerDay), "days", f.Days.ToString(CultureInfo.CurrentCulture)), -1);
            }
            LoanConfirm.Wrap(entry, this);
            if (own)
            {
                Lower(entry, rate);
                CreditLine(entry);
            }
        }

        // The loan's figures as the game would make it now: the amount typed, the term chosen, the bank's rate less a point agreed.
        private LoanFigures Figures(out float amount, out int rate)
        {
            amount = 0f;
            string text = _loanForm != null && _loanForm.amountInput != null ? _loanForm.amountInput.text : null;
            if (!string.IsNullOrEmpty(text) && (!float.TryParse(text, out amount) || amount < 0f))
            {
                amount = 0f;   // the game's own form reads a figure the same way, and refuses one under nothing
            }
            rate = _bank != null ? LoanTalkRules.Rate(_bank.annualInterestRate, _lowered ? LoanTalkRules.LowerRatePoints : 0) : 0;
            var v = SaveGameManager.Current.gameVariables;
            int years = _term >= 0 && _term < _termCount ? _terms[_term] : Math.Max(1, _bank != null ? _bank.yearsToPayLoan : 1);
            return LoanTalkRules.Figures(amount, years, rate, GameLoans.Multiplier, v.daysPerYear);
        }

        /// <summary>
        /// The term chosen and a point agreed, written into the game's form as its confirm runs, so the game's BankDialog makes
        /// the loan on them. Nothing where the form is not the one the kit shows, or the bank's own term and rate stand.
        /// </summary>
        internal void LoanAgreed()
        {
            if (_loanForm == null || _bank == null || _term < 0 || SaveGameManager.Current == null || !LoanTalk.Running || BankTerms.QuestHold(_bank))
            {
                return;
            }
            bool ownTerm = _terms[_term] == Math.Max(1, _bank.yearsToPayLoan);
            if (ownTerm && !_lowered)
            {
                return;   // the game's own figures, as its form worked them out
            }
            LoanFigures f = Figures(out float amount, out int rate);
            if (amount <= 0f)
            {
                return;
            }
            _loanForm.dailyInterest = f.Interest;
            _loanForm.dailyPayment = f.Principal;
            UiDirector.Logger?.Info("Dialog (kit): the bank's loan of " + Mathf.RoundToInt(amount) + " over " + _terms[_term] + " year(s) at " + rate + "% a year"
                                    + (_lowered ? " (a point off)" : string.Empty) + ": " + f.Interest + " interest and " + f.Principal + " paid off a day, "
                                    + f.Days + " days, " + f.Total + " in all; the game's BankDialog makes it.");
        }

        // A point off the rate: asked once, the bank answering on the line itself; greyed with the reason where it cannot be.
        private void Lower(DialogEntry entry, int rate)
        {
            int ownRate = _bank.annualInterestRate;
            LowerRateAnswer answer = LoanTalk.CanLower(ownRate, _lowered, out int score, out int wait);
            string name = UiText.T("reallife_ui_dialog_loan_lower");
            switch (answer)
            {
                case LowerRateAnswer.Off:
                    return;
                case LowerRateAnswer.Yes:
                {
                    int lower = LoanTalkRules.Rate(ownRate, LoanTalkRules.LowerRatePoints);
                    entry.ExtraHere(name, () => Lowered(score, lower), true, new DialogCost(UiText.Fill("reallife_ui_dialog_loan_lower_yes", "rate", RateWords(lower)), 1, -1));
                    return;
                }
                case LowerRateAnswer.Given:
                    entry.ExtraHere(name, null, false, new DialogCost(UiText.Fill("reallife_ui_dialog_loan_lower_given", "rate", RateWords(rate)), 1, -1));
                    return;
                case LowerRateAnswer.Rating:
                    entry.ExtraHere(name, null, false, new DialogCost(UiText.Fill("reallife_ui_dialog_loan_lower_rating", "need", LoanTalkRules.LowerRateFrom.ToString(CultureInfo.CurrentCulture),
                        "score", score.ToString(CultureInfo.CurrentCulture)), -1));
                    return;
                case LowerRateAnswer.Wait:
                    entry.ExtraHere(name, null, false, new DialogCost(UiText.Fill("reallife_ui_dialog_loan_lower_wait", "day",
                        (CreditLedger.Today + wait).ToString(CultureInfo.CurrentCulture)), -1));
                    return;
                default:
                    entry.ExtraHere(name, null, false, new DialogCost(UiText.Fill("reallife_ui_dialog_loan_lower_floor", "rate", RateWords(rate)), 0));
                    return;
            }
        }

        // Asked and agreed: the point off for this talk, the day kept so the next ask waits; the bank's words back.
        private string Lowered(int score, int lower)
        {
            if (_lowered || !LoanTalk.Lowered(_lender != null ? _lender.Name : null, lower))
            {
                return null;
            }
            _lowered = true;
            return UiText.Fill("reallife_ui_dialog_loan_lower_reply", "score", score.ToString(CultureInfo.CurrentCulture), "rate", RateWords(lower));
        }

        // A credit line at this bank in place of a loan: the bank's offer on its own line, greyed with the reason where there is none.
        private void CreditLine(DialogEntry entry)
        {
            if (_lender == null)
            {
                return;
            }
            if (!ReferenceEquals(_lineFor, entry))
            {
                _lineFor = entry;   // asked once a line: the offer stands while the form is filled in
                _line = LoanTalk.Line(_lender, out _lineKind);
            }
            string name = UiText.T("reallife_ui_dialog_loan_line");
            Lender bank = _lender;
            if (_line.Answer != ProductAnswer.Yes)
            {
                entry.Extra(name, null, false, new DialogCost(FinanceApp.Why(_line.Answer), -1));
                return;
            }
            string thing = UiText.T(_lineKind == ProductKind.CreditCard ? "reallife_econ_kind_card" : "reallife_econ_kind_overdraft");
            entry.Extra(name, () => LoanTalk.LineReply(bank, () => Again(entry)), true,
                new DialogCost(UiText.Fill("reallife_ui_dialog_loan_line_cost", "thing", thing, "rate", FinanceApp.Rate(_line.RateBp)), 0));
        }

        // The loan's form again, as the bank asked for it: the same line's answers, so the talk goes on where it was.
        private static DialogEntry Again(DialogEntry e)
        {
            return new DialogEntry
            {
                headerKey = e.headerKey, messageData = e.messageData, Template = e.Template, InputTemplate = e.InputTemplate, OnConfirm = e.OnConfirm,
                OnSecondOption = e.OnSecondOption, OnCancel = e.OnCancel, OnForcedCancel = e.OnForcedCancel, OnVisible = e.OnVisible, Icon = e.Icon,
                SecondaryIcon = e.SecondaryIcon, onCancelMessage = e.onCancelMessage, ConfirmTextOverride = e.ConfirmTextOverride,
                SecondOptionTextOverride = e.SecondOptionTextOverride, CancelTextOverride = e.CancelTextOverride
            };
        }

        private int KitIndex(int kit) => kit == TermRow ? _term : -1;

        private void KitOptions(int kit)
        {
            int n = kit == TermRow ? _termCount : 0;
            if (Pick.Options.Length < n)
            {
                Pick.Options = new string[Math.Max(n, 8)];
            }
            for (int i = 0; i < n; i++)
            {
                Pick.Options[i] = _termWords[i];
            }
            Pick.Count = n;
            Pick.Chosen = KitIndex(kit);
        }

        // A term chosen on the kit: nothing of the game's is pressed until the confirm, which takes it.
        private bool KitChoose(int kit, int option, bool exact)
        {
            if (kit != TermRow || _termCount == 0)
            {
                return false;
            }
            if (!exact)
            {
                option = Mathf.Clamp(option, 0, _termCount - 1);
            }
            if (option < 0 || option >= _termCount || (option == _term && !exact))
            {
                return false;
            }
            _term = option;
            return true;
        }

        private static string TermWords(int years, int daysPerYear)
        {
            string days = (years * Math.Max(1, daysPerYear)).ToString(CultureInfo.CurrentCulture);
            return years == 1
                ? UiText.Fill("reallife_ui_dialog_loan_term_year", "days", days)
                : UiText.Fill("reallife_ui_dialog_loan_term_years", "years", years.ToString(CultureInfo.CurrentCulture), "days", days);
        }

        // The rate a year as the game's form says it: the bank's whole per cent times the game's interest multiplier.
        private static string RateWords(int rate)
        {
            float effective = rate * GameLoans.Multiplier;
            return UiText.Fill("reallife_ui_dialog_loan_rate_value", "rate", effective.ToString("0.#", CultureInfo.CurrentCulture));
        }

        private static string Money(float amount) => UiText.Money(amount);

        // The line's own confirm, wrapped once: the kit's term and point written into the form, then the game's BankDialog.
        private sealed class LoanConfirm
        {
            private readonly Func<DialogEntry> _own;
            private readonly DialogForm _form;

            private LoanConfirm(Func<DialogEntry> own, DialogForm form)
            {
                _own = own;
                _form = form;
            }

            public static void Wrap(DialogEntry entry, DialogForm form)
            {
                if (entry.OnConfirm == null || entry.OnConfirm.Target is LoanConfirm)
                {
                    return;
                }
                entry.OnConfirm = new LoanConfirm(entry.OnConfirm, form).Give;
            }

            private DialogEntry Give()
            {
                try
                {
                    _form.LoanAgreed();
                }
                catch (Exception e)
                {
                    UiDirector.Logger?.Error(e);   // the bank's own figures stand: the game makes the loan as it would
                }
                return _own();
            }
        }
    }
}
