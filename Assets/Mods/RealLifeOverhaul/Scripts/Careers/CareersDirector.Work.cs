using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The working life: the city's vacancies (its businesses, the rivals' among them, its tech companies, the public sector
    /// and the rivals' ads on the jobs board the city shares), applications, interviews and offers, the job and its working
    /// days, notice and leaving (Careers/Work). On the hour a working day that is over is counted and the next is reminded
    /// of; on the day the vacancies are renewed, employers answer, a job that was accepted starts and one being left ends.
    /// A working day is worked at the player's own desk at the employer (or their computer at home on a day from home), on
    /// the game's own work panel, as the game's own job is (Careers/CareersDirector.Desk.cs).
    /// </summary>
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
        private readonly List<EmployerRef> _employers = new List<EmployerRef>(256);
        private readonly List<EmployerRef> _offices = new List<EmployerRef>(64);
        private readonly List<EmployerRef> _fit = new List<EmployerRef>(32);
        private readonly List<int> _reach = new List<int>(ProCatalogue.Count);
        private readonly List<BoardAd> _ads = new List<BoardAd>(40);
        private int _employersDay = -1;
        private long _hqSeen;   // the tech companies' headquarters as the posts were last moved to them
        private int _workReminded = -1, _interviewReminded = -1;

        public bool WorkRunning => _workOn;

        /// <summary>The working life; null while the part is off.</summary>
        public WorkFile Work => _workOn && _study != null ? _study.Work : null;

        // ---------------------------------------------------------------- starting and stopping

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

        // ---------------------------------------------------------------- the hour and the day

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
                if (a.Stage == (int)ApplyStep.Interview && a.InterviewDay == day && a.InterviewHour == hour + 1 && _interviewReminded != a.Post.Id)
                {
                    _interviewReminded = a.Post.Id;
                    Tell2(NotificationType.Info, "reallife_work_note_interview_soon",
                        Data("employer", CareersText.Employer(a.Post.Employer), "hour", CareersText.Hour(a.InterviewHour), "place", InterviewPlace(a)), TabApplications, CareerApp.ApplicationRow + a.Post.Id);
                }
            }
        }

        private bool Working => _sitting != null && (_sitting.Kind == SittingKind.Work || _sitting.Kind == SittingKind.Overtime);

        // The last day of a registration with the recruitment office: one reminder, from the morning (ProRules.AgencyEnds).
        // The day it was sent for is kept in the save, so a reload on that day does not send it again.
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

        /// <summary>A working day over: worked or missed. A missed one is a warning; the third is the sack, as the game's own employer has it.</summary>
        private void SettleDay(Job j, int day)
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
                j.Absent = Math.Max(0, j.Absent - 1);
                j.Due = Math.Max(0, j.Due - 1);
                return;
            }
            j.Warnings++;
            // (said by the boss, in a text of the game's own employer kind; a notification as before if it cannot be sent)
            if (j.Warnings >= WarningsToGo)
            {
                BossSays(j, "reallife_work_text_sacked", Data("employer", CareersText.Employer(j.Post.Employer), "count", WarningsToGo.ToString()), NotificationType.Error, "reallife_work_note_sacked", TabCv);
                EndJob(LeaveReason.Dismissed, day, false);
                return;
            }
            BossSays(j, "reallife_work_text_warning", Data("employer", CareersText.Employer(j.Post.Employer), "count", j.Warnings.ToString(), "most", WarningsToGo.ToString()),
                NotificationType.Warning, "reallife_work_note_missed", TabJob);
        }

        /// <summary>A day off sick is excused: the mod's own sickness, as the careers of the street count it.</summary>
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

        // ---------------------------------------------------------------- the city's vacancies

        private void ReadEmployers(int day)
        {
            if (_employersDay == day)
            {
                return;
            }
            _employersDay = day;
            WorkGame.Employers(_employers, _offices);
            WorkGame.PublicPlaces();
            AtHeadquarters();
        }

        // The tech companies' vacancies, applications and the job at their headquarters (Tech/Sites), where one was given an
        // office building by its name before the company had a headquarters, or before it moved (its building was let).
        private void AtHeadquarters()
        {
            if (CareersBridges.Headquarters == null || _study == null)
            {
                return;
            }
            WorkFile w = _study.Work;
            for (int i = 0; i < w.Openings.Count; i++)
            {
                AtHeadquarters(w.Openings[i]);
            }
            for (int i = 0; i < w.Applications.Count; i++)
            {
                AtHeadquarters(w.Applications[i].Post);
            }
            if (w.Job != null && AtHeadquarters(w.Job.Post))
            {
                // The player's employer has moved (its tower chose another building, or a save from before the towers): the
                // job is kept, on the company's floor of its tower now.
                Logger?.Info("Careers: the player's employer " + w.Job.Post.Employer + " is now at " + w.Job.Post.Street + " " + w.Job.Post.Number + "; the job and its desk follow it.");
                Tell(NotificationType.Info, "reallife_career_note_employer_moved", Data("employer", CareersText.Employer(w.Job.Post.Employer), "address", WorkGame.Address(w.Job.Post.Street, w.Job.Post.Number)), TabJob, null);
            }
        }

        // Once a second: the tech companies' headquarters have changed since they were last read (chosen after the load, or a
        // tower moved): the vacancies, applications and the job at them follow at once, not the next morning.
        private void HeadquartersMoved()
        {
            long stamp = CareersBridges.HeadquartersStampNow();
            if (stamp == _hqSeen || stamp == 0 || _study == null)
            {
                return;
            }
            _hqSeen = stamp;
            AtHeadquarters();
        }

        // True if the post was moved to its company's headquarters as they stand now.
        private static bool AtHeadquarters(Opening o)
        {
            if (o == null || o.Kind != (int)EmployerKind.TechFirm || Array.IndexOf(HiringCatalogue.RivalFirms, o.Employer) < 0 || CareersBridges.Headquarters == null)
            {
                return false;
            }
            try
            {
                if (CareersBridges.Headquarters(o.Employer, out string street, out int number) && (o.Street != street || o.Number != number))
                {
                    o.Street = street;
                    o.Number = number;
                    return true;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                CareersBridges.Headquarters = null;   // (asked no more this session)
            }
            return false;
        }

        /// <summary>The vacancies renewed: the lapsed ones go, the rivals' ads on the jobs board are added, and the city's own fill up to their number.</summary>
        private void Vacancies(int day)
        {
            WorkFile w = _study.Work;
            if (w.OpeningsDay == day)
            {
                return;
            }
            w.OpeningsDay = day;
            ReadEmployers(day);
            for (int i = w.Openings.Count - 1; i >= 0; i--)
            {
                if (w.Openings[i].To < day)
                {
                    w.Openings.RemoveAt(i);
                }
            }
            BoardVacancies(w, day);
            int cycle = CareersBridges.CycleNow();
            bool agency = w.AgencyUntil >= day;
            int wanted = ProRules.OpeningsWanted(cycle, agency);
            int own = 0;
            for (int i = 0; i < w.Openings.Count; i++)
            {
                own += w.Openings[i].Via != (int)Via.JobsBoard ? 1 : 0;
            }
            bool referrals = CareersBridges.FriendsNow() >= ProRules.FriendsForReferral;
            for (int n = 0; own < wanted && n < wanted * 3 && w.Openings.Count < WorkFile.OpeningsMost; n++)
            {
                Opening o = NewVacancy(day, n, agency && own >= wanted - ProRules.OpeningsAgency, referrals);
                if (o != null)
                {
                    w.Openings.Add(o);
                    own++;
                }
            }
            ReachVacancies(w, day, referrals);
        }

        /// <summary>
        /// While the player has no job, the city keeps a few first posts open in the professions the player could be
        /// offered one in now (ProRules.OpeningsWithinReach): drawn blind, a first post of one profession would come
        /// about once a month, so a player who has just passed the course for it would wait weeks for a vacancy.
        /// </summary>
        private void ReachVacancies(WorkFile w, int day, bool referrals)
        {
            if (w.Job != null)
            {
                return;
            }
            _reach.Clear();
            Candidate t = Template();
            for (int i = 0; i < ProCatalogue.All.Length; i++)
            {
                Profession p = ProCatalogue.All[i];
                Candidate c = t;
                c.Experience = ProRules.Experience(w, p.Id, day, GameCalendar.DaysPerYear);
                if (ProPath.Ready(_study, p, 0, null, c))
                {
                    _reach.Add(p.Id);
                }
            }
            if (_reach.Count == 0)
            {
                return;
            }
            int within = 0;
            for (int i = 0; i < w.Openings.Count; i++)
            {
                Opening o = w.Openings[i];
                within += o.Open(day) && o.Rank == 0 && o.Via != (int)Via.JobsBoard && _reach.Contains(o.Prof) ? 1 : 0;
            }
            for (int n = 0; within < ProRules.OpeningsWithinReach && n < ProRules.OpeningsWithinReach * 4 && w.Openings.Count < WorkFile.OpeningsMost; n++)
            {
                int prof = _reach[TechRandom.Range(StudySeed, 8310, day, n, 0, _reach.Count - 1)];
                Opening o = NewVacancy(day, ReachDraws + n, false, referrals, prof, 0);
                if (o != null)
                {
                    w.Openings.Add(o);
                    within++;
                }
            }
        }

        private const int ReachDraws = 500;   // the draws of the vacancies within reach, apart from the day's others (under a hundred)

        private void BoardVacancies(WorkFile w, int day)
        {
            _ads.Clear();
            try
            {
                CareersBridges.Board?.Invoke(_ads);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            int added = 0;
            for (int i = 0; i < _ads.Count && added < 12 && w.Openings.Count < WorkFile.OpeningsMost; i++)
            {
                BoardAd ad = _ads[i];
                if (ad.To < day || string.IsNullOrEmpty(ad.Rival))
                {
                    continue;
                }
                bool seen = false;
                for (int k = 0; k < w.Openings.Count && !seen; k++)
                {
                    Opening o = w.Openings[k];
                    seen = o.Via == (int)Via.JobsBoard && o.Family == ad.Family && o.Level == ad.Level && o.To == ad.To && o.Employer == ad.Rival;
                }
                if (seen)
                {
                    continue;
                }
                int roll = TechRandom.Range(StudySeed, 8201, day, i, 0, 99);
                int prof = ProCatalogue.OfFamily(ad.Family, roll);
                Profession p = ProCatalogue.Find(prof);
                if (p == null)
                {
                    continue;
                }
                EmployerRef at = OfficeFor(ad.Rival, p);
                if (at == null)
                {
                    continue;
                }
                int rank = ProCatalogue.RankOfLevel(p, ad.Level);
                bool firm = Array.IndexOf(HiringCatalogue.RivalFirms, ad.Rival) >= 0;
                EmployerKind kind = firm ? EmployerKind.TechFirm : EmployerKind.Rival;
                var o2 = new Opening
                {
                    Id = w.Next(), Prof = prof, Rank = rank, Kind = (int)kind, Employer = ad.Rival, Street = at.Street, Number = at.Number, Rival = at.Rival,
                    Pay = (int)((long)ProRules.PostPay(p, rank, kind, CareersBridges.ShortageOf(p.Family)) * (10000 + ad.PayBp) / 10000),
                    Work = (int)ProRules.ModeRoll(p, TechRandom.Mille(StudySeed, 8202, day, i)), From = day, To = ad.To, Via = (int)Via.JobsBoard, Family = ad.Family, Level = ad.Level
                };
                w.Openings.Add(o2);
                added++;
            }
        }

        /// <summary>Where a company of the jobs board works: a rival's own office business if it has one, else its headquarters (Tech/Sites), else an office building given by its name.</summary>
        private EmployerRef OfficeFor(string company, Profession p)
        {
            for (int i = 0; i < _employers.Count; i++)
            {
                EmployerRef e = _employers[i];
                if (e.Kind == EmployerKind.Rival && ProCatalogue.Holds(e.Professions, p, e.Type) && WorkGame.RivalName(e.Rival) == company)
                {
                    return e;
                }
            }
            if (CareersBridges.Headquarters != null && CareersBridges.Headquarters(company, out string street, out int number))
            {
                return new EmployerRef { Name = company, Street = street, Number = number, Kind = EmployerKind.TechFirm };
            }
            if (_offices.Count == 0)
            {
                return null;
            }
            return _offices[(TechRandom.Of(company) & int.MaxValue) % _offices.Count];
        }

        /// <summary>One new vacancy of the city's: a profession that has employers, one of them, a rung, the pay and how the days are worked.</summary>
        private Opening NewVacancy(int day, int n, bool agency, bool referrals, int forProf = -1, int forRank = -1)
        {
            int prof = forProf >= 0 ? forProf : TechRandom.Range(StudySeed, 8301, day, n, 0, ProCatalogue.Count - 1);
            Profession p = ProCatalogue.Find(prof);
            EmployerRef at = null;
            EmployerKind kind = EmployerKind.City;
            string name = null;
            if (p.Public)
            {
                at = WorkGame.PublicFor(p, TechRandom.Range(StudySeed, 8302, day, n, 0, 999));
                kind = EmployerKind.Public;
                name = PublicName(p);
            }
            else
            {
                _fit.Clear();
                for (int i = 0; i < _employers.Count; i++)
                {
                    if (ProCatalogue.Holds(_employers[i].Professions, p, _employers[i].Type))
                    {
                        _fit.Add(_employers[i]);
                    }
                }
                bool firm = p.Family >= 0 && p.Types.Length > 0 && p.Types[0] == ProCatalogue.TechTypes && _offices.Count > 0 && TechRandom.Mille(StudySeed, 8303, day, n) < 450;
                if (firm || _fit.Count == 0)
                {
                    if (p.Types.Length == 0 || p.Types[0] != ProCatalogue.TechTypes || _offices.Count == 0)
                    {
                        return null;
                    }
                    name = HiringCatalogue.RivalFirms[TechRandom.Range(StudySeed, 8304, day, n, 0, HiringCatalogue.RivalFirms.Length - 1)];
                    at = OfficeFor(name, p);
                    kind = EmployerKind.TechFirm;
                }
                else
                {
                    at = _fit[TechRandom.Range(StudySeed, 8305, day, n, 0, _fit.Count - 1)];
                    kind = at.Kind;
                    name = at.Name;
                }
            }
            if (at == null || string.IsNullOrEmpty(name))
            {
                return null;
            }
            int rank = forRank >= 0 ? Math.Min(forRank, p.Ranks.Length - 1) : ProRules.RankRoll(p, TechRandom.Mille(StudySeed, 8306, day, n));
            var o = new Opening
            {
                Id = _study.Work.Next(), Prof = prof, Rank = rank, Kind = (int)kind, Employer = name, Street = at.Street, Number = at.Number, Rival = at.Rival,
                Pay = ProRules.PostPay(p, rank, kind, CareersBridges.ShortageOf(p.Family)), Work = (int)ProRules.ModeRoll(p, TechRandom.Mille(StudySeed, 8307, day, n)), From = day,
                To = day + TechRandom.Range(StudySeed, 8308, day, n, ProRules.OpeningDaysLeast, ProRules.OpeningDaysMost),
                Via = (int)(agency ? Via.Agency : referrals && TechRandom.Mille(StudySeed, 8309, day, n) < ProRules.ReferralShare ? Via.Referral : Via.Board)
            };
            for (int i = 0; i < _study.Work.Openings.Count; i++)
            {
                if (_study.Work.Openings[i].Same(o))
                {
                    return null;
                }
            }
            return o;
        }

        /// <summary>The public sector's employers by name (a locale key, so the name follows the language).</summary>
        private static string PublicName(Profession p)
        {
            switch (p.Place)
            {
                case Workplace.Hospital: return CareersText.KeyName("reallife_work_employer_health");
                case Workplace.School: return CareersText.KeyName("reallife_work_employer_school");
                default: return CareersText.KeyName("reallife_work_employer_council");
            }
        }

        // ---------------------------------------------------------------- applying

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

        /// <summary>
        /// What every employer reads of the player alike (references, name, record, honours, the economy), read from the
        /// game once for a page of the Career app; the post's own part is added by <see cref="CandidateFor(Opening, Candidate)"/>.
        /// </summary>
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

        /// <summary>The fields of a refusal's notice (<see cref="TakeWhyData"/>); null for a reason with none.</summary>
        private Dictionary<string, string> _whyData;

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
                return TurnedDownKey;   // (the same post there waits ProRules.ReapplyDays after a no, so applying daily cannot beat the odds; other posts and employers stay open)
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
                return "reallife_work_why_no_chance";   // (the employer could not score the player high enough even with the best luck of both steps)
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
                    drop = w.Applications[i].Live || w.Applications[i].Bars(day) ? -1 : i;   // (a no still barring its post is kept while anything older can go)
                }
                w.Applications.RemoveAt(drop < 0 ? 0 : drop);
            }
            Tell2(NotificationType.Success, "reallife_work_note_applied", Data("title", CareersText.Title(o.Prof, o.Rank), "employer", CareersText.Employer(o.Employer)), TabApplications,
                CareerApp.ApplicationRow + a.Post.Id);
            Logger?.Info("Careers: applied to be " + p.Key + " rank " + o.Rank + " at " + o.Employer + " (via " + (Via)o.Via + "), scored " + a.Score + ".");
            Changed();
            return null;
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
                        if (day > a.InterviewDay)
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

        // An employer's no, at the screening or after the interview, with the reason when the option is on: how many points
        // of score the application was short of what they wanted (ProRules.ScoreShort), or that it was close (luck).
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
        private static int Weekday(int day)
        {
            while ((day - 1) % 7 >= 5)
            {
                day++;
            }
            return day;
        }

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
            if (now < at - 30 || now > at + ProRules.InterviewLateMinutes)
            {
                return "reallife_work_why_interview_hour";
            }
            if (a.Post.Work == (int)WorkMode.Remote)
            {
                return CareersGame.AtHome() ? (CareersBridges.HomeOnline() ? null : "reallife_study_why_no_internet") : "reallife_work_why_interview_home";
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

        private void InterviewEnded(Sitting s, int minutes, bool whole)
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
            a.Stage = (int)ApplyStep.Withdrawn;   // (done with: the job is the record now)
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

        // ---------------------------------------------------------------- the working day

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
                return CareersGame.AtHome() ? (CareersBridges.HomeOnline() ? null : "reallife_study_why_no_internet") : "reallife_work_why_home_day";
            }
            return WorkGame.AtWork(p, j.Post) ? null : "reallife_work_why_not_at_work";
        }

        /// <summary>
        /// The working day started from the Career app: only where no desk could be had (Careers/CareersDirector.Desk.cs:
        /// DeskFallback); everywhere else a day is started at the desk.
        /// </summary>
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
            StudyRules.Learn(_study, a, StudyCatalogue.WorkLearn * hours * (100 - StudyRules.Points(_study, a)) / 100, day);
            StudyRules.Learn(_study, b, StudyCatalogue.WorkLearn * hours * (100 - StudyRules.Points(_study, b)) / 100, day);
            WorkedMore(j, s, minutes, whole, day);
            if (ProRules.DayOver(p, day, day, CareersGame.MinuteOfDay) && j.SettledDay < day)
            {
                SettleDay(j, day);
            }
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

        /// <summary>
        /// The city's vacancies of today when the Career app opens: made now if the day has not made them yet (a new game,
        /// or the first day with professional jobs on, would otherwise show none until midnight), the same ones the day
        /// would make by the save's seed and the day; and the first posts within reach topped up, for a course passed
        /// since the morning.
        /// </summary>
        internal void VacanciesNow()
        {
            if (!_workOn || _study == null || Save == null)
            {
                return;
            }
            int day = Save.Day;
            if (_study.Work.OpeningsDay != day)
            {
                Vacancies(day);
                return;
            }
            ReadEmployers(day);
            ReachVacancies(_study.Work, day, CareersBridges.FriendsNow() >= ProRules.FriendsForReferral);
        }

        /// <summary>
        /// The first time the Career app is opened with nothing the player can apply for (no job, no application under way,
        /// no vacancy open to them), one notification says what the first step is. Told once a save.
        /// </summary>
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
            Logger?.Info("Careers: the first step to an offer was told on day " + day + ": " + (prof >= 0 ? ProCatalogue.All[prof].Key : "none") + (course >= 0 ? ", course " + course : string.Empty) + ".");
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

        // ---------------------------------------------------------------- the phone

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
                    Logger?.Warn("Careers: the game's phone is not built as this version expects, so the Career app is left off. A job under way still runs and the notifications still say what is due.");
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

        // ---------------------------------------------------------------- what working life adds (Careers/CareersDirector.Life.cs)

        partial void WorkDayMore(int day);

        partial void Hired(Job j, int day);

        partial void Ended(Job j, LeaveReason why, int day);

        partial void EndedOwn(Job j, LeaveReason why, int day);

        partial void WorkedMore(Job j, Sitting s, int minutes, bool whole, int day);

        partial void PaidMore(Job j, long cents, bool overtime);

        partial void LoanCleared();

        /// <summary>A notification that opens the Career app on a tab with a row picked.</summary>
        internal void Tell2(NotificationType type, string key, Dictionary<string, string> data, int tab, string pick)
        {
            Notifications.Show(type, key, data, 10f, null, Guard.Wrap(() => OpenCareer(tab, pick)), true, false);
        }
    }
}
