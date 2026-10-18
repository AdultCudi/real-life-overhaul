using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>The working life: the job and its working days, notice and leaving, and the Career app.</summary>
    public sealed partial class CareersDirector
    {
        public const int TabStart = 0, TabJobs = 1, TabApplications = 2, TabJob = 3, TabCv = 4;
        public const string CareerAppId = "RealLifeCareer";
        private const string SalaryKey = "reallife_transaction_work_salary";
        private const string LoanKey = "reallife_transaction_study_loan";
        private const string SalaryCategory = "ba:transactioncategory_salaryincome";
        public const int ApplicationsLive = 6;
        public const int WarningsToGo = 3;

        private bool _workOn;
        private PhoneApp _careerApp;
        private CareerApp _careerView;
        private bool _careerGaveUp;
        private int _workReminded = -1, _interviewReminded = -1;
        private int _interviewGoneDay = -1, _interviewGonePost = -1;   // the interview looked at as its window shut, so the morning does not look again

        public bool WorkRunning => _workOn;

        /// <summary>The working life; null while the part is off.</summary>
        public WorkFile Work => _workOn && _study != null ? _study.Work : null;

        private void StartWork()
        {
            _workOn = true;
            _employersDay = -1;
            WorkFile w = _study.Work;
            Logger?.Info("Careers: the working life " + (w.LastDay < 0 ? "opened" : "loaded") + " on day " + Save.Day + ": " + w.Openings.Count + " vacancies, " + w.Applications.Count
                         + " application(s), " + (w.Job != null ? "a job at " + w.Job.Post.Employer : "no job") + ".");
        }

        private void StopWork(bool unloading)
        {
            if (!_workOn)
            {
                return;
            }
            _workOn = false;
            if (_sitting != null && (_sitting.Kind == SittingKind.Work || _sitting.Kind == SittingKind.Overtime || _sitting.Kind == SittingKind.Interview))
            {
                CloseSitting();
            }
            DeskRelease("the working life stopped");
            DeskGame.Remove();
            _employers.Clear();
            _offices.Clear();
            if (!unloading && _careerApp != null)
            {
                _careerApp.Remove();
                _careerApp = null;
                _careerView = null;
            }
        }

        private void WorkHour(int day, int hour)
        {
            WorkFile w = _study.Work;
            int minute = hour * 60;
            AgencyEnds(w, day, hour);
            Job j = w.Job;
            if (j != null)
            {
                Profession p = j.Def;
                if (ProRules.DayOver(p, day, day, minute) && j.SettledDay < day && !Working)
                {
                    SettleDay(j, day);
                }
                DeskHour(j, day, hour);
                if (ProRules.WorkDay(p, day) && p.Hour == hour + 1 && day >= j.Hired && j.SettledDay < day && _workReminded != day)
                {
                    _workReminded = day;
                    Tell2(NotificationType.Info, ProRules.FromHome((WorkMode)j.Work, day) ? "reallife_work_note_day_home" : "reallife_work_note_day_soon",
                        Data("employer", CareersText.Employer(j.Post.Employer), "hour", CareersText.Hour(p.Hour), "place", WorkGame.Address(j.Post.Street, j.Post.Number)), TabJob, null);
                }
            }
            for (int i = 0; i < w.Applications.Count; i++)
            {
                Application a = w.Applications[i];
                if (a.InterviewDay == day && ProRules.InterviewGone(a, day, minute) && !ProRules.InterviewGone(a, day, minute - 60) && !InInterview(a.Post.Id))
                {
                    // the first hour its window has shut: looked at once
                    _interviewGoneDay = day;
                    _interviewGonePost = a.Post.Id;
                    InterviewMoved(a, day + 1);   // one not moved lapses the next morning
                }
                if (a.Stage == (int)ApplyStep.Interview && a.InterviewDay == day && a.InterviewHour == hour + 1 && _interviewReminded != a.Post.Id)
                {
                    _interviewReminded = a.Post.Id;
                    Tell2(NotificationType.Info, "reallife_work_note_interview_soon",
                        Data("employer", CareersText.Employer(a.Post.Employer), "hour", CareersText.Hour(a.InterviewHour), "place", InterviewPlace(a)),
                        TabApplications, CareerApp.ApplicationRow + a.Post.Id);
                }
            }
        }

        private bool Working => _sitting != null && (_sitting.Kind == SittingKind.Work || _sitting.Kind == SittingKind.Overtime);

        private bool InInterview(int post) => _sitting != null && _sitting.Kind == SittingKind.Interview && _sitting.Of == post;

        // The registration's last day: one reminder, the day kept in the save so a reload that day does not send it again.
        private void AgencyEnds(WorkFile w, int day, int hour)
        {
            if (!Settings.CareersAgencyNote || !ProRules.AgencyEnds(w.AgencyUntil, w.AgencyTold, day, hour))
            {
                return;
            }
            w.AgencyTold = w.AgencyUntil;
            Tell2(NotificationType.Info, "reallife_work_note_agency_ends", null, TabJobs, null);
            if (Verbose)
            {
                Logger?.Info("Careers check (recruitment office, day " + day + " at " + hour.ToString("00") + ":00): the registration ends today; the reminder sent once: PASS");
            }
        }

        private void WorkDayRun(int day)
        {
            WorkFile w = _study.Work;
            int last = w.LastDay;
            w.LastDay = day;
            Job j = w.Job;
            if (j != null)
            {
                for (int d = Math.Max(j.SettledDay + 1, Math.Max(j.Hired, last > 0 ? last - 14 : day - 14)); d < day && w.Job == j; d++)
                {
                    SettleDay(j, d);
                }
                if (w.Job == j && j.NoticeEnd > 0 && day > j.NoticeEnd)
                {
                    EndJob((LeaveReason)Math.Max(1, j.Leaving), day, true);
                }
            }
            Applications(day);
            Vacancies(day);
            WorkDayMore(day);
            if (OwnOn)
            {
                OwnDay(day);
            }
        }

        /// <summary>
        /// Settles a working day: a missed one is a warning, the third the sack. Off sick, on leave, or a day from home lost to
        /// an outage over its start window, it is excused: no pay, no missed day, no warning.
        /// </summary>
        private void SettleDay(Job j, int day)
        {
            ProRules.StartWindow(j.Def, day, out int from, out int to);
            SettleDay(j, day, from, to);
        }

        /// <summary>The same, with the home's internet read over the minutes given (the start window; the test passes now).</summary>
        private void SettleDay(Job j, int day, int from, int to)
        {
            bool due = ProRules.WorkDay(j.Def, day) && day > j.SettledDay && day >= j.Hired;
            if (!due)
            {
                return;
            }
            bool worked = ProRules.Settle(j, day);
            if (worked)
            {
                _study.Work.DaysWorked++;
                return;
            }
            if (Excused(day) || j.OffUntil >= day)
            {
                ProRules.Excuse(j);
                return;
            }
            if (OutageExcused(j, day, from, to))
            {
                ProRules.Excuse(j);
                BossSays(j, "reallife_work_text_outage", Data("employer", CareersText.Employer(j.Post.Employer)), NotificationType.Info, "reallife_work_note_outage", TabJob);
                return;
            }
            j.Warnings++;
            // said by the boss in a text; a notification if it cannot be sent
            if (j.Warnings >= WarningsToGo)
            {
                BossSays(j, "reallife_work_text_sacked", Data("employer", CareersText.Employer(j.Post.Employer), "count", WarningsToGo.ToString()),
                    NotificationType.Error, "reallife_work_note_sacked", TabCv);
                EndJob(LeaveReason.Dismissed, day, false);
                return;
            }
            BossSays(j, "reallife_work_text_warning", Data("employer", CareersText.Employer(j.Post.Employer), "count", j.Warnings.ToString(), "most", WarningsToGo.ToString()),
                NotificationType.Warning, "reallife_work_note_missed", TabJob);
        }

        /// <summary>True if a day not worked from home is excused by an outage over the minutes given; asks Utilities only then.</summary>
        private bool OutageExcused(Job j, int day, int from, int to)
        {
            if (!ProRules.FromHome((WorkMode)j.Work, day))
            {
                return false;
            }
            HomeNet net = CareersBridges.HomeNetBetween(from, to);
            bool excused = ProRules.DayExcused((WorkMode)j.Work, day, net);
            if (Verbose)
            {
                Logger?.Info("Careers check (working day from home gone by): " + j.Post.Employer + " on day " + day + ", not worked; the home's internet "
                             + (net == HomeNet.Online ? "up" : "down (" + net + ")") + " over its start window; " + (excused ? "excused, no warning" : "missed, a warning")
                             + "; wanted " + (StudyRules.Excuses(net) ? "excused" : "missed") + ": " + (excused == StudyRules.Excuses(net) ? "PASS" : "FAIL, the decision does not match the rule"));
            }
            return excused;
        }

        /// <summary>
        /// Moves a video interview lost to an outage over its window to the first weekday from <paramref name="first"/>, at the
        /// same hour; true if moved. Asks Utilities only for a remote post.
        /// </summary>
        private bool InterviewMoved(Application a, int first)
        {
            if (a.Post.Work != (int)WorkMode.Remote)
            {
                return false;
            }
            ProRules.InterviewWindow(a, out int from, out int to);
            HomeNet net = CareersBridges.HomeNetBetween(from, to);
            bool moved = ProRules.InterviewExcused(a, net);
            int was = a.InterviewDay;
            if (moved)
            {
                ProRules.Reschedule(a, first);
                Tell2(NotificationType.Info, "reallife_work_note_interview_moved", Data("employer", CareersText.Employer(a.Post.Employer), "day", CareersText.Day(a.InterviewDay),
                    "hour", CareersText.Hour(a.InterviewHour)), TabApplications, CareerApp.ApplicationRow + a.Post.Id);
            }
            if (Verbose)
            {
                Logger?.Info("Careers check (video interview gone by): " + a.Post.Employer + " on day " + was + ", the home's internet " + (net == HomeNet.Online ? "up" : "down (" + net + ")")
                             + " over its window; " + (moved ? "moved to day " + a.InterviewDay : "lost") + "; wanted " + (StudyRules.Excuses(net) ? "moved" : "lost") + ": "
                             + (moved == StudyRules.Excuses(net) && (!moved || (a.InterviewDay > was && a.Lapse == a.InterviewDay)) ? "PASS" : "FAIL, the decision does not match the rule"));
            }
            return moved;
        }

        /// <summary>True if the player is off sick that day, as the street careers count it.</summary>
        private static bool Excused(int day)
        {
            try
            {
                ICareerSickNote note = CareerLinks.Health;
                return note != null ? note.Covers(day) : ModState.IsSick;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The employers answer, invitations and offers lapse, and an accepted job begins.</summary>
        private void Applications(int day)
        {
            WorkFile w = _study.Work;
            for (int i = 0; i < w.Applications.Count; i++)
            {
                Application a = w.Applications[i];
                string employer = CareersText.Employer(a.Post.Employer), title = CareersText.Title(a.Post.Prof, a.Post.Rank);
                switch (a.At)
                {
                    case ApplyStep.Sent:
                        if (day < a.Answer)
                        {
                            break;
                        }
                        if (ProRules.Screened(a.Score, ProRules.Luck(StudySeed, a.Post.Id, 1, ProRules.ScreenLuck)))
                        {
                            a.Stage = (int)ApplyStep.Interview;
                            a.InterviewDay = Weekday(day + ProRules.InterviewAfterDays);
                            a.InterviewHour = TechRandom.Range(StudySeed, 8402, a.Post.Id, 0, ProRules.InterviewHourLeast, ProRules.InterviewHourMost);
                            a.Lapse = a.InterviewDay;
                            Tell2(NotificationType.Success, "reallife_work_note_invited", Data("employer", employer, "title", title, "day", CareersText.Day(a.InterviewDay)),
                                TabApplications, CareerApp.ApplicationRow + a.Post.Id);
                        }
                        else
                        {
                            a.Reject(day);
                            TurnedDown(a, employer, title, false);
                        }
                        break;
                    case ApplyStep.Interview:
                        if (day > a.InterviewDay && ((_interviewGoneDay == a.InterviewDay && _interviewGonePost == a.Post.Id) || !InterviewMoved(a, day)))   // not asked again if its hour did
                        {
                            a.Stage = (int)ApplyStep.Lapsed;
                            Tell2(NotificationType.Warning, "reallife_work_note_interview_missed", Data("employer", employer), TabApplications, CareerApp.ApplicationRow + a.Post.Id);
                        }
                        break;
                    case ApplyStep.Offered:
                        if (day > a.Lapse)
                        {
                            a.Stage = (int)ApplyStep.Lapsed;
                            Tell2(NotificationType.Info, "reallife_work_note_offer_lapsed", Data("employer", employer), TabApplications, CareerApp.ApplicationRow + a.Post.Id);
                        }
                        break;
                    case ApplyStep.Accepted:
                        if (day >= a.StartDay && (w.Job == null || w.Job.NoticeEnd > 0 && day > w.Job.NoticeEnd))
                        {
                            if (w.Job != null)
                            {
                                EndJob((LeaveReason)Math.Max(1, w.Job.Leaving), day, true);
                            }
                            Begin(a, day);
                        }
                        break;
                }
            }
        }

        /// <summary>The first day of a job accepted.</summary>
        private void Begin(Application a, int day)
        {
            WorkFile w = _study.Work;
            a.Stage = (int)ApplyStep.Accepted;
            a.Lapse = day;
            var j = new Job { Hired = day, RankDay = day, ReviewDay = day, Pay = a.Offer, Work = a.Post.Work, SettledDay = day - 1 };
            a.Post.CopyTo(j.Post);
            j.Post.Via = a.Post.Via;
            j.Boss = TechRandom.Range(StudySeed, 8501, TechRandom.Of(a.Post.Employer), 0, 0, ProRules.BossKinds - 1);
            Hired(j, day);
            w.Job = j;
            w.BenefitUntil = 0;
            a.Stage = (int)ApplyStep.Withdrawn;   // done with: the job is the record now
            for (int i = w.Openings.Count - 1; i >= 0; i--)
            {
                if (w.Openings[i].Same(j.Post))
                {
                    w.Openings.RemoveAt(i);
                }
            }
            if (j.Post.Via == (int)Via.JobsBoard)
            {
                try
                {
                    CareersBridges.TookAd?.Invoke(j.Post.Employer, j.Post.Family, j.Post.Level);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            Profession p = j.Def;
            Tell2(NotificationType.Success, "reallife_work_note_first_day",
                Data("title", CareersText.Title(j.Post.Prof, j.Post.Rank), "employer", CareersText.Employer(j.Post.Employer), "hours", CareersText.Hours(p)), TabJob, null);
            Logger?.Info("Careers: started as " + p.Key + " rank " + j.Post.Rank + " at " + j.Post.Employer + " on day " + day + ", " + j.Pay + " cents an hour at the start's prices.");
        }

        /// <summary>A job ends: it goes on the CV with its reference.</summary>
        private void EndJob(LeaveReason why, int day, bool tell)
        {
            WorkFile w = _study.Work;
            Job j = w.Job;
            if (j == null)
            {
                return;
            }
            if (_sitting != null && (_sitting.Kind == SittingKind.Work || _sitting.Kind == SittingKind.Overtime))
            {
                CloseSitting();
            }
            DeskRelease("the job ended");
            Spell s = ProRules.Spell(j, day, why);
            w.Remember(s);
            w.Job = null;
            Ended(j, why, day);
            EndedOwn(j, why, day);
            if (tell)
            {
                Tell2(NotificationType.Info, "reallife_work_note_left", Data("employer", CareersText.Employer(j.Post.Employer), "reference", CareersText.Reference(s.Reference)), TabCv, null);
            }
            Logger?.Info("Careers: left " + j.Post.Employer + " on day " + day + " (" + why + "), reference " + s.Reference + ".");
        }

        public string CanWork(out int minutes, out bool late)
        {
            minutes = 0;
            late = false;
            Job j = Work != null ? Work.Job : null;
            int day = Save != null ? Save.Day : 0;
            if (j == null)
            {
                return "reallife_work_why_no_job";
            }
            Profession p = j.Def;
            if (day < j.Hired || !ProRules.WorkDay(p, day) || j.SettledDay >= day)
            {
                return "reallife_work_why_not_today";
            }
            int now = CareersGame.MinuteOfDay;
            if (j.TodayDay == day && j.TodayMinutes > 0)
            {
                minutes = ProRules.Remaining(p, j, day, now);
                if (minutes <= 0)
                {
                    return "reallife_work_why_done_today";
                }
            }
            else if (!ProRules.CanStart(p, day, now, out minutes, out late))
            {
                return "reallife_work_why_not_hours";
            }
            if (ProRules.FromHome((WorkMode)j.Work, day))
            {
                return CareersGame.AtHome() ? StudyRules.NoNetWhy(CareersBridges.HomeNetNow()) : "reallife_work_why_home_day";
            }
            return WorkGame.AtWork(p, j.Post) ? null : "reallife_work_why_not_at_work";
        }

        /// <summary>Starts the working day from the Career app, only where no desk could be had (<see cref="DeskFallback"/>).</summary>
        public string StartWorkDay()
        {
            int minutes;
            bool late;
            string why = CanWork(out minutes, out late);
            if (why != null)
            {
                return why;
            }
            Job j = Work.Job;
            ClockIn(j, Save.Day, late);
            return Offer(DaySitting(j, minutes));
        }

        private void WorkEnded(Sitting s, int minutes, bool whole)
        {
            Job j = _study.Work.Job;
            if (j == null || minutes <= 0)
            {
                return;
            }
            int day = Save.Day;
            ProRules.Worked(j, day, minutes);
            Pay(j, minutes, s.Kind == SittingKind.Overtime);
            Profession p = j.Def;
            int a, b;
            p.SkillsOf(j.Post.Rank, out a, out b);
            int hours = Math.Max(1, minutes / 60);
            LearnAtWork(a, hours, day);
            LearnAtWork(b, hours, day);
            WorkedMore(j, s, minutes, whole, day);
            if (ProRules.DayOver(p, day, day, CareersGame.MinuteOfDay) && j.SettledDay < day)
            {
                SettleDay(j, day);
            }
        }

        // A skill learnt from hours of work, less the more of it the player has.
        private void LearnAtWork(int skill, int hours, int day)
        {
            StudyRules.Learn(_study, skill, StudyCatalogue.WorkLearn * hours * (100 - StudyRules.Points(_study, skill)) / 100, day);
        }

        /// <summary>The pay for minutes worked, with the student loan's repayment taken from it.</summary>
        private void Pay(Job j, int minutes, bool overtime)
        {
            int rate = Prices.At(j.Pay);
            long cents = ProRules.Wage(rate, minutes);
            if (cents <= 0)
            {
                return;
            }
            j.Earned += cents;
            _study.Work.Earned += cents;
            string name = CareersText.Employer(j.Post.Employer);
            CareersGame.Pay(cents / 100f, SalaryKey, SalaryCategory, name);
            long repay = StudyRules.LoanRepayment(_study.LoanCents, rate, Math.Max(1, minutes / 60), Prices.Level);
            if (repay > 0)
            {
                _study.LoanCents -= repay;
                _study.LoanRepaid += repay;
                CareersGame.Take(repay / 100f, LoanKey, name);
                if (_study.LoanCents <= 0)
                {
                    _study.LoanCents = 0;
                    LoanCleared();
                }
            }
            PaidMore(j, cents, overtime);
        }

        /// <summary>The player hands in their notice: the job ends after it, with a good reference if the days are worked.</summary>
        public string Resign()
        {
            Job j = Work != null ? Work.Job : null;
            if (j == null)
            {
                return "reallife_work_why_no_job";
            }
            if (j.NoticeEnd > 0)
            {
                return "reallife_work_why_leaving";
            }
            j.NoticeEnd = Save.Day + ProRules.NoticeDays(j.Rank, j.Post.Rank);
            j.Leaving = (int)LeaveReason.Resigned;
            Tell2(NotificationType.Info, "reallife_work_note_notice", Data("employer", CareersText.Employer(j.Post.Employer), "day", CareersText.Day(j.NoticeEnd)), TabJob, null);
            Changed();
            return null;
        }

        /// <summary>The player walks out today: no notice, a poor reference.</summary>
        public string WalkOut()
        {
            Job j = Work != null ? Work.Job : null;
            if (j == null)
            {
                return "reallife_work_why_no_job";
            }
            EndJob(LeaveReason.WalkedOut, Save.Day, true);
            Changed();
            return null;
        }

        /// <summary>Registers with the recruitment office the player stands in: more vacancies, and a word for the player in every application.</summary>
        public string Register()
        {
            WorkFile w = Work;
            if (w == null)
            {
                return "reallife_work_why_closed";
            }
            if (!CareersGame.InsideType(HiringCatalogue.AgencyType))
            {
                return "reallife_work_why_not_agency";
            }
            w.AgencyUntil = Save.Day + ProRules.AgencyDays;
            w.OpeningsDay = -1;
            Vacancies(Save.Day);
            Tell2(NotificationType.Success, "reallife_work_note_registered", Data("day", CareersText.Day(w.AgencyUntil)), TabJobs, null);
            Changed();
            return null;
        }

        private void InstallCareer()
        {
            var view = new CareerApp(this);
            GameSprites.Want(CareerGlyphs);
            bool tryAgain;
            _careerApp = PhoneApp.Install(new PhoneAppDef
            {
                Id = CareerAppId,
                TitleKey = "reallife_career_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(CareerGlyphs), new Color32(84, 132, 214, 255), new Color32(40, 64, 128, 255), 0.42f, "AppIcon-Career"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out tryAgain);
            if (_careerApp == null)
            {
                _careerGaveUp = !tryAgain;
                if (_careerGaveUp)
                {
                    Logger?.Warn("Careers: the game's phone is not built as this version expects, so the Career app is left off."
                                 + " A job under way still runs and the notifications still say what is due.");
                }
                return;
            }
            _careerView = view;
            CareerBadge();
        }

        private static readonly string[] CareerGlyphs = { "Icon-Briefcase", "Icon-Employees", "Icon-Building" };

        public void OpenCareer(int tab, string pick)
        {
            if (_careerApp != null && _careerView != null)
            {
                _careerView.Want(tab, pick);
                _careerApp.Open();
            }
        }

        /// <summary>The Career app's badge: offers to answer, interviews today, a working day to start now.</summary>
        private void CareerBadge()
        {
            WorkFile w = Work;
            if (_careerApp == null || w == null || Save == null)
            {
                return;
            }
            int count = 0;
            for (int i = 0; i < w.Applications.Count; i++)
            {
                Application a = w.Applications[i];
                count += a.Stage == (int)ApplyStep.Offered || (a.Stage == (int)ApplyStep.Interview && a.InterviewDay == Save.Day) ? 1 : 0;
            }
            _careerApp.SetBadge(count);
        }

        /// <summary>A notification that opens the Career app on a tab with a row picked.</summary>
        internal void Tell2(NotificationType type, string key, Dictionary<string, string> data, int tab, string pick)
        {
            Notifications.Show(type, key, data, 10f, null, Guard.Wrap(() => OpenCareer(tab, pick)), true, false);
        }
    }
}
