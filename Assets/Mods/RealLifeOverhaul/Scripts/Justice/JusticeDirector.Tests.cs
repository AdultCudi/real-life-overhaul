using System;
using Helpers;
using UI.Notification;

namespace RealLifeOverhaul
{
    // The options page's test buttons. Each works when pressed or says why not.
    public sealed partial class JusticeDirector
    {
        private JusticeRefusal Ready()
        {
            return !ModState.Loaded || !JusticeState.Loaded || SaveGameManager.Current == null || PlayerHelper.PlayerController == null ? JusticeRefusal.NotLoaded
                : !JusticeSettings.Enabled ? JusticeRefusal.FeatureOff
                : _broken ? JusticeRefusal.Broken
                : JusticeRefusal.None;
        }

        private JusticeRefusal Free()
        {
            JusticeRefusal why = Ready();
            return why != JusticeRefusal.None ? why
                : JusticeState.InPrison ? JusticeRefusal.InPrison
                : _routine || _hearing != null || _bailOpen || _fileOpen || JusticeState.JailHoursPending > 0 || JusticeState.HearNow != 0 ? JusticeRefusal.Busy
                : JusticeRefusal.None;
        }

        /// <summary>Test: opens a case. 0 minor (evading), 1 serious (an injury), 2 grave (a death), 3 one with no evidence behind it.</summary>
        public void TestCase(int kind)
        {
            try
            {
                JusticeRefusal why = Ready();
                if (why == JusticeRefusal.None && JusticeState.OpenCount >= MostOpenCases)
                {
                    why = JusticeRefusal.Busy;
                }
                if (why != JusticeRefusal.None)
                {
                    JusticeRefusals.Tell(Logger, "Open a case", why, "open cases " + JusticeState.OpenCount);
                    return;
                }
                JusticeReport report =
                    kind == 0 ? new JusticeReport(JusticeLaw.Evading, JusticeLaw.Reckless) { Witnesses = 1, InTheAct = true, Fled = true, PaidAtScene = 900, HeldHours = 3 }
                    : kind == 1 ? new JusticeReport(JusticeLaw.HitRunInjury, JusticeLaw.Reckless) { Witnesses = 2, Cameras = true, Fled = true, HeldHours = 4 }
                    : kind == 2 ? new JusticeReport(JusticeLaw.HitRunDeath, JusticeLaw.Evading) { Witnesses = 2, Cameras = true, InTheAct = true, Fled = true, HeldHours = 5 }
                    : new JusticeReport(JusticeLaw.Theft) { HeldHours = 4, PaidAtScene = 200, Complied = true, Evidence = new[] { 0.2f } };
                KeepTodaysCasesApart();
                OpenCase(report, arrested: true, test: true);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        // A test's case of its own each time: today's open cases do not take it in.
        private static void KeepTodaysCasesApart()
        {
            int today = TimeHelper.CurrentDay;
            for (int i = 0; i < JusticeState.Cases.Count; i++)
            {
                if (JusticeState.Cases[i].Open && JusticeState.Cases[i].OpenedDay == today)
                {
                    JusticeState.Cases[i].OpenedDay = today - 1;
                }
            }
        }

        /// <summary>
        /// Test: the sequence a player reported. A hit-and-run got away from, the plate seen, goes to court by the pursuit's own
        /// path; the case is dismissed at once; then no bill may be waiting for it. One check line, PASS or the reason.
        /// </summary>
        public void TestCameraDismissed()
        {
            try
            {
                JusticeRefusal why = Ready();
                if (why == JusticeRefusal.None && (JusticeState.OpenCount >= MostOpenCases || _hearing != null))
                {
                    why = JusticeRefusal.Busy;
                }
                if (why != JusticeRefusal.None)
                {
                    JusticeRefusals.Tell(Logger, "Camera bill after a dismissal", why, "open cases " + JusticeState.OpenCount);
                    return;
                }
                KeepTodaysCasesApart();
                int billBefore = ModState.PendingFine;
                float dueBefore = ModState.PendingFineDue;
                int damages = Prices.At(1200);   // a car hit on the way too
                JusticeCase c = PolicePursuit.SummonOrBill(damages, PolicePursuit.InjuryEscapedFine, killed: false, runningFine: 0);
                int billAfterSummons = ModState.PendingFine;
                if (c != null)
                {
                    Dismiss(c, announce: true);
                }
                int billAfter = ModState.PendingFine;
                string fault = c == null ? "no case was opened, so the bill stood alone"
                    : c.Damages != damages ? "the case holds " + c.Damages + " of damages, not " + damages
                    : c.Open || c.Outcome != CaseOutcome.Dismissed ? "the case was not dismissed"
                    : billAfterSummons != billBefore ? "a bill of " + (billAfterSummons - billBefore) + " was queued beside the summons"
                    : billAfter != billBefore ? "a bill of " + (billAfter - billBefore) + " was queued after the dismissal"
                    : null;
                if (c == null || billAfter != billBefore)
                {
                    ModState.PendingFine = billBefore;   // the test's own bill is never charged
                    ModState.PendingFineDue = dueBefore;
                }
                Logger?.Info("Justice check (camera bill after a dismissal): " + (c != null ? "case " + c.Number + ", damages held " + c.Damages : "no case")
                             + "; bill waiting before " + billBefore + ", after the summons " + billAfterSummons
                             + ", after the dismissal " + billAfter + ": " + (fault == null ? "PASS." : "FAIL, " + fault + "."));
                JusticeText.Toast(fault == null ? NotificationType.Success : NotificationType.Error,
                    fault == null ? "reallife_justice_test_camera_pass" : "reallife_justice_test_camera_fail", 10f);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        /// <summary>Test: the next open case's court date becomes today (tomorrow if the court has risen), with bail settled.</summary>
        public void TestCourtDate()
        {
            try
            {
                JusticeRefusal why = Free();
                JusticeCase c = JusticeState.NextOpen;
                if (why == JusticeRefusal.None && c == null)
                {
                    why = JusticeRefusal.NoCase;
                }
                if (why != JusticeRefusal.None)
                {
                    JusticeRefusals.Tell(Logger, "Advance to the court date", why);
                    return;
                }
                int today = TimeHelper.CurrentDay;
                if (c.Stage == CaseStage.Warrant)
                {
                    JusticeText.Toast(NotificationType.Info, "reallife_justice_test_date_warrant", 9f);
                    return;
                }
                c.Stage = CaseStage.Pending;
                c.CourtDay = TimeHelper.CurrentHour < JusticeLaw.CourtCloses - 1 ? today : today + 1;
                c.Reminded = 0;
                _lastHour = -1;   // the reminders are looked at again on the next second
                JusticeText.Toast(NotificationType.Info, "reallife_justice_test_date_set", "day", JusticeText.N(c.CourtDay), "place", CourtPlaceName(), 10f);
                Logger?.Info("Justice test: case " + c.Number + " is now due on day " + c.CourtDay + ".");
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        /// <summary>Test: the next open case is heard as soon as the menu is closed, wherever the player is.</summary>
        public void TestHearing()
        {
            try
            {
                JusticeRefusal why = Free();
                JusticeCase c = JusticeState.NextOpen;
                if (why == JusticeRefusal.None && c == null)
                {
                    why = JusticeRefusal.NoCase;
                }
                if (why != JusticeRefusal.None)
                {
                    JusticeRefusals.Tell(Logger, "Run the hearing", why);
                    return;
                }
                if (c.Stage == CaseStage.Bail)
                {
                    c.Stage = CaseStage.Pending;
                    c.CourtDay = TimeHelper.CurrentDay;
                }
                RequestHearing(c, remote: true);
                JusticeText.Toast(NotificationType.Info, "reallife_justice_test_soon", 6f);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        /// <summary>Test: a prison term of this many days begins as soon as the menu is closed. The days really pass.</summary>
        public void TestPrison(int days)
        {
            try
            {
                JusticeRefusal why = Free();
                if (why == JusticeRefusal.None && JusticeSettings.NoPrison)
                {
                    why = JusticeRefusal.PartOff;
                }
                if (why != JusticeRefusal.None)
                {
                    JusticeRefusals.Tell(Logger, "Prison", why);
                    return;
                }
                JusticeState.ClearPrison();
                JusticeState.PrisonTotal = JusticeState.PrisonOriginal = JusticeLaw.ClampCap(days);
                JusticeText.Toast(NotificationType.Info, "reallife_justice_test_soon", 6f);
                Logger?.Info("Justice test: a prison term of " + JusticeState.PrisonTotal + " days.");
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        /// <summary>Test: a three-day term picked up on the morning the parole board sits, with behaviour that makes the answer matter.</summary>
        public void TestParole()
        {
            try
            {
                JusticeRefusal why = Free();
                if (why == JusticeRefusal.None && JusticeSettings.NoPrison)
                {
                    why = JusticeRefusal.PartOff;
                }
                if (why != JusticeRefusal.None)
                {
                    JusticeRefusals.Tell(Logger, "Parole hearing", why);
                    return;
                }
                JusticeState.ClearPrison();
                JusticeState.PrisonTotal = JusticeState.PrisonOriginal = 3;
                JusticeState.PrisonServed = JusticeLaw.ParoleDay(3) - 1;
                JusticeState.PrisonBehaviour = 1 + Justice.Priors;   // honest gets out, silent does not, flattery is a coin
                JusticeText.Toast(NotificationType.Info, "reallife_justice_test_soon", 6f);
                Logger?.Info("Justice test: parole hearing (a 3-day term, day " + (JusticeState.PrisonServed + 1) + ").");
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        public void TestCivil()
        {
            try
            {
                JusticeRefusal why = Ready();
                if (why == JusticeRefusal.None && !JusticeSettings.Civil)
                {
                    why = JusticeRefusal.PartOff;
                }
                if (why == JusticeRefusal.None && JusticeState.Suits.Count > 0)
                {
                    why = JusticeRefusal.Busy;
                }
                if (why != JusticeRefusal.None)
                {
                    JusticeRefusals.Tell(Logger, "Civil suit", why);
                    return;
                }
                Sue(2400, 1, 0, TimeHelper.CurrentDay);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        /// <summary>Test: four hours of community service owed (if none are), and a shift begun where the player stands.</summary>
        public void TestCommunity()
        {
            try
            {
                JusticeRefusal why = Free();
                if (why != JusticeRefusal.None)
                {
                    JusticeRefusals.Tell(Logger, "Community service", why);
                    return;
                }
                bool lent = JusticeState.CommunityHours == 0;
                if (lent)
                {
                    JusticeState.CommunityHours = JusticeLaw.CommunityShiftHours;
                    JusticeState.CommunityDue = TimeHelper.CurrentDay + JusticeLaw.CommunityDeadlineDays;
                }
                if (StartCommunity(announceRefusal: true) != JusticeRefusal.None && lent)
                {
                    JusticeState.CommunityHours = 0;
                    JusticeState.CommunityDue = 0;
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        public void TestFile()
        {
            JusticeRefusal why = Free();
            if (why != JusticeRefusal.None)
            {
                JusticeRefusals.Tell(Logger, "Legal file", why);
                return;
            }
            QueueFile();
            JusticeText.Toast(NotificationType.Info, "reallife_justice_test_soon", 6f);
        }

        /// <summary>Test: every case, debt, sentence and record entry is forgotten.</summary>
        public void TestClear()
        {
            try
            {
                if (!JusticeState.Loaded)
                {
                    JusticeRefusals.Tell(Logger, "Clear everything", JusticeRefusal.NotLoaded);
                    return;
                }
                if (_routine)
                {
                    JusticeRefusals.Tell(Logger, "Clear everything", JusticeRefusal.Busy);
                    return;
                }
                Letgo();
                JusticeState.Clear();
                _lastUnlicensedDay = 0;
                CallBridge(() => Justice.Bridges.Licences.SuspendDriving(0));
                JusticeText.Toast(NotificationType.Success, "reallife_justice_test_cleared", 8f);
                Logger?.Info("Justice test: everything cleared.");
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }
    }
}
