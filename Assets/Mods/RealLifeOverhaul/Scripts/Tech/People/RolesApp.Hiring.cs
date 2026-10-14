using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The hiring tabs of Open Roles: the jobs board (the player's ads and the page that posts one), the applicants
    /// (what is known of each, the checks, the interview and the offer), the labour market by family, and the search
    /// firms. Each page shows the one thing picked; the figures are the rules' (Tech/Hiring/HiringRules).
    /// </summary>
    public sealed partial class RolesApp
    {
        public const string ApplicantRow = "app:";
        private const string AdRow = "ad:", NewAdRow = "newad", FamilyRow = "fam:", SearchRow = "srch:", NewSearchRow = "newsearch";

        private static readonly int[] PayBands = { 900, 1000, 1100, 1200, 1350 };   // thousandths of the going rate an ad's top pay can be set at
        private static readonly int[] OfferBands = { 900, 950, 1000, 1050, 1100, 1200 };

        private readonly List<BuildingRegistration> _places = new List<BuildingRegistration>(16);
        private int _adPlace, _adRole, _adLevel = 1, _adPay = 1, _adSkill, _adWork, _adDays = 1, _searchRole = TalentCatalogue.SoftwareEngineer, _searchLevel = TalentCatalogue.Senior;
        private bool _adBoost;
        private int _offer = 2;

        // ---------------------------------------------------------------- the jobs board

        private TableSpec WriteJobs(InfoBox box, int day)
        {
            HiringFile f = _director.Hiring;
            TableRow add = Row(NewAdRow);
            add.Pinned = true;
            add.Set(0, TechText.T("reallife_hire_row_new_ad"));
            int open = 0;
            JobAd picked = null;
            string id = Picked(_picked, AdRow);
            for (int i = 0; i < f.Ads.Count; i++)
            {
                JobAd ad = f.Ads[i];
                if (ad.Search)
                {
                    continue;
                }
                BuildingRegistration at = TalentGame.Find(ad.Place);
                TableRow row = Row(AdRow + ad.Id);
                row.Set(0, TechText.Role(ad.Role));
                row.Set(1, TechText.Level(ad.Level), ad.Level);
                row.Set(2, at != null ? TalentGame.NameOf(at) : "-");
                row.Set(3, TechText.Fill("reallife_hire_row_pay", "least", TechText.Rate(ad.PayLeast), "most", TechText.Rate(ad.PayMost)), ad.PayMost);
                row.Set(4, TechText.Work(ad.Work));
                row.Set(5, ad.Came.ToString(), ad.Came);
                row.Set(6, ad.Open(day) ? TechText.T("reallife_hire_row_day", "day", ad.To.ToString()) : TechText.T("reallife_hire_row_ended"), ad.To);
                row.Set(7, TechText.T(ad.Boost ? "reallife_hire_row_boosted" : ad.Open(day) ? "reallife_hire_row_open" : "reallife_hire_row_closed"), ad.Open(day) ? TableRow.Plain : TableRow.Yellow);
                row.Kind = ad.Open(day) ? RolesColumns.AdOpen : RolesColumns.AdClosed;
                open += ad.Open(day) ? 1 : 0;
                picked = id == ad.Id.ToString() ? ad : picked;
            }
            if (_picked == NewAdRow)
            {
                WriteNewAd(box, day, open);
            }
            else if (picked != null)
            {
                WriteAd(box, picked, day);
            }
            else
            {
                _picked = null;
                box.Title(TechText.T("reallife_hire_jobs_title"));
                box.Section("reallife_look_sec_now");
                box.Row("reallife_hire_sum_ads", TechText.T("reallife_hire_sum_ads_value", "count", open.ToString(), "most", HiringCatalogue.AdsMost.ToString()));
                box.Line(TechText.Fill("reallife_hire_jobs_office", "week", HiringCatalogue.AdWeekMost.ToString(), "boosted", HiringCatalogue.AdWeekMostBoosted.ToString(),
                    "more", HiringCatalogue.OfficeBonusLeast.ToString()));
                box.Note(TechText.T("reallife_hire_jobs_about"));
                Meta.Guide.Point(box, JobsGuide);
            }
            return RolesColumns.Jobs;
        }

        private void WriteAd(InfoBox box, JobAd ad, int day)
        {
            BuildingRegistration at = TalentGame.Find(ad.Place);
            box.Title(TechText.T("reallife_hire_ad_title", "role", TechText.Role(ad.Role), "business", at != null ? TalentGame.NameOf(at) : "-"));
            box.Line(TechText.Fill("reallife_hire_ad_terms", "level", TechText.Level(ad.Level), "least", TechText.Rate(ad.PayLeast), "most", TechText.Rate(ad.PayMost), "work", TechText.Work(ad.Work),
                "skill", ad.MinSkill > 0 ? TechText.Percent(ad.MinSkill) : TechText.T("reallife_talent_none")));
            box.Line(TechText.Fill("reallife_hire_ad_run", "from", ad.From.ToString(), "to", ad.To.ToString(), "came", ad.Came.ToString(), "cap", HiringRules.WeekCap(ad, day).ToString()));
            box.Line(TechText.T("reallife_hire_ad_going", "going", TechText.Rate(_director.GoingFor(ad.Role, ad.Level))));
            Transform buttons = box.ButtonRow();
            if (ad.Open(day))
            {
                int adId = ad.Id;
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_hire_button_close_ad", () => Do(() => _director.CloseAd(adId)));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_hire_button_applicants", () => ShowTab(TechDirector.TabApplicants));
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, JobsGuide);
        }

        private void WriteNewAd(InfoBox box, int day, int open)
        {
            _director.Businesses(_places);
            box.Title(TechText.T("reallife_hire_new_title"));
            if (_places.Count == 0)
            {
                box.Line(TechText.T("reallife_hire_new_no_business"));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
                return;
            }
            _adPlace = Mathf.Clamp(_adPlace, 0, _places.Count - 1);
            RoleDef r = TalentCatalogue.Role(_adRole);
            _adLevel = TalentCatalogue.Clamp(_adLevel, r.LeastLevel, r.MostLevel);
            int going = _director.GoingFor(r.Id, _adLevel);
            int most = going * PayBands[Mathf.Clamp(_adPay, 0, PayBands.Length - 1)] / 1000, least = most * 85 / 100;
            int works = r.Remote ? 3 : 2;
            _adWork = Mathf.Clamp(_adWork, 0, works - 1);
            int skill = _adSkill == 0 ? 0 : _adSkill == 1 ? TalentCatalogue.LevelSkillLeast[_adLevel] : (TalentCatalogue.LevelSkillLeast[_adLevel] + TalentCatalogue.LevelSkillMost[_adLevel]) / 2;
            int days = HiringCatalogue.AdDays[Mathf.Clamp(_adDays, 0, HiringCatalogue.AdDays.Length - 1)];
            if (box.HasSelector)
            {
                box.Selector(_places.Count, _adPlace, i => TalentGame.NameOf(_places[i]), i => Set(() => _adPlace = i));
                box.Selector(TalentCatalogue.RoleCount, _adRole, TechText.Role, i => Set(() => _adRole = i));
                box.Selector(r.MostLevel - r.LeastLevel + 1, _adLevel - r.LeastLevel, i => TechText.Level(r.LeastLevel + i), i => Set(() => _adLevel = r.LeastLevel + i));
                box.Selector(PayBands.Length, _adPay, i => TechText.Fill("reallife_hire_new_pay", "most", TechText.Rate(going * PayBands[i] / 1000), "share", TechText.Mille(PayBands[i])), i => Set(() => _adPay = i));
                box.Selector(3, _adSkill, i => TechText.T(i == 0 ? "reallife_hire_new_skill_any" : i == 1 ? "reallife_hire_new_skill_level" : "reallife_hire_new_skill_strong"), i => Set(() => _adSkill = i));
                box.Selector(works, _adWork, TechText.Work, i => Set(() => _adWork = i));
                box.Selector(HiringCatalogue.AdDays.Length, _adDays, i => TechText.T("reallife_hire_new_days", "days", HiringCatalogue.AdDays[i].ToString()), i => Set(() => _adDays = i));
            }
            if (box.HasSwitch)
            {
                box.Switch("reallife_hire_new_boost", _adBoost, on => Set(() => _adBoost = on));
            }
            int cost = Prices.At(HiringRules.AdCost(days, _adBoost));
            box.Line(TechText.Fill("reallife_hire_new_cost", "cost", TechText.Money(cost), "least", TechText.Rate(least), "most", TechText.Rate(most), "skill", skill > 0 ? TechText.Percent(skill) : TechText.T("reallife_talent_none")));
            Transform buttons = box.ButtonRow();
            if (open < HiringCatalogue.AdsMost)
            {
                string place = TalentGame.Place(_places[_adPlace]);
                int role = r.Id, level = _adLevel, work = _adWork;
                bool boost = _adBoost;
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_hire_button_post", () => Do(() =>
                {
                    if (_director.PostAd(place, role, level, least, most, skill, work, days, boost))
                    {
                        _picked = null;
                    }
                }));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
        }

        private void Set(System.Action change)
        {
            change();
            Refresh();
        }

        // ---------------------------------------------------------------- the applicants

        private TableSpec WriteApplicants(InfoBox box, int day)
        {
            HiringFile f = _director.Hiring;
            TableFilter jobs = RolesColumns.Applicants.Filters[1];
            jobs.Names.Clear();
            Applicant picked = null;
            string id = Picked(_picked, ApplicantRow);
            int live = 0;
            for (int i = 0; i < f.Applicants.Count; i++)
            {
                Applicant a = f.Applicants[i];
                Seeker s = f.SeekerOf(a.Seeker);
                JobAd ad = f.AdOf(a.Ad);
                if (s == null || ad == null)
                {
                    continue;
                }
                bool tested = (a.Revealed & HiringRules.RevealSkill) != 0;
                TableRow row = Row(ApplicantRow + a.Id);
                row.Set(0, s.P.Name);
                row.Set(1, TechText.Role(s.P.Role));
                row.Set(2, TechText.Level(s.P.Level), s.P.Level);
                int skill = tested ? s.P.Main / 1000 : s.CvMain;
                row.Set(3, tested ? TechText.Percent(skill) : TechText.T("reallife_hire_row_cv", "skill", TechText.Percent(skill)), skill);
                bool ask = (a.Revealed & HiringRules.RevealAsk) != 0 || ad.Search;
                row.Set(4, ask ? TechText.Rate(s.Ask) : "?", ask ? s.Ask : 0);
                string job = TechText.T(ad.Search ? "reallife_hire_row_for_search" : "reallife_hire_row_for", "role", TechText.Role(ad.Role));
                row.Set(5, job);
                row.Set(6, a.Day.ToString(), a.Day);
                row.Set(7, TechText.Mille(a.Interest), a.Interest);
                row.Set(8, Stage(a), a.At == ApplyStage.Countered ? TableRow.Yellow : a.At == ApplyStage.Accepted ? TableRow.Green : TableRow.Plain);
                row.Kind = a.Stage == (int)ApplyStage.New ? RolesColumns.StageNew : a.Stage <= (int)ApplyStage.Interviewed ? RolesColumns.StageScreened
                    : a.Stage <= (int)ApplyStage.Countered ? RolesColumns.StageOffer : a.Stage <= (int)ApplyStage.Started ? RolesColumns.StageHired : RolesColumns.StageGone;
                row.Kind2 = KindOf(jobs, job);
                live += a.Live ? 1 : 0;
                picked = id == a.Id.ToString() ? a : picked;
            }
            if (picked != null)
            {
                WriteApplicant(box, picked, f.SeekerOf(picked.Seeker), f.AdOf(picked.Ad), day);
            }
            else
            {
                _picked = null;
                box.Title(TechText.T("reallife_hire_applicants_title"));
                box.Section("reallife_look_sec_now");
                box.Row("reallife_hire_sum_live", live.ToString());
                box.Note(TechText.T("reallife_hire_applicants_about"));
                Meta.Guide.Point(box, JobsGuide);
            }
            return RolesColumns.Applicants;
        }

        private static string Stage(Applicant a)
        {
            switch (a.At)
            {
                case ApplyStage.New: return TechText.T("reallife_hire_stage_new");
                case ApplyStage.Shortlisted: return TechText.T("reallife_hire_stage_shortlisted");
                case ApplyStage.Interviewed: return TechText.T("reallife_hire_stage_interviewed");
                case ApplyStage.Offered: return TechText.T("reallife_hire_stage_offered");
                case ApplyStage.Countered: return TechText.T("reallife_hire_stage_countered", "amount", TechText.Rate(a.Counter));
                case ApplyStage.Accepted: return TechText.T("reallife_hire_stage_accepted", "day", a.Start.ToString());
                case ApplyStage.Started: return TechText.T("reallife_hire_stage_started");
                case ApplyStage.Declined: return TechText.T("reallife_hire_stage_declined");
                case ApplyStage.Rejected: return TechText.T("reallife_hire_stage_rejected");
                case ApplyStage.Withdrawn: return TechText.T("reallife_hire_stage_withdrawn");
                default: return TechText.T("reallife_hire_stage_lapsed");
            }
        }

        private void WriteApplicant(InfoBox box, Applicant a, Seeker s, JobAd ad, int day)
        {
            int id = a.Id;
            bool tested = (a.Revealed & HiringRules.RevealSkill) != 0, years = (a.Revealed & HiringRules.RevealYears) != 0, fit = (a.Revealed & HiringRules.RevealFit) != 0;
            bool ask = (a.Revealed & HiringRules.RevealAsk) != 0 || ad.Search;
            box.Title(s.P.Name);
            bool open = a.Live && a.Stage < (int)ApplyStage.Offered;
            if (!open && a.At == ApplyStage.Countered)
            {
                box.Line(TechText.Fill(a.RivalBid > 0 ? "reallife_hire_counter_rival" : "reallife_hire_counter", "name", s.P.Name, "amount", TechText.Rate(a.Counter), "offer", TechText.Rate(a.Offer),
                    "day", (a.OfferDay + HiringCatalogue.OfferDays).ToString()));
            }
            else if (!open)
            {
                box.Line(Stage(a) + (a.Offer > 0 ? " " + TechText.T("reallife_hire_offer_was", "pay", TechText.Rate(a.Offer)) : string.Empty));
            }
            box.Line(TechText.Fill("reallife_hire_cv", "role", TechText.Role(s.P.Role), "level", TechText.Level(s.P.Level), "skill", TechText.Percent(tested ? s.P.Main / 1000 : s.CvMain),
                "tested", TechText.T(tested ? "reallife_hire_cv_tested" : "reallife_hire_cv_claimed"), "school", TechText.School(s.P.School), "certs", TechText.Certs(s.P)));
            box.Line(TechText.Fill("reallife_hire_cv_more", "years", TechText.Years(years ? s.P.YearsDays : s.P.YearsDays + s.Claim * 120), "district", HiringGame.District(s.District),
                "now", s.Employed ? TechText.T("reallife_hire_cv_employed", "days", s.Notice.ToString()) : TechText.T("reallife_hire_cv_looking")));
            string refs = a.Refs ? TechText.T("reallife_hire_refs_value", "score", s.Refs.ToString()) : TechText.T("reallife_hire_refs_none");
            string record = a.Background ? TechText.T(s.P.Record ? "reallife_hire_record_found" : "reallife_hire_record_clean") : TechText.T("reallife_hire_record_none");
            box.Line(refs + " " + record);
            box.Line(TechText.Fill("reallife_hire_known", "keen", TechText.Mille(a.Interest), "fit", fit ? TechText.T(s.Fit >= 650 ? "reallife_hire_fit_good" : s.Fit >= 400 ? "reallife_hire_fit_fair" : "reallife_hire_fit_poor") : "?",
                "ask", ask ? TechText.Rate(s.Ask) : "?", "work", (a.Revealed & HiringRules.RevealAsk) != 0 ? TechText.Work(s.Want) : "?"));
            Transform first = open || a.At == ApplyStage.Countered ? box.ButtonRow() : null;
            if (open)
            {
                if (!a.Interviewed)
                {
                    InfoBox.Button(first, InfoBox.Positive, "reallife_hire_button_interview", () => Do(() => _director.Interview(id)));
                }
                if (!a.Refs)
                {
                    InfoBox.Button(first, InfoBox.Additional, "reallife_hire_button_refs", () => Do(() => _director.Check(id, false)));
                }
                if (!a.Background)
                {
                    InfoBox.Button(first, InfoBox.Additional, "reallife_hire_button_background", () => Do(() => _director.Check(id, true)));
                }
                int based = ask ? s.Ask : ad.Search ? _director.GoingFor(ad.Role, ad.Level) : ad.PayMost;
                _offer = Mathf.Clamp(_offer, 0, OfferBands.Length - 1);
                if (box.HasSelector)
                {
                    box.Selector(OfferBands.Length, _offer, i => TechText.Fill(ask ? "reallife_hire_offer_of_ask" : "reallife_hire_offer_of_top", "pay", TechText.Rate(based * OfferBands[i] / 1000),
                        "share", TechText.Mille(OfferBands[i])), i => Set(() => _offer = i));
                }
                int pay = based * OfferBands[_offer] / 1000;
                Transform second = box.ButtonRow();
                InfoBox.Button(second, InfoBox.Positive, "reallife_hire_button_offer", () => Do(() => _director.MakeOffer(id, pay)));
                if (a.Stage == (int)ApplyStage.New)
                {
                    InfoBox.Button(second, InfoBox.Additional, "reallife_hire_button_shortlist", () => Do(() => _director.Shortlist(id, true)));
                }
                InfoBox.Button(second, InfoBox.Negative, "reallife_hire_button_reject", () => Do(() => _director.Shortlist(id, false)));
            }
            else if (a.At == ApplyStage.Countered)
            {
                InfoBox.Button(first, InfoBox.Positive, "reallife_hire_button_take_counter", () => Do(() => _director.TakeCounter(id)));
                InfoBox.Button(first, InfoBox.Negative, "reallife_hire_button_walk_away", () => Do(() => _director.WalkAway(id)));
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, JobsGuide);
        }

        // ---------------------------------------------------------------- the market

        private TableSpec WriteMarket(InfoBox box, int day)
        {
            HiringFile f = _director.Hiring;
            int pickedFamily = -1;
            string id = Picked(_picked, FamilyRow);
            int[] levels = new int[TalentCatalogue.Levels];
            int all = 0;
            for (int fam = 0; fam < (int)Family.Count; fam++)
            {
                for (int l = 0; l < levels.Length; l++)
                {
                    levels[l] = 0;
                }
                int seekers = 0, openings = 0, yours = 0;
                for (int i = 0; i < f.Seekers.Count; i++)
                {
                    Seeker s = f.Seekers[i];
                    if (!s.Taken && !s.Passive && (int)s.P.Def.Family == fam)
                    {
                        seekers++;
                        levels[s.P.Level]++;
                    }
                }
                for (int i = 0; i < f.RivalAds.Count; i++)
                {
                    openings += f.RivalAds[i].Family == fam && f.RivalAds[i].To > day ? 1 : 0;
                }
                for (int i = 0; i < f.Ads.Count; i++)
                {
                    yours += !f.Ads[i].Search && f.Ads[i].Open(day) && (int)TalentCatalogue.Role(f.Ads[i].Role).Family == fam ? 1 : 0;
                }
                all += seekers;
                int shortage = _director.ShortageOf((Family)fam);
                int first = HiringRules.RoleOf((Family)fam, 0);
                int going = first >= 0 ? _director.GoingFor(first, TalentCatalogue.Mid) : 0;
                TableRow row = Row(FamilyRow + fam);
                row.Set(0, TechText.FamilyName((Family)fam));
                row.Set(1, seekers.ToString(), seekers);
                row.Set(2, levels[0].ToString(), levels[0]);
                row.Set(3, levels[1].ToString(), levels[1]);
                int seniors = levels[2] + levels[3] + levels[4];
                row.Set(4, seniors.ToString(), seniors, seniors <= 2 ? TableRow.Red : TableRow.Plain);
                row.Set(5, openings.ToString(), openings);
                row.Set(6, yours.ToString(), yours);
                row.Set(7, TechText.Mille(shortage), shortage, shortage >= 1200 ? TableRow.Red : shortage >= 1080 ? TableRow.Yellow : TableRow.Plain);
                row.Set(8, going > 0 ? TechText.Rate(going) : "-", going);
                pickedFamily = id == fam.ToString() ? fam : pickedFamily;
            }
            int cycle = HiringGame.Cycle;
            if (pickedFamily >= 0)
            {
                WriteFamily(box, f, (Family)pickedFamily, day);
            }
            else
            {
                _picked = null;
                box.Title(TechText.T("reallife_hire_market_title"));
                if (HiringGame.Summer(day))
                {
                    box.Line(TechText.T("reallife_hire_market_summer"));
                }
                box.Row("reallife_hire_sum_seekers", all.ToString());
                box.Row("reallife_hire_sum_unemployment", TechText.Mille(HiringRules.Unemployment(cycle)));
                box.Row("reallife_hire_sum_rivals", f.RivalAds.Count.ToString());
                box.Note(TechText.T("reallife_hire_market_about"));
                Meta.Guide.Point(box, JobsGuide);
            }
            return RolesColumns.Market;
        }

        private void WriteFamily(InfoBox box, HiringFile f, Family family, int day)
        {
            box.Title(TechText.FamilyName(family));
            var rivals = new Dictionary<string, int>(8);
            for (int i = 0; i < f.RivalAds.Count; i++)
            {
                RivalAd ad = f.RivalAds[i];
                if (ad.Family == (int)family && ad.To > day)
                {
                    int n;
                    rivals.TryGetValue(ad.Rival, out n);
                    rivals[ad.Rival] = n + 1;
                }
            }
            var text = new System.Text.StringBuilder(128);
            foreach (KeyValuePair<string, int> pair in rivals)
            {
                if (text.Length > 0)
                {
                    text.Append(", ");
                }
                text.Append(pair.Key).Append(" (").Append(pair.Value).Append(')');
            }
            box.Row("reallife_hire_family_rivals", text.Length > 0 ? text.ToString() : TechText.T("reallife_talent_none"));
            var districts = new Dictionary<string, int>(8);
            for (int i = 0; i < f.Seekers.Count; i++)
            {
                Seeker s = f.Seekers[i];
                if (!s.Taken && !s.Passive && s.P.Def.Family == family)
                {
                    string d = HiringGame.District(s.District);
                    int n;
                    districts.TryGetValue(d, out n);
                    districts[d] = n + 1;
                }
            }
            text.Length = 0;
            foreach (KeyValuePair<string, int> pair in districts)
            {
                if (text.Length > 0)
                {
                    text.Append(", ");
                }
                text.Append(pair.Key).Append(' ').Append(pair.Value);
            }
            box.Row("reallife_hire_family_districts", text.Length > 0 ? text.ToString() : TechText.T("reallife_talent_none"));
            box.Line(TechText.T("reallife_hire_family_shortage", "shortage", TechText.Mille(_director.ShortageOf(family))));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, JobsGuide);
        }

        // ---------------------------------------------------------------- search firms

        private TableSpec WriteSearch(InfoBox box, int day)
        {
            HiringFile f = _director.Hiring;
            TableRow add = Row(NewSearchRow);
            add.Pinned = true;
            add.Set(0, TechText.T("reallife_hire_row_new_search"));
            JobAd picked = null;
            string id = Picked(_picked, SearchRow);
            for (int i = 0; i < f.Ads.Count; i++)
            {
                JobAd ad = f.Ads[i];
                if (!ad.Search)
                {
                    continue;
                }
                BuildingRegistration at = TalentGame.Find(ad.Place);
                TableRow row = Row(SearchRow + ad.Id);
                row.Set(0, TechText.Role(ad.Role));
                row.Set(1, TechText.Level(ad.Level), ad.Level);
                row.Set(2, at != null ? TalentGame.NameOf(at) : "-");
                row.Set(3, TechText.Cents(ad.Fee), ad.Fee / 100.0);
                row.Set(4, TechText.Cents(ad.FeePaid), ad.FeePaid / 100.0);
                row.Set(5, TechText.T("reallife_hire_row_day", "day", ad.Due.ToString()), ad.Due);
                row.Set(6, ad.Came.ToString(), ad.Came);
                row.Set(7, TechText.T(ad.Came > 0 ? "reallife_hire_row_delivered" : "reallife_hire_row_searching"));
                picked = id == ad.Id.ToString() ? ad : picked;
            }
            if (_picked == NewSearchRow)
            {
                WriteNewSearch(box);
            }
            else if (picked != null)
            {
                box.Title(TechText.T("reallife_hire_search_title", "role", TechText.Role(picked.Role)));
                box.Line(TechText.Fill("reallife_hire_search_terms", "level", TechText.Level(picked.Level), "fee", TechText.Cents(picked.Fee), "paid", TechText.Cents(picked.FeePaid),
                    "day", picked.Due.ToString(), "count", HiringCatalogue.SearchCandidates.ToString()));
                Transform buttons = box.ButtonRow();
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_hire_button_applicants", () => ShowTab(TechDirector.TabApplicants));
                InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
                Meta.Guide.Point(box, JobsGuide);
            }
            else
            {
                _picked = null;
                box.Title(TechText.T("reallife_hire_searches_title"));
                box.Note(TechText.Fill("reallife_hire_searches_about", "share", TechText.Bp(HiringCatalogue.SearchFeeBp), "count", HiringCatalogue.SearchCandidates.ToString(),
                    "least", HiringCatalogue.SearchDaysLeast.ToString(), "most", HiringCatalogue.SearchDaysMost.ToString()));
                Meta.Guide.Point(box, JobsGuide);
            }
            return RolesColumns.Search;
        }

        private void WriteNewSearch(InfoBox box)
        {
            _director.Businesses(_places);
            box.Title(TechText.T("reallife_hire_new_search_title"));
            if (_places.Count == 0)
            {
                box.Line(TechText.T("reallife_hire_new_no_business"));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
                return;
            }
            _adPlace = Mathf.Clamp(_adPlace, 0, _places.Count - 1);
            RoleDef r = TalentCatalogue.Role(_searchRole);
            int least = System.Math.Max(r.LeastLevel, HiringCatalogue.SearchLeastLevel);
            if (r.MostLevel < least)
            {
                _searchRole = TalentCatalogue.SoftwareEngineer;
                r = TalentCatalogue.Role(_searchRole);
                least = HiringCatalogue.SearchLeastLevel;
            }
            _searchLevel = TalentCatalogue.Clamp(_searchLevel, least, r.MostLevel);
            if (box.HasSelector)
            {
                box.Selector(_places.Count, _adPlace, i => TalentGame.NameOf(_places[i]), i => Set(() => _adPlace = i));
                box.Selector(TalentCatalogue.RoleCount, _searchRole, TechText.Role, i => Set(() => _searchRole = i));
                box.Selector(r.MostLevel - least + 1, _searchLevel - least, i => TechText.Level(least + i), i => Set(() => _searchLevel = least + i));
            }
            int fee, retainer;
            _director.SearchQuote(r.Id, _searchLevel, out fee, out retainer);
            box.Line(TechText.Fill("reallife_hire_new_search_cost", "fee", TechText.Cents(fee), "retainer", TechText.Cents(retainer), "count", HiringCatalogue.SearchCandidates.ToString()));
            Transform buttons = box.ButtonRow();
            if (r.MostLevel >= HiringCatalogue.SearchLeastLevel)
            {
                string place = TalentGame.Place(_places[_adPlace]);
                int role = r.Id, level = _searchLevel;
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_hire_button_brief", () => Do(() =>
                {
                    if (_director.StartSearch(place, role, level))
                    {
                        _picked = null;
                    }
                }));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
        }
    }
}
