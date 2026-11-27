using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What naming somebody manager would do, as the question asks it: who, where, the pay rise that comes with it, and what it takes from their own work.</summary>
    public struct ManagerPlan
    {
        public string Name, Firm, Replaces;   // the person, the firm, and the manager it relieves (empty: none)
        public int Skill, Span;
        public int PaidBefore, PaidAfter;     // hundredths an hour; equal when they were raised lately and the rise waits
        public bool Rise;                     // the pay rise comes with it
    }

    /// <summary>
    /// A tech firm's named manager (<see cref="ManagerPost"/>, FirmManagerRules): the player names one of the firm's professionals, who keeps their role and works part of their
    /// hours for the team (TalentRules.Output), is raised for it, and each morning does four things the player switches on: balances the firm's roles to targets the
    /// player sets (by the bulk change's rules, a few moves a day by their skill), replaces people who leave (an ad, the best applicant, an offer), agrees the routine raises
    /// people ask for at review and matches a small rival offer, and flags those wearing thin or idle. They tell the week in one notice. The same switch as the assignment of
    /// support and sales (<see cref="AssignDay"/>) lets them do that too, in their name. Every decision is arithmetic (FirmManagerRules), none of it a roll. Runs with the people's
    /// day, never per frame.
    /// </summary>
    public sealed partial class TechDirector
    {
        private readonly Dictionary<string, int> _idleRuns = new Dictionary<string, int>(16);   // daily runs in a row with no hours, by file id: a reading, never saved
        private readonly int[] _have = new int[TalentCatalogue.RoleCount];
        private readonly List<ManagerTarget> _short = new List<ManagerTarget>(FirmManagerCatalogue.TargetsMost);
        private readonly List<ManagerApplicant> _mApplicants = new List<ManagerApplicant>(8);
        private readonly List<Applicant> _mApps = new List<Applicant>(8);
        private readonly List<MoveFolk> _mFolks = new List<MoveFolk>(32);
        private readonly List<PlaceFloor> _mFloors = new List<PlaceFloor>(8);
        private readonly RoleMovePlan _mPlan = new RoleMovePlan();

        /// <summary>True while managers act: the option is on and the people part runs.</summary>
        internal bool ManagersOn => _peopleOn && _talent != null && Settings.TechManager;

        /// <summary>The named manager's post at a firm; null with none, or with the option off.</summary>
        internal ManagerPost ManagerOf(string place) => ManagersOn && !string.IsNullOrEmpty(place) ? _talent.ManagerAt(place) : null;

        /// <summary>The post a person holds, or null.</summary>
        internal ManagerPost PostOf(string id)
        {
            if (!ManagersOn)
            {
                return null;
            }
            for (int i = 0; i < _talent.Managers.Count; i++)
            {
                if (_talent.Managers[i].Id == id)
                {
                    return _talent.Managers[i];
                }
            }
            return null;
        }

        private bool IsManager(Profile p)
        {
            if (!ManagersOn)
            {
                return false;
            }
            ManagerPost post = _talent.ManagerAt(p.Place);
            return post != null && post.Id == p.Id;
        }

        /// <summary>The name of the firm's manager while they can act: in the firm, not on notice and not off sick; null for none.</summary>
        internal string ManagerNameAt(string place)
        {
            ManagerPost post = ManagerOf(place);
            Person person = post != null ? PersonOf(post.Id) : null;
            return person != null && person.File.NoticeEnd == 0 && !person.File.Sick && person.File.Place == place ? person.Name : null;
        }

        /// <summary>A manager's skill as the duties use it (FirmManagerRules.Skill), and their span, for a page.</summary>
        internal int ManagerSkill(ManagerPost post, out int span)
        {
            Person person = post != null ? PersonOf(post.Id) : null;
            span = 0;
            if (person == null)
            {
                return 0;
            }
            int skill = FirmManagerRules.Skill(person.File, person.MainPoints);
            span = FirmManagerRules.Span(skill, person.File.Role);
            return skill;
        }

        private int PeopleAt(string place)
        {
            int n = 0;
            for (int i = 0; i < _people.Count; i++)
            {
                n += _people[i].File.Place == place && _people[i].File.NoticeEnd == 0 ? 1 : 0;
            }
            return n;
        }

        /// <summary>The text of why somebody cannot be named manager, or null when they can.</summary>
        internal string ManagerRefusal(string id)
        {
            Person person = PersonOf(id);
            if (!ManagersOn)
            {
                return "reallife_tech_mgr_off";
            }
            if (person == null || string.IsNullOrEmpty(person.File.Place) || person.At == null)
            {
                return "reallife_tech_mgr_no_firm";
            }
            if (person.File.NoticeEnd > 0)
            {
                return "reallife_tech_mgr_leaving";
            }
            ManagerPost post = _talent.ManagerAt(person.File.Place);
            if (post != null && post.Id == id)
            {
                return "reallife_tech_mgr_already";
            }
            return PeopleAt(person.File.Place) < 2 ? "reallife_tech_mgr_alone" : null;
        }

        /// <summary>What naming somebody manager would do, read now; nothing is changed. False where they cannot be named.</summary>
        internal bool PlanManager(string id, out ManagerPlan plan)
        {
            plan = default;
            Person person = PersonOf(id);
            if (person == null || Save == null || ManagerRefusal(id) != null)
            {
                return false;
            }
            Profile p = person.File;
            plan.Name = person.Name;
            plan.Firm = person.Business;
            ManagerPost old = _talent.ManagerAt(p.Place);
            Person was = old != null ? PersonOf(old.Id) : null;
            plan.Replaces = was != null ? was.Name : old != null ? old.Name : string.Empty;
            plan.Skill = FirmManagerRules.Skill(p, person.MainPoints);
            plan.Span = FirmManagerRules.Span(plan.Skill, p.Role);
            plan.PaidBefore = PaidNow(person);
            plan.Rise = TalentRules.CanRise(p, Save.Day);
            plan.PaidAfter = plan.Rise ? PaidAfter(person, FirmManagerCatalogue.RiseBp) : plan.PaidBefore;
            return true;
        }

        /// <summary>Names somebody the firm's manager, relieving the one before: the pay rise that comes with it (when they were not raised lately), the duties as they were set, and one notice.</summary>
        internal bool NameManager(string id)
        {
            if (!PlanManager(id, out ManagerPlan plan))
            {
                return false;
            }
            Person person = PersonOf(id);
            Profile p = person.File;
            int day = Save.Day;
            ManagerPost post = _talent.ManagerAt(p.Place);
            if (post == null)
            {
                post = new ManagerPost { Place = p.Place };
                _talent.Managers.Add(post);
            }
            else
            {
                Person was = PersonOf(post.Id);
                if (was != null)
                {
                    was.File.Managing = false;
                }
                post.ClearWeek();
                post.Vacancies.Clear();
            }
            post.Id = p.Id;
            post.Name = person.Name;
            post.Since = day;
            post.LastDay = -1;
            post.ReportDay = day;
            post.Last = string.Empty;
            p.Managing = true;
            if (plan.Rise)
            {
                Raise(person, FirmManagerCatalogue.RiseBp, day);
                TalentRules.Raised(p, day, FirmManagerCatalogue.RiseBp);
            }
            Tell(NotificationType.Success, plan.Rise ? "reallife_tech_mgr_named" : "reallife_tech_mgr_named_rested",
                Data("name", person.Name, "firm", person.Business, "percent", TechText.Bp(FirmManagerCatalogue.RiseBp), "skill", plan.Skill.ToString(CultureInfo.InvariantCulture),
                    "span", plan.Span.ToString(CultureInfo.InvariantCulture)), TabTeam, Row(p));
            Logger?.Info("Tech: " + person.Name + " named the manager of " + person.Business + " on day " + day + ": skill " + plan.Skill + ", " + plan.Span + " role move(s) a day, "
                         + (plan.Rise ? "paid " + plan.PaidBefore + " then " + plan.PaidAfter + " (hundredths an hour)" : "no rise, raised lately") + ".");
            Changed();
            return true;
        }

        /// <summary>The post ended: the person keeps their role and their pay, their own work is back to full, and the player is told.</summary>
        internal bool RemoveManager(string place)
        {
            ManagerPost post = ManagerOf(place);
            if (post == null)
            {
                return false;
            }
            Person person = PersonOf(post.Id);
            if (person != null)
            {
                person.File.Managing = false;
            }
            _talent.Managers.Remove(post);
            string name = person != null ? person.Name : post.Name;
            Tell(NotificationType.Info, "reallife_tech_mgr_removed", Data("name", name, "firm", person != null ? person.Business : string.Empty), TabTeam, person != null ? Row(person.File) : null);
            Changed();
            return true;
        }

        /// <summary>A role target set for a firm's manager (a count of 0 takes it away). False where the post keeps as many as it can.</summary>
        internal bool SetManagerTarget(string place, int role, int count)
        {
            ManagerPost post = ManagerOf(place);
            if (post == null || !post.SetTarget(role, count))
            {
                return false;
            }
            Changed();
            return true;
        }

        internal void ClearManagerTargets(string place)
        {
            ManagerPost post = ManagerOf(place);
            if (post != null && post.Targets.Count > 0)
            {
                post.Targets.Clear();
                Changed();
            }
        }

        internal void SetManagerDuty(string place, int duty, bool on)
        {
            ManagerPost post = ManagerOf(place);
            if (post != null && post.Has(duty) != on)
            {
                post.Duties = on ? post.Duties | duty : post.Duties & ~duty;
                Changed();
            }
        }

        // The balancing's raise handling in the people's own review: whether the manager will answer this person's request this morning, so the player is not asked as well.
        private bool ManagerHandles(Profile p)
        {
            ManagerPost post = ManagerOf(p.Place);
            if (post == null || !post.Has(FirmManagerCatalogue.DutyRaises) || post.Id == p.Id || ManagerNameAt(p.Place) == null)
            {
                return false;
            }
            int limit = RivalMatchRules.Limit(Settings.EconomyCompanyAgreeUpTo, Economy.CompanyCatalogue.AgreeUpToMost) * 100;
            return p.RaiseAsk > 0 && p.RaiseAsk <= FirmManagerRules.Ceiling(limit) && Save != null && TalentRules.CanRise(p, Save.Day);
        }

        // Somebody left (resigned, was poached or burnt out): the manager means to fill the place. A lay-off or dismissal is the player's own decision and is not filled.
        private void ManagerLeft(Person person, LeaveKind why, int day)
        {
            ManagerPost post = ManagerOf(person.File.Place);
            if (post == null || post.Id == person.File.Id || !post.Has(FirmManagerCatalogue.DutyHire) || why == LeaveKind.LaidOff || why == LeaveKind.Dismissed)
            {
                return;
            }
            FirmManagerRules.Left(post, person.File.Role, person.File.Level, day);
        }

        // ---------------------------------------------------------------- the morning

        private static readonly int HotDayManager = HotSpots.Slot("Tech: day, firms' managers");

        /// <summary>
        /// The firms' managers' morning, after the people's own day: each manager at work runs their firm once (their raises, their eye on the staff, their role balancing and
        /// their hiring), and the week's tally is told every <see cref="FirmManagerCatalogue.ReportDays"/> days. A post whose manager has gone is closed with a notice.
        /// </summary>
        private void ManagerDay(int day)
        {
            if (!ManagersOn || _talent.Managers.Count == 0)
            {
                return;
            }
            using var hotSpot = HotSpots.Time(HotDayManager);
            float started = UnityEngine.Time.realtimeSinceStartup;
            int runs = 0;
            for (int i = _talent.Managers.Count - 1; i >= 0; i--)
            {
                ManagerPost post = _talent.Managers[i];
                Person boss = PersonOf(post.Id);
                if (boss == null || boss.File.Place != post.Place)
                {
                    _talent.Managers.RemoveAt(i);
                    Tell(NotificationType.Warning, "reallife_tech_mgr_gone", Data("name", string.IsNullOrEmpty(post.Name) ? TechText.T("reallife_tech_mgr_someone") : post.Name,
                        "firm", FirmNameAt(post.Place)), TabTeam, null);
                    continue;
                }
                post.Name = boss.Name;
                boss.File.Managing = true;
                if (post.LastDay >= day || boss.File.NoticeEnd > 0 || boss.File.Sick)
                {
                    continue;   // once a day; and not while they work out a notice or are off sick: the firm runs itself
                }
                post.LastDay = day;
                runs++;
                int skill = FirmManagerRules.Skill(boss.File, boss.MainPoints), span = FirmManagerRules.Span(skill, boss.File.Role);
                post.Last = string.Empty;
                if (post.Has(FirmManagerCatalogue.DutyRaises))
                {
                    ManagerRaises(post, boss, skill, day);
                }
                if (post.Has(FirmManagerCatalogue.DutyWatch))
                {
                    ManagerWatch(post, boss, skill);
                }
                int moved = post.Has(FirmManagerCatalogue.DutyBalance) ? ManagerBalance(post, boss, span, day) : 0;
                if (post.Has(FirmManagerCatalogue.DutyHire) && _hiringOn)
                {
                    ManagerHire(post, boss, skill, day);
                }
                ManagerReport(post, boss, day);
                if (Verbose)
                {
                    Logger?.Info("Tech check (manager, " + boss.Business + ", day " + day + "): skill " + skill + ", " + span + " role move(s) a day, " + moved + " moved, " + post.WRaises + " raise(s) and "
                                 + post.WAds + " ad(s) and " + post.WOffers + " offer(s) this week, " + post.Worn.Count + " wearing thin and " + post.Idle.Count + " idle named: "
                                 + (moved <= span ? "PASS" : "FAIL"));
                }
            }
            if (runs > 0)
            {
                Changed();
            }
            if (Verbose && runs > 0)
            {
                Logger?.Info("Tech check (managers' morning, day " + day + "): " + runs + " firm(s) run in " + ((UnityEngine.Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms: PASS");
            }
        }

        private string FirmNameAt(string place)
        {
            for (int i = 0; i < _people.Count; i++)
            {
                if (_people[i].File.Place == place)
                {
                    return _people[i].Business;
                }
            }
            return string.Empty;
        }

        // Routine raises at review time, and a small rival offer matched: by the player's own limit where it is higher than a routine raise (FirmManagerRules.Ceiling).
        private void ManagerRaises(ManagerPost post, Person boss, int skill, int day)
        {
            int limit = RivalMatchRules.Limit(Settings.EconomyCompanyAgreeUpTo, Economy.CompanyCatalogue.AgreeUpToMost) * 100;
            int ceiling = FirmManagerRules.Ceiling(limit);
            for (int i = 0; i < _people.Count; i++)
            {
                Person person = _people[i];
                Profile p = person.File;
                if (p.Place != post.Place || p.Id == post.Id || p.NoticeEnd > 0)
                {
                    continue;
                }
                if (p.RaiseAsk > 0)
                {
                    int bp = FirmManagerRules.Raise(skill, p.RaiseAsk, ceiling, !TalentRules.CanRise(p, day));
                    if (bp > 0)
                    {
                        Raise(person, bp, day);
                        TalentRules.Raised(p, day, bp);
                        post.WRaises++;
                    }
                }
                else if (TalentRules.OfferOpen(p, day) && FirmManagerRules.Match(p.PoachBp, ceiling) && !(person.Game != null && TalentRules.GivesWay(p, TalentGame.GameOffer(person.Game))))
                {
                    Raise(person, p.PoachBp, day);
                    TalentRules.Answer(p, true, day, Year);
                    post.WRaises++;
                }
            }
        }

        // Who is wearing thin (by the manager's own eye) and who has had no work for days: counted, their names kept for the page; the week keeps the most on any one day.
        private void ManagerWatch(ManagerPost post, Person boss, int skill)
        {
            int worn = 0, idle = 0;
            post.Worn.Clear();
            post.Idle.Clear();
            for (int i = 0; i < _people.Count; i++)
            {
                Person person = _people[i];
                Profile p = person.File;
                if (p.Place != post.Place || p.Id == post.Id || p.NoticeEnd > 0)
                {
                    continue;
                }
                if (FirmManagerRules.WearingThin(p.Burnout, skill))
                {
                    worn++;
                    if (post.Worn.Count < 3)
                    {
                        post.Worn.Add(person.Name);
                    }
                }
                _idleRuns.TryGetValue(p.Id, out int runs);
                runs = person.HoursYesterday <= 0 && !p.Sick && !p.OnCourse(Save.Day) ? runs + 1 : 0;
                _idleRuns[p.Id] = runs;
                if (FirmManagerRules.Idle(runs))
                {
                    idle++;
                    if (post.Idle.Count < 3)
                    {
                        post.Idle.Add(person.Name);
                    }
                }
            }
            if (_idleRuns.Count > 4096)
            {
                _idleRuns.Clear();   // people who have gone: a reading is let go, the runs begin again
            }
            post.WWorn = Math.Max(post.WWorn, worn);
            post.WIdle = Math.Max(post.WIdle, idle);
        }

        // The firm's roles brought to the post's targets: a few moves a day by the manager's span, the best fit first, from roles that have a surplus, by the bulk change's own
        // rules (it refuses what would break a product being built, a contract or the cloud). Nobody the player put in their role by hand is moved.
        private int ManagerBalance(ManagerPost post, Person boss, int span, int day)
        {
            if (post.Targets.Count == 0)
            {
                return 0;
            }
            int moved = 0;
            for (int turn = 0; turn < span; turn++)
            {
                CityFloor city = ReadFloors(_mFloors);   // read again after each move: the one before changed what the firm has
                CountRoles(post.Place);
                if (FirmManagerRules.Short(post, _have, _short) == 0)
                {
                    break;
                }
                bool any = false;
                for (int s = 0; s < _short.Count && !any; s++)
                {
                    int to = _short[s].Role;
                    _mFolks.Clear();
                    for (int i = 0; i < _people.Count; i++)
                    {
                        Person person = _people[i];
                        Profile p = person.File;
                        if (p.Place != post.Place || p.Id == post.Id || p.Role == to || !FirmManagerRules.Spare(post, p.Role, _have))
                        {
                            continue;
                        }
                        MoveFolk folk = FolkOf(person, to, day);
                        folk.Away |= p.Pinned;   // put in their role by hand: left there
                        _mFolks.Add(folk);
                    }
                    RoleMoveRules.Plan(_mFolks, to, 1, _mFloors, city, _mPlan);
                    if (_mPlan.Moved == 0)
                    {
                        if (_mPlan.Refused > 0 && post.Last.Length == 0)
                        {
                            int why = _mPlan.Why[RoleMovePlan.WhyProduct] > 0 ? RoleMovePlan.WhyProduct : _mPlan.Why[RoleMovePlan.WhyContract] > 0 ? RoleMovePlan.WhyContract
                                : _mPlan.Why[RoleMovePlan.WhyBranding] > 0 ? RoleMovePlan.WhyBranding : _mPlan.Why[RoleMovePlan.WhyCloud] > 0 ? RoleMovePlan.WhyCloud : RoleMovePlan.WhyHyper;
                            post.Last = TechText.Fill("reallife_tech_mgr_held", "role", TechText.Role(to), "why", TechText.Fill(RoleMoveRules.WhyKey(why), "firm", _mPlan.WhyPlace[why] ?? string.Empty));
                        }
                        continue;
                    }
                    Person who = PersonOf(_mPlan.Ids[0]);
                    if (who == null)
                    {
                        continue;
                    }
                    int was = who.File.Role;
                    Reassign(who, to, false);
                    _assigned[who.File.Id] = new Assigned { Day = day, Why = AssignRules.WhyBalance, By = boss.Name };
                    post.WMoves++;
                    post.Last = TechText.Fill("reallife_tech_mgr_moved", "name", who.Name, "from", TechText.Role(was), "to", TechText.Role(to));
                    moved++;
                    any = true;
                }
                if (!any)
                {
                    break;
                }
            }
            return moved;
        }

        private void CountRoles(string place)
        {
            Array.Clear(_have, 0, _have.Length);
            for (int i = 0; i < _people.Count; i++)
            {
                Profile p = _people[i].File;
                if (p.Place == place && p.NoticeEnd == 0)
                {
                    _have[p.Role]++;
                }
            }
        }

        // Replacing people who left: an ad for the role and level they held, the best applicant by the manager's eye offered what they ask (within the going rate's ceiling), a counter taken
        // when it is within that ceiling too. The ad is paid by the firm as the player's own would be; a firm without the cash is tried again after RetryDays.
        private void ManagerHire(ManagerPost post, Person boss, int skill, int day)
        {
            for (int i = post.Vacancies.Count - 1; i >= 0; i--)
            {
                if (!FirmManagerRules.Fresh(post.Vacancies[i], day))
                {
                    post.Vacancies.RemoveAt(i);
                }
            }
            for (int i = post.Ads.Count - 1; i >= 0; i--)
            {
                JobAd ad = _hiring.AdOf(post.Ads[i]);
                if (ad == null || (day >= ad.To && !AnyLive(ad.Id)))
                {
                    post.Ads.RemoveAt(i);
                }
            }
            while (post.Vacancies.Count > 0 && post.Ads.Count < FirmManagerCatalogue.AdsMost && day >= post.RetryDay)
            {
                Vacancy v = post.Vacancies[0];
                RoleDef r = TalentCatalogue.Role(v.Role);
                int most = GoingFor(r.Id, v.Level), least = most * 85 / 100;
                int level = TalentCatalogue.Clamp(v.Level, r.LeastLevel, r.MostLevel);
                int before = _hiring.Ads.Count;
                bool posted = PostAd(post.Place, r.Id, level, least, most, TalentCatalogue.LevelSkillLeast[level], r.Remote ? (int)Arrangement.Hybrid : (int)Arrangement.OnSite,
                    HiringCatalogue.AdDays[Math.Min(1, HiringCatalogue.AdDays.Length - 1)], false);
                if (!posted || _hiring.Ads.Count <= before)
                {
                    post.RetryDay = day + FirmManagerCatalogue.RetryDays;
                    break;
                }
                post.Ads.Add(_hiring.Ads[_hiring.Ads.Count - 1].Id);
                post.Vacancies.RemoveAt(0);
                post.WAds++;
                post.Last = TechText.Fill("reallife_tech_mgr_posted", "role", TechText.Role(r.Id), "level", TechText.Level(level));
            }
            for (int i = 0; i < post.Ads.Count; i++)
            {
                JobAd ad = _hiring.AdOf(post.Ads[i]);
                if (ad != null)
                {
                    ManagerOffer(post, boss, ad, skill, day);
                }
            }
        }

        private void ManagerOffer(ManagerPost post, Person boss, JobAd ad, int skill, int day)
        {
            int going = GoingFor(ad.Role, ad.Level);
            _mApps.Clear();
            _mApplicants.Clear();
            bool waiting = false;
            for (int k = 0; k < _hiring.Applicants.Count; k++)
            {
                Applicant a = _hiring.Applicants[k];
                if (a.Ad != ad.Id || !a.Live)
                {
                    continue;
                }
                Seeker s = _hiring.SeekerOf(a.Seeker);
                if (s == null || s.Taken)
                {
                    continue;
                }
                if (a.Stage == (int)ApplyStage.Countered)
                {
                    if (FirmManagerRules.TakeCounter(a.Counter, going))
                    {
                        TakeCounter(a.Id);
                        post.WOffers++;
                    }
                    else
                    {
                        WalkAway(a.Id);
                    }
                    waiting = true;
                }
                else if (a.Stage >= (int)ApplyStage.Offered)
                {
                    waiting = true;   // an offer is out or accepted: the one post is theirs
                }
                else
                {
                    _mApps.Add(a);
                    _mApplicants.Add(new ManagerApplicant { Id = a.Id, Claimed = s.CvMain, True = s.P.Main / 1000, Ask = s.Ask, Going = going });
                }
            }
            if (waiting || _mApps.Count == 0)
            {
                return;
            }
            int pick = FirmManagerRules.Pick(skill, _mApplicants);
            if (pick < 0)
            {
                return;
            }
            Applicant chosen = _mApps[pick];
            Seeker seeker = _hiring.SeekerOf(chosen.Seeker);
            if (seeker != null && MakeOffer(chosen.Id, seeker.Ask))
            {
                post.WOffers++;
                post.Last = TechText.Fill("reallife_tech_mgr_offered", "name", seeker.P.Name, "role", TechText.Role(ad.Role), "amount", TechText.Rate(seeker.Ask));
            }
        }

        // The week's tally told in one notice; a quiet week says nothing.
        private void ManagerReport(ManagerPost post, Person boss, int day)
        {
            if (day - post.ReportDay < FirmManagerCatalogue.ReportDays)
            {
                return;
            }
            if (!post.QuietWeek)
            {
                Tell(NotificationType.Info, "reallife_tech_mgr_week", Data("name", boss.Name, "firm", boss.Business, "moves", post.WMoves.ToString(CultureInfo.InvariantCulture),
                    "raises", post.WRaises.ToString(CultureInfo.InvariantCulture), "ads", post.WAds.ToString(CultureInfo.InvariantCulture), "offers", post.WOffers.ToString(CultureInfo.InvariantCulture),
                    "worn", post.WWorn.ToString(CultureInfo.InvariantCulture), "idle", post.WIdle.ToString(CultureInfo.InvariantCulture)), TabTeam, Row(boss.File));
            }
            post.ClearWeek();
            post.ReportDay = day;
        }

        /// <summary>A firm's page with a line for its manager among the facts (who and what they did this week), or how to name one when it has people and none.</summary>
        private FirmPage WithManager(FirmPage page, FirmPlace firm)
        {
            if (page == null || firm == null || !ManagersOn)
            {
                return page;
            }
            string line = ManagerSummary(firm.File.Place);
            if (line.Length > 0)
            {
                StudioViews.Fact(page, "reallife_tech_mgr_fact", line);
            }
            else if (PeopleAt(firm.File.Place) >= 2)
            {
                StudioViews.Fact(page, "reallife_tech_mgr_fact", TechText.T("reallife_tech_mgr_fact_none"));
            }
            return page;
        }

        /// <summary>A manager's page line: who, their skill and what they did last, or the week so far. Empty with no manager at the firm.</summary>
        internal string ManagerSummary(string place)
        {
            ManagerPost post = ManagerOf(place);
            if (post == null)
            {
                return string.Empty;
            }
            int skill = ManagerSkill(post, out int span);
            string week = TechText.Fill("reallife_tech_mgr_tally", "moves", post.WMoves.ToString(CultureInfo.InvariantCulture), "raises", post.WRaises.ToString(CultureInfo.InvariantCulture),
                "ads", post.WAds.ToString(CultureInfo.InvariantCulture), "offers", post.WOffers.ToString(CultureInfo.InvariantCulture));
            return TechText.Fill("reallife_tech_mgr_line", "name", post.Name, "skill", skill.ToString(CultureInfo.InvariantCulture), "span", span.ToString(CultureInfo.InvariantCulture), "week", week);
        }
    }
}
