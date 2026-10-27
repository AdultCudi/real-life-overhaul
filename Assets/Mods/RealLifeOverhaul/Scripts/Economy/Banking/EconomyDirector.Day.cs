using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The credit side's hour and day: the game's loans read against the file, each instalment settled, the record, the banks' terms.</summary>
    public sealed partial class EconomyDirector
    {
        private static readonly int HotOnHour = HotSpots.Slot("Economy: EconomyDirector.OnHour");
        private static readonly int HotOnDay = HotSpots.Slot("Economy: EconomyDirector.OnDay");
        private static readonly int HotSettle = HotSpots.Slot("Economy: the day, the loans settled");
        private static readonly int HotAfterDay = HotSpots.Slot("Economy: the day, the features' days (AfterDay)");
        private static readonly int HotBooksDay = HotSpots.Slot("Economy: the day, yesterday's statement brought up to date");
        private static readonly int HotOnGameEvent = HotSpots.Slot("Economy: EconomyDirector.OnGameEvent");

        // GlobalEvents.onNewHour: every game hour, and first thing in the game's NewDay, before its daily run.
        private void OnHour()
        {
            using var hotSpot = HotSpots.Time(HotOnHour);
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                int day = Save.Day;
                KeepLoanRates();
                DeskDo("the hour", _deskKeep);   // what is held on a loan is in place before the loans are read: the night's run may follow at once
                SyncLoans(day);
                if (day != CreditLedger.File.SettledDay)
                {
                    bool first = _preRunDay != day;
                    _preRunDay = day;   // the marks are now exactly as the loans stand before this day's run
                    if (first)
                    {
                        DeskDo("the overdraft", () => _desk.BeforeRun(day));   // the overdraft covers a day that is short
                    }
                }
                DeskDo("the hour", () => _desk.Hour(day));
                CreditLedger.Touch();
                KeepTerms("the hour");
            }
            catch (Exception e)
            {
                Fault("the hour", e);
            }
            CustomerDraw.Hold("the economy's hour");   // the features' footfall writes of the hour: one batch (Core/CustomerDraw)
            try
            {
                Guard.Each(AfterHour);
            }
            finally
            {
                CustomerDraw.Release();
            }
        }

        // GlobalEvents.onNewDay: after the game's daily run (GameManager.NewDay; LoanHelper.RunDaily is in BusinessHelper.RunDaily before it).
        private void OnDay()
        {
            using var hotSpot = HotSpots.Time(HotOnDay);
            if (!_running || _broken || Save == null)
            {
                return;
            }
            CustomerDraw.Morning();   // the game has just drawn every business's customers: noted before any feature writes a figure
            try
            {
                using var settleSpot = HotSpots.Time(HotSettle);
                Settle(Save.Day);
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
            CustomerDraw.Hold("the economy's day");   // every feature's footfall writes of the morning: one batch, each business drawn again once
            try
            {
                using var afterSpot = HotSpots.Time(HotAfterDay);
                Guard.Each(AfterDay);
            }
            finally
            {
                CustomerDraw.Release();
            }
            try
            {
                using var booksSpot = HotSpots.Time(HotBooksDay);
                // the game built yesterday's statement before the mod's day: the costs booked for it since are put into it
                if (GameBooks.Refresh(Save.Day - 1) && Verbose)
                {
                    Logger?.Info("Economy check (books): the game's statement of day " + (Save.Day - 1) + " brought up to date with the mod's costs booked for it after it was made: PASS.");
                }
                // another author's dealerships (Core/EmpireMotors): what they spent is deducted by the game's tax, once a day
                float deducted = GameBooks.Deduct(Save.Day - 1);
                if (Verbose && (deducted > 0f || GameBooks.DealerTakings != 0f || GameBooks.DealerCosts != 0f))
                {
                    Logger?.Info("Economy check (Empire Motors): day " + (Save.Day - 1) + " the dealerships took " + GameBooks.DealerTakings.ToString("0") + " and spent "
                                 + GameBooks.DealerCosts.ToString("0") + " in the game's statements; " + deducted.ToString("0") + " given to the game's tax to deduct: "
                                 + (deducted <= Mathf.Max(0f, GameBooks.DealerCosts) + 1f ? "PASS" : "FAIL") + ".");
                }
            }
            catch (Exception e)
            {
                Fault("the game's statement", e);
            }
        }

        // The loans against the file's marks: what the player did since the last look.
        private void SyncLoans(int day)
        {
            CreditFile file = CreditLedger.File;
            GameLoans.Read(_live);
            _findings.Clear();
            LoanBook.Sync(file.Loans, _live, day, _findings);
            int arranged = 0;
            bool frozenPaid = false;
            for (int i = 0; i < _findings.Count; i++)
            {
                LoanFinding f = _findings[i];
                if ((f.Note == LoanNote.Gone || f.Note == LoanNote.ExtraPaid) && (f.Mark.Held & LoanHold.Frozen) != 0)
                {
                    arranged += Math.Max(0, f.Amount);   // paid by hand while a debt arrangement held it (EconoView, or a bank's app)
                    frozenPaid = true;
                }
                switch (f.Note)
                {
                    case LoanNote.New:
                        // a loan made at the bank: one made from the phone is on file before this runs
                        CreditLedger.Post(CreditKind.Application, f.Amount, day, GameLoans.AccountKey);
                        CreditLedger.Post(CreditKind.NewCredit, f.Amount, day, GameLoans.AccountKey);
                        break;
                    case LoanNote.Gone:
                        CreditLedger.Post(CreditKind.PaidOffEarly, f.Amount, day, GameLoans.AccountKey);
                        break;
                    case LoanNote.ExtraPaid:
                        CreditLedger.Post(CreditKind.ExtraPayment, f.Amount, day, GameLoans.AccountKey);
                        break;
                    case LoanNote.PaymentChanged:
                        CreditLedger.Post(CreditKind.PaymentChanged, f.Amount, day, GameLoans.AccountKey);
                        break;
                }
            }
            if (frozenPaid)
            {
                DeskDo("a loan paid in an arrangement", () => _desk.PaidByHand(arranged, "one of the game's loans, read from the game"));   // it ends here if that was the last of it
            }
        }

        // The game prices every loan again when its interest multiplier is changed (SaveGameManager.ApplyNewDifficulty calls
        // LoanHelper.RecalculateLoanPayments), at the rate each bank offers today, which is the rating's: each loan gets back the
        // rate it was made at, at the new multiplier, and its payment. Called before anything takes a loan's figures as its own.
        private void KeepLoanRates()
        {
            float now = GameLoans.Multiplier, was = _multiplier;
            CreditFile file = CreditLedger.File;
            if (Mathf.Approximately(now, was) || file == null)
            {
                return;
            }
            _multiplier = now;
            bool holds = !_deskBroken && _desk.Running;   // a desk that is off has written every loan's own figures back
            int kept = 0;
            for (int i = 0; i < file.Loans.Count; i++)
            {
                LoanMark m = file.Loans[i];
                var loan = m.Ref as Loan;
                if (loan == null)
                {
                    continue;
                }
                Lender bank = BankTerms.At(m.Street, m.Number);
                int years = bank != null && bank.Settings != null ? bank.Settings.yearsToPayLoan : 1;
                int interest = loan.dailyInterest, payment = loan.dailyPayment;
                LoanHolds.Repriced(m, ref interest, ref payment, was, now, years, AccountRules.DaysPerYear, holds);
                kept += interest != loan.dailyInterest || payment != loan.dailyPayment ? 1 : 0;
                loan.dailyInterest = interest;
                loan.dailyPayment = payment;
            }
            CreditLedger.Touch();
            Logger?.Info("Economy: the game's interest multiplier went from " + was.ToString("0.##", CultureInfo.InvariantCulture) + " to " + now.ToString("0.##", CultureInfo.InvariantCulture)
                         + " and the game priced its loans again at today's rates: " + kept + " of " + file.Loans.Count
                         + " loan(s) given back the rate each was made at, at the new multiplier, and its payment.");
        }

        // A loan paid off in EconoView is read at once, so a debt arrangement it clears ends the moment it is paid.
        private void OnGameEvent(string gameEvent)
        {
            using var hotSpot = HotSpots.Time(HotOnGameEvent);
            if (gameEvent != GameLoans.LoanPaidEvent || !_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                KeepLoanRates();
                SyncLoans(Save.Day);
                CreditLedger.Touch();
            }
            catch (Exception e)
            {
                Fault("a loan paid off", e);
            }
        }

        private void Settle(int day)
        {
            CreditFile file = CreditLedger.File;
            bool exact = _preRunDay == day;
            KeepLoanRates();
            GameLoans.Read(_live);
            _findings.Clear();
            LoanBook.Settle(file.Loans, _live, _findings);
            int payDay = day - 1;   // the game books its daily run on the day that has ended (LoanHelper.RunDaily)
            int paid = 0, paidAmount = 0, missed = 0, missedAmount = 0;
            StringBuilder log = Verbose ? new StringBuilder(200) : null;
            for (int i = 0; i < _findings.Count; i++)
            {
                LoanFinding f = _findings[i];
                switch (f.Note)
                {
                    case LoanNote.Paid:
                    case LoanNote.PaidOff:
                        paid++;
                        paidAmount += f.Amount;
                        break;
                    case LoanNote.Missed:
                        missed++;
                        missedAmount += f.Amount;
                        break;
                }
                if (log != null)
                {
                    log.Append(i > 0 ? "; " : string.Empty).Append(f.Mark.Street).Append(' ').Append(f.Mark.Number).Append(", ").Append(f.Mark.Total.ToString("0"))
                        .Append(", left ").Append(f.Mark.Remaining.ToString("0")).Append(" before the run: ")
                        .Append(f.Note == LoanNote.Paid ? "paid " : f.Note == LoanNote.PaidOff ? "paid off with " : "missed ").Append(f.Amount);
                }
            }
            file.Roll(day);
            CreditLedger.Touch();
            int before = CreditLedger.Score.Value;
            if (paid > 0)
            {
                CreditLedger.Post(CreditKind.PaidOnTime, paidAmount, payDay, GameLoans.AccountKey, paid);
            }
            for (int i = 0; i < _findings.Count; i++)
            {
                if (_findings[i].Note == LoanNote.PaidOff)
                {
                    CreditLedger.Post(CreditKind.PaidOff, (int)_findings[i].Mark.Total, payDay, GameLoans.AccountKey);
                }
            }
            int missEffect = missed > 0 ? CreditLedger.Post(CreditKind.Missed, missedAmount, payDay, GameLoans.AccountKey, missed) : 0;
            DeskDo("the day", () => _desk.Day(day, _findings));   // the mod's accounts take their payments after the game's loans
            ReadLoansQuietly(day);   // the marks were brought up to date a moment ago: nothing of the borrower's can be passed over
            ReadRecord(day);
            file.SettledDay = day;
            CreditLedger.Touch();
            int score = CreditLedger.Refresh();
            if (log != null)
            {
                Logger?.Info("Economy check (day " + day + "): " + _findings.Count + " instalment(s) read "
                             + (exact ? "against the loans as they stood before the run" : "against the last hour's reading (no hour event came before this run)")
                             + (log.Length > 0 ? ". " + log : string.Empty) + ". Rating " + before + " to " + score + ": " + (exact || _findings.Count == 0 ? "PASS" : "FAIL"));
            }
            KeepTerms("the day");
            if (missed > 0)
            {
                Notifications.Show(NotificationType.Error, "reallife_econ_note_missed", new Dictionary<string, string>
                {
                    { "amount", FinanceText.Money(missedAmount) }, { "points", Mathf.Abs(missEffect).ToString() },
                    { "score", score.ToString() }, { "grade", FinanceText.Grade(CreditLedger.Grade) }
                }, 12f, "reallife_econ_note_missed", Guard.Wrap(OpenCredit), true, false);
            }
            Badge();
        }

        // Read again with nothing said: only when the day's comparison has just brought every mark up to date.
        private void ReadLoansQuietly(int day)
        {
            GameLoans.Read(_live);
            LoanBook.Sync(CreditLedger.File.Loans, _live, day, null, true);
        }

        private void ReadRecord(int day)
        {
            CreditFile file = CreditLedger.File;
            LegalStanding now = Legal(), seen = file.LegalSeen;
            if (now.Convictions > seen.Convictions)
            {
                CreditLedger.Post(CreditKind.Conviction, 0, day, LegalSource, now.Convictions - seen.Convictions);
            }
            if (now.Judgments > seen.Judgments)
            {
                CreditLedger.Post(CreditKind.Judgment, 0, day, LegalSource, now.Judgments - seen.Judgments);
            }
            if (now.LateDays > seen.LateDays)
            {
                CreditLedger.Post(CreditKind.FineLate, now.Outstanding, day, LegalSource, now.LateDays - seen.LateDays);
            }
            else if (seen.LateDays > 0 && now.LateDays == 0 && now.Outstanding == 0)
            {
                CreditLedger.Post(CreditKind.DebtsCleared, seen.Outstanding, day, LegalSource);
            }
            file.LegalSeen = now;
        }

        private void KeepTerms(string when)
        {
            int score = CreditLedger.Score.Value;
            int changed = BankTerms.Apply(score);
            if (changed > 0 && Verbose)
            {
                string detail;
                bool ok = BankTerms.Check(score, false, out detail);
                Logger?.Info("Economy check (terms, " + when + "): rating " + score + ", " + changed + " bank(s) changed. " + detail + ": " + (ok ? "PASS" : "FAIL"));
            }
        }

        private void OnGrade(CreditGrade was, CreditGrade now)
        {
            try
            {
                bool up = now > was;
                int score = CreditLedger.File != null ? CreditLedger.File.LastScore : 0;
                bool shut = score < CreditCatalogue.LendingThreshold;
                bool told = !up || Settings.EconomyGradeUpNotes;   // a better grade may go untold; a worse one is always told
                if (told)
                {
                    Notifications.Show(up ? NotificationType.Success : NotificationType.Warning,
                        up ? "reallife_econ_note_grade_up" : shut ? "reallife_econ_note_grade_down_shut" : "reallife_econ_note_grade_down",
                        new Dictionary<string, string>
                        {
                            { "score", score.ToString() }, { "grade", FinanceText.Grade(now) }, { "was", FinanceText.Grade(was) },
                            { "threshold", CreditCatalogue.LendingThreshold.ToString() }
                        }, 10f, null, Guard.Wrap(OpenCredit), true, false);
                }
                if (Verbose)
                {
                    Logger?.Info("Economy: the grade changed from " + was + " to " + now + " at " + score + (told ? "." : "; no notification: a better grade is switched off."));
                }
                Badge();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void OnPosted(CreditEvent e, int before, int after)
        {
            if (Verbose)
            {
                Logger?.Info("Economy: " + (e.Test ? "test " : string.Empty) + "event " + e.Kind + " (day " + e.Day + ", amount " + e.Amount + ", count " + e.Count + ", from '" + e.Source
                             + "'): rating " + before + " to " + after + ". " + Factors(CreditLedger.Score));
            }
        }

        private static string Factors(CreditScore s)
        {
            return "Factors: payments " + s.Factor[0].ToString("0.00", CultureInfo.InvariantCulture) + " (" + s.OnTime + " on time, " + s.Missed + " missed), owed "
                + s.Factor[1].ToString("0.00", CultureInfo.InvariantCulture)
                + ", length " + s.Factor[2].ToString("0.00", CultureInfo.InvariantCulture) + " (" + s.HistoryDays + " days), record " + s.Factor[3].ToString("0.00", CultureInfo.InvariantCulture)
                + ", applications " + s.Factor[4].ToString("0.00", CultureInfo.InvariantCulture) + " (" + s.Applications + ").";
        }
    }
}
