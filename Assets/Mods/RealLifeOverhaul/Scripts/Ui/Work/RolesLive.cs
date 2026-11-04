using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Open Roles on the kit: the player's ads, the applicants of each with their skills, asks, past jobs and any rival's named offer with its
    /// deadline, the market of seekers, the search firm and the team. Read from the hiring part of the tech sector; every button goes to the
    /// director that owns it (TechDirector: Shortlist, MakeOffer, TakeCounter, WalkAway, CloseAd). Nothing a frame.
    /// </summary>
    public sealed class RolesLive : IWorkSource
    {
        public static readonly RolesLive Instance = new RolesLive();

        private const int Shortlist = 100, Offer = 101, Counter = 102, Walk = 103, Close = 104;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private int _local, _wanted = -1, _readGeneration = -1;
        private WorkApp _app = new WorkApp();

        private RolesLive() { }

        public string Id => "open_roles";
        public string Title => W.T("reallife_roles_app", "Open Roles");
        public UiIcon Icon => UiIcon.People;
        public bool Across => true;
        public string T(string key, string english) => UiText.Words(key, english);
        private static UtilitiesWords W => CareerLive.Words();

        public int Generation
        {
            get
            {
                GameInstance save = SaveGameManager.Current;
                return DaySeriesDirector.Generation + _local + (save != null ? save.Day * 24 + save.Hour : 0);
            }
        }

        public static void Want(int tab)
        {
            Instance._wanted = tab;
            Instance._local++;
        }

        public int TakeWanted()
        {
            int tab = _wanted;
            _wanted = -1;
            return tab;
        }

        public UiNavEntry[] Nav(WorkView view)
        {
            Read();
            return _app.Nav;
        }

        public WorkPage Page(WorkView view)
        {
            Read();
            return WorkTabPages.Build(_app, view, W);
        }

        private void Read()
        {
            int generation = Generation;
            if (_readGeneration == generation)
            {
                return;
            }
            _readGeneration = generation;
            TechDirector d = TechDirector.Instance;
            HiringFile h = d != null ? d.Hiring : null;
            GameInstance save = SaveGameManager.Current;
            UtilitiesWords w = W;
            _app = new WorkApp { Ready = h != null && save != null, OffBody = w.T("reallife_work_roles_off",
                "Load a save with the tech sector switched on to hire for your firms.") };
            int applicants = 0;
            for (int i = 0; h != null && i < h.Applicants.Count; i++)
            {
                applicants += h.Applicants[i].Live ? 1 : 0;
            }
            _app.Nav = new[]
            {
                new UiNavEntry { Icon = UiIcon.ClipboardText, Label = w.T("reallife_roles_tab_jobs", "Jobs"), Count = h != null
                    && h.Ads.Count > 0 ? h.Ads.Count.ToString(Inv) : null },
                new UiNavEntry { Icon = UiIcon.People, Label = w.T("reallife_roles_tab_applicants", "Applicants"),
                    Count = applicants > 0 ? applicants.ToString(Inv) : null },
                new UiNavEntry { Icon = UiIcon.Globe, Label = w.T("reallife_roles_tab_market", "Market") },
                new UiNavEntry { Icon = UiIcon.Search, Label = w.T("reallife_roles_tab_search", "Search") },
                new UiNavEntry { Icon = UiIcon.Person, Label = w.T("reallife_roles_tab_team", "Team"), Count = d != null
                    && d.People.Count > 0 ? d.People.Count.ToString(Inv) : null }
            };
            if (!_app.Ready)
            {
                return;
            }
            int day = save.Day;
            _app.Tabs = new[] { Jobs(h, day), Applicants(d, h, day), Market(h), Search(h), Team(d) };
        }

        private static UiColumn Col(string title, bool grow = false, bool right = false) => new UiColumn { Title = title, Grow = grow,
            Width = grow ? 0f : 184f, Right = right };

        private WorkTab Jobs(HiringFile h, int day)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { TableTitle = w.T("reallife_roles_tab_jobs", "Jobs"), Lead = w.T("reallife_work_no_ads",
                "No open roles. Post an ad in the game's own Open Roles app and applicants arrive over a few days."),
                Columns = new[] { Col(w.T("reallife_work_col_role", "Role"), true), Col(w.T("reallife_work_col_posts", "Posts to fill")),
                    Col(w.T("reallife_work_col_applicants", "Applicants")), Col(w.T("reallife_work_col_pay_up_to", "Up to"), false, true) } };
            for (int i = 0; i < h.Ads.Count; i++)
            {
                JobAd ad = h.Ads[i];
                if (ad.Search)
                {
                    continue;
                }
                int came = 0;
                for (int k = 0; k < h.Applicants.Count; k++)
                {
                    came += h.Applicants[k].Ad == ad.Id && h.Applicants[k].Live ? 1 : 0;
                }
                int left = HireOffers.ToFill(h, ad);
                var r = new WorkRow { Id = ad.Id, Icon = UiIcon.ClipboardText,
                    Cells = new[] { TechText.Role(ad.Role) + ", " + TechText.Level(ad.Level),
                    left.ToString(Inv) + " / " + HireOffers.Posts(ad).ToString(Inv), came.ToString(Inv), TechText.Rate(ad.PayMost) },
                    Status = ad.Open(day) ? w.T("reallife_work_ad_open", "Open") : w.T("reallife_work_ad_closed", "Closed"),
                        Tone = ad.Open(day) ? UiRole.Ok : UiRole.Warn,
                    Detail = w.F("reallife_work_ad_line", "{role}, up to {pay} an hour, {n} posts to fill.", "role", TechText.Role(ad.Role), "pay",
                        TechText.Rate(ad.PayMost), "n", left.ToString(Inv)) };
                r.Acts.Add(new WorkAct { Kind = Close, Label = w.T("reallife_work_close_ad", "Close the ad"), Style = UiButtonKind.Destructive,
                    Icon = UiIcon.Close });
                t.Rows.Add(r);
            }
            for (int i = 0; i < h.Applicants.Count && t.People.Count < 6; i++)
            {
                Applicant a = h.Applicants[i];
                Seeker hired = h.SeekerOf(a.Seeker);
                if (a.Stage == (int)ApplyStage.Started && a.Start >= day - 30 && hired != null)
                {
                    t.People.Add(new WorkPerson { Name = hired.P.Name, Role = TechText.Role(hired.P.Role) + ", " + TechText.Day(a.Start), Icon = UiIcon.Person });
                }
            }
            t.PeopleTitle = w.T("reallife_work_hired_month", "Hired this month");
            return t;
        }

        private WorkTab Applicants(TechDirector d, HiringFile h, int day)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { TableTitle = w.T("reallife_roles_tab_applicants", "Applicants"), Lead = w.T("reallife_work_no_applicants",
                "No applicants yet. They arrive over a few days, and other firms hire too."), GradeColumn = 2,
                Columns = new[] { Col(w.T("reallife_work_col_applicant", "Applicant"), true), Col(w.T("reallife_work_col_from", "From")),
                    Col(w.T("reallife_work_col_skill", "Skill")), Col(w.T("reallife_work_col_asks", "Asks")), Col(w.T("reallife_work_col_stage",
                    "Stage"), false, true) } };
            for (int i = 0; i < h.Applicants.Count; i++)
            {
                Applicant a = h.Applicants[i];
                Seeker s = h.SeekerOf(a.Seeker);
                JobAd ad = h.AdOf(a.Ad);
                if (s == null || !a.Live || ad == null)
                {
                    continue;
                }
                bool competing = HireOffers.Competing(a, day);
                var r = new WorkRow { Id = a.Id, Icon = UiIcon.Person, Grade = s.CvMain,
                    Cells = new[] { s.P.Name, string.IsNullOrEmpty(s.Employer) ? w.T("reallife_work_self_taught", "Self taught") : s.Employer,
                        s.CvMain.ToString(Inv), TechText.Rate(s.Ask), StageName(a) },
                    Status = competing ? w.T("reallife_work_competing_offer", "Competing offer") : StageName(a), Tone = competing ? UiRole.Warn : UiRole.Ok };
                var sb = new System.Text.StringBuilder();
                sb.Append(s.P.Name).Append(", ").Append(TechText.Role(s.P.Role)).Append(", ").Append(TechText.Rate(s.Ask)).Append('.');
                if (competing)
                {
                    sb.Append(' ').Append(w.F("reallife_work_rival_offer",
                        "{rival} offered {pay} an hour; they answer by day {day}. Offer as much to hire them.", "rival", a.RivalBy, "pay",
                        TechText.Rate(a.RivalBid), "day", a.RivalLapse.ToString(Inv)));
                }
                var past = new List<PastJob>(HireOffers.HistoryMost);
                if (HireOffers.Read(HireOffers.HistoryOf(d.TalentSeed, s), past) > 0)
                {
                    sb.Append(' ').Append(w.T("reallife_work_so_far", "Work so far:") + " ");
                    for (int k = 0; k < past.Count; k++)
                    {
                        sb.Append(k > 0 ? "; " : string.Empty).Append(TechText.Role(s.P.Role)).Append(", ").Append(TechText.Level(past[k].Level)).Append(", ")
                            .Append(past[k].Firm).Append(", ").Append(past[k].Years.ToString(Inv)).Append(' ').Append(w.T("reallife_work_years", "years"));
                    }
                    sb.Append('.');
                }
                r.Detail = sb.ToString();
                int teamAverage = TeamAverage(d);
                r.NeedsTitle = w.T("reallife_work_fit_for_role", "Fit for the role");
                r.Needs.Add(new SplitNeed { Words = w.F("reallife_work_fit_craft", "Craft above your team's average of {n}", "n", teamAverage.ToString(Inv)), Met = s.CvMain > teamAverage });
                r.Needs.Add(new SplitNeed { Words = w.F("reallife_work_fit_pay", "Asks less than the {pay} you set", "pay", TechText.Rate(ad.PayMost)), Met = s.Ask <= ad.PayMost });
                r.Needs.Add(new SplitNeed { Words = w.T("reallife_work_fit_refs", "References checked"), Met = a.Refs });
                int cents = competing ? Math.Max(a.RivalBid, s.Ask) : s.Ask;
                r.Acts.Add(new WorkAct { Kind = Offer, Arg = a.Id, Label = w.F("reallife_work_hire_at", "Hire at {pay} an hour", "pay",
                    TechText.Rate(cents)), Style = UiButtonKind.Primary, Icon = UiIcon.Check,
                    Why = a.Stage >= (int)ApplyStage.Accepted ? w.T("reallife_work_already_hired", "They have already accepted.") : null });
                if (a.Stage == (int)ApplyStage.Countered)
                {
                    r.Acts.Add(new WorkAct { Kind = Counter, Arg = a.Id, Label = w.T("reallife_work_take_counter", "Take their counter"),
                        Style = UiButtonKind.Secondary, Icon = UiIcon.Check });
                }
                else
                {
                    r.Acts.Add(new WorkAct { Kind = Shortlist, Arg = a.Id, Label = w.T("reallife_hire_button_shortlist", "Shortlist"),
                        Style = UiButtonKind.Secondary, Icon = UiIcon.Star,
                        Why = a.Stage == (int)ApplyStage.Shortlisted ? w.T("reallife_work_already_shortlisted", "Already on your shortlist.") : null });
                }
                r.Acts.Add(new WorkAct { Kind = Walk, Arg = a.Id, Label = w.T("reallife_work_walk_away", "Let them go"),
                    Style = UiButtonKind.Destructive, Icon = UiIcon.Close });
                t.Rows.Add(r);
            }
            return t;
        }

        // The mean of the main skill of the player's own people, whole points; 0 with nobody on the payroll.
        private static int TeamAverage(TechDirector d)
        {
            int sum = 0;
            for (int i = 0; i < d.People.Count; i++)
            {
                sum += d.People[i].MainPoints;
            }
            return d.People.Count == 0 ? 0 : sum / d.People.Count;
        }

        private static string StageName(Applicant a) => TechText.T("reallife" + "_hire_stage_" + a.At.ToString().ToLowerInvariant());

        private WorkTab Market(HiringFile h)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { TableTitle = w.T("reallife_roles_tab_market", "Market"), Lead = w.T("reallife_work_market_lead",
                "Professionals looking for work now. Post an ad and the ones who fit apply."),
                Columns = new[] { Col(w.T("reallife_work_col_role", "Role"), true), Col(w.T("reallife_work_col_level", "Level")),
                    Col(w.T("reallife_work_col_looking", "Looking")), Col(w.T("reallife_work_col_going_rate", "Asks"), false, true) } };
            var seen = new Dictionary<int, int>();
            var rate = new Dictionary<int, long>();
            for (int i = 0; i < h.Seekers.Count; i++)
            {
                Seeker s = h.Seekers[i];
                if (s.Taken || s.Passive)
                {
                    continue;
                }
                int key = s.P.Role * 10 + s.P.Level;
                seen[key] = seen.TryGetValue(key, out int n) ? n + 1 : 1;
                rate[key] = (rate.TryGetValue(key, out long total) ? total : 0) + s.Ask;
            }
            foreach (KeyValuePair<int, int> pair in seen)
            {
                if (t.Rows.Count >= 24)
                {
                    break;
                }
                t.Rows.Add(new WorkRow { Id = pair.Key, Icon = UiIcon.Person, Cells = new[] { TechText.Role(pair.Key / 10),
                    TechText.Level(pair.Key % 10), pair.Value.ToString(Inv), TechText.Rate((int)(rate[pair.Key] / pair.Value)) },
                    Detail = TechText.Role(pair.Key / 10) + ", " + TechText.Level(pair.Key % 10) + ": " + pair.Value.ToString(Inv) + " looking." });
            }
            return t;
        }

        private WorkTab Search(HiringFile h)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { Lead = w.T("reallife_work_search_lead",
                "A search firm finds senior people, and a crew search finds technicians for a data centre. Brief one in the game's own Open Roles app; its fee is a share of a year's pay."),
                TableTitle = w.T("reallife_roles_tab_search", "Search"), Columns = new[] { Col(w.T("reallife_work_col_role", "Role"), true),
                    Col(w.T("reallife_work_col_fee", "Fee")), Col(w.T("reallife_work_col_due", "Delivers"), false, true) } };
            for (int i = 0; i < h.Ads.Count; i++)
            {
                JobAd ad = h.Ads[i];
                if (ad.Search)
                {
                    t.Rows.Add(new WorkRow { Id = ad.Id, Icon = UiIcon.Search,
                        Cells = new[] { TechText.Role(ad.Role) + (ad.CrewSearch ? " x" + ad.Crew.ToString(Inv) : string.Empty),
                        TechText.Money(ad.Fee / 100), TechText.Day(ad.Due) }, Detail = TechText.Role(ad.Role) });
                }
            }
            return t;
        }

        private WorkTab Team(TechDirector d)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { TableTitle = w.T("reallife_roles_tab_team", "Team"), Lead = w.T("reallife_work_no_team",
                "No professionals on your payroll yet."),
                Columns = new[] { Col(w.T("reallife_work_col_applicant", "Name"), true), Col(w.T("reallife_work_col_role", "Role")),
                    Col(w.T("reallife_work_col_skill", "Skill"), false, true) }, GradeColumn = 2 };
            for (int i = 0; i < d.People.Count && i < 60; i++)
            {
                Person p = d.People[i];
                t.Rows.Add(new WorkRow { Id = i, Icon = UiIcon.Person, Grade = Math.Min(100, p.MainPoints), Cells = new[] { p.Name,
                    TechText.Role(p.File.Role) + ", " + TechText.Level(p.File.Level), p.MainPoints.ToString(Inv) }, Detail = p.Name + ", " + p.Business });
            }
            return t;
        }

        // ------------------------------------------------------------------------------------------------- doing

        public bool Do(WorkDo act, WorkView view)
        {
            TechDirector d = TechDirector.Instance;
            HiringFile h = d != null ? d.Hiring : null;
            if (h == null)
            {
                return false;
            }
            try
            {
                Applicant a = h.ApplicantOf(act.Arg);
                switch (act.Kind)
                {
                    case Shortlist: d.Shortlist(act.Arg, true); break;
                    case Offer:
                        Seeker s = a != null ? h.SeekerOf(a.Seeker) : null;
                        if (a == null || s == null)
                        {
                            return false;
                        }
                        d.MakeOffer(act.Arg, HireOffers.Competing(a, SaveGameManager.Current.Day) ? Math.Max(a.RivalBid, s.Ask) : s.Ask);
                        break;
                    case Counter: d.TakeCounter(act.Arg); break;
                    case Walk: d.WalkAway(act.Arg); break;
                    case Close: d.CloseAd(act.Arg); break;
                    default: return false;
                }
                _local++;
                return true;
            }
            catch (Exception e)
            {
                TechDirector.Logger?.Warn("Open Roles (kit): " + act.Kind + " failed: " + e.Message);
                return false;
            }
        }
    }
}
