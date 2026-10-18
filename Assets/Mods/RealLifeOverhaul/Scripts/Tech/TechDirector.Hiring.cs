using System;
using System.Collections.Generic;
using System.Globalization;
using Entities;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The hiring part: the labour market, the player's ads, applicants and searches, and the recruitment office's extra candidates.
    /// Each game day, before the people's.
    /// </summary>
    public sealed partial class TechDirector
    {
        public const int TabJobs = 1, TabApplicants = 2, TabMarket = 3, TabSearch = 4;

        private readonly List<string> _districts = new List<string>(16);
        private readonly List<string> _rivals = new List<string>(24);
        private readonly List<MarketNote> _notes = new List<MarketNote>(8);
        private readonly List<RecruitmentCampaign> _campaigns = new List<RecruitmentCampaign>(4);
        private readonly List<EmployeeInstance> _candidates = new List<EmployeeInstance>(16);
        private HiringFile _hiring;
        private bool _hiringOn;
        private HiringRules.RateOf _rate;
        private InterviewMeeting _interview;

        /// <summary>The labour market and the player's hiring; null while the part is off.</summary>
        public HiringFile Hiring => _hiringOn ? _hiring : null;

        /// <summary>The player has taken a rival's post (Careers): the first such ad is filled. True if there was one.</summary>
        internal bool FillRivalAd(string rival, int family, int level)
        {
            if (!_hiringOn || _hiring == null)
            {
                return false;
            }
            for (int i = 0; i < _hiring.RivalAds.Count; i++)
            {
                RivalAd ad = _hiring.RivalAds[i];
                if (ad.Rival == rival && ad.Family == family && ad.Level == level)
                {
                    _hiring.RivalAds.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        private void StartHiring()
        {
            GameInstance save = Save;
            HiringFile file = HiringStore.Load(save.modData, out int version);
            bool fresh = file == null;
            if (fresh)
            {
                file = new HiringFile { Seed = new System.Random().Next(1, int.MaxValue), FirstDay = save.Day };
            }
            _hiring = file;
            _hiringOn = true;
            _rate = (role, points) => TalentGame.GameRate(role.GameSkill, points);
            HiringGame.Districts(_districts);
            HiringGame.Rivals(_rivals);
            if (_interview == null)
            {
                _interview = new InterviewMeeting(this);
            }
            if (fresh)
            {
                HiringRules.Day(_hiring, save.Day, HiringGame.Cycle, HiringGame.Summer(save.Day), _rivals, _rate, _districts, null);
                NameSeekers();
            }
            Logger?.Info("Tech: the labour market " + (fresh ? "opened for this save" : "loaded (version " + version + ")") + " on day " + save.Day + ": "
                         + HiringRules.Open(_hiring) + " seeker(s), " + _hiring.Ads.Count + " ad(s) of the player's, " + _hiring.RivalAds.Count + " of rivals, "
                         + _hiring.Applicants.Count + " applicant(s).");
        }

        private void StopHiring(bool keep)
        {
            if (!_hiringOn)
            {
                return;
            }
            if (keep && Save != null && _hiring != null)
            {
                WriteHiring(Save.modData);
            }
            _interview?.End();
            _hiringOn = false;
        }

        private void WriteHiring(Dictionary<string, string> data)
        {
            if (_hiring != null && data != null)
            {
                data[HiringStore.Key] = HiringStore.Write(_hiring);
            }
        }

        private void NameSeekers()
        {
            for (int i = 0; i < _hiring.Seekers.Count; i++)
            {
                if (string.IsNullOrEmpty(_hiring.Seekers[i].P.Name))
                {
                    HiringGame.Name(_hiring.Seekers[i]);
                }
            }
        }

        private void HiringDay(int day)
        {
            if (_hiring.LastDay >= day)
            {
                return;
            }
            float started = Time.realtimeSinceStartup;
            _notes.Clear();
            int esteem = TechBridges.HiringEsteem();   // as an employer, with their best office's
            HiringRules.Day(_hiring, day, HiringGame.Cycle, HiringGame.Summer(day), _rivals, _rate, _districts, _notes);
            int delivered = 0;
            for (int i = 0; i < _hiring.Ads.Count; i++)
            {
                JobAd ad = _hiring.Ads[i];
                if (ad.Search && ad.Came == 0 && day >= ad.Due)
                {
                    delivered += HiringRules.Deliver(_hiring, ad, day, _rate, HiringRules.Shortage(_hiring, TalentCatalogue.Role(ad.Role).Family, day), _districts);
                    Tell(NotificationType.Success, "reallife_hire_note_search", Data("role", TechText.Role(ad.Role), "count", HiringCatalogue.SearchCandidates.ToString()), TabApplicants, null);
                }
            }
            int came = HiringRules.Apply(_hiring, day, esteem);
            NameSeekers();
            if (came > 0)
            {
                Tell(NotificationType.Info, "reallife_hire_note_applied", Data("count", came.ToString()), TabApplicants, null);
            }
            HiringRules.Answers(_hiring, day, esteem, f => HiringRules.Shortage(_hiring, f, day), _notes);
            for (int i = 0; i < _notes.Count; i++)
            {
                Said(_notes[i]);
            }
            int startedHires = Starts(day);
            int bonus = Office(day);
            Files(day);
            for (int i = _hiring.Ads.Count - 1; i >= 0; i--)
            {
                JobAd ad = _hiring.Ads[i];
                if (day > ad.To + HiringCatalogue.ApplicantDays && !AnyLive(ad.Id))
                {
                    _hiring.Ads.RemoveAt(i);
                }
            }
            for (int i = _hiring.Applicants.Count - 1; i >= 0; i--)
            {
                Applicant a = _hiring.Applicants[i];
                if (!a.Live && day > a.Day + 30)
                {
                    _hiring.Applicants.RemoveAt(i);
                }
            }
            _hiring.LastDay = day;
            if (Verbose)
            {
                Logger?.Info("Tech check (labour market, day " + day + "): " + HiringRules.Open(_hiring) + " seeker(s) of "
                             + HiringRules.Target(HiringGame.Cycle, HiringGame.Summer(day)) + " wanted, " + _hiring.RivalAds.Count + " rival ad(s), " + came + " application(s), "
                             + _notes.Count + " answer(s) and withdrawal(s), " + startedHires + " start(s), " + delivered + " from searches, " + bonus + " extra from the recruitment office; "
                             + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms: PASS");
            }
        }

        private bool AnyLive(int ad)
        {
            for (int i = 0; i < _hiring.Applicants.Count; i++)
            {
                if (_hiring.Applicants[i].Ad == ad && _hiring.Applicants[i].Live)
                {
                    return true;
                }
            }
            return false;
        }

        private void Said(MarketNote note)
        {
            Applicant a = _hiring.ApplicantOf(note.Applicant);
            Seeker s = a != null ? _hiring.SeekerOf(a.Seeker) : null;
            if (a == null || s == null)
            {
                return;
            }
            string row = RolesApp.ApplicantRow + a.Id;
            switch (note.Kind)
            {
                case HiringRules.WithdrewNote:
                    string employer = s.Employer.Length > 0 ? s.Employer : TechText.T("reallife_talent_rival_default");
                    Tell(NotificationType.Warning, "reallife_hire_note_withdrew", Data("name", s.P.Name, "employer", employer), TabApplicants, row);
                    break;
                case HiringRules.LapsedNote:
                    Tell(NotificationType.Info, "reallife_hire_note_counter_lapsed", Data("name", s.P.Name), TabApplicants, row);
                    break;
                case HiringRules.AnsweredNote:
                    if (a.At == ApplyStage.Accepted)
                    {
                        Tell(NotificationType.Success, "reallife_hire_note_accepted", Data("name", s.P.Name, "day", a.Start.ToString()), TabApplicants, row);
                    }
                    else if (a.At == ApplyStage.Countered)
                    {
                        Tell(NotificationType.Warning, a.RivalBid > 0 ? "reallife_hire_note_rival_bid" : "reallife_hire_note_countered",
                            Data("name", s.P.Name, "amount", TechText.Rate(a.Counter)), TabApplicants, row);
                    }
                    else
                    {
                        Tell(NotificationType.Warning, "reallife_hire_note_declined", Data("name", s.P.Name), TabApplicants, row);
                    }
                    break;
            }
        }

        // Accepted applicants whose first day has come start: as the game's employee, or on the mod's payroll for a role the
        // game has not or a job done from home. A search's fee is settled.
        private int Starts(int day)
        {
            int n = 0;
            for (int i = 0; i < _hiring.Applicants.Count; i++)
            {
                Applicant a = _hiring.Applicants[i];
                if (a.Stage != (int)ApplyStage.Accepted || day < a.Start)
                {
                    continue;
                }
                Seeker s = _hiring.SeekerOf(a.Seeker);
                JobAd ad = _hiring.AdOf(a.Ad);
                BuildingRegistration at = ad != null ? TalentGame.Find(ad.Place) : null;
                if (s == null || ad == null || at == null)
                {
                    a.Stage = (int)ApplyStage.Withdrawn;   // the business is no longer the player's
                    continue;
                }
                bool payroll = !s.P.Def.InGame || ad.Work == (int)Arrangement.Remote;
                bool ok;
                if (payroll)
                {
                    ok = StartPayroll(s, ad, a, day);
                }
                else
                {
                    EmployeeInstance candidate = HiringGame.Candidate(s, at, null);
                    ok = candidate != null && HiringGame.Hire(candidate, a.Offer);
                    if (ok)
                    {
                        File(HiringGame.Id(candidate), s.P, day, ad.Work);
                    }
                }
                if (!ok)
                {
                    continue;   // tried again tomorrow
                }
                if (ad.Search && ad.FeePaid < ad.Fee)
                {
                    TalentGame.Cost(ad.Fee - ad.FeePaid, HiringGame.SearchKey, at, TalentGame.NameOf(at), false, true);
                    ad.FeePaid = ad.Fee;
                }
                a.Stage = (int)ApplyStage.Started;
                s.Taken = true;
                n++;
                Tell(NotificationType.Success, payroll ? "reallife_hire_note_started_payroll" : "reallife_hire_note_started",
                    Data("name", s.P.Name, "business", TalentGame.NameOf(at), "role", TechText.Role(s.P.Role)), TabTeam, null);
            }
            return n;
        }

        private bool StartPayroll(Seeker s, JobAd ad, Applicant a, int day)
        {
            if (_talent == null || _talent.People.Count >= TalentStore.PeopleMost)
            {
                return false;
            }
            Profile p = Copy(s.P);
            p.Id = "pro:" + _talent.NextPro++;
            p.Main = s.P.Main;
            p.Name = s.P.Name;
            p.Wage = Math.Max(a.Offer, TalentCatalogue.RateLeast);
            p.Place = ad.Place;
            p.Work = ad.Work;
            p.Joined = day;
            p.LevelDay = day;
            p.ReviewDay = TalentRules.FirstReview(day, Year);
            p.Morale = TalentCatalogue.Clamp(550 + s.Fit / 4, 0, 1000);
            _talent.People.Add(p);
            return true;
        }

        private static Profile Copy(Profile from)
        {
            return new Profile
            {
                Role = from.Role, Level = from.Level, A = from.A, B = from.B, Main = from.Main, YearsDays = from.YearsDays, School = from.School, Certs = from.Certs,
                Record = from.Record, Female = from.Female, Age = from.Age, Name = from.Name
            };
        }

        /// <summary>A file made ahead for a candidate of the game's: the people part takes it when they are hired.</summary>
        private void File(string candidateId, Profile from, int day, int work)
        {
            if (string.IsNullOrEmpty(candidateId) || _hiring.CandidateOf(candidateId) != null || _hiring.Candidates.Count >= 64)
            {
                return;
            }
            Profile p = Copy(from);
            p.Id = candidateId;
            p.Work = TalentCatalogue.Clamp(work, 0, 1);
            _hiring.Candidates.Add(new CandidateFile { Id = candidateId, P = p, Day = day });
        }

        /// <summary>The file made ahead for a candidate of the game's who is now an employee; null if there is none.</summary>
        internal Profile Ahead(string employeeId)
        {
            if (!_hiringOn || _hiring == null)
            {
                return null;
            }
            CandidateFile c = _hiring.CandidateOf(employeeId);
            if (c == null)
            {
                return null;
            }
            _hiring.Candidates.Remove(c);
            return c.P;
        }

        // The recruitment office: each campaign for a professional skill brings extra seekers (HiringRules.OfficeBonus) for
        // the roles the business wants, a share each day it runs.
        private int Office(int day)
        {
            HiringGame.Campaigns(_campaigns);
            int made = 0;
            for (int i = 0; i < _campaigns.Count; i++)
            {
                RecruitmentCampaign game = _campaigns[i];
                string key = HiringGame.KeyOf(game);
                OfficeCampaign c = null;
                for (int k = 0; k < _hiring.Campaigns.Count && c == null; k++)
                {
                    c = _hiring.Campaigns[k].Key == key ? _hiring.Campaigns[k] : null;
                }
                if (c == null)
                {
                    HiringGame.Days(game, out int from, out int to);
                    c = new OfficeCampaign
                    {
                        Key = key, Place = HiringGame.Place(game.businessAddress), Agency = HiringGame.Place(game.agencyAddress),
                        Skill = game.skillRequirement.skillName, From = Math.Max(day, from), To = Math.Max(day, to), Planned = HiringRules.OfficeBonus(game.amountOfCandidates)
                    };
                    if (_hiring.Campaigns.Count >= 16)
                    {
                        _hiring.Campaigns.RemoveAt(0);
                    }
                    _hiring.Campaigns.Add(c);
                    Tell(NotificationType.Info, "reallife_hire_note_office", Data("count", c.Planned.ToString()), TabTeam, null);
                }
                if (day < c.From || c.Delivered >= c.Planned)
                {
                    continue;
                }
                BuildingRegistration business = TalentGame.Find(c.Place);
                if (business == null)
                {
                    continue;
                }
                int wantedCount = WantedRoles(TalentGame.TypeOf(business), _wanted);
                int left = c.Planned - c.Delivered, daysLeft = Math.Max(1, c.To - day + 1);
                int today = (left + daysLeft - 1) / daysLeft;
                for (int k = 0; k < today; k++)
                {
                    Seeker s = HiringRules.ForOffice(_hiring, c.Skill, _wanted, wantedCount);
                    if (s == null)
                    {
                        int role = TalentRules.Pick(c.Skill, _wanted, wantedCount, _hiring.Seed, _hiring.NextId);
                        if (role < 0)
                        {
                            break;
                        }
                        s = HiringRules.NewSeeker(_hiring.Seed, _hiring.Next(), day, false, role, -1, _rate, 1000, _districts.Count > 0 ? _districts[0] : string.Empty);
                        s.P.Main = Math.Max(s.P.Main, HiringCatalogue.OfficeSkillFloor * 1000);
                        HiringGame.Name(s);
                    }
                    EmployeeInstance candidate = HiringGame.Candidate(s, business, game.agencyAddress);
                    if (candidate == null)
                    {
                        break;
                    }
                    File(HiringGame.Id(candidate), s.P, day, (int)Arrangement.OnSite);
                    s.Taken = true;
                    c.Delivered++;
                    made++;
                }
            }
            for (int k = _hiring.Campaigns.Count - 1; k >= 0; k--)
            {
                if (day > _hiring.Campaigns[k].To + 7)
                {
                    _hiring.Campaigns.RemoveAt(k);
                }
            }
            return made;
        }

        // A file made ahead for each of the office's own candidates; files older than ten days are dropped.
        private void Files(int day)
        {
            HiringGame.OfficeCandidates(_candidates);
            for (int i = 0; i < _candidates.Count; i++)
            {
                EmployeeInstance e = _candidates[i];
                if (_hiring.CandidateOf(e.id) != null)
                {
                    continue;
                }
                string skill = TalentGame.Skill(e);
                int main = TalentGame.MainPoints(e);
                BuildingRegistration at = TalentGame.Business(e);
                int wantedCount = WantedRoles(TalentGame.TypeOf(at), _wanted);
                int n = TechRandom.Of(e.id);
                int role = TalentRules.Pick(skill, _wanted, wantedCount, _hiring.Seed, n);
                if (role < 0)
                {
                    continue;
                }
                Profile p = TalentRules.Make(_hiring.Seed, n, role, TalentRules.LevelFor(main, TalentCatalogue.Role(role)), day);
                p.A = TalentCatalogue.Clamp(main + TechRandom.Range(_hiring.Seed, n, 81, 0, -6, 10), 1, 100) * 1000;
                p.B = TalentCatalogue.Clamp(main + TechRandom.Range(_hiring.Seed, n, 82, 0, -10, 6), 1, 100) * 1000;
                p.Record = false;
                File(e.id, p, day, (int)Arrangement.OnSite);
            }
            for (int i = _hiring.Candidates.Count - 1; i >= 0; i--)
            {
                if (day - _hiring.Candidates[i].Day > 10)
                {
                    _hiring.Candidates.RemoveAt(i);
                }
            }
        }

        /// <summary>The going rate for a role at a level, cents an hour: the game's wage at the middle of the level's band, by the shortage.</summary>
        internal int GoingFor(int role, int level)
        {
            RoleDef r = TalentCatalogue.Role(role);
            level = TalentCatalogue.Clamp(level, r.LeastLevel, r.MostLevel);
            int mid = (TalentCatalogue.LevelSkillLeast[level] + TalentCatalogue.LevelSkillMost[level]) / 2;
            int shortage = _hiringOn ? HiringRules.Shortage(_hiring, r.Family, Save != null ? Save.Day : 0) : 1000;
            return TalentRules.Going(TalentGame.GameRate(r.GameSkill, mid), r, shortage, 0);
        }

        /// <summary>An ad posted on the jobs board, paid now by the business. False if there is no room or the cash does not cover it.</summary>
        internal bool PostAd(string place, int role, int level, int payLeast, int payMost, int minSkill, int work, int days, bool boost)
        {
            if (!_hiringOn || Save == null)
            {
                return false;
            }
            int open = 0;
            for (int i = 0; i < _hiring.Ads.Count; i++)
            {
                open += !_hiring.Ads[i].Search && _hiring.Ads[i].To > Save.Day ? 1 : 0;
            }
            BuildingRegistration at = TalentGame.Find(place);
            RoleDef r = TalentCatalogue.Role(role);
            if (open >= HiringCatalogue.AdsMost || at == null)
            {
                return false;
            }
            if (work == (int)Arrangement.Remote && !r.Remote)
            {
                work = (int)Arrangement.Hybrid;
            }
            int cost = Prices.At(HiringRules.AdCost(days, boost));
            if (!HiringGame.Pay(cost * 100, HiringGame.AdKey, at, TalentGame.NameOf(at)))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            _hiring.Ads.Add(new JobAd
            {
                Id = _hiring.Next(), Place = place, Role = r.Id, Level = TalentCatalogue.Clamp(level, r.LeastLevel, r.MostLevel), PayLeast = payLeast,
                PayMost = Math.Max(payLeast, payMost), MinSkill = minSkill, Work = work, From = Save.Day, To = Save.Day + days, Boost = boost
            });
            Changed();
            return true;
        }

        /// <summary>An ad taken down early: nobody new applies; who has applied stays.</summary>
        internal void CloseAd(int id)
        {
            JobAd ad = _hiringOn ? _hiring.AdOf(id) : null;
            if (ad != null && Save != null && ad.To > Save.Day)
            {
                ad.To = Save.Day;
                Changed();
            }
        }

        internal Applicant ApplicantOf(int id) => _hiringOn ? _hiring.ApplicantOf(id) : null;

        internal void Shortlist(int id, bool keep)
        {
            Applicant a = ApplicantOf(id);
            if (a != null && a.Live && a.Stage < (int)ApplyStage.Offered)
            {
                a.Stage = keep ? Math.Max(a.Stage, (int)ApplyStage.Shortlisted) : (int)ApplyStage.Rejected;
                Changed();
            }
        }

        /// <summary>References asked for (they say how the CV holds up) or a background check run (a past conviction shows), paid now.</summary>
        internal bool Check(int id, bool background)
        {
            Applicant a = ApplicantOf(id);
            JobAd ad = a != null ? _hiring.AdOf(a.Ad) : null;
            BuildingRegistration at = ad != null ? TalentGame.Find(ad.Place) : null;
            if (a == null || at == null || !a.Live || (background ? a.Background : a.Refs))
            {
                return false;
            }
            int cost = Prices.At(background ? HiringCatalogue.BackgroundCost : HiringCatalogue.ReferencesCost);
            if (!HiringGame.Pay(cost * 100, HiringGame.ChecksKey, at, TalentGame.NameOf(at)))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            if (background)
            {
                a.Background = true;
            }
            else
            {
                a.Refs = true;
            }
            Changed();
            return true;
        }

        internal bool Interview(int id)
        {
            Applicant a = ApplicantOf(id);
            Seeker s = a != null ? _hiring.SeekerOf(a.Seeker) : null;
            if (a == null || s == null || !a.Live || a.Stage >= (int)ApplyStage.Offered || _interview == null || _interview.Active)
            {
                return false;
            }
            return _interview.Begin(a, s, _hiring.AdOf(a.Ad));
        }

        /// <summary>An offer made at a pay; they answer the next day.</summary>
        internal void MakeOffer(int id, int cents)
        {
            Applicant a = ApplicantOf(id);
            if (a != null && a.Live && a.Stage < (int)ApplyStage.Offered && Save != null && cents > 0)
            {
                HiringRules.Offer(a, cents, Save.Day);
                Changed();
            }
        }

        /// <summary>Their counter taken: the offer is raised to it and they accept at once.</summary>
        internal void TakeCounter(int id)
        {
            Applicant a = ApplicantOf(id);
            Seeker s = a != null ? _hiring.SeekerOf(a.Seeker) : null;
            if (a == null || s == null || a.Stage != (int)ApplyStage.Countered || Save == null)
            {
                return;
            }
            a.Offer = a.Counter;
            a.Stage = (int)ApplyStage.Accepted;
            a.Start = Save.Day + (s.Employed ? Math.Max(HiringCatalogue.StartLeast, s.Notice) : HiringCatalogue.StartLeast);
            Tell(NotificationType.Success, "reallife_hire_note_accepted", Data("name", s.P.Name, "day", a.Start.ToString()), TabApplicants, RolesApp.ApplicantRow + a.Id);
            Changed();
        }

        internal void WalkAway(int id)
        {
            Applicant a = ApplicantOf(id);
            if (a != null && a.Live && a.Stage != (int)ApplyStage.Accepted)
            {
                a.Stage = (int)ApplyStage.Rejected;
                Changed();
            }
        }

        /// <summary>A search firm briefed for a senior role: a third of the fee now, the rest on a hire. False without room or cash.</summary>
        internal bool StartSearch(string place, int role, int level)
        {
            if (!_hiringOn || Save == null)
            {
                return false;
            }
            int open = 0;
            for (int i = 0; i < _hiring.Ads.Count; i++)
            {
                JobAd ad = _hiring.Ads[i];
                open += ad.Search && (ad.Came == 0 || AnyLive(ad.Id)) ? 1 : 0;
            }
            BuildingRegistration at = TalentGame.Find(place);
            RoleDef r = TalentCatalogue.Role(role);
            level = TalentCatalogue.Clamp(Math.Max(level, HiringCatalogue.SearchLeastLevel), r.LeastLevel, r.MostLevel);
            if (open >= HiringCatalogue.SearchesMost || at == null || level < HiringCatalogue.SearchLeastLevel)
            {
                return false;
            }
            SearchQuote(role, level, out int fee, out int retainer);
            if (!HiringGame.Pay(retainer, HiringGame.SearchKey, at, TalentGame.NameOf(at)))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            int day = Save.Day;
            _hiring.Ads.Add(new JobAd
            {
                Id = _hiring.Next(), Place = place, Role = r.Id, Level = level, PayLeast = 0, PayMost = 0, Work = (int)Arrangement.OnSite, From = day,
                To = day + HiringCatalogue.ApplicantDays, Search = true, Fee = fee, FeePaid = retainer,
                Due = day + TechRandom.Range(_hiring.Seed, _hiring.NextId, 43, 0, HiringCatalogue.SearchDaysLeast, HiringCatalogue.SearchDaysMost)
            });
            Changed();
            return true;
        }

        /// <summary>What a search for a role at a level would cost: the fee and the retainer, cents.</summary>
        internal void SearchQuote(int role, int level, out int fee, out int retainer)
        {
            fee = HiringRules.SearchFee(GoingFor(role, Math.Max(level, HiringCatalogue.SearchLeastLevel)) * (10000 + HiringCatalogue.EmployedPremiumBp) / 10000);
            retainer = HiringRules.Retainer(fee);
        }

        internal int ShortageOf(Family family) => _hiringOn && Save != null ? HiringRules.Shortage(_hiring, family, Save.Day) : 1000;
    }
}
