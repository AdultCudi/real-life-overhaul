using System.Collections.Generic;
using BAModAPI;
using Localizor;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;
using UI;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The Career app, in the game's frame (GameUi.AppShell, copied from MyEmployees). Its tabs over the table on the right,
    /// and on the left the white box with the one thing picked:
    ///
    ///   Professions    how offers come, and for each profession what the player still needs (a course and where it is
    ///                  taught, more skill, a vacancy) and its ladder; the first step (CareerApp.Start.cs)
    ///   Jobs           the city's vacancies: its businesses (the rivals' among them), its tech companies, the public sector,
    ///                  the rivals' ads on the jobs board; what each pays and asks and how well the player fits; apply
    ///   Applications   every application: invited to an interview, an offer to take, ask more for or turn down
    ///   My job         the job now, its working days and where they are worked; start the day, give notice
    ///   CV             the jobs before with their references, and the qualifications passed
    ///   Own work       freelance contracts, the player's practice, leaving to found a company or a practice, former
    ///                  colleagues to bring along, and a restraint clause that binds (CareerApp.Own.cs)
    ///
    /// Everything on it is the game's. Nothing runs while the app is closed: the rows and figures are made when it opens,
    /// when a tab is picked and when the player changes something in it.
    /// </summary>
    public sealed partial class CareerApp
    {
        public const string OpeningRow = "post:", ApplicationRow = "app:", ProfessionRow = "prof:";
        private const string DayRow = "day:", SpellRow = "spell:", QualRow = "qual:";
        // The Guide's pages the tabs point to at their foot: finding a job, the working life, working for yourself.
        private const string JobsGuide = "reallife_meta_title_careers_jobs", LifeGuide = "reallife_meta_title_careers_life", OwnGuide = "reallife_meta_title_careers_own";

        private static readonly string[] TabKeys = { "reallife_career_tab_start", "reallife_career_tab_jobs", "reallife_career_tab_applications", "reallife_career_tab_job", "reallife_career_tab_cv", "reallife_career_tab_own" };

        private readonly CareersDirector _director;
        private readonly List<TableRow> _rows = new List<TableRow>(64);
        private readonly List<TableRow> _pool = new List<TableRow>(64);

        private AppShell _shell;
        private DataTable _table;
        private int _tab, _wanted = -1;
        private string _wantPick, _picked;
        private bool _ready, _writing;

        public CareerApp(CareersDirector director)
        {
            _director = director;
        }

        public GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("career: the app being built from the game's MyEmployees");
            _shell = AppShell.Make(menu, dormant, CareersDirector.CareerAppId);
            _table = DataTable.On(_shell, "Career");
            if (_table == null || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(CareerColumns.Off))
            {
                return null;
            }
            _table.Money = amount => CareersText.Money((long)amount);
            _table.SelectionLost = ClosePick;
            _table.onRowSelected.AddListener(row => UiClone.Safe(() => RowClicked(row)));
            for (int i = 0; i < TabKeys.Length; i++)
            {
                int tab = i;
                UiClone.Key(_shell.TabLabels[i], TabKeys[i]);
                UiClone.Rewire(_shell.Tabs[i], () => ShowTab(tab));
            }
            return _shell.Root;
        }

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        public void Want(int tab, string pick)
        {
            _wanted = Mathf.Clamp(tab, 0, TabKeys.Length - 1);
            _wantPick = pick;
            if (Showing)
            {
                Opened();
            }
        }

        public void Opened()
        {
            if (_shell == null || _table == null)
            {
                return;
            }
            if (_wanted >= 0)
            {
                _tab = _wanted;
                _picked = _wantPick;
                _wanted = -1;
                _wantPick = null;
            }
            _ready = _director.WorkRunning;
            if (_ready)
            {
                UiClone.Safe(_director.VacanciesNow);
            }
            Refresh();
            if (_ready)
            {
                UiClone.Safe(_director.FirstStepNotice);
            }
        }

        public void Closed()
        {
            _ready = false;
        }

        public void Changed()
        {
            if (Showing && _ready && !_writing)
            {
                Refresh();
            }
        }

        private void ShowTab(int tab)
        {
            if (tab != _tab || _picked != null)
            {
                _tab = tab;
                _picked = null;
                Refresh();
            }
        }

        private void Refresh()
        {
            if (_shell == null || _table == null || _writing)
            {
                return;
            }
            _writing = true;
            try
            {
                for (int i = 0; i < _rows.Count; i++)
                {
                    _pool.Add(_rows[i]);
                }
                _rows.Clear();
                _shell.ShowTab(_tab);
                InfoBox box = _shell.Box;
                box.Clear();
                TableSpec table = CareerColumns.Off;
                WorkFile w = _director.Work;
                StudyFile f = _director.Study;
                if (!_ready || SaveGameManager.Current == null || w == null || f == null)
                {
                    UiClone.Words(_shell.Header, CareersText.T("reallife_career_app"));
                    box.Title(CareersText.T("reallife_career_off_title"));
                    box.Line(CareersText.T("reallife_career_off_body"));
                    _picked = null;
                }
                else
                {
                    UiClone.Words(_shell.Header, w.Job != null
                        ? CareersText.T("reallife_career_head_job", "title", CareersText.Title(w.Job.Post.Prof, w.Job.Post.Rank), "employer", CareersText.Employer(w.Job.Post.Employer))
                        : CareersText.T("reallife_career_head_none"));
                    int day = SaveGameManager.Current.Day;
                    switch (_tab)
                    {
                        case CareersDirector.TabStart: table = WriteStart(box, w, f, day); break;
                        case CareersDirector.TabApplications: table = WriteApplications(box, w, day); break;
                        case CareersDirector.TabJob: table = WriteJob(box, w, f, day); break;
                        case CareersDirector.TabCv: table = WriteCv(box, w, f, day); break;
                        case CareersDirector.TabOwn: table = WriteOwn(box, w, f, day); break;
                        default: table = WriteJobs(box, w, f, day); break;
                    }
                }
                box.Done();
                _table.Show(table, _rows, _picked);
            }
            finally
            {
                _writing = false;
            }
        }

        private TableRow Row(string id)
        {
            TableRow row;
            if (_pool.Count > 0)
            {
                row = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                row = new TableRow();
            }
            row.Reset(id);
            row.Pick = true;
            row.Index = _rows.Count;
            _rows.Add(row);
            return row;
        }

        private static int PickedId(string id, string kind)
        {
            int n;
            return id != null && id.StartsWith(kind, System.StringComparison.Ordinal) && int.TryParse(id.Substring(kind.Length), out n) ? n : -1;
        }

        private void RowClicked(TableRow row)
        {
            if (row == null || !row.Pick)
            {
                _table.ClearSelection();
                return;
            }
            _picked = row.Id;
            Refresh();
        }

        private void ClosePick()
        {
            _picked = null;
            _table.ClearSelection();
            Refresh();
        }

        private void Do(System.Func<string> act)
        {
            string why = null;
            UiClone.Safe(() => why = act());
            if (why != null)
            {
                Notifications.Show(NotificationType.Error, why, null, 8f);
            }
            Refresh();
        }

        private static int KindOf(TableFilter filter, string name)
        {
            int kind = filter.Names.IndexOf(name);
            if (kind < 0)
            {
                kind = filter.Names.Count;
                filter.Names.Add(name);
            }
            return kind;
        }

        // ---------------------------------------------------------------- Jobs

        private TableSpec WriteJobs(InfoBox box, WorkFile w, StudyFile f, int day)
        {
            TableFilter professions = CareerColumns.Jobs.Filters[0];
            professions.Names.Clear();
            Opening picked = null;
            int id = PickedId(_picked, OpeningRow), open = 0;
            Candidate template = _director.Template();
            for (int i = 0; i < w.Openings.Count; i++)
            {
                Opening o = w.Openings[i];
                Profession p = o.Def;
                if (p == null || !o.Open(day))
                {
                    continue;
                }
                TableRow row = Row(OpeningRow + o.Id);
                int points = ProRules.Points(f, p, o.Rank), need = ProRules.Need(f, p, o.Rank);
                bool qualified = ProRules.Qualified(f, p, o.Rank), eligible = qualified && _director.OpenToMe(o, template);
                bool applied = w.Applied(o) != null;
                row.Set(0, CareersText.Title(o.Prof, o.Rank));
                row.Set(1, CareersText.ProName(o.Prof));
                row.Set(2, CareersText.Employer(o.Employer));
                row.Set(3, CareersText.ViaName(o.Via), o.Via == (int)Via.Board ? TableRow.Plain : TableRow.Green);
                int pay = Prices.At(o.Pay);
                row.Set(4, CareersText.T("reallife_career_row_rate", "rate", CareersText.Rate(pay)), pay);
                row.Set(5, CareersText.WorkWay(o.Work));
                row.Set(6, CareersText.Day(o.To), o.To);
                row.Set(7, CareersText.T("reallife_career_row_fit", "points", points.ToString(), "need", need.ToString()), points - need,
                    !qualified ? TableRow.Red : points >= need ? TableRow.Green : eligible ? TableRow.Yellow : TableRow.Red);
                row.Set(8, CareersText.T(applied ? "reallife_career_state_applied" : !qualified ? "reallife_career_state_unqualified" : eligible ? "reallife_career_state_open" : "reallife_career_state_short"),
                    applied ? TableRow.Green : eligible ? TableRow.Plain : TableRow.Yellow);
                row.Kind = KindOf(professions, CareersText.ProName(o.Prof));
                row.Kind2 = applied ? CareerColumns.Applied : eligible ? CareerColumns.Open : CareerColumns.Short;
                open += eligible && !applied ? 1 : 0;
                picked = o.Id == id ? o : picked;
            }
            if (picked != null)
            {
                WriteOpening(box, w, f, picked, day, template);
                return CareerColumns.Jobs;
            }
            _picked = null;
            box.Title(CareersText.T("reallife_career_jobs_title"));
            box.Line(CareersText.Pair("reallife_career_sum_open", open.ToString()));
            box.Line(w.AgencyUntil >= day
                ? CareersText.T("reallife_career_jobs_agency_on", "day", CareersText.Day(w.AgencyUntil))
                : CareersText.T("reallife_career_jobs_agency_off"));
            if (CareersGame.InsideType(HiringCatalogue.AgencyType))
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_career_button_register", () => Do(_director.Register));
            }
            box.Line(CareersText.T("reallife_career_jobs_about"));
            if (open == 0)
            {
                box.Line(CareersText.T("reallife_career_jobs_none_open"));
            }
            Meta.Guide.Point(box, JobsGuide);
            return CareerColumns.Jobs;
        }

        private void WriteOpening(InfoBox box, WorkFile w, StudyFile f, Opening o, int day, Candidate template)
        {
            Profession p = o.Def;
            ProRank r = p.Rank(o.Rank);
            box.Title(CareersText.Title(o.Prof, o.Rank));
            bool applied = w.Applied(o) != null;
            if (applied)
            {
                box.Line(CareersText.T("reallife_career_post_applied"));
            }
            if (!ProRules.Qualified(f, p, o.Rank))
            {
                box.Line(CareersText.T("reallife_career_post_unqualified"));
            }
            else if (ProRules.SelfTaught(p) && o.Rank == 0 && r.Quals.Length > 0 && ProRules.Need(f, p, 0) > r.Need)
            {
                box.Line(CareersText.T("reallife_career_post_self_taught", "extra", ProRules.SelfTaughtExtra.ToString()));
            }
            box.Line(CareersText.Fill("reallife_career_post_at", "employer", CareersText.Employer(o.Employer), "kind", CareersText.Kind((EmployerKind)o.Kind),
                "place", p.Place == Workplace.Hospital ? CareersText.T("reallife_career_place_hospitals") : WorkGame.Address(o.Street, o.Number)));
            box.Line(CareersText.Fill("reallife_career_post_terms", "pay", CareersText.Rate(Prices.At(o.Pay)), "hours", CareersText.Hours(p), "mode", CareersText.WorkWay(o.Work),
                "closes", CareersText.Day(o.To)));
            int a, b;
            p.SkillsOf(o.Rank, out a, out b);
            box.Line(CareersText.Fill("reallife_career_post_asks", "a", CareersText.Skill(a), "b", CareersText.Skill(b), "need", ProRules.Need(f, p, o.Rank).ToString(),
                "points", ProRules.Points(f, p, o.Rank).ToString()));
            string needs = Needs(p, o.Rank);
            if (needs.Length > 0)
            {
                box.Line(CareersText.T("reallife_career_post_needs", "needs", needs));
            }
            if (o.Via == (int)Via.Referral)
            {
                box.Line(CareersText.T("reallife_career_post_referral"));
            }
            if (!applied && ProRules.Qualified(f, p, o.Rank) && o.Via != (int)Via.Headhunter)
            {
                Candidate c = _director.CandidateFor(o, template);
                int chance = ProPath.OfferChance(ProRules.HireScore(c));
                box.Line(chance > 0
                    ? CareersText.T("reallife_career_post_chance", "chance", CareersText.Percent(chance))
                    : CareersText.T("reallife_career_post_no_chance", "points", ProPath.PointsShort(c).ToString()));
            }
            Transform buttons = box.ButtonRow();
            int id = o.Id;
            if (!applied)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_career_button_apply", () => Do(() => _director.Apply(id)));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, JobsGuide);
        }

        /// <summary>What a rank needs, in words: the courses of the last rung that names some (any one of them), and the certificates up to it.</summary>
        private static string Needs(Profession p, int rank)
        {
            var b = new System.Text.StringBuilder(96);
            int last = -1;
            for (int r = 0; r <= rank && r < p.Ranks.Length; r++)
            {
                last = p.Ranks[r].Quals.Length > 0 ? r : last;
            }
            if (last >= 0)
            {
                int[] quals = p.Ranks[last].Quals;
                for (int i = 0; i < quals.Length; i++)
                {
                    if (i > 0)
                    {
                        b.Append(' ').Append(CareersText.T("reallife_campus_or")).Append(' ');
                    }
                    b.Append(CareersText.Course(quals[i]));
                }
            }
            int seen = 0;
            for (int r = 0; r <= rank && r < p.Ranks.Length; r++)
            {
                int cert = p.Ranks[r].Cert;
                if (cert >= 0 && (seen & (1 << cert)) == 0)
                {
                    seen |= 1 << cert;
                    if (b.Length > 0)
                    {
                        b.Append(", ");
                    }
                    b.Append(TechText.Cert(cert));
                }
            }
            return b.ToString();
        }

        // ---------------------------------------------------------------- Applications

        private TableSpec WriteApplications(InfoBox box, WorkFile w, int day)
        {
            Application picked = null;
            int id = PickedId(_picked, ApplicationRow), offers = 0;
            for (int i = w.Applications.Count - 1; i >= 0; i--)
            {
                Application a = w.Applications[i];
                TableRow row = Row(ApplicationRow + a.Post.Id);
                row.Set(0, CareersText.Title(a.Post.Prof, a.Post.Rank));
                row.Set(1, CareersText.Employer(a.Post.Employer));
                row.Set(2, CareersText.Day(a.Day), a.Day);
                row.Set(3, CareersText.ViaName(a.Post.Via));
                bool interview = a.Stage == (int)ApplyStep.Interview;
                row.Set(4, interview ? CareersText.T("reallife_campus_row_next", "day", CareersText.Day(a.InterviewDay), "hour", CareersText.Hour(a.InterviewHour)) : "-",
                    interview ? a.InterviewDay : 0);
                row.Set(5, a.Offer > 0 ? CareersText.Rate(Prices.At(a.Offer)) : "-", a.Offer);
                int by = a.Stage == (int)ApplyStep.Sent ? a.Answer : a.Stage == (int)ApplyStep.Offered || interview ? a.Lapse : a.Stage == (int)ApplyStep.Accepted ? a.StartDay : 0;
                row.Set(6, by > 0 ? CareersText.Day(by) : "-", by);
                row.Set(7, CareersText.Step(a.At), a.Stage == (int)ApplyStep.Offered || interview ? TableRow.Green : a.Live ? TableRow.Plain : TableRow.Yellow);
                offers += a.Stage == (int)ApplyStep.Offered ? 1 : 0;
                picked = a.Post.Id == id ? a : picked;
            }
            if (picked != null)
            {
                WriteApplication(box, w, picked, day);
                return CareerColumns.Applications;
            }
            _picked = null;
            box.Title(CareersText.T("reallife_career_applications_title"));
            box.Line(CareersText.Pair("reallife_career_sum_offers", offers.ToString()));
            box.Line(CareersText.T("reallife_career_applications_about", "most", CareersDirector.ApplicationsLive.ToString(), "days", ProRules.OfferDays.ToString()));
            Meta.Guide.Point(box, JobsGuide);
            return CareerColumns.Applications;
        }

        private void WriteApplication(InfoBox box, WorkFile w, Application a, int day)
        {
            int id = a.Post.Id;
            Profession p = a.Post.Def;
            box.Title(CareersText.Title(a.Post.Prof, a.Post.Rank));
            box.Line(CareersText.Fill("reallife_career_app_line", "employer", CareersText.Employer(a.Post.Employer), "day", CareersText.Day(a.Day), "step", CareersText.Step(a.At),
                "score", a.Score.ToString()));
            switch (a.At)
            {
                case ApplyStep.Sent:
                    box.Line(CareersText.T("reallife_career_app_sent", "day", CareersText.Day(a.Answer)));
                    InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_career_button_withdraw", () => Do(() => _director.Decline(id)));
                    break;
                case ApplyStep.Interview:
                    {
                        box.Line(CareersText.Fill("reallife_career_app_interview", "day", CareersText.Day(a.InterviewDay), "hour", CareersText.Hour(a.InterviewHour), "place", _director.InterviewPlace(a)));
                        int minutes;
                        string why = _director.CanInterview(a, out minutes);
                        if (why != null && why != "reallife_work_why_no_interview")
                        {
                            box.Line(CareersText.T(why));
                        }
                        Transform buttons = box.ButtonRow();
                        InfoBox.Button(buttons, InfoBox.Positive, "reallife_career_button_interview", () => Do(() => _director.Interview(id)));
                        InfoBox.Button(buttons, InfoBox.Negative, "reallife_career_button_withdraw", () => Do(() => _director.Decline(id)));
                        break;
                    }
                case ApplyStep.Offered:
                    {
                        box.Line(CareersText.Fill("reallife_career_app_offer", "pay", CareersText.Rate(Prices.At(a.Offer)), "day", CareersText.Day(a.Lapse), "hours", CareersText.Hours(p),
                            "mode", CareersText.WorkWay(a.Post.Work)));
                        if (w.Job != null)
                        {
                            box.Line(CareersText.T("reallife_career_app_notice", "days", ProRules.NoticeDays(w.Job.Rank, w.Job.Post.Rank).ToString(), "employer", CareersText.Employer(w.Job.Post.Employer)));
                        }
                        Transform buttons = box.ButtonRow();
                        InfoBox.Button(buttons, InfoBox.Positive, "reallife_career_button_accept", () => Do(() => _director.Accept(id)));
                        if (a.Asks < ProRules.AsksMost)
                        {
                            InfoBox.Button(buttons, InfoBox.Additional, "reallife_career_button_ask_small", () => Do(() => _director.Ask(id, ProRules.AskSmallBp)));
                            InfoBox.Button(buttons, InfoBox.Additional, "reallife_career_button_ask_large", () => Do(() => _director.Ask(id, ProRules.AskLargeBp)));
                        }
                        InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_career_button_decline", () => Do(() => _director.Decline(id)));
                        break;
                    }
                case ApplyStep.Accepted:
                    box.Line(CareersText.T("reallife_career_app_accepted", "day", CareersText.Day(a.StartDay)));
                    break;
                default:
                    box.Line(CareersText.T("reallife_career_app_over"));
                    break;
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, JobsGuide);
        }

        // ---------------------------------------------------------------- My job

        private TableSpec WriteJob(InfoBox box, WorkFile w, StudyFile f, int day)
        {
            Job j = w.Job;
            _picked = null;
            if (j == null)
            {
                box.Title(CareersText.T("reallife_career_job_none_title"));
                box.Line(CareersText.T(w.Retired > 0 ? "reallife_career_job_retired" : "reallife_career_job_none"));
                NoJobMore(box, w, day);
                Meta.Guide.Point(box, LifeGuide);
                return CareerColumns.Days;
            }
            Profession p = j.Def;
            for (int d = day, n = 0; n < 7; d++, n++)
            {
                if (!ProRules.WorkDay(p, d) || d < j.Hired || (j.NoticeEnd > 0 && d > j.NoticeEnd))
                {
                    continue;
                }
                TableRow row = Row(DayRow + d);
                row.Pick = false;
                row.Set(0, CareersText.Day(d));
                row.Set(1, CareersText.T("reallife_career_row_hours", "from", CareersText.Hour(p.Hour), "to", CareersText.Hour((p.Hour + p.Hours) % 24)));
                row.Set(2, ProRules.FromHome((WorkMode)j.Work, d) ? CareersText.T("reallife_career_row_home") : p.Place == Workplace.Hospital ? CareersText.T("reallife_career_place_hospitals")
                    : WorkGame.Address(j.Post.Street, j.Post.Number));
                bool today = d == day;
                string state = today && j.SettledDay >= day ? CareersText.T("reallife_career_row_done")
                    : today && j.TodayDay == day && j.TodayMinutes > 0 ? CareersText.T("reallife_career_row_part", "time", CareersText.Minutes(j.TodayMinutes))
                    : today ? CareersText.T("reallife_career_row_today") : CareersText.T("reallife_career_row_ahead");
                row.Set(3, state, today ? TableRow.Green : TableRow.Plain);
            }
            box.Title(CareersText.Title(j.Post.Prof, j.Post.Rank));
            if (j.NoticeEnd > 0)
            {
                box.Line(j.Leaving == (int)LeaveReason.Redundant ? CareersText.T("reallife_life_job_redundant", "day", CareersText.Day(j.NoticeEnd))
                    : CareersText.T("reallife_career_job_leaving", "day", CareersText.Day(j.NoticeEnd)));
            }
            box.Line(CareersText.Fill("reallife_career_job_pay", "pay", CareersText.Rate(Prices.At(j.Pay)), "hours", CareersText.Hours(p), "earned", CareersText.Cents(j.Earned)));
            box.Line(CareersText.Fill("reallife_career_job_days", "worked", j.Worked.ToString(), "due", j.Due.ToString(), "late", j.Late.ToString(), "warnings", j.Warnings.ToString(),
                "most", CareersDirector.WarningsToGo.ToString()));
            box.Line(CareersText.Fill("reallife_career_job_at", "employer", CareersText.Employer(j.Post.Employer), "since", CareersText.Day(j.Hired), "mode", CareersText.WorkWay(j.Work)));
            JobMore(box, w, f, j, day);
            int minutes;
            bool late;
            string why = _director.CanWork(out minutes, out late);
            bool canStart = why == null || (why != "reallife_work_why_not_today" && why != "reallife_work_why_done_today");
            if (canStart && why != null)
            {
                box.Line(CareersText.T(why));
            }
            Transform buttons = box.ButtonRow();
            if (canStart)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_career_button_work", () => Do(_director.StartWorkDay));
            }
            JobButtons(buttons, j, day);
            Transform leave = box.ButtonRow();
            if (j.NoticeEnd <= 0)
            {
                string employer = CareersText.Employer(j.Post.Employer);
                InfoBox.Button(leave, InfoBox.Negative, "reallife_career_button_resign", () => GameConfirm.Show(
                    "reallife_career_confirm_resign_title".Localize(new { employer }),
                    "reallife_career_confirm_resign_body".Localize(new { days = ProRules.NoticeDays(j.Rank, j.Post.Rank).ToString() }),
                    () => UiClone.Safe(() => Do(_director.Resign)), null, null, null, true, true));
            }
            InfoBox.Button(leave, InfoBox.Negative, "reallife_career_button_walk_out", () => GameConfirm.Show(
                "reallife_career_confirm_walk_title".Localize(), "reallife_career_confirm_walk_body".Localize(),
                () => UiClone.Safe(() => Do(_director.WalkOut)), null, null, null, true, true));
            Meta.Guide.Point(box, LifeGuide);
            return CareerColumns.Days;
        }

        // ---------------------------------------------------------------- CV

        private TableSpec WriteCv(InfoBox box, WorkFile w, StudyFile f, int day)
        {
            _picked = null;
            if (w.Job != null)
            {
                Job j = w.Job;
                TableRow row = Row(SpellRow + "now");
                row.Pick = false;
                row.Set(0, CareersText.Title(j.Post.Prof, j.Post.Rank));
                row.Set(1, CareersText.Employer(j.Post.Employer));
                row.Set(2, CareersText.Day(j.Hired), j.Hired);
                row.Set(3, CareersText.T("reallife_career_row_now"), day);
                row.Set(4, CareersText.ProName(j.Post.Prof));
                row.Set(5, CareersText.Cents(j.Earned), j.Earned / 100.0);
                row.Kind = CareerColumns.CvJobs;
            }
            for (int i = w.History.Count - 1; i >= 0; i--)
            {
                Spell s = w.History[i];
                TableRow row = Row(SpellRow + i);
                row.Pick = false;
                row.Set(0, CareersText.Title(s.Prof, s.Top));
                row.Set(1, CareersText.Employer(s.Employer));
                row.Set(2, CareersText.Day(s.From), s.From);
                row.Set(3, CareersText.Day(s.To), s.To);
                row.Set(4, CareersText.Fill("reallife_career_row_spell", "leaving", CareersText.Leaving(s.Leaving), "reference", CareersText.Reference(s.Reference)),
                    s.Reference == 1 ? TableRow.Red : s.Reference == 3 ? TableRow.Green : TableRow.Plain);
                row.Set(5, CareersText.Cents(s.Earned), s.Earned / 100.0);
                row.Kind = CareerColumns.CvJobs;
            }
            for (int i = 0; i < f.Results.Count; i++)
            {
                Enrolment e = f.Results[i];
                CourseDef c = e.Def;
                if (c == null || e.Stage != (int)EnrolStage.Passed)
                {
                    continue;
                }
                TableRow row = Row(QualRow + i);
                row.Pick = false;
                row.Set(0, CareersText.Course(c.Id));
                row.Set(1, _director.PlaceOf(c, (StudyMode)e.Mode));
                row.Set(2, CareersText.Day(e.Start), e.Start);
                row.Set(3, CareersText.Day(e.Ended), e.Ended);
                row.Set(4, CareersText.Grade(c, e.Grade), StudyRules.Honours(c, e.Grade) ? TableRow.Green : TableRow.Plain);
                row.Set(5, "-");
                row.NoTotal = true;
                row.Kind = CareerColumns.CvQualifications;
            }
            int good, poor;
            ProRules.References(w, out good, out poor);
            box.Title(CareersText.T("reallife_career_cv_title"));
            box.Line(CareersText.Pair("reallife_career_sum_school", TechText.School(StudyRules.School(f))));
            box.Line(CareersText.Pair("reallife_career_sum_refs", CareersText.Fill("reallife_career_refs", "good", good.ToString(), "poor", poor.ToString())));
            box.Line(CareersText.Pair("reallife_career_sum_earned", CareersText.Cents(w.Earned)));
            box.Line(CareersText.Pair("reallife_career_sum_days", w.DaysWorked.ToString()));
            box.Line(CareersText.Pair("reallife_career_sum_name", CareersBridges.ReputationNow().ToString()));
            CvMore(box, w, f, day);
            box.Line(CareersText.T("reallife_career_cv_about"));
            Meta.Guide.Point(box, JobsGuide);
            return CareerColumns.Cv;
        }

        // ---------------------------------------------------------------- what working life adds (CareerApp.Life.cs)

        partial void NoJobMore(InfoBox box, WorkFile w, int day);

        partial void JobMore(InfoBox box, WorkFile w, StudyFile f, Job j, int day);

        partial void JobButtons(Transform buttons, Job j, int day);

        partial void CvMore(InfoBox box, WorkFile w, StudyFile f, int day);
    }
}
