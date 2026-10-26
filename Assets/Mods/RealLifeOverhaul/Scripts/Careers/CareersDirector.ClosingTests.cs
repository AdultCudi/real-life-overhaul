using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Careers
{
    /// <summary>The test buttons of an employer that closes or moves (options page, Tests): a test job, the employer shut or moved, the look made, the result written to Player.log.</summary>
    public sealed partial class CareersDirector
    {
        // The test job and what the lay-off changes of the save, so that a test leaves the player's working life as it found it.
        private sealed class ClosingTest
        {
            public EmployerRef At;
            public int History, BenefitUntil, BenefitWeek, BenefitPaid, LaidOffDay, DaysWorked, Restraint;
            public string LaidOffFrom;
            public float Money;
        }

        // A test job at the first business of the city that employs a profession, as if hired today and a year in; null (with a notice) if none can be had or the player holds a job.
        private ClosingTest BeginClosingTest(out Job job)
        {
            job = null;
            if (!WorkReady())
            {
                return null;
            }
            WorkFile w = _study.Work;
            if (w.Job != null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_work_test_closed_busy", Data("employer", CareersText.Employer(w.Job.Post.Employer)), 10f);
                return null;
            }
            int day = Save.Day;
            ReadEmployers(day);
            for (int i = 0; i < _employers.Count && job == null; i++)
            {
                EmployerRef e = _employers[i];
                if (e.Kind == EmployerKind.Public || string.IsNullOrEmpty(e.Name))
                {
                    continue;
                }
                bool other = false;
                for (int k = 0; k < _employers.Count && !other; k++)
                {
                    other = k != i && _employers[k].Name != e.Name && (_employers[k].Street != e.Street || _employers[k].Number != e.Number);   // (a second business to move to)
                }
                if (!other || TestJobAt(e.Street, e.Number) != null || w.Job == null)
                {
                    continue;
                }
                var t = new ClosingTest
                {
                    At = e, History = w.History.Count, BenefitUntil = w.BenefitUntil, BenefitWeek = w.BenefitWeek, BenefitPaid = w.BenefitPaid, LaidOffDay = w.LaidOffDay,
                    LaidOffFrom = w.LaidOffFrom, DaysWorked = w.DaysWorked, Restraint = w.RestraintUntil, Money = Save.Money
                };
                job = w.Job;
                job.Hired = job.RankDay = job.ReviewDay = day - (2 * Year + 5);   // two years in, so the pay is a week for each of them
                job.SettledDay = day - 1;
                return t;
            }
            Notifications.Show(NotificationType.Warning, "reallife_work_test_none", null, 8f);
            Logger?.Info("Careers check (employer closes): no business of the city employs a profession the player could hold; not run.");
            return null;
        }

        // How many of the open posts the player cannot apply for because an employer turned them down for it lately (the only wait there is, ProRules.ReapplyDays).
        private static int Barred(WorkFile w, int day)
        {
            int n = 0;
            for (int i = 0; i < w.Openings.Count; i++)
            {
                n += w.Openings[i].Open(day) && w.TurnedDown(w.Openings[i], day) != null ? 1 : 0;
            }
            return n;
        }

        // The save put back: the test spell off the CV, the benefit, the day laid off and the pay taken back.
        private void EndClosingTest(ClosingTest t, bool keepJob)
        {
            WorkFile w = _study.Work;
            WorkGame.TestShut = null;
            _testMoveTo = null;
            if (!keepJob)
            {
                while (w.History.Count > t.History)
                {
                    w.History.RemoveAt(w.History.Count - 1);
                }
                w.Job = null;
                w.BenefitUntil = t.BenefitUntil;
                w.BenefitWeek = t.BenefitWeek;
                w.BenefitPaid = t.BenefitPaid;
                w.LaidOffDay = t.LaidOffDay;
                w.LaidOffFrom = t.LaidOffFrom;
                w.DaysWorked = t.DaysWorked;
            }
            float back = Save.Money - t.Money;
            if (back > 0.005f)
            {
                CareersGame.Take(back, ClosedKey, CareersText.Employer(t.At.Name));   // the pay of the test, taken back
            }
            DeskRelease("a test ended");
            LadderKeep(Save.Day);
            Changed();
        }

        /// <summary>
        /// A test job at a city business, the business shut as the game shuts one, and the look made: the job must end as a lay-off
        /// (on the CV as the employer closed with a good reference, no restraint, no warning), the pay must be the rule's, scaled by the
        /// price level, the player must be able to apply at once and be answered first. The save is put back afterwards.
        /// </summary>
        public void TestClosed()
        {
            ClosingTest t = null;
            try
            {
                t = BeginClosingTest(out Job j);
                if (t == null)
                {
                    return;
                }
                WorkFile w = _study.Work;
                int day = Save.Day;
                long expected = ClosureRules.Payment(j, day, Year);
                int whole = Prices.At((int)(expected / 100));
                string employer = CareersText.Employer(t.At.Name);
                int restraint = w.RestraintUntil, barredBefore = Barred(w, day);
                WorkGame.TestShut = t.At;
                bool changed = EmployerKeep(day, false);
                WorkGame.TestShut = null;
                Spell s = w.History.Count > t.History ? w.History[w.History.Count - 1] : null;
                float paid = Save.Money - t.Money;
                bool ended = changed && w.Job == null && s != null && s.Leaving == (int)LeaveReason.EmployerClosed && s.Employer == t.At.Name;
                bool clean = s != null && s.Reference == 3 && w.RestraintUntil == restraint && s.Leaving != (int)LeaveReason.Resigned && s.Leaving != (int)LeaveReason.Dismissed
                             && s.Leaving != (int)LeaveReason.WalkedOut;
                bool pay = Math.Abs(paid - whole) < 0.5f && whole > 0;
                bool soon = ClosureRules.Priority(w, day) && w.LaidOffFrom == t.At.Name && w.BenefitUntil >= day && ClosureRules.AnswerDay(w, day, 2) == day + 1;
                int barredAfter = Barred(w, day);
                bool wait = barredAfter == barredBefore && w.Job == null;   // the only wait before applying again is a no (ProRules.ReapplyDays); the lay-off adds none
                bool ok = ended && clean && pay && soon && wait;
                Logger?.Info("Careers check (employer closes): a test job at " + t.At.Name + " (" + t.At.Street + " " + t.At.Number
                    + "), shut; ended as " + (s != null ? ((LeaveReason)s.Leaving).ToString() : "nothing")
                             + " (" + (ended ? "a lay-off" : "NOT a lay-off") + "), reference " + (s != null ? s.Reference.ToString() : "-")
                             + ", no restraint or mark " + (clean ? "held" : "BROKEN") + ", pay "
                             + paid.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " against the rule's " + whole + " ("
                             + (pay ? "right" : "WRONG") + "), the priority and benefit "
                             + (soon ? "set" : "NOT set") + ", posts barred to apply for " + barredBefore + " before and " + barredAfter + " after"
                             + (wait ? " (no wait added)" : " (a wait ADDED)") + ": " + (ok ? "PASS" : "FAIL"));
                Notifications.Show(ok ? NotificationType.Success : NotificationType.Error, ok ? "reallife_work_test_closed_pass" : "reallife_work_test_closed_fail",
                    Data("employer", employer, "amount", CareersText.Money(whole)), 12f);
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
            finally
            {
                if (t != null)
                {
                    EndClosingTest(t, false);
                }
            }
        }

        /// <summary>
        /// A test job at a city business, which moves to another address as its business does: the job must follow it (the same
        /// employer, the post at the new address, no spell on the CV, no mark) and say so. The save is put back afterwards.
        /// </summary>
        public void TestMoved()
        {
            ClosingTest t = null;
            try
            {
                t = BeginClosingTest(out Job j);
                if (t == null)
                {
                    return;
                }
                WorkFile w = _study.Work;
                int day = Save.Day;
                EmployerRef other = null;
                for (int i = 0; i < _employers.Count && other == null; i++)
                {
                    EmployerRef e = _employers[i];
                    other = e.Kind != EmployerKind.Public && (e.Street != t.At.Street || e.Number != t.At.Number) && e.Name != t.At.Name ? e : null;
                }
                if (other == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_work_test_none", null, 8f);
                    return;
                }
                var there = new EmployerRef { Name = t.At.Name, Street = other.Street, Number = other.Number, Type = t.At.Type, Rival = t.At.Rival, Kind = t.At.Kind, Professions = t.At.Professions };
                int warnings = j.Warnings, spells = w.History.Count;
                WorkGame.TestShut = t.At;
                _testMoveTo = there;
                bool changed = EmployerKeep(day, false);
                _testMoveTo = null;
                WorkGame.TestShut = null;
                Job now = w.Job;
                bool followed = changed && now == j && now.Post.Street == there.Street && now.Post.Number == there.Number && now.Post.Employer == t.At.Name;
                bool clean = now != null && now.Warnings == warnings && w.History.Count == spells && w.LaidOffDay == t.LaidOffDay;
                bool ok = followed && clean;
                Logger?.Info("Careers check (employer moves): a test job at " + t.At.Name + " moved from " + t.At.Street + " " + t.At.Number
                    + " to " + there.Street + " " + there.Number + "; the job is "
                             + (followed ? "at the new address" : "NOT at the new address") + ", "
                             + (clean ? "no spell and no mark" : "a spell or a mark WRONGLY made") + ": " + (ok ? "PASS" : "FAIL"));
                Notifications.Show(ok ? NotificationType.Success : NotificationType.Error, ok ? "reallife_work_test_moved_pass" : "reallife_work_test_moved_fail",
                    Data("employer", CareersText.Employer(t.At.Name), "address", WorkGame.Address(there.Street, there.Number)), 12f);
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
            finally
            {
                if (t != null)
                {
                    EndClosingTest(t, false);
                }
            }
        }
    }
}
