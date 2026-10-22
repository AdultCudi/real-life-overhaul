using System;
using System.Collections.Generic;
using System.Globalization;
using Dialogs;
using Helpers;
using RealLifeOverhaul.GameUi;
using SpecialServices.Bank;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The credit side as the player meets it: the Finance app, a loan from the phone, and a bank's dialogue under the threshold.</summary>
    public sealed partial class EconomyDirector
    {
        private void Install()
        {
            using var hotSpot = HotSpots.Time(HotInstall);
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            var view = new FinanceApp(this);
            GameSprites.Want(FinanceApp.AppGlyphs);
            bool tryAgain;
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_econ_app",
                Icon = like => AppIconPainter.Paint(like, FinanceApp.Glyph(), new Color32(92, 214, 148, 255), new Color32(24, 148, 110, 255), 0.42f, "AppIcon-Finance"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Economy: the game's phone is not built as this version expects, so the Finance app is left off. The rating and the banks' terms still work.");
                }
                return;
            }
            _view = view;
            Badge();
        }

        // Under the threshold the bank's cap is 0 and the game's dialogue says only "the most we can lend you is $0", so when a
        // bank's dialogue opens (at the counter or on a call) the reason is said once, through the game's notification.
        private void TellAtTheBank()
        {
            DialogController open = DialogController.current;
            if (open == null || open.dialog == null)
            {
                _toldDialogue = null;
                return;
            }
            if (!(open.dialog is BankDialog) || ReferenceEquals(open.dialog, _toldDialogue))
            {
                return;
            }
            _toldDialogue = open.dialog;
            BankSettings settings = open.contact != null && open.contact.Address != null ? LoanHelper.GetBankSettings(open.contact.Address) : null;
            if (settings != null && BankTerms.QuestHold(settings))
            {
                return;   // one of the game's quest loans is open here: this bank lends on its own terms
            }
            TellRefused();
            if (Verbose)
            {
                Logger?.Info("Economy: a bank's dialogue opened with the rating at " + CreditLedger.Score.Value + ", under " + CreditCatalogue.LendingThreshold
                             + ": the reason was said (the bank's cap is 0, so the game refuses any amount).");
            }
        }

        /// <summary>For the credit desk: a loan it has just made gets its mark now, so the hour does not take it for one made at a bank.</summary>
        public LoanMark Adopt(Loan loan)
        {
            if (!_running || CreditLedger.File == null || Save == null || loan == null)
            {
                return null;
            }
            List<LoanMark> marks = CreditLedger.File.Loans;
            for (int i = 0; i < marks.Count; i++)
            {
                if (ReferenceEquals(marks[i].Ref, loan))
                {
                    return marks[i];
                }
            }
            var bank = loan.bankAddress;
            var mark = new LoanMark
            {
                Street = bank != null ? CreditStore.Clean(bank.streetName) : string.Empty, Number = bank != null ? bank.streetNumber : 0, Total = loan.totalAmount, FirstDay = Save.Day,
                Remaining = loan.remainingAmount, Payment = loan.dailyPayment, Interest = loan.dailyInterest, Ref = loan
            };
            marks.Add(mark);
            return mark;
        }

        /// <summary>For the credit desk: something that moves the rating or a bank's terms has changed.</summary>
        public void TermsChanged(string when)
        {
            if (!_running || _broken || Save == null)
            {
                return;
            }
            CreditLedger.Touch();
            KeepTerms(when);
            Badge();
        }

        /// <summary>The refusal, with the one or two things that would raise the rating most.</summary>
        public void TellRefused()
        {
            DeskDo("a refusal", _deskRefused);   // the private lender makes itself known, once
            CreditScore score = CreditLedger.Score;
            Notifications.Show(NotificationType.Warning, "reallife_econ_note_refused", new Dictionary<string, string>
            {
                { "score", score.Value.ToString() }, { "grade", FinanceText.Grade(score.Grade) },
                { "threshold", CreditCatalogue.LendingThreshold.ToString() }, { "tips", FinanceText.Tips(score, _tips) }
            }, 14f, "reallife_econ_note_refused", Guard.Wrap(OpenCredit), true, false);
        }

        /// <summary>A loan from the Finance app or a bank app, the rating and the lender's limit asked again now.</summary>
        public LoanAnswer Borrow(Lender lender, int amount, out LoanQuote quote)
        {
            quote = default(LoanQuote);
            if (!_running || _broken || Save == null)
            {
                return LoanAnswer.BelowThreshold;
            }
            try
            {
                int day = Save.Day;
                SyncLoans(day);   // anything done at a bank since the last hour is on file first
                CreditLedger.Touch();
                int score = CreditLedger.Score.Value;
                float most = PhoneLending.MostNow(lender, score);
                Loan loan;
                LoanAnswer answer = PhoneLending.Take(lender, amount, score, out quote, out loan);
                if (answer == LoanAnswer.Approved)
                {
                    GameLoans.Read(_live);
                    LoanBook.Sync(CreditLedger.File.Loans, _live, day, null, true);   // the new loan has its mark: the hour will not take it for one made at the bank
                    CreditLedger.Post(CreditKind.Application, amount, day, PhoneSource);
                    CreditLedger.Post(CreditKind.NewCredit, amount, day, PhoneSource);
                    KeepTerms("a loan by phone");
                }
                if (Verbose)
                {
                    Logger?.Info("Economy check (application): " + amount + " from " + (lender != null ? lender.Name : "nobody") + " by phone at rating " + score + ", the most it lends now "
                                 + most.ToString("0")
                                 + (answer == LoanAnswer.Approved
                                     ? ": approved at " + quote.AnnualRate.ToString("0.#", CultureInfo.InvariantCulture) + "% a year, " + quote.DailyPayment + " a day and " + quote.DailyInterest
                                         + " interest for " + quote.Days + " days, fee " + quote.Fee + ": PASS"
                                     : ": refused (" + answer + "): PASS"));
                }
                return answer;
            }
            catch (Exception e)
            {
                Fault("a loan by phone", e);
                return LoanAnswer.BelowThreshold;
            }
        }

        public void Open(int tab)
        {
            if (_app != null && _view != null && _running)
            {
                _view.Want(tab);
                _app.Open();
            }
        }

        private void OpenCredit()
        {
            Open(Finance.Credit);
        }

        public void OpenStanding()
        {
            if (_app != null && _view != null && _running)
            {
                _view.WantStanding();
                _app.Open();
            }
        }

        public void OpenRivals()
        {
            if (_app != null && _view != null && _running)
            {
                _view.WantRivals();
                _app.Open();
            }
        }

        public void OpenCity()
        {
            if (_app != null && _view != null && _running)
            {
                _view.WantCity();
                _app.Open();
            }
        }

        /// <summary>The Finance app has opened: the file is brought up to date with the game, and what the badge counted has been seen. False if it cannot be.</summary>
        public bool AppOpened()
        {
            if (!_running || _broken || Save == null)
            {
                return false;
            }
            try
            {
                SyncLoans(Save.Day);
                CreditLedger.Touch();
                KeepTerms("the app opened");
                CreditFile file = CreditLedger.File;
                file.UnseenMissed = 0;
                file.UnseenGrade = false;
                Badge();
                return true;
            }
            catch (Exception e)
            {
                Fault("opening the app", e);
                return false;
            }
        }

        /// <summary>For a bank app: the file brought up to date with the game, as when the Finance app opens. False if it cannot be.</summary>
        public bool BringUpToDate(string why)
        {
            if (!_running || _broken || Save == null)
            {
                return false;
            }
            try
            {
                SyncLoans(Save.Day);
                CreditLedger.Touch();
                KeepTerms(why);
                Badge();
                return true;
            }
            catch (Exception e)
            {
                Fault(why, e);
                return false;
            }
        }

        private void Badge()
        {
            CreditFile file = CreditLedger.File;
            if (_app != null && file != null)
            {
                _app.SetBadge(_app.Showing ? 0 : file.UnseenMissed + (file.UnseenGrade ? 1 : 0));
            }
        }
    }
}
