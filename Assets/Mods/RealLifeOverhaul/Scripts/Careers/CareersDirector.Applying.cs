using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>Applying: what an employer reads of the player, the application, the interview, the offer and haggling.</summary>
    public sealed partial class CareersDirector
    {
        /// <summary>The fields of a refusal's notice (<see cref="TakeWhyData"/>); null for a reason with none.</summary>
        private Dictionary<string, string> _whyData;

        /// <summary>What an employer reads of the player for a post.</summary>
        internal Candidate CandidateFor(Opening o)
        {
            StudyFile f = _study;
            WorkFile w = f.Work;
            Profession p = o.Def;
            int good, poor;
            ProRules.References(w, out good, out poor);
            int day = Save.Day;
            return new Candidate
            {
                Points = ProRules.Points(f, p, o.Rank), Need = ProRules.Need(f, p, o.Rank), Qualified = ProRules.Qualified(f, p, o.Rank),
                Experience = ProRules.Experience(w, o.Prof, day, GameCalendar.DaysPerYear), Good = good, Poor = poor, Reputation = CareersBridges.ReputationNow(),
                Referral = o.Via == (int)Via.Referral, Agency = o.Via == (int)Via.Agency || w.AgencyUntil >= day, Record = CareersBridges.RecordNow(),
                Honours = AnyHonours(f), Master = StudyRules.School(f) >= TalentCatalogue.Master, Cycle = CareersBridges.CycleNow(),
                Summer = GameCalendar.SeasonOf(day) == Events.Season.Summer, Rank = o.Rank
            };
        }

        /// <summary>What every employer reads of the player alike, read once for a page; <see cref="CandidateFor(Opening, Candidate)"/> adds the post's part.</summary>
        internal Candidate Template()
        {
            Candidate t = CandidateFor(new Opening { Prof = 0, Rank = 0, Via = (int)Via.Board });
            t.Experience = 0;
            return t;
        }

        /// <summary>What an employer reads of the player for a post, from the template: the same as <see cref="CandidateFor(Opening)"/>.</summary>
        internal Candidate CandidateFor(Opening o, Candidate t)
        {
            WorkFile w = _study.Work;
            int day = Save != null ? Save.Day : 0;
            Candidate c = ProPath.With(_study, o.Def, o.Rank, null, t);
            c.Experience = ProRules.Experience(w, o.Prof, day, GameCalendar.DaysPerYear);
            c.Referral = o.Via == (int)Via.Referral;
            c.Agency = o.Via == (int)Via.Agency || w.AgencyUntil >= day;
            return c;
        }

        /// <summary>The quickest way to a first offer from here (Careers/Work/ProPath): a profession, and the course to take first or -1.</summary>
        internal bool FirstStep(Candidate t, out int prof, out int course)
        {
            prof = course = -1;
            return _study != null && ProPath.FirstStep(_study, t, UniversityOpen, out prof, out course);
        }

        /// <summary>True if a vacancy is open to the player now: qualified, near enough what it asks, and an offer can come of it.</summary>
        internal bool OpenToMe(Opening o, Candidate t)
        {
            Profession p = o.Def;
            if (p == null || !ProRules.Qualified(_study, p, o.Rank) || !ProRules.Eligible(_study, p, o.Rank))
            {
                return false;
            }
            return o.Via == (int)Via.Headhunter || ProPath.CanBeOffered(ProRules.HireScore(CandidateFor(o, t)));
        }

        private static bool AnyHonours(StudyFile f)
        {
            for (int i = 0; i < f.Results.Count; i++)
            {
                Enrolment e = f.Results[i];
                if (e.Stage == (int)EnrolStage.Passed && e.Def != null && e.Def.Kind != CourseKind.Short && StudyRules.Honours(e.Def, e.Grade))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The fields the last reason <see cref="Apply"/> gave needs filled in, once; null if it needs none.</summary>
        internal Dictionary<string, string> TakeWhyData()
        {
            Dictionary<string, string> d = _whyData;
            _whyData = null;
            return d;
        }

        /// <summary>Why the player cannot apply for a post at an employer that turned them down: who, when, and from which day they can.</summary>
        internal static string TurnedDownText(Application a)
        {
            return CareersText.Fill(TurnedDownKey, "employer", CareersText.Employer(a.Post.Employer), "day", CareersText.Day(a.Refused), "day2", CareersText.Day(ProRules.ReapplyFrom(a.Refused)));
        }

        private const string TurnedDownKey = "reallife_work_why_turned_down";

        /// <summary>Applies for a vacancy. Null if done, else a reason key (its fields, if any, from <see cref="TakeWhyData"/>).</summary>
        public string Apply(int opening)
        {
            _whyData = null;
            WorkFile w = Work;
            Opening o = w != null ? w.OpeningOf(opening) : null;
            int day = Save != null ? Save.Day : 0;
            if (o == null || !o.Open(day))
            {
                return "reallife_work_why_gone";
            }
            if (w.Applied(o) != null)
            {
                return "reallife_work_why_applied";
            }
            if (w.Job != null && w.Job.Post.Same(o))
            {
                return "reallife_work_why_yours";
            }
            Application turned = w.TurnedDown(o, day);
            if (turned != null)
            {
                _whyData = Data("employer", CareersText.Employer(turned.Post.Employer), "day", CareersText.Day(turned.Refused), "day2", CareersText.Day(ProRules.ReapplyFrom(turned.Refused)));
                return TurnedDownKey;   // the same post waits ReapplyDays after a no, so applying daily cannot beat the odds
            }
            int live = 0;
            for (int i = 0; i < w.Applications.Count; i++)
            {
                live += w.Applications[i].Live ? 1 : 0;
            }
            if (live >= ApplicationsLive)
            {
                return "reallife_work_why_many";
            }
            Profession p = o.Def;
            if (!ProRules.Qualified(_study, p, o.Rank))
            {
                return "reallife_work_why_unqualified";
            }
            if (!ProRules.Eligible(_study, p, o.Rank))
            {
                return "reallife_work_why_skills";
            }
            if (o.Via != (int)Via.Headhunter && !ProPath.CanBeOffered(ProRules.HireScore(CandidateFor(o))))
            {
                return "reallife_work_why_no_chance";   // not even the best luck at both steps would do
            }
            var a = new Application { Day = day, Stage = (int)ApplyStep.Sent };
            o.CopyTo(a.Post);
            a.Score = ProRules.HireScore(CandidateFor(o));
            a.Answer = day + TechRandom.Range(StudySeed, 8401, o.Id, 0, ProRules.AnswerDaysLeast, ProRules.AnswerDaysMost);
            if (o.Via == (int)Via.Headhunter)
            {
                Offer(a, day);
            }
            w.Applications.Add(a);
            while (w.Applications.Count > WorkFile.ApplicationsMost)
            {
                int drop = -1;
                for (int i = 0; i < w.Applications.Count && drop < 0; i++)
                {
                    drop = w.Applications[i].Live || w.Applications[i].Bars(day) ? -1 : i;   // a no still barring its post is kept while anything older can go
                }
                w.Applications.RemoveAt(drop < 0 ? 0 : drop);
            }
            Tell2(NotificationType.Success, "reallife_work_note_applied", Data("title", CareersText.Title(o.Prof, o.Rank), "employer", CareersText.Employer(o.Employer)), TabApplications,
                CareerApp.ApplicationRow + a.Post.Id);
            Logger?.Info("Careers: applied to be " + p.Key + " rank " + o.Rank + " at " + o.Employer + " (via " + (Via)o.Via + "), scored " + a.Score + ".");
            Changed();
            return null;
        }

        // An employer's no, with the reason when the option is on: how far short the score was (ProRules.ScoreShort), or close.
        private void TurnedDown(Application a, string employer, string title, bool interview)
        {
            if (!Settings.CareersRejectReason)
            {
                Tell2(NotificationType.Info, interview ? "reallife_work_note_not_after_interview" : "reallife_work_note_rejected",
                    interview ? Data("employer", employer) : Data("employer", employer, "title", title), TabApplications, CareerApp.ApplicationRow + a.Post.Id);
                return;
            }
            int shortBy = ProRules.ScoreShort(a.Score, interview);
            string why = shortBy == 0 ? CareersText.T("reallife_work_why_close")
                : shortBy == 1 ? CareersText.T("reallife_work_why_short_one") : CareersText.T("reallife_work_why_short", "points", shortBy.ToString());
            Tell2(NotificationType.Info, interview ? "reallife_work_note_chosen_why" : "reallife_work_note_rejected_why", Data("employer", employer, "title", title, "why", why),
                TabApplications, CareerApp.ApplicationRow + a.Post.Id);
        }

        /// <summary>A day, or the next weekday after it.</summary>
        private static int Weekday(int day) => ProRules.Weekday(day);

        private void Offer(Application a, int day)
        {
            a.Stage = (int)ApplyStep.Offered;
            a.Offer = ProRules.OfferPay(a.Post.Pay, a.Score);
            a.Lapse = day + ProRules.OfferDays;
            Tell2(NotificationType.Success, "reallife_work_note_offer",
                Data("employer", CareersText.Employer(a.Post.Employer), "title", CareersText.Title(a.Post.Prof, a.Post.Rank), "pay", CareersText.Rate(Prices.At(a.Offer))),
                TabApplications, CareerApp.ApplicationRow + a.Post.Id);
        }

        /// <summary>Where an interview is held: at the employer's entrance, or by video from home for a post worked from home.</summary>
        internal string InterviewPlace(Application a)
        {
            return a.Post.Work == (int)WorkMode.Remote ? CareersText.T("reallife_work_interview_video")
                : CareersText.T("reallife_work_interview_entrance", "address", WorkGame.Address(a.Post.Street, a.Post.Number));
        }

        public string CanInterview(Application a, out int minutes)
        {
            minutes = ProRules.InterviewMinutes;
            if (a == null || a.Stage != (int)ApplyStep.Interview || Save.Day != a.InterviewDay)
            {
                return "reallife_work_why_no_interview";
            }
            int now = CareersGame.MinuteOfDay, at = a.InterviewHour * 60;
            if (now < at - ProRules.InterviewEarlyMinutes || now > at + ProRules.InterviewLateMinutes)
            {
                return "reallife_work_why_interview_hour";
            }
            if (a.Post.Work == (int)WorkMode.Remote)
            {
                return CareersGame.AtHome() ? StudyRules.NoNetWhy(CareersBridges.HomeNetNow()) : "reallife_work_why_interview_home";
            }
            return WorkGame.AtWork(a.Post.Def, a.Post) ? null : "reallife_work_why_interview_place";
        }

        public string Interview(int id)
        {
            Application a = Work != null ? Work.ApplicationOf(id) : null;
            int minutes;
            string why = CanInterview(a, out minutes);
            if (why != null)
            {
                return why;
            }
            return Offer(new Sitting(this, SittingKind.Interview, id, minutes, CareersText.T("reallife_work_skip_interview", "employer", CareersText.Employer(a.Post.Employer)), false,
                CareersText.T("reallife_work_skip_interview_for", "title", CareersText.Title(a.Post.Prof, a.Post.Rank))));
        }

        private void InterviewEnded(Sitting s, bool whole)
        {
            Application a = _study.Work.ApplicationOf(s.Of);
            if (a == null || a.Stage != (int)ApplyStep.Interview)
            {
                return;
            }
            if (!whole)
            {
                Tell2(NotificationType.Warning, "reallife_work_note_interview_left", Data("employer", CareersText.Employer(a.Post.Employer)), TabApplications, CareerApp.ApplicationRow + a.Post.Id);
                return;
            }
            int day = Save.Day;
            StudyRules.Use(_study, TalentCatalogue.Relationships, day);
            if (ProRules.Offered(a.Score, ProRules.Luck(StudySeed, a.Post.Id, 2, ProRules.InterviewLuck)))
            {
                Offer(a, day);
            }
            else
            {
                a.Reject(day);
                TurnedDown(a, CareersText.Employer(a.Post.Employer), CareersText.Title(a.Post.Prof, a.Post.Rank), true);
            }
        }

        /// <summary>The player asks for more than the offer. Null if answered (the answer is a notification), else a reason key.</summary>
        public string Ask(int id, int bp)
        {
            Application a = Work != null ? Work.ApplicationOf(id) : null;
            if (a == null || a.Stage != (int)ApplyStep.Offered)
            {
                return "reallife_work_why_no_offer";
            }
            if (a.Asks >= ProRules.AsksMost)
            {
                return "reallife_work_why_asked";
            }
            int chance = ProRules.AskChance(a.Score, bp, a.Asks);
            int roll = TechRandom.Mille(StudySeed, 8403, a.Post.Id, a.Asks);
            a.Asks++;
            string employer = CareersText.Employer(a.Post.Employer);
            if (roll < chance * 10)
            {
                a.Offer = (int)((long)a.Offer * (10000 + bp) / 10000);
                Tell2(NotificationType.Success, "reallife_work_note_ask_yes", Data("employer", employer, "pay", CareersText.Rate(Prices.At(a.Offer))), TabApplications, CareerApp.ApplicationRow + id);
            }
            else if (ProRules.Withdraws(bp, a.Asks - 1, TechRandom.Mille(StudySeed, 8404, a.Post.Id, a.Asks)))
            {
                a.Reject(Save.Day);
                Tell2(NotificationType.Warning, "reallife_work_note_ask_withdrawn", Data("employer", employer), TabApplications, CareerApp.ApplicationRow + id);
            }
            else
            {
                Tell2(NotificationType.Info, "reallife_work_note_ask_no", Data("employer", employer, "pay", CareersText.Rate(Prices.At(a.Offer))), TabApplications, CareerApp.ApplicationRow + id);
            }
            Changed();
            return null;
        }

        /// <summary>The player takes an offer: the job now (if any) is left with notice, and the new one begins after it.</summary>
        public string Accept(int id)
        {
            WorkFile w = Work;
            Application a = w != null ? w.ApplicationOf(id) : null;
            if (a == null || a.Stage != (int)ApplyStep.Offered)
            {
                return "reallife_work_why_no_offer";
            }
            for (int i = 0; i < w.Applications.Count; i++)
            {
                if (w.Applications[i].Stage == (int)ApplyStep.Accepted)
                {
                    return "reallife_work_why_accepted";
                }
            }
            int day = Save.Day;
            Job j = w.Job;
            if (j != null)
            {
                if (j.NoticeEnd <= 0)
                {
                    j.NoticeEnd = day + ProRules.NoticeDays(j.Rank, j.Post.Rank);
                    j.Leaving = (int)(a.Post.Via == (int)Via.Headhunter ? LeaveReason.Poached : LeaveReason.Resigned);
                }
                a.StartDay = j.NoticeEnd + 1;
            }
            else
            {
                a.StartDay = day + ProRules.StartDays;
            }
            a.Stage = (int)ApplyStep.Accepted;
            Tell2(NotificationType.Success, j != null ? "reallife_work_note_accepted_notice" : "reallife_work_note_accepted",
                Data("employer", CareersText.Employer(a.Post.Employer), "day", CareersText.Day(a.StartDay), "old", j != null ? CareersText.Employer(j.Post.Employer) : string.Empty), TabJob, null);
            Changed();
            return null;
        }

        public string Decline(int id)
        {
            Application a = Work != null ? Work.ApplicationOf(id) : null;
            if (a == null || !a.Live || a.Stage == (int)ApplyStep.Accepted)
            {
                return "reallife_work_why_no_offer";
            }
            a.Stage = a.Stage == (int)ApplyStep.Offered ? (int)ApplyStep.Declined : (int)ApplyStep.Withdrawn;
            Changed();
            return null;
        }

        /// <summary>Once a save, the first time the Career app opens with nothing to apply for, one notice says the first step.</summary>
        internal void FirstStepNotice()
        {
            WorkFile w = Work;
            if (w == null || w.StartTold || w.Job != null || Save == null)
            {
                return;
            }
            int day = Save.Day;
            for (int i = 0; i < w.Applications.Count; i++)
            {
                if (w.Applications[i].Live)
                {
                    return;
                }
            }
            Candidate t = Template();
            for (int i = 0; i < w.Openings.Count; i++)
            {
                Opening o = w.Openings[i];
                if (o.Open(day) && w.Applied(o) == null && OpenToMe(o, t))
                {
                    return;
                }
            }
            w.StartTold = true;
            int prof, course;
            string step = FirstStep(t, out prof, out course) ? FirstStepLine(prof, course) : CareersText.T("reallife_career_step_none");
            Tell2(NotificationType.Info, "reallife_career_note_start", Data("step", step), TabStart, prof >= 0 ? CareerApp.ProfessionRow + prof : null);
            Logger?.Info("Careers: the first step to an offer was told on day " + day + ": " + (prof >= 0 ? ProCatalogue.All[prof].Key : "none")
                         + (course >= 0 ? ", course " + course : string.Empty) + ".");
        }

        /// <summary>The first step in words: the course to take and where, or the profession to look for a vacancy of.</summary>
        internal string FirstStepLine(int prof, int course)
        {
            CourseDef c = StudyCatalogue.Find(course);
            string title = CareersText.Title(prof, 0);
            if (c == null)
            {
                return CareersText.T("reallife_career_step_vacancy", "title", title);
            }
            if (_study != null && _study.Live(c.Id) != null)
            {
                return CareersText.T("reallife_career_step_finish", "course", CareersText.Course(c.Id), "title", title);
            }
            return CareersText.Fill("reallife_career_step_course", "course", CareersText.Course(c.Id), "place", PlaceOf(c, StudyMode.FullTime), "title", title);
        }
    }
}
