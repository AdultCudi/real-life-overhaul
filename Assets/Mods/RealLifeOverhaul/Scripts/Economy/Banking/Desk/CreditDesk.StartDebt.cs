using System;
using System.Collections.Generic;
using Helpers;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The credit desk making the student loan a new game starts with (option "New games start with a student loan"): one of
    /// the game's own loans at the biggest bank, with no money paid out and nothing written on the file but the loan itself, as
    /// if the save had begun with it. The loan carries the study it paid for, so the apps name it, and the player is told once.
    /// From then on the game takes its payments and the credit side reads them, and the ones it cannot take, like every loan's.
    /// Cost: nothing per frame; once a second only in the first seconds of a game, while it waits for a bank to be found.
    /// </summary>
    public sealed partial class CreditDesk
    {
        private const int StartDebtSeconds = 30;   // seconds the city's banks are waited for once the file is opened, as the director does

        private bool _startDebtWaiting;   // the file was just opened with the option on: the loan is asked for each second until answered
        private int _startDebtWaited;

        // The file was opened for this save a moment ago, or was not: a save the credit side has seen is never given a loan.
        private void StartDebtBegin(bool opened)
        {
            _startDebtWaiting = opened && Settings.StartDebt;
            _startDebtWaited = 0;
            if (_startDebtWaiting)
            {
                StartDebtSecond();
            }
        }

        // Asked as the file opens and each second after while no bank has been found; every other answer is the last.
        private void StartDebtSecond()
        {
            if (Save == null)
            {
                return;
            }
            Lender bank = BankTerms.House;
            bool lends = bank != null && bank.Settings != null && bank.Address != null;
            StartDebtAnswer answer = StartDebtRules.Decide(Settings.StartDebt, Today, Save.Loans.Count, GoingPublic.InSession,
                TutorialHelper.IsTutorialEnabled(), lends);
            if (answer == StartDebtAnswer.NoBank && ++_startDebtWaited < StartDebtSeconds)
            {
                return;
            }
            _startDebtWaiting = false;
            if (answer == StartDebtAnswer.Yes)
            {
                MakeStartDebt(bank);
                return;
            }
            if (answer == StartDebtAnswer.Tutorial)
            {
                Note(NotificationType.Info, "reallife_econ_note_start_debt_tutorial", null, null, 14f);
            }
            Logger?.Info("Economy: the student loan this game was to start with was not made: " + StartDebtWhy(answer) + ".");
        }

        private static string StartDebtWhy(StartDebtAnswer answer)
        {
            switch (answer)
            {
                case StartDebtAnswer.Off: return "the option is off";
                case StartDebtAnswer.NotNew: return "the game is past its first day, or already owes";
                case StartDebtAnswer.Session: return "a Going Public session is on";
                case StartDebtAnswer.Tutorial: return "the game's tutorial is on";
                default: return "the city has no bank";
            }
        }

        // The figures for the study chosen, at this bank's own terms for paying a loan over.
        private static StartDebtFigures StartDebtFiguresAt(Lender bank)
        {
            return StartDebtRules.Figures(Settings.StartDebtDegree, Prices.Level, bank.Settings.yearsToPayLoan, Multiplier,
                AccountRules.DaysPerYear);
        }

        // The loan itself: in the game's list and on the file at once, so the hour does not take it for one made at a bank and
        // no application is counted; no money moves and no event is raised, as the game raises none for a loan it begins with.
        private void MakeStartDebt(Lender bank)
        {
            int day = Today;
            StartDebtFigures f = StartDebtFiguresAt(bank);
            var loan = new Loan(f.Amount, f.Interest, f.Payment, bank.Address);
            Save.Loans.Add(loan);
            var mark = new LoanMark
            {
                Street = bank.Street, Number = bank.Number, Total = loan.totalAmount, FirstDay = day, Remaining = loan.remainingAmount,
                Payment = loan.dailyPayment, Interest = loan.dailyInterest, Ref = loan, Purpose = LoanPurpose.Student
            };
            _file.Loans.Add(mark);
            if (_file.FirstCreditDay <= 0)
            {
                _file.FirstCreditDay = Math.Max(1, day);   // holding credit is where a history begins
            }
            CreditLedger.Touch();
            // when the banks were found after the start this moves the rating and the terms; at the start the director works both out next
            _director.TermsChanged("a student loan");
            Note(NotificationType.Warning, "reallife_econ_note_start_debt", StartDebtData(bank, f), null, 16f);
            int least = StartDebtRules.MinimumPayment(f.Amount, bank.Settings.yearsToPayLoan, AccountRules.DaysPerYear);
            Logger?.Info("Economy: this game starts owing a student loan of " + f.Amount + " from " + bank.Name + " (choice "
                         + StartDebtRules.Level(Settings.StartDebtDegree) + "): " + f.Payment + " a day and " + f.Interest
                         + " interest, paid off in " + f.Days + " days at that; the bank's least payment is " + least + ".");
            bool made = Save.Loans.Contains(loan) && _file.Loans.Contains(mark) && mark.Purpose == LoanPurpose.Student;
            Check("start debt: " + f.Amount + " from " + bank.Name + ", " + f.Payment + " a day (least " + least + ") and " + f.Interest
                  + " interest, " + Save.Loans.Count + " loan(s) in the game", made && f.Amount > 0 && f.Payment >= least);
        }

        // A dictionary each: the game keeps a notification's data for as long as it lists the notification.
        private static Dictionary<string, string> StartDebtData(Lender bank, StartDebtFigures f)
        {
            return Data("amount", FinanceText.Money(f.Amount), "bank", bank.Name, "payment", FinanceText.Money(f.Payment),
                "interest", FinanceText.Money(f.Interest));
        }

        /// <summary>
        /// "Show the student loan a new game would start with": the figures for the study chosen at the biggest bank, logged
        /// and told; nothing is made.
        /// </summary>
        public void TestStartDebt()
        {
            if (!TestReady("Start debt"))
            {
                return;
            }
            Lender bank = BankTerms.House;
            if (bank == null || bank.Settings == null)
            {
                TestRefusals.TellKey(Logger, "Start debt", "reallife_econ_row_no_banks");
                return;
            }
            StartDebtFigures f = StartDebtFiguresAt(bank);
            int least = StartDebtRules.MinimumPayment(f.Amount, bank.Settings.yearsToPayLoan, AccountRules.DaysPerYear);
            Logger?.Warn("Economy check (test: the student loan a new game starts with): " + f.Amount + " from " + bank.Name + ", "
                         + f.Payment + " a day (the bank's least " + least + ") and " + f.Interest + " interest, paid off in " + f.Days
                         + " days at that; nothing was made: " + (f.Payment >= least && f.Amount > 0 ? "PASS" : "FAIL"));
            Notifications.Show(NotificationType.Info, "reallife_econ_note_start_debt_test", StartDebtData(bank, f), 12f, null,
                Guard.Wrap(_openAccounts), true, false);
        }
    }
}
