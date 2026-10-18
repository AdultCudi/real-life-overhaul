using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>The city's vacancies: its businesses', the rivals', the tech companies', the public sector's and the jobs board's.</summary>
    public sealed partial class CareersDirector
    {
        private readonly List<EmployerRef> _employers = new List<EmployerRef>(256);
        private readonly List<EmployerRef> _offices = new List<EmployerRef>(64);
        private readonly List<EmployerRef> _fit = new List<EmployerRef>(32);
        private readonly List<int> _reach = new List<int>(ProCatalogue.Count);
        private readonly List<BoardAd> _ads = new List<BoardAd>(40);
        private int _employersDay = -1;
        private long _hqSeen;   // the tech headquarters' stamp the posts last followed

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

        // Posts at a tech company's headquarters follow it when the company gets one or moves (its building was let).
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
                // the employer moved (another tower, or a save from before the towers): the job goes with it
                Logger?.Info("Careers: the player's employer " + w.Job.Post.Employer + " is now at " + w.Job.Post.Street + " " + w.Job.Post.Number + "; the job and its desk follow it.");
                Tell(NotificationType.Info, "reallife_career_note_employer_moved",
                    Data("employer", CareersText.Employer(w.Job.Post.Employer), "address", WorkGame.Address(w.Job.Post.Street, w.Job.Post.Number)), TabJob, null);
            }
        }

        // Once a second: the posts follow headquarters changed since they were last read, not the next morning.
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
                CareersBridges.Headquarters = null;   // not asked again this session
            }
            return false;
        }

        /// <summary>Renews the vacancies: the lapsed go, the rivals' board ads come in, and the city's own fill up to their number.</summary>
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
        /// With no job, keeps a few first posts open in the professions the player could be offered now: drawn blind, one would
        /// come about once a month, and a player who just passed its course would wait weeks.
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

        private const int ReachDraws = 500;   // draw offset of the vacancies within reach, clear of the day's others (under a hundred)

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

        /// <summary>
        /// Today's vacancies when the Career app opens: made now if the day has not made them (the same ones, by the seed), and
        /// the first posts within reach topped up for a course passed since the morning.
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
    }
}
