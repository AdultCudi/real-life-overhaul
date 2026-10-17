using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The Borrow tab: every bank's loan and the other kinds of credit; a bank picked shows its offer.</summary>
    public sealed partial class FinanceApp
    {
        private Lender _lender;   // the bank whose offer shows; null for none

        private string WriteBorrow(InfoBox box, CreditScore score)
        {
            _spec = FinanceTables.Borrow;
            if (!score.Lends)
            {
                _lender = null;
                box.Title(FinanceText.T("reallife_econ_borrow_refused_title"));
                box.Chip(FinanceText.Grade(score.Grade), Tone(score.Grade));
                box.Line(FinanceText.T("reallife_econ_borrow_refused", "score", score.Value.ToString(), "grade", FinanceText.Grade(score.Grade),
                    "threshold", CreditCatalogue.LendingThreshold.ToString()), CellTone.Red);
                TipLines(box, score);
                Say("refused", FinanceText.T("reallife_econ_row_no_lender", "threshold", CreditCatalogue.LendingThreshold.ToString()));
                Meta.Guide.Point(box, GuideTitle);
                return null;
            }
            IReadOnlyList<Lender> lenders = BankTerms.Lenders;
            string selected = ProductRows();
            bool found = false;
            string loanKind = FinanceText.T(FinanceTables.OfferKinds[0]);
            for (int i = 0; i < lenders.Count; i++)
            {
                Lender lender = lenders[i];
                TableRow row = Row("lender:" + i);
                row.Pick = true;
                row.Index = i;
                row.Kind = 0;
                float most = PhoneLending.MostNow(lender, score.Value), rate = PhoneLending.RateOffered(lender, score.Value);
                bool lends = most >= CreditCatalogue.LoanLeast;
                row.Set(0, lender.Name);
                row.Set(1, loanKind);
                row.Set(2, FinanceText.Money(most), most, lends ? TableRow.Plain : TableRow.Red);
                row.Set(3, FinanceText.T("reallife_econ_row_rate", "rate", rate.ToString("0.#")), rate);
                if (lends)
                {
                    // Borrowing the most on offer: what it takes each day, its fee and its term, one figure to compare banks by.
                    LoanQuote quote = PhoneLending.Quote(lender, Mathf.FloorToInt(most), score.Value);
                    row.Set(4, FinanceText.Money(quote.DailyPayment + quote.DailyInterest), quote.DailyPayment + quote.DailyInterest);
                    row.Set(5, FinanceText.Money(quote.Fee), quote.Fee);
                    row.Set(6, FinanceText.T("reallife_econ_row_days", "days", quote.Days.ToString()), quote.Days);
                }
                row.Set(7, FinanceText.T(lends ? "reallife_econ_row_on_offer" : "reallife_econ_row_not_offered"), lends ? TableRow.Plain : TableRow.Red);
                if (lender == _lender)
                {
                    selected = row.Id;
                    found = true;
                }
            }
            if (!found)
            {
                _lender = null;
            }
            if (lenders.Count == 0)
            {
                Say("none", FinanceText.T("reallife_econ_row_no_banks"));
            }
            if (_lender == null && _product >= 0)
            {
                WriteProduct(box);
                return selected;
            }
            if (_lender == null)
            {
                box.Title(FinanceText.T("reallife_econ_tab_borrow"));
                box.Chip(FinanceText.Grade(score.Grade), Tone(score.Grade));
                box.Note(FinanceText.T("reallife_econ_borrow_pick"));
                box.Section("reallife_ibox_sec_how");
                string share = (CreditCatalogue.PhoneFeeShare * 100f).ToString("0.#") + "%";
                box.Note(FinanceText.T("reallife_econ_borrow_fee_note", "share", share, "least", FinanceText.Money(CreditCatalogue.PhoneFeeLeast)));
                box.Note(FinanceText.T("reallife_tab_borrow_columns_note"));
                box.Note(FinanceText.T("reallife_econ_borrow_repay_note"));
                Meta.Guide.Point(box, GuideTitle);
                return null;
            }
            WriteOffer(box, score);
            return selected;
        }

        private void WriteOffer(InfoBox box, CreditScore score)
        {
            Lender lender = _lender;
            int most = Mathf.FloorToInt(PhoneLending.MostNow(lender, score.Value));
            box.Title(lender.Name);
            box.Subtitle(FinanceText.T(FinanceTables.OfferKinds[0]));
            box.Section("reallife_ibox_sec_terms");
            box.Row("reallife_econ_offer_most", FinanceText.Money(most));
            if (most < CreditCatalogue.LoanLeast)
            {
                box.Line(FinanceText.T("reallife_econ_offer_nothing", "least", FinanceText.Money(CreditCatalogue.LoanLeast)), CellTone.Red);
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
                return;
            }
            _amount = Mathf.Clamp(_amount, CreditCatalogue.LoanLeast, most);
            Amounts(most);
            LoanQuote quote = PhoneLending.Quote(lender, _amount, score.Value);
            box.Row("reallife_econ_offer_rate", FinanceText.T("reallife_econ_row_rate", "rate", quote.AnnualRate.ToString("0.#")));
            AmountSelector(box);
            UIs uis = InstanceBehavior<UIs>.Instance;
            Transform entry = box.ButtonRow();
            if (uis != null && uis.textInputUi != null)
            {
                InfoBox.Button(entry, InfoBox.Additional, "reallife_econ_offer_type", () =>
                    uis.textInputUi.Show("reallife_econ_offer_placeholder", "common_confirm", text => UiClone.Safe(() => Typed(text)), null, _amount.ToString(), false, TypedAmountLength));
            }
            box.Headline("reallife_econ_offer_cost");
            box.Row("reallife_econ_offer_payment", FinanceText.Money(quote.DailyPayment));
            box.Row("reallife_econ_offer_interest", FinanceText.Money(quote.DailyInterest));
            box.Row("reallife_econ_offer_days", quote.Days.ToString());
            box.Row("reallife_econ_offer_fee", FinanceText.Money(quote.Fee));
            box.Row("reallife_econ_offer_total", FinanceText.Money(quote.Cost));
            box.Row("reallife_tab_offer_repay", FinanceText.Money(_amount + quote.Cost));
            float owedThem = GameLoans.OwedTo(lender.Address);
            if (owedThem > 0f)
            {
                box.Row("reallife_tab_offer_owed_them", FinanceText.Money(owedThem), CellTone.Yellow);
            }
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_offer_apply", Apply);
            InfoBox.Button(buttons, InfoBox.Additional, "common_cancel", ClosePick);
            // What the rating did to the rate: this bank's rate at the best rating there is, against the one offered.
            float best = PhoneLending.RateOffered(lender, CreditCatalogue.ScoreMax);
            box.Note(quote.AnnualRate > best + 0.05f
                ? FinanceText.T("reallife_tab_offer_rating_adds", "points", (quote.AnnualRate - best).ToString("0.#"), "best", best.ToString("0.#"))
                : FinanceText.T("reallife_tab_offer_rating_best"));
            Meta.Guide.Point(box, GuideTitle);
        }

        // Round figures under the most on offer, the most, and the amount chosen.
        private void Amounts(int most)
        {
            _amounts.Clear();
            for (int i = 0; i < AmountSteps.Length && AmountSteps[i] < most; i++)
            {
                _amounts.Add(AmountSteps[i]);
            }
            _amounts.Add(most);
            InsertAmount();
        }

        // A typed amount outside the least and the most is moved to it, with a notice.
        private void Typed(string text)
        {
            if (_lender == null || !CreditLedger.Ready)
            {
                return;
            }
            if (!Whole(text, out int typed))
            {
                Notifications.ShowError("common_notification_invalid_amount", null, false);
                return;
            }
            int most = Mathf.FloorToInt(PhoneLending.MostNow(_lender, CreditLedger.Score.Value));
            if (typed < CreditCatalogue.LoanLeast)
            {
                TellTooLittle();
                typed = CreditCatalogue.LoanLeast;
            }
            else if (typed > most)
            {
                Notifications.Show(NotificationType.Error, "reallife_econ_note_too_much", new Dictionary<string, string> { { "lender", _lender.Name }, { "most", FinanceText.Money(most) } },
                    6f, null, null, true, false);
                typed = most;
            }
            _amount = typed;
            Refresh();
        }

        // Everything the loan costs is said in the game's own question box before it is taken.
        private void Apply()
        {
            Lender lender = _lender;
            if (lender == null || !CreditLedger.Ready)
            {
                return;
            }
            if (!CreditLedger.CanBorrow(out CreditRefusal _))
            {
                _director.TellRefused();
                Refresh();
                return;
            }
            int amount = _amount;
            LoanQuote quote = PhoneLending.Quote(lender, amount, CreditLedger.Score.Value);
            GameConfirm.Show("reallife_econ_confirm_title".Localize(new { lender = lender.Name }),
                "reallife_econ_confirm_body".Localize(new
                {
                    amount = FinanceText.Money(amount),
                    lender = lender.Name,
                    rate = quote.AnnualRate.ToString("0.#"),
                    payment = FinanceText.Money(quote.DailyPayment),
                    interest = FinanceText.Money(quote.DailyInterest),
                    days = quote.Days.ToString(),
                    fee = FinanceText.Money(quote.Fee),
                    cost = FinanceText.Money(quote.Cost)
                }),
                () => UiClone.Safe(() => Confirmed(lender, amount)), null, "reallife_econ_offer_apply", "common_cancel", false, false);
        }

        private void Confirmed(Lender lender, int amount)
        {
            LoanAnswer answer = _director.Borrow(lender, amount, out LoanQuote quote);
            switch (answer)
            {
                case LoanAnswer.Approved:
                    Notifications.Show(NotificationType.Success, "reallife_econ_note_lent", new Dictionary<string, string>
                    {
                        { "lender", lender.Name }, { "amount", FinanceText.Money(amount) }, { "fee", FinanceText.Money(quote.Fee) },
                        { "payment", FinanceText.Money(quote.DailyPayment + quote.DailyInterest) }
                    }, 10f, null, null, true, false);
                    _lender = null;
                    _tab = Finance.Overview;
                    break;
                case LoanAnswer.TooMuch:
                    Notifications.Show(NotificationType.Error, "reallife_econ_note_too_much", new Dictionary<string, string>
                    {
                        { "lender", lender.Name }, { "most", FinanceText.Money(PhoneLending.MostNow(lender, CreditLedger.Value)) }
                    }, 8f, null, null, true, false);
                    break;
                case LoanAnswer.TooLittle:
                    TellTooLittle();
                    break;
                default:
                    if (CreditLedger.Ready)
                    {
                        _director.TellRefused();
                    }
                    break;
            }
            After();
        }

        private static void TellTooLittle()
        {
            var least = new Dictionary<string, string> { { "least", FinanceText.Money(CreditCatalogue.LoanLeast) } };
            Notifications.Show(NotificationType.Error, "reallife_econ_note_too_little", least, 6f, null, null, true, false);
        }
    }
}
