using System;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Tech;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    using Candidate = RealLifeOverhaul.Careers.Candidate;
    using Contract = RealLifeOverhaul.Careers.Contract;
    using Job = RealLifeOverhaul.Careers.Job;
    using Profession = RealLifeOverhaul.Careers.Profession;

    /// <summary>
    /// Reads the Career app's facts from the careers' own records and carries each button to the director that already owns it, as the old
    /// app did: the same methods, the same refusals as notices, the same questions in the game's own box (GameConfirm). Read when the day
    /// series, the hour or a button moves the generation, or the player's choice changes; nothing a frame. The Show the desk and Show on map
    /// buttons only locate; nothing here starts a shift, as the desk does that.
    /// </summary>
    public sealed class CareerLive : IWorkSource
    {
        public static readonly CareerLive Instance = new CareerLive();

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private int _local;
        private int _wanted = -1;
        private CareerFacts _facts = new CareerFacts();
        private int _readGeneration = -1;

        private CareerLive() { }

        public static UtilitiesWords Words() => new UtilitiesWords(UiText.Words, v => FinanceText.Money(v), () => Inv);
        private static UtilitiesWords W => Words();

        public string Id => "career";
        public string Title => W.T("reallife_career_app", "Career");
        public UiIcon Icon => UiIcon.Briefcase;
        public bool Across => false;
        public string T(string key, string english) => UiText.Words(key, english);

        public int Generation
        {
            get
            {
                GameInstance save = SaveGameManager.Current;
                return DaySeriesDirector.Generation + _local + (save != null ? save.Day * 24 + save.Hour : 0);
            }
        }

        /// <summary>A notice asked for a tab: the screen shows it the next time it reads.</summary>
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
            return CareerPages.Nav(_facts, W);
        }

        public WorkPage Page(WorkView view)
        {
            Read();
            return CareerPages.Build(_facts, view, W);
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
            _facts = new CareerFacts();
            CareersDirector d = CareersDirector.Instance;
            GameInstance save = SaveGameManager.Current;
            WorkFile w = d != null ? d.Work : null;
            StudyFile study = d != null ? d.Study : null;
            _facts.Ready = d != null && d.WorkRunning && save != null && w != null && study != null;
            if (!_facts.Ready)
            {
                return;
            }
            _facts.Player = RealLifeOverhaul.Rivals.RivalGame.PlayerName();
            _facts.Day = CareersText.Day(save.Day);
            ReadJob(d, w, save.Day);
            ReadOpenings(d, w, study, save.Day);
            ReadApplications(w, save.Day);
            ReadProfessions(w, study);
            ReadLadder(d);
            ReadOwn(w, save.Day);
            ReadCv(w, study);
        }

        private void ReadJob(CareersDirector d, WorkFile w, int day)
        {
            Job j = w.Job;
            if (j == null)
            {
                if (w.Retired > 0)
                {
                    _facts.Laid = W.T("reallife_career_job_retired", "You have retired.");
                }
                return;
            }
            Profession p = j.Def;
            var cj = new CareerJob
            {
                Title = d.JobTitle(j), Employer = CareersText.Employer(j.Post.Employer), Pay = W.F("reallife_career_row_rate", "{rate} an hour",
                    "rate", CareersText.Rate(Prices.At(j.Pay))),
                Hours = CareersText.Week(p), Months = Math.Max(0, (day - j.Hired) / 30).ToString(Inv) + " " + W.T("reallife_work_months", "months"),
                NextTitle = W.T("reallife_work_next_shift", "Next shift"), NextLine = CareersText.Week(p), Desk = d.DeskLine(j), Leaving = j.NoticeEnd > 0,
                Progress = j.Due > 0 ? Math.Min(100, j.Worked * 100 / Math.Max(1, j.Due)) : -1
            };
            cj.Status = j.NoticeEnd > 0 ? W.T("reallife_ibox_chip_leaving", "Leaving") : j.Late == 0
                && j.Absent == 0 ? W.T("reallife_work_status_on_time", "On time") : W.T("reallife_work_status_late", "Late shifts");
            cj.StatusTone = j.NoticeEnd > 0 || j.Late > 0 ? UiRole.Warn : UiRole.Ok;
            cj.NextNote = WorkGame.Address(j.Post.Street, j.Post.Number);
            Standing s = d.StandingNow();
            if (s.Measured)
            {
                cj.Punctuality = s.Punctuality;
                cj.Reliability = s.Reliability;
                cj.Teamwork = s.Teamwork;
            }
            cj.Performance = s.Performance;
            for (int r = 0; r < p.Ranks.Length; r++)
            {
                cj.Rungs.Add(new CareerRung { Title = CareersText.Title(p.Id, r), Pay = CareersText.Rate(Prices.At(p.Ranks[r].Pay)),
                    State = r < j.Post.Rank ? 0 : r == j.Post.Rank ? 1 : 2 });
            }
            NextRungNeeds(d, w.Job, cj, p, day);
            int weekMinutes = j.WeekOf == day / 7 ? j.WeekMinutes : 0;
            int contract = 0;
            for (int bit = 0; bit < 7; bit++)
            {
                contract += (p.Days >> bit & 1) * p.Hours;
            }
            cj.WeekHours = (weekMinutes / 60).ToString(Inv) + " / " + contract.ToString(Inv);
            cj.WeekPay = CareersText.Cents((long)Prices.At(j.Pay) * weekMinutes / 60);
            StandingFile f = d.Standing;
            if (f != null)
            {
                for (int i = 0; i < f.Shifts.Count; i++)
                {
                    ShiftRecord sr = f.Shifts[i];
                    cj.Shifts.Add(new CareerShift { Day = CareersText.Day(sr.Day), Hours = (sr.Minutes / 60f).ToString("0.#", Inv) + "h", Score = sr.Score, Pay = CareersText.Money(sr.Pay) });
                }
                for (int i = 0; i < f.Peers.Count; i++)
                {
                    Peer c = f.Peers[i];
                    cj.Peers.Add(new CareerPeer { Name = c.Name, Role = CareersText.Title(p.Id, Math.Min(p.Ranks.Length - 1, c.Rank)),
                        Score = c.Score, Team = c.Teamwork, Hue = UiColour.HueOf(c.Name),
                        Tag = c.Role == (int)PeerRole.UpForPost ? W.T("reallife_work_up_for_post",
                            "up for the post") : c.Role == (int)PeerRole.Lead ? W.T("reallife_work_your_lead", "your lead") : null });
                    if (c.Role == (int)PeerRole.UpForPost)
                    {
                        cj.RivalName = c.Name;
                        cj.Theirs = Array.ConvertAll(Slice(c.Ring, c.Count), x => x);
                    }
                }
                cj.Mine = Slice(f.Ring, f.Count);
            }
            _facts.Job = cj;
        }

        // What the next rung asks, from the profession's own ranks: the days at this one, the skills, a qualification and a review's rating.
        private void NextRungNeeds(CareersDirector d, Job job, CareerJob cj, Profession p, int day)
        {
            int next = job.Post.Rank + 1;
            if (next >= p.Ranks.Length)
            {
                return;
            }
            ProRank r = p.Ranks[next];
            cj.NextRung = CareersText.Title(p.Id, next);
            int need = LifeRules.Paced(r.Days, GameCalendar.DaysPerYear), had = day - job.RankDay;
            cj.Needs.Add(new CareerNeed { Words = W.F("reallife_work_need_days", "{n} days in this role", "n", need.ToString(Inv)), Met = had >= need, Have = W.F("reallife_work_you_have", "you have {n}", "n", Math.Max(0, had).ToString(Inv)) });
            int points = ProRules.Points(d.Study, p, next), asked = ProRules.Need(d.Study, p, next);
            cj.Needs.Add(new CareerNeed { Words = W.F("reallife_work_need_skill_points", "Skills at {n} points", "n", asked.ToString(Inv)), Met = points >= asked, Have = W.F("reallife_work_you_have", "you have {n}", "n", points.ToString(Inv)) });
            if (r.Quals.Length > 0)
            {
                bool held = false;
                for (int i = 0; i < r.Quals.Length; i++)
                {
                    held |= d.Study.Holds(r.Quals[i]);
                }
                cj.Needs.Add(new CareerNeed { Words = CareersText.Course(r.Quals[0]), Met = held });
            }
            cj.Needs.Add(new CareerNeed { Words = W.F("reallife_work_need_rating", "A review rating of {n}", "n", LifeRules.PromoteRating.ToString(Inv)), Met = job.Rating >= LifeRules.PromoteRating,
                Have = job.Rating > 0 ? W.F("reallife_work_you_have", "you have {n}", "n", job.Rating.ToString(Inv)) : null });
        }

        private static int[] Slice(int[] ring, int count)
        {
            var a = new int[Math.Max(0, Math.Min(count, ring.Length))];
            Array.Copy(ring, a, a.Length);
            return a;
        }

        private void ReadOpenings(CareersDirector d, WorkFile w, StudyFile study, int day)
        {
            Candidate template = d.Template();
            int mine = w.Job != null ? Prices.At(w.Job.Pay) : 0;
            for (int i = 0; i < w.Openings.Count; i++)
            {
                Opening o = w.Openings[i];
                Profession p = o.Def;
                if (p == null || !o.Open(day))
                {
                    continue;
                }
                bool qualified = ProRules.Qualified(study, p, o.Rank), eligible = qualified && d.OpenToMe(o, template), applied = w.Applied(o) != null;
                var r = new CareerRow
                {
                    Id = o.Id, Icon = UiIcon.Briefcase, Open = true, Qualify = eligible, Near = false,
                    Cells = new[] { CareersText.Title(o.Prof, o.Rank) + ", " + CareersText.Employer(o.Employer), WorkGame.Address(o.Street,
                        o.Number), CareersText.Rate(Prices.At(o.Pay)),
                        mine > 0 ? WorkBuilder.Percent((int)Math.Round((Prices.At(o.Pay) - mine) * 100.0 / mine)) : "-" },
                    Status = applied ? W.T("reallife_career_kind_applied", "Applied") : eligible ? W.T("reallife_work_you_qualify",
                        "You qualify") : qualified ? W.T("reallife_work_needs_more", "Needs more") : W.T("reallife_work_needs_licence", "Needs a licence"),
                    Tone = applied || eligible ? UiRole.Ok : UiRole.Warn,
                    Detail = W.F("reallife_work_post_line", "{title} at {employer}, {where}.", "title", CareersText.Title(o.Prof, o.Rank),
                        "employer", CareersText.Employer(o.Employer), "where", WorkGame.Address(o.Street, o.Number))
                };
                r.Needs.Add(new CareerNeed { Words = W.T("reallife_work_need_qualified", "The qualifications the post asks for"), Met = qualified });
                r.Needs.Add(new CareerNeed { Words = W.T("reallife_work_need_skills", "The skills the post asks for"), Met = eligible });
                var others = new List<Peer>(StandingRules.OthersPerPostMost);
                d.AlsoApplied(o, others);
                for (int k = 0; k < others.Count; k++)
                {
                    r.Others.Add(new CareerPeer { Name = others[k].Name, Role = CareersText.Title(o.Prof, others[k].Rank),
                        Day = CareersText.Day(others[k].Day), Score = others[k].Score, Team = others[k].Teamwork, Hue = UiColour.HueOf(others[k].Name) });
                }
                r.Acts.Add(CareerAct.Apply);
                r.Why.Add(applied ? W.T("reallife_work_why_applied",
                    "You have applied to this post.") : !qualified ? W.T("reallife_work_why_unqualified",
                    "You are not qualified for this post.") : !eligible ? W.T("reallife_work_why_skills",
                    "Your skills are short for this post.") : string.Empty);
                r.Acts.Add(CareerAct.ShowPost);
                r.Why.Add(string.Empty);
                _facts.Jobs.Add(r);
                _facts.OpenNow++;
                _facts.Qualify += eligible ? 1 : 0;
            }
        }

        private void ReadApplications(WorkFile w, int day)
        {
            for (int i = w.Applications.Count - 1; i >= 0; i--)
            {
                Careers.Application a = w.Applications[i];
                var r = new CareerRow
                {
                    Id = a.Post.Id, Icon = UiIcon.ClipboardText, Cells = new[] { CareersText.Title(a.Post.Prof, a.Post.Rank),
                        CareersText.Employer(a.Post.Employer), CareersText.Day(a.Day), CareersText.Step(a.At) },
                    Status = CareersText.Step(a.At), Tone = a.Stage == (int)ApplyStep.Offered
                        || a.Stage == (int)ApplyStep.Interview ? UiRole.Ok : a.Live ? UiRole.Accent : UiRole.Warn,
                    Detail = W.F("reallife_work_app_line", "{title} at {employer}. {step}", "title", CareersText.Title(a.Post.Prof, a.Post.Rank),
                        "employer", CareersText.Employer(a.Post.Employer), "step", CareersText.Step(a.At))
                };
                switch (a.At)
                {
                    case ApplyStep.Sent: r.Acts.Add(CareerAct.Withdraw); r.Why.Add(string.Empty); break;
                    case ApplyStep.Interview: r.Acts.Add(CareerAct.Interview); r.Why.Add(string.Empty); r.Acts.Add(CareerAct.Withdraw); r.Why.Add(string.Empty); break;
                    case ApplyStep.Offered:
                        r.Acts.Add(CareerAct.Accept); r.Why.Add(string.Empty);
                        if (a.Asks < ProRules.AsksMost) { r.Acts.Add(CareerAct.AskSmall); r.Why.Add(string.Empty); r.Acts.Add(CareerAct.AskLarge); r.Why.Add(string.Empty); }
                        r.Acts.Add(CareerAct.Decline); r.Why.Add(string.Empty);
                        break;
                }
                r.Acts.Add(CareerAct.ShowPost);
                r.Why.Add(string.Empty);
                _facts.Applications.Add(r);
                _facts.ApplicationsWaiting += a.Live ? 1 : 0;
            }
        }

        private void ReadProfessions(WorkFile w, StudyFile study)
        {
            for (int i = 0; i < ProCatalogue.All.Length; i++)
            {
                Profession p = ProCatalogue.All[i];
                var r = new CareerRow { Id = p.Id, Icon = UiIcon.Briefcase, Cells = new[] { CareersText.ProName(p.Id), CareersText.Title(p.Id, 0),
                    CareersText.Rate(Prices.At(p.Ranks[0].Pay)),
                        ProRules.Qualified(study, p, 0) ? W.T("reallife_work_open_to_you", "Open to you") : W.T("reallife_work_needs_course",
                            "Needs a course") },
                    Detail = W.F("reallife_work_prof_line", "{name}: the ladder starts at {pay} an hour.", "name", CareersText.ProName(p.Id), "pay",
                        CareersText.Rate(Prices.At(p.Ranks[0].Pay))) };
                r.Acts.Add(CareerAct.OpenCampus);
                r.Why.Add(string.Empty);
                _facts.Professions.Add(r);
            }
        }

        private void ReadLadder(CareersDirector d)
        {
            LadderFile f = d.Ladder;
            if (f == null)
            {
                _facts.ManagementLine = W.T("reallife_ladder_empty", "The management ladder opens when you work at one of the city's companies.");
                return;
            }
            for (int i = 0; i < f.Tasks.Count; i++)
            {
                LadderTask t = f.Tasks[i];
                var r = new CareerRow { Id = t.Id, Icon = UiIcon.People, Cells = new[] { TaskTitle(t), f.Company.Stores.Count > t.Store
                    && t.Store >= 0 ? f.Company.Stores[t.Store].Name : "-", CareersText.Day(t.Day), CareersText.Day(t.Lapse) },
                    Status = W.T("reallife_work_decide", "Decide"), Tone = UiRole.Warn, Detail = TaskTitle(t) };
                r.Acts.Add(CareerAct.TaskYes); r.Why.Add(string.Empty);
                r.Acts.Add(CareerAct.TaskNo); r.Why.Add(string.Empty);
                r.Acts.Add(CareerAct.TaskUp); r.Why.Add(string.Empty);
                _facts.Management.Add(r);
            }
            if (f.Tasks.Count == 0)
            {
                _facts.ManagementLine = W.T("reallife_work_nothing_to_decide",
                    "Nothing waits for your decision. Tasks come from the stores as their staff ask.");
            }
        }

        private static string TaskTitle(LadderTask t) => CareersText.T(t.Kind == (int)TaskKind.Cover ? "reallife_ladder_task_cover"
                : t.Kind == (int)TaskKind.Hire ? "reallife_ladder_task_hire" : "reallife_ladder_task_demand");

        private void ReadOwn(WorkFile w, int day)
        {
            for (int i = 0; i < w.Contracts.Count; i++)
            {
                Contract c = w.Contracts[i];
                var r = new CareerRow { Id = c.Id, Icon = UiIcon.Wrench, Cells = new[] { c.Client, CareersText.ProName(c.Prof),
                    CareersText.Money(c.Fee), CareersText.Day(c.Due) },
                    Status = c.At == ContractStep.Offered ? W.T("reallife_own_kind_open", "Open") : W.T("reallife_work_underway", "Under way"),
                        Tone = UiRole.Accent,
                    Detail = W.F("reallife_work_contract_line", "{client}: {fee}, due {day}.", "client", c.Client, "fee", CareersText.Money(c.Fee),
                        "day", CareersText.Day(c.Due)) };
                if (c.At == ContractStep.Offered)
                {
                    r.Acts.Add(CareerAct.TakeContract); r.Why.Add(string.Empty);
                }
                r.Acts.Add(CareerAct.DropContract); r.Why.Add(string.Empty);
                _facts.Own.Add(r);
            }
        }

        private void ReadCv(WorkFile w, StudyFile study)
        {
            if (w.Job != null)
            {
                _facts.Cv.Add(new CareerRow { Id = 0, Icon = UiIcon.Briefcase, Cells = new[] { CareersText.Title(w.Job.Post.Prof, w.Job.Post.Rank),
                    CareersText.Employer(w.Job.Post.Employer), CareersText.Day(w.Job.Hired), W.T("reallife_career_row_now", "now") },
                    Detail = W.T("reallife_work_cv_now", "Your job now.") });
            }
            for (int i = w.History.Count - 1; i >= 0; i--)
            {
                Spell s = w.History[i];
                _facts.Cv.Add(new CareerRow { Id = 100 + i, Icon = UiIcon.Briefcase, Cells = new[] { CareersText.Title(s.Prof, s.Top),
                    CareersText.Employer(s.Employer), CareersText.Day(s.From), CareersText.Day(s.To) },
                    Detail = CareersText.Fill("reallife_career_row_spell", "leaving", CareersText.Leaving(s.Leaving), "reference",
                        CareersText.Reference(s.Reference)) });
            }
            for (int i = 0; i < study.Results.Count; i++)
            {
                Enrolment e = study.Results[i];
                if (e.Def != null && e.Stage == (int)EnrolStage.Passed)
                {
                    _facts.Cv.Add(new CareerRow { Id = 300 + i, Icon = UiIcon.Cap, Cells = new[] { CareersText.Course(e.Course), "-",
                        CareersText.Day(e.Start), CareersText.Day(e.Ended) }, Detail = CareersText.Grade(e.Def, e.Grade) });
                }
            }
        }

        // ------------------------------------------------------------------------------------------------- doing

        public bool Do(WorkDo act, WorkView view)
        {
            CareersDirector d = CareersDirector.Instance;
            if (d == null)
            {
                return false;
            }
            try
            {
                bool done = Carry((CareerAct)act.Kind, act.Arg, d);
                if (done)
                {
                    _local++;
                }
                return done;
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Warn("Career (kit): " + (CareerAct)act.Kind + " failed: " + e.Message);
                return false;
            }
        }

        private bool Carry(CareerAct act, int arg, CareersDirector d)
        {
            WorkFile w = d.Work;
            switch (act)
            {
                case CareerAct.Apply: return Said(d.Apply(arg), d);
                case CareerAct.Interview: return Said(d.Interview(arg), d);
                case CareerAct.Withdraw:
                case CareerAct.Decline: return Said(d.Decline(arg), d);
                case CareerAct.Accept: return Said(d.Accept(arg), d);
                case CareerAct.AskSmall: return Said(d.Ask(arg, ProRules.AskSmallBp), d);
                case CareerAct.AskLarge: return Said(d.Ask(arg, ProRules.AskLargeBp), d);
                case CareerAct.Register: return Said(d.Register(), d);
                case CareerAct.AskRise: return Said(d.AskRise(), d);
                case CareerAct.TakeContract: return Said(d.TakeContract(arg), d);
                case CareerAct.DropContract: return Said(d.DropContract(arg), d);
                case CareerAct.TaskYes: return Said(d.LadderAnswer(arg, Verdict.Yes, -1), d);
                case CareerAct.TaskNo: return Said(d.LadderAnswer(arg, Verdict.No, -1), d);
                case CareerAct.TaskUp: return Said(d.LadderAnswer(arg, Verdict.Up, -1), d);
                case CareerAct.ShowPost:
                {
                    Opening o = w != null ? w.OpeningOf(arg) : null;
                    Careers.Application a = w != null ? w.ApplicationOf(arg) : null;
                    return Said(d.ShowPost(o ?? (a != null ? a.Post : null)), d);
                }
                case CareerAct.ShowDesk: return Said(d.ShowWork(), d);
                case CareerAct.OpenCampus: d.OpenCampus(CareersDirector.TabCourses, null); return true;
                case CareerAct.Resign:
                    GameConfirm.Show("reallife_career_confirm_resign_title".Localize(new { employer = w != null
                        && w.Job != null ? CareersText.Employer(w.Job.Post.Employer) : string.Empty }),
                        "reallife_career_confirm_resign_body".Localize(new { days = w != null && w.Job != null ? ProRules.NoticeDays(w.Job.Rank,
                            w.Job.Post.Rank).ToString() : "0" }),
                        () => Said(d.Resign(), d), null, null, null, true, true);
                    return true;
                default: return false;
            }
        }

        // A refusal said as the old app said it: a notice with its reason.
        private bool Said(string why, CareersDirector d)
        {
            if (why == null)
            {
                return true;
            }
            Notifications.Show(NotificationType.Error, why, d.TakeWhyData(), 8f);
            return false;
        }
    }
}
