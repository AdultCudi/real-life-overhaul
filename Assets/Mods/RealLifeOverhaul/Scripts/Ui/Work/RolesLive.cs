using System;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Tech;
using TFamily = RealLifeOverhaul.Tech.Family;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Open Roles on the kit: the player's ads and a new ad (with its posts to fill), the applicants of each role as a squad page (faces, graded skill, asking
    /// pay, any rival's named offer with its deadline) and a person's page for each (File, Career, Contract), the market of seekers by line, the search firm and
    /// the data centre crew briefs, and the team with a page for each. Read from the hiring part of the tech sector; every button goes to the director that
    /// owns it. Nothing a frame.
    /// </summary>
    public sealed partial class RolesLive : IWorkSource
    {
        public static readonly RolesLive Instance = new RolesLive();

        // the actions of this app, from 100 (the screen's own are 1 to 3)
        private const int Shortlist = 100, Offer = 101, Counter = 102, Walk = 103, Close = 104, NewAd = 105, PostAd = 107, OpenApplicant = 110, OpenPerson = 111, Interview = 112,
            Refs = 113, Background = 114, Reject = 115, Buyout = 116, Brief = 120, BriefCrew = 121, Match = 130, LetThem = 131, GiveRise = 132, RefuseRise = 133, Promote = 134, SetRole = 141,
            SetWork = 142, Clause = 143, LayOff = 144, Dismiss = 145, CourseA = 146, CourseB = 147, Degree = 148, Exam = 149;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly int[] PayBands = { 900, 1000, 1100, 1200, 1350 };   // thousandths of the going rate an ad's top pay can be set at
        private int _local, _wanted = -1, _readGeneration = -1;
        private WorkApp _app = new WorkApp();
        private readonly List<BuildingRegistration> _places = new List<BuildingRegistration>(8);

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
            TechDirector d = TechDirector.Instance;
            HiringFile h = d != null ? d.Hiring : null;
            GameInstance save = SaveGameManager.Current;
            if (_app.Ready && h != null && save != null)
            {
                int day = save.Day;
                _app.Tabs[0] = view.Sub == 3 ? NewAdTab(d, h, day, view) : Jobs(d, h, day);
                _app.Tabs[1] = view.Sub == 1 ? ApplicantPage(d, h, day, view) : Applicants(d, h, day, view);
                _app.Tabs[3] = Search(d, h, day, view);
                _app.Tabs[4] = view.Sub == 2 ? PersonPage(d, day, view) : Team(d, day);
            }
            return WorkTabPages.Build(_app, view, W);
        }

        // ------------------------------------------------------------------------------------------------- reading

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
            _app = new WorkApp { Ready = h != null && save != null, OffTitle = w.T("reallife_work_roles_off_title", "Hiring is switched off"),
                OffBody = w.T("reallife_work_roles_off", "Load a save with the tech sector switched on to hire for your firms.") };
            int applicants = 0, ads = 0;
            for (int i = 0; h != null && i < h.Applicants.Count; i++)
            {
                applicants += h.Applicants[i].Live ? 1 : 0;
            }
            for (int i = 0; h != null && i < h.Ads.Count; i++)
            {
                ads += !h.Ads[i].Search ? 1 : 0;
            }
            _app.Nav = new[]
            {
                new UiNavEntry { Icon = UiIcon.ClipboardText, Label = w.T("reallife_roles_tab_jobs", "Jobs"), Count = ads > 0 ? ads.ToString(Inv) : null },
                new UiNavEntry { Icon = UiIcon.People, Label = w.T("reallife_roles_tab_applicants", "Applicants"), Count = applicants > 0 ? applicants.ToString(Inv) : null },
                new UiNavEntry { Icon = UiIcon.Globe, Label = w.T("reallife_roles_tab_market", "Market") },
                new UiNavEntry { Icon = UiIcon.Search, Label = w.T("reallife_roles_tab_search", "Search") },
                new UiNavEntry { Icon = UiIcon.Person, Label = w.T("reallife_roles_tab_team", "Team"), Count = d != null && d.People.Count > 0 ? d.People.Count.ToString(Inv) : null }
            };
            if (!_app.Ready)
            {
                return;
            }
            _app.Tabs = new[] { new WorkTab(), new WorkTab(), Market(d, h, save.Day), new WorkTab(), new WorkTab() };
        }

        private static UiIcon RoleGlyph(int role)
        {
            RoleDef r = TalentCatalogue.Role(role);
            switch (r.Family)
            {
                case TFamily.Engineering: return UiIcon.Code;
                case TFamily.Data: return UiIcon.Database;
                case TFamily.Security: return UiIcon.ShieldCheck;
                case TFamily.Infrastructure: return UiIcon.HardDrives;
                case TFamily.Product: return UiIcon.Layout;
                case TFamily.Design: return UiIcon.PaintBrush;
                case TFamily.Commercial: return UiIcon.Handshake;
                case TFamily.Support: return UiIcon.Headset;
                case TFamily.Corporate: return UiIcon.Building;
                case TFamily.Built: return UiIcon.Hammer;
                default: return UiIcon.Person;
            }
        }

        // ------------------------------------------------------------------------------------------------- Jobs

        private WorkTab Jobs(TechDirector d, HiringFile h, int day)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { Shape = WorkShape.Table };
            t.Columns = new[] { WorkBuilder.Col(w.T("reallife_work_col_role", "Role"), UiCellKind.Tile), WorkBuilder.Col(w.T("reallife_work_col_posts", "Posts to fill"), UiCellKind.Text, 112),
                WorkBuilder.Col(w.T("reallife_work_col_applicants", "Applicants"), UiCellKind.Figure, 96, true), WorkBuilder.Col(w.T("reallife_work_col_pay_up_to", "Up to"), UiCellKind.Money, 88),
                WorkBuilder.Col(w.T("reallife_work_col_state", "State"), UiCellKind.Chip, 136) };
            t.EmptyTitle = w.T("reallife_work_no_ads_title", "No role is open");
            t.EmptyBody = w.T("reallife_work_no_ads", "Post an ad and applicants arrive over a few days.");
            t.EmptyIcon = UiIcon.ClipboardText;
            t.Acts.Add(new WorkAct { Kind = NewAd, Label = w.T("reallife_work_new_ad", "New ad"), Style = UiButtonKind.Primary, Icon = UiIcon.Plus, Opens = true });
            var hired = new List<Applicant>(8);
            for (int i = 0; i < h.Applicants.Count; i++)
            {
                if (h.Applicants[i].Stage == (int)ApplyStage.Started && h.Applicants[i].Start >= day - 30)
                {
                    hired.Add(h.Applicants[i]);
                }
            }
            int openPosts = 0, waiting = 0;
            var payIcons = new List<UiIcon>(3);
            var payNames = new List<string>(3);
            var payHints = new List<string>(3);
            var payValues = new List<string>(3);
            var closeIcons = new List<UiIcon>(3);
            var closeNames = new List<string>(3);
            var closeHints = new List<string>(3);
            var closeValues = new List<string>(3);
            for (int i = 0; i < h.Ads.Count; i++)
            {
                JobAd ad = h.Ads[i];
                if (ad.Search)
                {
                    continue;
                }
                int came = 0, shortlisted = 0, offers = 0;
                for (int k = 0; k < h.Applicants.Count; k++)
                {
                    Applicant a = h.Applicants[k];
                    bool mine = a.Ad == ad.Id && a.Live;
                    came += mine ? 1 : 0;
                    shortlisted += mine && a.Stage == (int)ApplyStage.Shortlisted ? 1 : 0;
                    offers += mine && (a.Stage == (int)ApplyStage.Offered || a.Stage == (int)ApplyStage.Countered) ? 1 : 0;
                }
                int left = HireOffers.ToFill(h, ad), posts = HireOffers.Posts(ad);
                bool open = ad.Open(day);
                if (open)
                {
                    openPosts += left;
                    waiting += came;
                    int goingNow = d.GoingFor(ad.Role, ad.Level);
                    payIcons.Add(RoleGlyph(ad.Role));
                    payNames.Add(TechText.Role(ad.Role) + ", " + TechText.Level(ad.Level));
                    payHints.Add(w.F("reallife_work_going_n", "Going rate {rate}", "rate", TechText.Rate(goingNow)));
                    payValues.Add(TechText.Rate(ad.PayMost) + (goingNow > 0 ? " (" + WorkBuilder.Percent((ad.PayMost - goingNow) * 100 / goingNow) + ")" : string.Empty));
                    if (ad.To - day <= 7)
                    {
                        closeIcons.Add(RoleGlyph(ad.Role));
                        closeNames.Add(TechText.Role(ad.Role) + ", " + TechText.Level(ad.Level));
                        closeHints.Add(w.F("reallife_work_closes_on", "Closes {day}", "day", TechText.Day(ad.To)));
                        closeValues.Add(w.F("reallife_work_n_applicants", "{n} applicants", "n", came.ToString(Inv)));
                    }
                }
                string state = open ? (ad.To - day <= 3 ? w.F("reallife_work_ad_closes_in", "Closes in {n} days", "n", Math.Max(0, ad.To - day).ToString(Inv)) : w.T("reallife_work_ad_open", "Open"))
                    : w.T("reallife_work_ad_closed", "Closed");
                UiRole tone = open && ad.To - day > 3 ? UiRole.Ok : UiRole.Warn;
                var r = new WorkRow { Id = ad.Id, Icon = RoleGlyph(ad.Role), Name = TechText.Role(ad.Role) + ", " + TechText.Level(ad.Level),
                    Sub = TalentGame.NameOf(TalentGame.Find(ad.Place)), Group = ad.Id,
                    Cells = new[] { null, left.ToString(Inv) + " / " + posts.ToString(Inv), came.ToString(Inv), TechText.Rate(ad.PayMost), state },
                        Values = new[] { 0, 0, Math.Min(100, came * 8), 0, 0 },
                    Status = state, Tone = tone, Chip = state, ChipTone = tone, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, tone } };
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_pay_up_to", "Pay up to"), w.F("reallife_career_row_rate", "{rate} an hour", "rate", TechText.Rate(ad.PayMost))));
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_posts", "Posts to fill"), left.ToString(Inv) + " / " + posts.ToString(Inv)));
                var facts = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_the_ad", "The ad") };
                facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_ad_skill", "Minimum skill"), ad.MinSkill > 0 ? ad.MinSkill.ToString(Inv) : w.T("reallife_talent_none", "none")));
                facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_ad_arrangement", "Arrangement"), TechText.Work(ad.Work)));
                facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_ad_closes", "Closes"), TechText.Day(ad.To)));
                facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_ad_boosted", "Boosted"), ad.Boost ? w.T("reallife_work_yes", "Yes") : w.T("reallife_work_no", "No")));
                facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_ad_going", "Going rate"), TechText.Rate(d.GoingFor(ad.Role, ad.Level))));
                r.Sections.Add(facts);
                var going = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_how_it_goes", "How it is going") };
                going.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_applicants", "Applicants"), came.ToString(Inv)));
                going.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_shortlisted_n", "On your shortlist"), shortlisted.ToString(Inv)));
                going.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_offers_out", "Offers out"), offers.ToString(Inv)));
                r.Sections.Add(going);
                var hiredHere = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_hired_via_ad", "Hired through this ad") };
                for (int k = 0; k < hired.Count && hiredHere.People.Count < 3; k++)
                {
                    Seeker s = h.SeekerOf(hired[k].Seeker);
                    if (hired[k].Ad == ad.Id && s != null)
                    {
                        hiredHere.People.Add(new WorkPerson { Name = s.P.Name, Role = TechText.Role(s.P.Role) + ", " + TechText.Day(hired[k].Start), Ring = UiRole.Ok });
                    }
                }
                if (hiredHere.People.Count > 0)
                {
                    hiredHere.Note = hiredHere.People.Count.ToString(Inv);
                    r.Sections.Add(hiredHere);
                }
                r.Acts.Add(new WorkAct { Kind = WorkKinds.GoTab, Arg = 1, Label = w.T("reallife_hire_button_applicants", "Applicants"),
                    Style = UiButtonKind.Secondary, Icon = UiIcon.People, Opens = true });
                r.Acts.Add(new WorkAct { Kind = Close, Arg = ad.Id, Label = w.T("reallife_work_close_ad", "Close the ad"), Style = UiButtonKind.Destructive, Icon = UiIcon.Close });
                t.Rows.Add(r);
            }
            for (int i = 0; i < h.RivalAds.Count && i < 6; i++)
            {
                RivalAd ra = h.RivalAds[i];
                if (ra.To <= day)
                {
                    continue;
                }
                int family = ra.Family;
                int first = HiringRules.RoleOf((TFamily)family, 0);
                var r = new WorkRow { Id = 900 + i, Icon = UiIcon.Flag, Name = TechText.FamilyName((TFamily)family) + ", " + TechText.Level(ra.Level),
                    Sub = ra.Rival, Status = w.T("reallife_work_rival_ad", "Rival ad"),
                    Tone = UiRole.Muted, Chip = w.T("reallife_work_rival_ad", "Rival ad"), ChipTone = UiRole.Muted, Group = 900 + i,
                    Cells = new[] { null, string.Empty, string.Empty, first >= 0 ? TechText.Rate(d.GoingFor(first, ra.Level) * (10000 + ra.PayBp) / 10000) : string.Empty, w.T("reallife_work_rival_ad", "Rival ad") },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Muted },
                    Sentence = w.F("reallife_work_rival_ad_line", "{rival} hires the same line and pays {bp} over the going rate. Their applicants may take an offer before yours.", "rival", ra.Rival,
                        "bp", TechText.Bp(ra.PayBp)) };
                t.Rows.Add(r);
            }
            t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_open_posts", "Open posts"), openPosts.ToString(Inv)));
            t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_waiting", "Applicants waiting"), waiting.ToString(Inv)));
            t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_hired_month", "Hired this month"), hired.Count.ToString(Inv)));
            WorkBands.AddIfAny(t.Below, WorkBands.Tiles(w.T("reallife_work_band_pay_vs_going", "Your pay against the going rate"), payIcons.ToArray(), payNames.ToArray(), payHints.ToArray(), payValues.ToArray()));
            WorkBands.AddIfAny(t.Below, WorkBands.Tiles(w.T("reallife_work_band_closing", "Closing soon"), closeIcons.ToArray(), closeNames.ToArray(), closeHints.ToArray(), closeValues.ToArray()));
            return t;
        }

        // ------------------------------------------------------------------------------------------------- the new ad

        private WorkTab NewAdTab(TechDirector d, HiringFile h, int day, WorkView v)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { Shape = WorkShape.Form, Sub = true };
            var form = new WorkFormModel { Lead = w.T("reallife_work_new_ad_lead", "An ad runs for the days you set and brings applicants over a few days. It is paid when it is posted.") };
            t.Form = form;
            TalentGame.Businesses(_places);
            if (_places.Count == 0)
            {
                form.Lead = TechText.T("reallife_hire_new_no_business");
                t.Acts.Add(new WorkAct { Kind = WorkKinds.Back, Label = w.T("reallife_work_cancel", "Cancel"), Style = UiButtonKind.Secondary, Icon = UiIcon.Back, Opens = true });
                return t;
            }
            int place = Math.Max(0, Math.Min(_places.Count - 1, v.Get(2, 0)));
            int roleAt = Math.Max(0, Math.Min(TalentCatalogue.RoleCount - 1, v.Get(0, 0)));
            RoleDef r = TalentCatalogue.Role(roleAt);
            int levelAt = Math.Max(0, Math.Min(r.MostLevel - r.LeastLevel, v.Get(1, Math.Max(0, TalentCatalogue.Mid - r.LeastLevel))));
            int level = r.LeastLevel + levelAt;
            int going = d.GoingFor(r.Id, level);
            int payAt = Math.Max(0, Math.Min(PayBands.Length - 1, v.Get(3, 1)));
            int most = going * PayBands[payAt] / 1000, least = most * 85 / 100;
            int skillAt = Math.Max(0, Math.Min(2, v.Get(5, 0)));
            int skill = skillAt == 0 ? 0 : skillAt == 1 ? TalentCatalogue.LevelSkillLeast[level] : (TalentCatalogue.LevelSkillLeast[level] + TalentCatalogue.LevelSkillMost[level]) / 2;
            int works = r.Remote ? 3 : 2;
            int work = Math.Max(0, Math.Min(works - 1, v.Get(6, 0)));
            int daysAt = Math.Max(0, Math.Min(HiringCatalogue.AdDays.Length - 1, v.Get(7, 1)));
            int days = HiringCatalogue.AdDays[daysAt];
            int posts = Math.Max(1, Math.Min(HireOffers.PostsMost, v.Get(8, 1)));
            int boost = Math.Max(0, Math.Min(1, v.Get(4, 0)));
            var names = new string[_places.Count];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = TalentGame.NameOf(_places[i]);
            }
            var roleNames = new string[TalentCatalogue.RoleCount];
            for (int i = 0; i < roleNames.Length; i++)
            {
                roleNames[i] = TechText.Role(i);
            }
            var levelNames = new string[r.MostLevel - r.LeastLevel + 1];
            for (int i = 0; i < levelNames.Length; i++)
            {
                levelNames[i] = TechText.Level(r.LeastLevel + i);
            }
            var payNames = new string[PayBands.Length];
            for (int i = 0; i < payNames.Length; i++)
            {
                payNames[i] = TechText.Fill("reallife_hire_new_pay", "most", TechText.Rate(going * PayBands[i] / 1000), "share", TechText.Mille(PayBands[i]));
            }
            string[] skillNames = { TechText.T("reallife_hire_new_skill_any"), TechText.T("reallife_hire_new_skill_level"), TechText.T("reallife_hire_new_skill_strong") };
            var workNames = new string[works];
            for (int i = 0; i < works; i++)
            {
                workNames[i] = TechText.Work(i);
            }
            var dayNames = new string[HiringCatalogue.AdDays.Length];
            for (int i = 0; i < dayNames.Length; i++)
            {
                dayNames[i] = TechText.T("reallife_hire_new_days", "days", HiringCatalogue.AdDays[i].ToString());
            }
            string[] boostNames = { w.T("reallife_work_no", "No"), w.T("reallife_work_yes", "Yes") };
            // the ids are the view's: place 2, role 0, level 1, pay 3, boost 4, skill 5, arrangement 6, days 7, posts 8
            t.Amounts.Add(Choice(0, roleAt, roleNames));
            t.Amounts.Add(Choice(1, level - r.LeastLevel, levelNames));
            t.Amounts.Add(Choice(2, place, names));
            t.Amounts.Add(Choice(3, payAt, payNames));
            t.Amounts.Add(Choice(4, boost, boostNames));
            t.Amounts.Add(Choice(5, skillAt, skillNames));
            t.Amounts.Add(Choice(6, work, workNames));
            t.Amounts.Add(Choice(7, daysAt, dayNames));
            t.Amounts.Add(new WorkAmount { Id = 8, Value = posts, Min = 1, Max = HireOffers.PostsMost, Figure = posts.ToString(Inv) });
            var role = new WorkFormBand { Title = w.T("reallife_work_the_role", "The role") };
            role.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_col_role", "Role"), Stepper = 0 });
            role.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_col_level", "Level"), Stepper = 1 });
            role.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_ad_place", "At"), Stepper = 2 });
            role.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_ad_posts", "Posts to fill"), Stepper = 8 });
            role.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_ad_arrangement", "Arrangement"), Stepper = 6 });
            role.Rows.Add(new WorkFormRow { Field = WorkField.Text, Value = TechText.T(SeatRules.Needs(r) == Workstation.Desk ? "reallife_hire_seat_desk" : "reallife_hire_seat_payroll") });
            form.Left.Add(role);
            var terms = new WorkFormBand { Title = w.T("reallife_work_the_terms", "Pay and terms") };
            terms.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_ad_pay_most", "Pay up to"), Stepper = 3 });
            terms.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_ad_skill", "Minimum skill"), Stepper = 5 });
            terms.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_ad_days", "The ad runs"), Stepper = 7 });
            terms.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_ad_boost", "Boost the ad"), Stepper = 4 });
            int cost = Prices.At(HiringRules.AdCost(days, boost == 1));
            form.Right.Add(terms);
            var sum = new WorkFormBand { Title = w.T("reallife_work_ad_summary", "What it comes to") };
            sum.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_ad_going", "Going rate"), Value = TechText.Rate(going) });
            sum.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_ad_pay_least", "Pay from"), Value = TechText.Rate(least) });
            sum.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_ad_cost", "It costs"), Value = TechText.Money(cost) });
            form.Right.Add(sum);
            NewAdFoot(d, h, day, r, form);
            bool seats = HiringRules.Seats(TalentGame.Shifts(_places[place]), r);
            string why = !seats ? TechText.T(TechDirector.SeatRefusal) : null;
            t.Acts.Add(new WorkAct { Kind = WorkKinds.Back, Label = w.T("reallife_work_cancel", "Cancel"), Style = UiButtonKind.Secondary, Icon = UiIcon.Back, Opens = true });
            t.Acts.Add(new WorkAct { Kind = PostAd, Label = w.T("reallife_hire_button_post", "Post the ad"), Style = UiButtonKind.Primary, Icon = UiIcon.Check, Why = why });
            return t;
        }

        // The new ad's foot: who of the team already does this role, and what the market holds for its line (the seekers, the rival ads and the shortage).
        private void NewAdFoot(TechDirector d, HiringFile h, int day, RoleDef r, WorkFormModel form)
        {
            UtilitiesWords w = W;
            var names = new List<string>(3);
            var roles = new List<string>(3);
            var pays = new List<string>(3);
            for (int i = 0; i < d.People.Count && names.Count < 3; i++)
            {
                Person p = d.People[i];
                if (p.File.Role == r.Id)
                {
                    names.Add(p.Name);
                    roles.Add(TechText.Role(p.File.Role) + ", " + TechText.Level(p.File.Level) + ", " + p.Business);
                    pays.Add(TechText.Rate(p.Game != null ? TalentGame.PaidCents(p.Game) : p.File.GameWage));
                }
            }
            WorkBands.AddIfAny(form.Below, WorkBands.People(w.T("reallife_work_band_already_team", "Already on your team"), names.ToArray(), roles.ToArray(), pays.ToArray()));
            int looking = 0, rivalAds = 0;
            for (int i = 0; i < h.Seekers.Count; i++)
            {
                looking += !h.Seekers[i].Taken && !h.Seekers[i].Passive && h.Seekers[i].P.Def.Family == r.Family ? 1 : 0;
            }
            for (int i = 0; i < h.RivalAds.Count; i++)
            {
                rivalAds += h.RivalAds[i].Family == (int)r.Family && h.RivalAds[i].To > day ? 1 : 0;
            }
            WorkBands.AddIfAny(form.Below, WorkBands.Pairs(w.T("reallife_work_band_market_for", "The market for it"),
                new[] { w.T("reallife_work_col_looking", "Looking"), w.T("reallife_work_col_rival_ads", "Rival ads"), w.T("reallife_work_shortage", "Shortage") },
                new[] { looking.ToString(Inv), rivalAds.ToString(Inv), TechText.Mille(d.ShortageOf(r.Family)) }));
        }

        private static WorkAmount Choice(int id, int value, string[] words) => new WorkAmount { Id = id, Value = Math.Max(0,
            Math.Min(words.Length - 1, value)), Min = 0, Max = words.Length - 1, Words = words,
            Figure = words[Math.Max(0, Math.Min(words.Length - 1, value))] };

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
                bool done = Carry(act, d, h, view);
                if (done)
                {
                    _local++;
                }
                return done;
            }
            catch (Exception e)
            {
                TechDirector.Logger?.Warn("Open Roles (kit): " + act.Kind + " failed: " + e.Message);
                return false;
            }
        }
    }
}
