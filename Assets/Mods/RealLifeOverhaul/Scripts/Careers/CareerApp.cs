using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>The Career app in the game's frame (GameUi.AppShell): its tabs' tables and the box with the row picked, written only when it opens or changes.</summary>
    public sealed partial class CareerApp
    {
        public const string OpeningRow = "post:", ApplicationRow = "app:", ProfessionRow = "prof:";
        private const string DayRow = "day:", SpellRow = "spell:", QualRow = "qual:", OlderApplicationRow = "appx:";
        private readonly System.Collections.Generic.HashSet<int> _seenPosts = new System.Collections.Generic.HashSet<int>();
        // The Guide's pages the tabs point to at their foot: finding a job, the working life, working for yourself.
        private const string JobsGuide = "reallife_meta_title_careers_jobs", LifeGuide = "reallife_meta_title_careers_life", OwnGuide = "reallife_meta_title_careers_own";

        private static readonly string[] TabKeys =
        {
            "reallife_career_tab_start", "reallife_career_tab_jobs", "reallife_career_tab_applications", "reallife_career_tab_job", "reallife_career_tab_cv",
            "reallife_career_tab_own", "reallife_career_tab_ladder"
        };

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
                    box.Note(CareersText.T("reallife_career_off_body"));
                    _picked = null;
                }
                else
                {
                    UiClone.Words(_shell.Header, w.Job != null
                        ? CareersText.T("reallife_career_head_job", "title", _director.JobTitle(w.Job), "employer", CareersText.Employer(w.Job.Post.Employer))
                        : CareersText.T("reallife_career_head_none"));
                    int day = SaveGameManager.Current.Day;
                    switch (_tab)
                    {
                        case CareersDirector.TabStart: table = WriteStart(box, w, f, day); break;
                        case CareersDirector.TabApplications: table = WriteApplications(box, w, day); break;
                        case CareersDirector.TabJob: table = WriteJob(box, w, day); break;
                        case CareersDirector.TabCv: table = WriteCv(box, w, f, day); break;
                        case CareersDirector.TabOwn: table = WriteOwn(box, w, f, day); break;
                        case CareersDirector.TabLadder: table = WriteLadder(box, w, day); break;
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
                Notifications.Show(NotificationType.Error, why, _director.TakeWhyData(), 8f);
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

        private TableSpec WriteApplications(InfoBox box, WorkFile w, int day)
        {
            Application picked = null;
            int id = PickedId(_picked, ApplicationRow), older = PickedId(_picked, OlderApplicationRow), offers = 0;
            // A post applied to again after a withdrawal has two applications: the newest keeps the post's row (notices open
            // it by the post), an older one gets a row of its own, so a click never lands on the withdrawn one instead.
            _seenPosts.Clear();
            for (int i = w.Applications.Count - 1; i >= 0; i--)
            {
                Application a = w.Applications[i];
                bool newest = _seenPosts.Add(a.Post.Id);
                TableRow row = Row(newest ? ApplicationRow + a.Post.Id : OlderApplicationRow + i);
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
                picked = (newest && a.Post.Id == id) || (!newest && i == older) ? a : picked;
            }
            if (picked != null)
            {
                WriteApplication(box, w, picked, day);
                return CareerColumns.Applications;
            }
            _picked = null;
            box.Title(CareersText.T("reallife_career_applications_title"));
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_career_sum_offers", offers.ToString(), offers > 0 ? CellTone.Green : CellTone.Plain);
            box.Section("reallife_ibox_sec_how");
            box.Note(CareersText.T("reallife_career_applications_about", "most", CareersDirector.ApplicationsLive.ToString(), "days", ProRules.OfferDays.ToString()));
            Meta.Guide.Point(box, JobsGuide);
            return CareerColumns.Applications;
        }

        private void WriteApplication(InfoBox box, WorkFile w, Application a, int day)
        {
            int id = a.Post.Id;
            Profession p = a.Post.Def;
            bool interviewing = a.Stage == (int)ApplyStep.Interview;
            box.Title(CareersText.Title(a.Post.Prof, a.Post.Rank));
            box.Subtitle(CareersText.Fill("reallife_ibox_post_sub", "employer", CareersText.Employer(a.Post.Employer), "place", WorkGame.Address(a.Post.Street, a.Post.Number)));
            box.Chip(CareersText.Step(a.At), a.Stage == (int)ApplyStep.Offered || interviewing ? CellTone.Green : a.Live ? CellTone.Plain : CellTone.Yellow);
            box.Section("reallife_ibox_sec_application");
            box.Row("reallife_career_col_sent", CareersText.Day(a.Day));
            box.Row("reallife_career_col_via", CareersText.ViaName(a.Post.Via));
            box.Meter("reallife_ibox_their_rating", a.Score / 100f, CareersText.T("reallife_ibox_of", "value", a.Score.ToString(), "most", "100"),
                a.Score >= 60 ? CellTone.Green : a.Score >= 40 ? CellTone.Plain : CellTone.Yellow);
            switch (a.At)
            {
                case ApplyStep.Sent:
                    box.Row("reallife_ibox_answer_by", CareersText.Day(a.Answer));
                    InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_career_button_withdraw", () => Do(() => _director.Decline(id)));
                    break;
                case ApplyStep.Interview:
                    {
                        box.Section("reallife_career_col_interview");
                        box.Row("reallife_career_col_day", CareersText.T("reallife_campus_row_next", "day", CareersText.Day(a.InterviewDay), "hour", CareersText.Hour(a.InterviewHour)));
                        box.Row("reallife_career_col_where", _director.InterviewPlace(a));
                        box.Note(CareersText.T("reallife_ibox_interview_early"));
                        int minutes;
                        string why = _director.CanInterview(a, out minutes);
                        if (why != null && why != "reallife_work_why_no_interview")
                        {
                            box.Line(CareersText.T(why), CellTone.Yellow);
                        }
                        Transform buttons = box.ButtonRow();
                        InfoBox.Button(buttons, InfoBox.Positive, "reallife_career_button_interview", () => Do(() => _director.Interview(id)));
                        InfoBox.Button(buttons, InfoBox.Negative, "reallife_career_button_withdraw", () => Do(() => _director.Decline(id)));
                        break;
                    }
                case ApplyStep.Offered:
                    {
                        box.Section("reallife_career_col_offer");
                        box.Row("reallife_career_col_pay", CareersText.T("reallife_career_row_rate", "rate", CareersText.Rate(Prices.At(a.Offer))), CellTone.Green);
                        box.Row("reallife_career_col_hours", CareersText.Week(p));
                        box.Row("reallife_career_col_mode", CareersText.WorkWay(a.Post.Work));
                        box.Row("reallife_ibox_reply_by", CareersText.Day(a.Lapse));
                        if (w.Job != null)
                        {
                            box.Line(CareersText.T("reallife_career_app_notice", "days", ProRules.NoticeDays(w.Job.Rank, w.Job.Post.Rank).ToString(),
                                "employer", CareersText.Employer(w.Job.Post.Employer)), CellTone.Yellow);
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
                    box.Line(CareersText.T("reallife_career_app_accepted", "day", CareersText.Day(a.StartDay)), CellTone.Green);
                    break;
                default:
                    box.Note(CareersText.T("reallife_career_app_over"));
                    break;
            }
            MapButton(box.ButtonRow(), () => _director.ShowPost(a.Post));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, JobsGuide);
        }

        private TableSpec WriteJob(InfoBox box, WorkFile w, int day)
        {
            Job j = w.Job;
            _picked = null;
            if (j == null)
            {
                box.Title(CareersText.T("reallife_career_job_none_title"));
                box.Note(CareersText.T(w.Retired > 0 ? "reallife_career_job_retired" : "reallife_career_job_none"));
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
                row.Set(2, ProRules.FromHome((WorkMode)j.Work, d) ? CareersText.T("reallife_career_row_home") : WorkGame.Address(j.Post.Street, j.Post.Number));
                bool today = d == day;
                string state = today && j.SettledDay >= day ? CareersText.T("reallife_career_row_done")
                    : today && j.TodayDay == day && j.TodayMinutes > 0 ? CareersText.T("reallife_career_row_part", "time", CareersText.Minutes(j.TodayMinutes))
                    : today ? CareersText.T("reallife_career_row_today") : CareersText.T("reallife_career_row_ahead");
                row.Set(3, state, today ? TableRow.Green : TableRow.Plain);
            }
            box.Title(_director.JobTitle(j));
            box.Subtitle(CareersText.Fill("reallife_ibox_post_sub", "employer", CareersText.Employer(j.Post.Employer), "place", WorkGame.Address(j.Post.Street, j.Post.Number)));
            if (j.NoticeEnd > 0)
            {
                bool cut = j.Leaving == (int)LeaveReason.Redundant;
                box.Chip(CareersText.T(cut ? "reallife_ibox_chip_cut" : "reallife_ibox_chip_leaving"), cut ? CellTone.Red : CellTone.Yellow);
                box.Line(cut ? CareersText.T("reallife_life_job_redundant", "day", CareersText.Day(j.NoticeEnd))
                    : CareersText.T("reallife_career_job_leaving", "day", CareersText.Day(j.NoticeEnd)), cut ? CellTone.Red : CellTone.Yellow);
            }
            box.Section("reallife_ibox_sec_pay");
            box.Row("reallife_career_col_pay", CareersText.T("reallife_career_row_rate", "rate", CareersText.Rate(Prices.At(j.Pay))));
            box.Row("reallife_career_col_hours", CareersText.Week(p));
            box.Row("reallife_career_col_mode", CareersText.WorkWay(j.Work));
            box.Row("reallife_ibox_since", CareersText.Day(j.Hired));
            box.Row("reallife_ibox_earned_here", CareersText.Cents(j.Earned));
            box.Section("reallife_ibox_sec_record");
            box.Meter("reallife_ibox_days_worked", LookRules.Fill(j.Worked, j.Due), CareersText.T("reallife_ibox_of", "value", j.Worked.ToString(), "most", j.Due.ToString()),
                j.Worked >= j.Due ? CellTone.Green : CellTone.Yellow);
            box.Row("reallife_ibox_late", j.Late.ToString(), j.Late > 0 ? CellTone.Yellow : CellTone.Plain);
            box.Row("reallife_ibox_warnings", CareersText.T("reallife_ibox_of", "value", j.Warnings.ToString(), "most", CareersDirector.WarningsToGo.ToString()),
                j.Warnings > 0 ? CellTone.Red : CellTone.Plain);
            JobMore(box, w, j);
            // Where the desk is and what to do today: the working day starts at the desk, from here only where none could be had.
            box.Section("reallife_career_desk_title");
            box.Line(_director.DeskLine(j));
            string fallback = _director.DeskFallbackWhy(j);
            bool canStart = false;
            if (fallback != null)
            {
                int minutes;
                bool late;
                string why = _director.CanWork(out minutes, out late);
                canStart = why == null || (why != "reallife_work_why_not_today" && why != "reallife_work_why_done_today");
                box.Line(CareersText.T(fallback), CellTone.Yellow);
                if (canStart && why != null)
                {
                    box.Line(CareersText.T(why), CellTone.Yellow);
                }
            }
            else
            {
                string today = _director.DeskToday(j);
                if (today != null)
                {
                    box.Line(today, CellTone.Green);
                }
            }
            Transform buttons = box.ButtonRow();
            if (canStart)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_career_button_work", () => Do(_director.StartWorkDay));
            }
            MapButton(buttons, _director.ShowWork);
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
            box.Note(CareersText.T("reallife_career_cv_about"));
            box.Section("reallife_ibox_sec_record");
            box.Row("reallife_career_sum_school", TechText.School(StudyRules.School(f)));
            box.Row("reallife_career_sum_refs", CareersText.Fill("reallife_career_refs", "good", good.ToString(), "poor", poor.ToString()), poor > good ? CellTone.Yellow : CellTone.Plain);
            box.Row("reallife_career_sum_earned", CareersText.Cents(w.Earned));
            box.Row("reallife_career_sum_days", w.DaysWorked.ToString());
            int name = CareersBridges.ReputationNow();
            box.Row("reallife_career_sum_name", name.ToString(), name > 0 ? CellTone.Green : name < 0 ? CellTone.Red : CellTone.Plain);
            CvMore(box, w);
            Meta.Guide.Point(box, JobsGuide);
            return CareerColumns.Cv;
        }
    }
}
