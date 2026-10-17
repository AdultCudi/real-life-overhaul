using System;
using System.Collections.Generic;
using System.Text;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The credit side for the save tools and the options page's test buttons.</summary>
    public sealed partial class EconomyDirector
    {
        /// <summary>For the save tools: the banks get their own terms back and the app leaves the phone; the timer starts it again if the option is on.</summary>
        public void StopAll()
        {
            try
            {
                if (_running)
                {
                    DeskDo("leaving the save", () => _desk.Leave());   // nothing of the mod's stays on a loan, and what cash allows is settled
                }
                StandDown("the save tools asked", true, true);
            }
            catch (Exception e)
            {
                Fault("the save tools", e);
            }
        }

        /// <summary>For the save tools: the save's line has been removed, and so is what was read from it.</summary>
        public void Forget()
        {
            _running = false;
            CreditLedger.Close();
        }

        private bool TestReady(string test)
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.Economy || _broken || !_running)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            return true;
        }

        private void TestDone(string what)
        {
            KeepTerms("a test");
            Badge();
            Notifications.Show(NotificationType.Info, "reallife_econ_test_done", new Dictionary<string, string>
            {
                { "what", FinanceText.T(what) }, { "score", CreditLedger.Score.Value.ToString() }, { "grade", FinanceText.Grade(CreditLedger.Grade) }
            }, 8f, null, Guard.Wrap(OpenCredit), true, false);
        }

        /// <summary>"Test: show my credit report in the log".</summary>
        public void TestReport()
        {
            if (!TestReady("Credit report"))
            {
                return;
            }
            SyncLoans(Save.Day);
            CreditLedger.Touch();
            CreditScore s = CreditLedger.Score;
            CreditFile file = CreditLedger.File;
            CreditInputs inputs = CreditLedger.Gather();
            var text = new StringBuilder(900);
            text.Append("Economy: credit report, day ").Append(Save.Day).Append(". Rating ").Append(s.Value).Append(" (").Append(s.Grade).Append("), lenders want ")
                .Append(CreditCatalogue.LendingThreshold).Append(": ").Append(s.Lends ? "they lend" : "nobody lends").Append(". ").Append(Factors(s))
                .Append(" Owed ").Append(inputs.Owed.ToString("0")).Append(" of ").Append(inputs.Limit.ToString("0")).Append(" that could be borrowed; payments a day ")
                .Append(inputs.DailyService.ToString("0")).Append(" against an income of ").Append(inputs.DailyIncome.ToString("0")).Append(". Record: ")
                .Append(inputs.Legal.Convictions).Append(" conviction(s), ").Append(inputs.Legal.Judgments).Append(" civil judgment(s), ").Append(inputs.Legal.Outstanding)
                .Append(" owed in fines and court debts, ").Append(inputs.Legal.LateDays).Append(" day(s) late, ").Append(inputs.Legal.RecentOffences).Append(" recent offence(s). File: opened day ")
                .Append(file.StartDay).Append(", first credit day ").Append(file.FirstCreditDay).Append(", ").Append(file.Events.Count).Append(" event(s), ").Append(file.Days.Count)
                .Append(" day(s) of instalments, ").Append(file.Loans.Count).Append(" loan(s), test offset ").Append(file.TestOffset).Append(". Banks: ");
            string detail;
            bool ok = BankTerms.Check(s.Value, false, out detail);
            text.Append(detail).Append(": ").Append(ok ? "PASS" : "FAIL");
            Logger?.Warn(text.ToString());   // asked for by a button: written whether or not diagnostics are on
            for (int i = Mathf.Max(0, file.Events.Count - CreditCatalogue.LedgerShown); i < file.Events.Count; i++)
            {
                CreditEvent e = file.Events[i];
                Logger?.Warn("Economy:   day " + e.Day + " " + e.Kind + " amount " + e.Amount + " count " + e.Count + " effect " + e.Effect + (e.Test ? " (test)" : string.Empty));
            }
            TestDone("reallife_econ_test_done_report");
        }

        /// <summary>"Test: record a missed payment": a test entry, which "clear test entries" takes out.</summary>
        public void TestMissed()
        {
            if (TestReady("Missed payment"))
            {
                CreditLedger.Post(CreditKind.Missed, 100, Save.Day, TestSource, 1, true);
                TestDone("reallife_econ_test_done_missed");
            }
        }

        /// <summary>"Test: record 30 days of payments on time": a test entry a day, back from today.</summary>
        public void TestOnTime()
        {
            if (!TestReady("Payments on time"))
            {
                return;
            }
            int today = Save.Day;
            for (int back = TestDays - 1; back >= 0; back--)
            {
                CreditLedger.File.CountPayments(today - back, 1, 0, 0, true);
            }
            CreditLedger.Touch();
            int before = CreditLedger.File.LastScore;
            int after = CreditLedger.Refresh();
            var e = new CreditEvent { Kind = CreditKind.PaidOnTime, Day = today, Amount = 0, Count = TestDays, Effect = after - before, Test = true, Source = TestSource };
            CreditLedger.File.Add(e);
            OnPosted(e, before, after);
            TestDone("reallife_econ_test_done_on_time");
        }

        /// <summary>"Test: set the rating to ...": a test entry that moves the score there.</summary>
        public void TestSet(int target)
        {
            if (TestReady("Set the rating"))
            {
                CreditLedger.TestSet(target, Save.Day);
                TestDone("reallife_econ_test_done_set");
            }
        }

        /// <summary>"Test: clear test entries".</summary>
        public void TestClear()
        {
            if (TestReady("Clear test entries"))
            {
                bool any = CreditLedger.TestClear();
                TestDone(any ? "reallife_econ_test_done_cleared" : "reallife_econ_test_done_nothing");
            }
        }

        /// <summary>"Test: restore the banks' own terms": they stay the banks' own until the next game hour writes the rating's again.</summary>
        public void TestRestore()
        {
            if (Save == null)
            {
                TestRefusals.Tell(Logger, "Restore the banks' terms", TestRefusal.NotLoaded);
                return;
            }
            int restored = BankTerms.Restore();
            string detail;
            bool ok = BankTerms.Check(0, true, out detail);
            Logger?.Warn("Economy check (terms restored, the test button): " + restored + " bank(s) put back. " + detail + ": " + (ok ? "PASS" : "FAIL"));
            Notifications.Show(NotificationType.Info, "reallife_econ_test_restored", null, 8f, null, null, true, false);
        }
    }
}
