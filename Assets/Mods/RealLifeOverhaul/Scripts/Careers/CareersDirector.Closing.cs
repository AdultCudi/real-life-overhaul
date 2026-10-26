using System;
using RealLifeOverhaul.Tech;
using UI.Notification;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// An employer that stops existing or stops trading where the player works (Work/ClosureRules): the game's shut-down of a failing
    /// business, a redevelopment's eviction, a franchise wound up, a business bought by the player, a building knocked down. The job
    /// ends as a lay-off, not a quit or a sacking: redundancy pay, a good reference on the CV with the title earned, no mark, no
    /// notice and no wait before applying again, the desk and the planner's shifts gone with the job, one text from the employer and
    /// a notice, and a short priority in the next applications. A business that stands somewhere else under the same name takes the
    /// job with it. Looked at once a game hour, at every load (a save whose employer closed while the mod was away is mended there)
    /// and each morning; applications and vacancies at a closed business go with it. The mod's own closers (Construction's eviction,
    /// the franchises) shut the game's registration by the game's own call, so the hour's look finds them all by what the city holds.
    /// COST: nothing per frame; once a game hour a pass over the save's registrations for each of the job, the applications and the
    /// vacancies (about sixty posts at most).
    /// </summary>
    public sealed partial class CareersDirector
    {
        private const string ClosedKey = "reallife_transaction_work_closed";

        private EmployerRef _testMoveTo;   // a test's business the employer has moved to (Options > Tests); null for none
        private bool _fresh;               // the employers read again since this look began

        /// <summary>True if the post's employer is gone from where the post is; false while it trades there, or while the city cannot be told.</summary>
        internal static bool Gone(Opening post)
        {
            if (post == null || post.Kind == (int)EmployerKind.Public || string.IsNullOrEmpty(post.Street))
            {
                return false;
            }
            return WorkGame.PremisesAt(post.Street, post.Number, out Premises at) && !ClosureRules.Employs(post, at);
        }

        /// <summary>
        /// The look: the job, then the applications and vacancies. The job is laid off or follows its employer; an application there
        /// is closed; a vacancy goes. Returns true if anything changed.
        /// </summary>
        private bool EmployerKeep(int day, bool load)
        {
            if (_study == null)
            {
                return false;
            }
            WorkFile w = _study.Work;
            _fresh = false;
            bool changed = false;
            Job j = w.Job;
            if (j != null && Gone(j.Post))
            {
                EmployerRef to = MovedTo(j.Post, day);
                if (to != null)
                {
                    MoveJob(j, to, day);
                }
                else
                {
                    LayOff(j, day, load);
                }
                changed = true;
            }
            for (int i = 0; i < w.Applications.Count; i++)
            {
                Application a = w.Applications[i];
                if (a.Live && Gone(a.Post))
                {
                    changed |= FollowOrClose(a, day);
                }
            }
            for (int i = w.Openings.Count - 1; i >= 0; i--)
            {
                Opening o = w.Openings[i];
                if (Gone(o) && !Followed(o, day))
                {
                    w.Openings.RemoveAt(i);
                    changed = true;
                }
            }
            if (changed)
            {
                Changed();
            }
            return changed;
        }

        /// <summary>
        /// At a load: a job already on the CV as a resignation, a sacking or a walk out that came while or after its employer had
        /// closed is put right as the redundancy it was (Work/ClosureRules.Mend), so that it stops costing every application a poor or
        /// fair reference, a restraint it set is lifted and the priority applies. It reads the game's record of the closing, else the
        /// city's employers. Nothing is paid back. One line in Player.log. The no-show strikes went with the job.
        /// </summary>
        private int MendSpells(int day)
        {
            WorkFile w = _study.Work;
            int mended = 0, poor = 0;
            bool read = false;
            for (int i = w.History.Count - 1; i >= 0; i--)
            {
                Spell s = w.History[i];
                if (!ClosureRules.Mendable(s))
                {
                    continue;
                }
                if (!read)
                {
                    read = true;
                    ReadEmployers(day);
                }
                int closed = WorkGame.ClosedOn(s.Employer, s.From, s.To);
                bool gone = closed < 0 && !Listed(s.Employer);
                if (ClosureRules.Mends(s, closed, gone, day))
                {
                    poor += s.Reference == 1 ? 1 : 0;
                    ClosureRules.Mend(w, s, day);
                    mended++;
                }
            }
            if (mended > 0)
            {
                Logger?.Info("Careers: " + mended
                    + " job(s) on the CV that ended in a resignation, a sacking or a walk out after the employer had closed are now redundancies with a good reference ("
                             + poor + " had a poor one, ten points off every application); the restraint they set, if any, is lifted; nothing is paid back.");
                Changed();
            }
            return mended;
        }

        // True if a business of the city's, or a rival's of that name, is listed as an employer today.
        private bool Listed(string name)
        {
            for (int i = 0; i < _employers.Count; i++)
            {
                if (_employers[i].Name == name || (_employers[i].Rival.Length > 0 && WorkGame.RivalName(_employers[i].Rival) == name))
                {
                    return true;
                }
            }
            return false;
        }

        // The business the employer of a post has moved to, or null. The employers are read again once a look, only when one is gone.
        private EmployerRef MovedTo(Opening post, int day)
        {
            if (_testMoveTo != null)
            {
                return _testMoveTo;
            }
            if (!_fresh)
            {
                _fresh = true;
                _employersDay = -1;
                ReadEmployers(day);
            }
            int at = ClosureRules.Successor(post, _employers);
            return at >= 0 ? _employers[at] : null;
        }

        // A vacancy whose employer moved goes with it (and is fitted to the new place's hours); true if it did.
        private bool Followed(Opening o, int day)
        {
            EmployerRef to = MovedTo(o, day);
            if (to == null)
            {
                return false;
            }
            o.Street = to.Street;
            o.Number = to.Number;
            return FitShift(o);
        }

        // An application at a business that is gone: with the employer at its new address if it moved, else over. True if it changed.
        private bool FollowOrClose(Application a, int day)
        {
            EmployerRef to = MovedTo(a.Post, day);
            if (to != null)
            {
                a.Post.Street = to.Street;
                a.Post.Number = to.Number;
                FitShift(a.Post);
                return true;
            }
            string employer = CareersText.Employer(a.Post.Employer);
            bool told = a.Stage != (int)ApplyStep.Sent;   // one the player is waiting on is said; one not yet answered is closed quietly
            ClosureRules.Shut(a);
            if (told)
            {
                Tell2(NotificationType.Info, "reallife_work_note_app_closed", Data("employer", employer), TabApplications, CareerApp.ApplicationRow
                    + a.Post.Id);
            }
            Logger?.Info("Careers: the application to " + a.Post.Employer + " is over: the business is gone from " + a.Post.Street + " " + a.Post.Number + ".");
            return true;
        }

        /// <summary>The employer moved: the job is where it is, at the new address, its desk on the floor there, said once by the employer.</summary>
        private void MoveJob(Job j, EmployerRef to, int day)
        {
            string from = WorkGame.Address(j.Post.Street, j.Post.Number);
            DeskRelease("the employer moved");
            j.Post.Street = to.Street;
            j.Post.Number = to.Number;
            KeepJobShift(j, day, false);   // the new place's hours
            string address = WorkGame.Address(j.Post.Street, j.Post.Number);
            Logger?.Info("Careers: " + j.Post.Employer + " moved from " + from + " to " + address + " on day " + day + "; the job and its desk follow it.");
            BossSays(j, "reallife_work_text_moved", Data("employer", CareersText.Employer(j.Post.Employer), "address", address), NotificationType.Info,
                "reallife_career_note_employer_moved", TabJob);
        }

        /// <summary>
        /// The employer is gone: the job ends as a redundancy (Work/ClosureRules.Close): pay scaled by the price level, the CV, the
        /// council's benefit; the desk, the sitting and the management ladder's company let go; an accepted job elsewhere starts soon.
        /// One text from the employer and a notice.
        /// </summary>
        private void LayOff(Job j, int day, bool load)
        {
            WorkFile w = _study.Work;
            string employer = CareersText.Employer(j.Post.Employer);
            int notice = j.NoticeEnd, warnings = j.Warnings;
            if (_book.Working)
            {
                CloseSitting();
            }
            DeskRelease("the employer closed");
            Spell s = ClosureRules.Close(w, j, day, Year, out long cents);
            int whole = Prices.At((int)(cents / 100));
            CareersGame.Pay(whole, ClosedKey, null, employer);
            EndedOwn(j, LeaveReason.EmployerClosed, day);
            for (int i = 0; i < w.Applications.Count; i++)
            {
                ClosureRules.StartSooner(w.Applications[i], day);   // the notice that held it back no longer runs
            }
            LadderKeep(day);   // the company of the management ladder, with the people the mod keeps of it, goes with the job
            string money = CareersText.Money(whole);
            DeskGame.BossText(employer, j.Post.Street, j.Post.Number, "reallife_work_text_closed", Data("employer", employer, "amount", money));   // the employer's own text, if it can be sent
            Tell2(NotificationType.Warning, "reallife_work_note_closed", Data("employer", employer, "amount", money), TabJob, null);
            Logger?.Info("Careers: " + j.Post.Employer + " is gone from " + j.Post.Street + " " + j.Post.Number
                + (load ? " (found at the load)" : string.Empty) + ": laid off on day " + day
                         + " as " + CareersText.Title(s.Prof, s.Top) + " after " + (day - j.Hired) + " day(s), redundancy " + whole
                         + " paid, reference " + s.Reference
                         + ", no mark" + (notice > 0 ? ", the notice to " + CareersText.Day(notice) + " cancelled" : string.Empty)
                         + (warnings > 0 ? ", " + warnings + " warning(s) with the job" : string.Empty) + ".");
            if (Verbose)
            {
                bool ok = w.Job == null && s.Leaving == (int)LeaveReason.EmployerClosed && s.Reference == 3 && w.LaidOffDay == day && ClosureRules.Priority(w, day);
                Logger?.Info("Careers check (employer closed): the job ended as " + (LeaveReason)s.Leaving + " with reference " + s.Reference
                    + ", priority " + ClosureRules.Priority(w, day)
                             + "; wanted a lay-off, a good reference and the priority: " + (ok ? "PASS" : "FAIL"));
            }
        }
    }
}
