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
    public sealed partial class CareerLive : IWorkSource
    {
        public static readonly CareerLive Instance = new CareerLive();

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private int _local;
        private int _wanted = -1;
        private CareerFacts _facts = new CareerFacts();
        private int _readGeneration = -1;
        private readonly Dictionary<int, int> _metres = new Dictionary<int, int>(64);

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
            CareersDirector d = CareersDirector.Instance;
            GameInstance save = SaveGameManager.Current;
            if (_facts.Ready && d != null && save != null)
            {
                // the tabs that hold choices the player steps are built for the view
                _facts.Management = ManagementTab(d, view, save.Day);
                _facts.Own = OwnTab(d, d.Work, d.Study, view, save.Day);
                _facts.Cv = CvTab(d, d.Work, d.Study);
            }
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
            UtilitiesWords words = W;
            _facts.Jobs = CareerPages.JobsShell(words);
            _facts.Applications = CareerPages.ApplicationsShell(words);
            _facts.Professions = CareerPages.ProfessionsShell(words);
            _facts.Player = RealLifeOverhaul.Rivals.RivalGame.PlayerName();
            _facts.Day = CareersText.Day(save.Day);
            ReadJob(d, w, save.Day);
            ReadOpenings(d, w, study, save.Day);
            ReadApplications(w, save.Day);
            ReadProfessions(w, study);
            if (_facts.Job == null)
            {
                BestFor(words);
            }
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
                    cj.Shifts.Add(new CareerShift { Day = CareersText.Day(sr.Day), Hours = (sr.Minutes / 60f).ToString("0.#", Inv) + "h",
                        Score = sr.Score, Pay = CareersText.Money(sr.Pay) });
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
                        cj.Theirs = Slice(c.Ring, c.Count);
                    }
                }
                cj.Mine = Slice(f.Ring, f.Count);
            }
            cj.CanAsk = day - j.AskDay >= LifeRules.AskGapDays && j.NoticeEnd <= 0;
            cj.AskWhy = cj.CanAsk ? null : W.T("reallife_work_why_rise", "You asked for a rise not long ago.");
            ReadLife(j, w, day, cj);
            ReadJobActs(d, w, cj, day);
            _facts.Job = cj;
        }

        // What the job lets the player see of the working life: the next review, the boss, overtime, burnout and the pension.
        private void ReadLife(Job j, WorkFile w, int day, CareerJob cj)
        {
            UtilitiesWords words = W;
            cj.Life.Add(new KeyValuePair<string, string>(words.T("reallife_ibox_next_review", "Next review"), CareersText.Day(j.ReviewDay + LifeRules.ReviewEvery(GameCalendar.DaysPerYear))));
            cj.Life.Add(new KeyValuePair<string, string>(words.T("reallife_ibox_boss", "Boss"), CareersText.Boss((BossKind)j.Boss)));
            cj.Life.Add(new KeyValuePair<string, string>(words.T("reallife_ibox_overtime", "Overtime since the review"), CareersText.Minutes(j.Overtime)));
            cj.Life.Add(new KeyValuePair<string, string>(words.T("reallife_life_sum_pension", "Pension pot"), CareersText.Cents(w.Pension)));
            cj.Burnout = Math.Max(0, Math.Min(100, j.Burnout / 10));
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
            cj.Needs.Add(new CareerNeed { Words = W.F("reallife_work_need_days", "{n} days in this role", "n", need.ToString(Inv)),
                Met = had >= need, Have = W.F("reallife_work_you_have", "you have {n}", "n", Math.Max(0, had).ToString(Inv)) });
            int points = ProRules.Points(d.Study, p, next), asked = ProRules.Need(d.Study, p, next);
            cj.Needs.Add(new CareerNeed { Words = W.F("reallife_work_need_skill_points", "Skills at {n} points", "n", asked.ToString(Inv)),
                Met = points >= asked, Have = W.F("reallife_work_you_have", "you have {n}", "n", points.ToString(Inv)) });
            if (r.Quals.Length > 0)
            {
                bool held = false;
                for (int i = 0; i < r.Quals.Length; i++)
                {
                    held |= d.Study.Holds(r.Quals[i]);
                }
                cj.Needs.Add(new CareerNeed { Words = CareersText.Course(r.Quals[0]), Met = held });
            }
            cj.Needs.Add(new CareerNeed { Words = W.F("reallife_work_need_rating", "A review rating of {n}", "n",
                LifeRules.PromoteRating.ToString(Inv)), Met = job.Rating >= LifeRules.PromoteRating,
                Have = job.Rating > 0 ? W.F("reallife_work_you_have", "you have {n}", "n", job.Rating.ToString(Inv)) : null });
        }

        private static int[] Slice(int[] ring, int count)
        {
            var a = new int[Math.Max(0, Math.Min(count, ring.Length))];
            Array.Copy(ring, a, a.Length);
            return a;
        }

        // ------------------------------------------------------------------------------------------------- Jobs

        private int MetresOf(Opening o)
        {
            if (string.IsNullOrEmpty(o.Street))
            {
                return -1;
            }
            int key = o.Id;
            if (!_metres.TryGetValue(key, out int metres))
            {
                metres = WorkGame.MetresFromHome(o.Street, o.Number);
                if (_metres.Count > 256)
                {
                    _metres.Clear();
                }
                _metres[key] = metres;
            }
            return metres;
        }

        private void ReadOpenings(CareersDirector d, WorkFile w, StudyFile study, int day)
        {
            UtilitiesWords words = W;
            WorkTab tab = _facts.Jobs;
            Candidate template = d.Template();
            int mine = w.Job != null ? Prices.At(w.Job.Pay) : 0;
            var others = new List<Peer>(StandingRules.OthersPerPostMost);
            for (int i = 0; i < w.Openings.Count; i++)
            {
                Opening o = w.Openings[i];
                Profession p = o.Def;
                if (p == null || !o.Open(day))
                {
                    continue;
                }
                bool qualified = ProRules.Qualified(study, p, o.Rank), eligible = qualified && d.OpenToMe(o, template), applied = w.Applied(o) != null;
                bool remote = o.Work == (int)WorkMode.Remote;
                bool open = PostFilterRules.Open(remote, !remote && WorkGame.ShutNow(o.Street, o.Number));
                bool near = PostFilterRules.Near(MetresOf(o), remote);
                int pay = Prices.At(o.Pay);
                int against = mine > 0 ? (int)Math.Round((pay - mine) * 100.0 / mine) : 0;
                string status = applied ? words.T("reallife_career_kind_applied", "Applied") : eligible ? words.T("reallife_work_you_qualify", "You qualify") : qualified
                    ? words.T("reallife_work_needs_more", "Needs more") : words.T("reallife_work_needs_licence", "Needs a licence");
                UiRole tone = applied || eligible ? UiRole.Ok : UiRole.Warn;
                string employer = CareersText.Employer(o.Employer), where = WorkGame.Address(o.Street, o.Number), title = CareersText.Title(o.Prof, o.Rank);
                var r = new WorkRow
                {
                    Id = o.Id, Icon = WorkGlyphs.Profession(o.Prof), Name = title, Sub = employer, HeadSub = where, Chip = employer, ChipTone = UiRole.Accent, Status = status, Tone = tone,
                    Cells = new[] { null, where, null, CareersText.Hours(p), CareersText.Rate(pay), mine > 0 ? WorkBuilder.Percent(against) : "-", status },
                    Values = new[] { 0, 0, 0, 0, 0, against < 0 ? -1 : against > 0 ? 1 : 0, 0 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, tone },
                    Mask = (open ? 1 : 0) | (eligible ? 2 : 0) | (near ? 4 : 0),
                    Locate = new WorkAct { Kind = (int)CareerAct.ShowPost, Arg = o.Id, Label = words.T("reallife_career_button_map", "Show on map"), Opens = true }
                };
                r.Figures.Add(new KeyValuePair<string, string>(words.T("reallife_career_col_pay", "Pay"), words.F("reallife_career_row_rate", "{rate} an hour", "rate", CareersText.Rate(pay))));
                r.Figures.Add(new KeyValuePair<string, string>(words.T("reallife_work_col_hours", "Hours"), CareersText.Hours(p)));
                r.Sections.Add(NeedsOf(d, study, o, p, qualified, eligible, w, day));
                if (mine > 0)
                {
                    var against2 = new WorkSection { Part = WorkPart.Pairs, Title = words.T("reallife_work_pay_against", "Against your pay") };
                    against2.Pairs.Add(new KeyValuePair<string, string>(words.T("reallife_work_your_pay_now", "Your pay now"), CareersText.Rate(mine)));
                    against2.Pairs.Add(new KeyValuePair<string, string>(words.T("reallife_work_difference", "The difference"), WorkBuilder.Percent(against)));
                    r.Sections.Add(against2);
                }
                d.AlsoApplied(o, others);
                if (others.Count > 0)
                {
                    var also = new WorkSection { Part = WorkPart.People, Title = words.T("reallife_work_also_applied", "Also applied"),
                        Note = words.F("reallife_work_n_people", "{n} people", "n", others.Count.ToString(Inv)) };
                    for (int k = 0; k < others.Count; k++)
                    {
                        also.People.Add(new WorkPerson { Name = others[k].Name, Role = CareersText.Title(o.Prof,
                            others[k].Rank) + ", " + words.F("reallife_work_performance_n", "performance {n}", "n", others[k].Score.ToString(Inv)),
                            Right = CareersText.Day(others[k].Day), Ring = UiRole.Ok });
                    }
                    r.Sections.Add(also);
                    r.HintLead = words.T("reallife_work_still_apply", "You can still apply.");
                    r.Hint = words.F("reallife_work_weighs_all",
                        "The employer weighs your record, and {name} has applied too. When you get the job, start each shift at your desk.", "name", others[0].Name);
                }
                string why = applied ? words.T("reallife_work_why_applied", "You have applied for that already.") : !qualified ? words.T("reallife_work_why_unqualified",
                    "You do not hold what that post needs.") : !eligible ? words.T("reallife_work_why_skills", "Your skills are too far below what that post asks.") : null;
                r.Acts.Add(new WorkAct { Kind = (int)CareerAct.Apply, Arg = o.Id, Label = words.T("reallife_career_button_apply", "Apply"),
                    Style = UiButtonKind.Primary, Icon = UiIcon.Check, Why = why });
                r.Acts.Add(new WorkAct { Kind = (int)CareerAct.ShowPost, Arg = o.Id, Label = words.T("reallife_career_button_map", "Show on map"),
                    Style = UiButtonKind.Secondary, Icon = UiIcon.Locate, Opens = true });
                tab.Rows.Add(r);
            }
        }

        // What a post asks as check lines: the qualification, the skill points, the employer's own wish and, for a step up at the player's own employer, the
        // performance and teamwork the promotion goes by.
        private WorkSection NeedsOf(CareersDirector d, StudyFile study, Opening o, Profession p, bool qualified, bool eligible, WorkFile w, int day)
        {
            UtilitiesWords words = W;
            var sec = new WorkSection { Part = WorkPart.Checks, Title = words.T("reallife_work_what_it_needs", "What it needs") };
            ProRank rank = p.Ranks[Math.Max(0, Math.Min(p.Ranks.Length - 1, o.Rank))];
            if (rank.Quals.Length > 0)
            {
                bool held = false;
                for (int i = 0; i < rank.Quals.Length; i++)
                {
                    held |= study.Holds(rank.Quals[i]);
                }
                sec.Checks.Add(new WorkNeed { Words = CareersText.Course(rank.Quals[0]), Met = held });
            }
            int points = ProRules.Points(study, p, o.Rank), asked = ProRules.Need(study, p, o.Rank);
            sec.Checks.Add(new WorkNeed { Words = words.F("reallife_work_need_skill_points", "Skills at {n} points", "n", asked.ToString(Inv)), Met = points >= asked,
                Have = points >= asked ? null : words.F("reallife_work_you_have", "you have {n}", "n", points.ToString(Inv)) });
            if (rank.Days > 0)
            {
                int months = Math.Max(1, LifeRules.Paced(rank.Days, GameCalendar.DaysPerYear) / 30);
                sec.Checks.Add(new WorkNeed { Words = words.F("reallife_work_need_months", "{n} months at the rank below", "n", months.ToString(Inv)), Met = qualified || w.Job == null,
                    Have = null });
            }
            sec.Checks.Add(new WorkNeed { Words = words.T("reallife_work_need_takes_you", "The employer takes people like you"), Met = eligible });
            Job job = w.Job;
            if (job != null && job.Post.Employer == o.Employer && o.Prof == job.Post.Prof && o.Rank > job.Post.Rank)
            {
                // a step up where the player works: the review's own bar for a promotion and the standing with colleagues
                Standing s = d.StandingNow();
                int bar = LifeRules.RatingFrom[LifeRules.PromoteRating - 1];
                sec.Checks.Add(new WorkNeed { Words = words.F("reallife_work_need_performance", "Performance {n}", "n", bar.ToString(Inv)), Met = s.Performance >= bar,
                    Have = s.Performance >= bar ? null : words.F("reallife_work_you_have", "you have {n}", "n", s.Performance.ToString(Inv)) });
                sec.Checks.Add(new WorkNeed { Words = words.F("reallife_work_need_teamwork", "Teamwork {n}", "n", PostFilterRules.PromotionTeamwork.ToString(Inv)),
                    Met = s.Teamwork >= PostFilterRules.PromotionTeamwork,
                        Have = s.Teamwork >= PostFilterRules.PromotionTeamwork ? null : words.F("reallife_work_you_have", "you have {n}", "n", s.Teamwork.ToString(Inv)) });
            }
            int met = 0;
            for (int i = 0; i < sec.Checks.Count; i++)
            {
                met += sec.Checks[i].Met ? 1 : 0;
            }
            sec.Note = words.F("reallife_work_needs_met", "{met} of {all} met", "met", met.ToString(Inv), "all", sec.Checks.Count.ToString(Inv));
            return sec;
        }

        // The vacancies a player with no job could go for first: the ones they qualify for, best pay first.
        private void BestFor(UtilitiesWords words)
        {
            var rows = new List<WorkRow>(_facts.Jobs.Rows);
            rows.RemoveAll(r => (r.Mask & 2) == 0);
            rows.Sort((a, b) => string.CompareOrdinal(b.Cells[4], a.Cells[4]));
            for (int i = 0; i < rows.Count && i < 5; i++)
            {
                WorkRow r = rows[i];
                _facts.Best.Add(new WorkRow { Id = r.Id, Icon = r.Icon, Name = r.Name, Sub = r.Sub + ", " + r.Cells[1], Price = r.Cells[4] + "/h", Status = r.Cells[6], Tone = UiRole.Ok });
            }
        }

        // ------------------------------------------------------------------------------------------------- Applications, Professions

        private void ReadApplications(WorkFile w, int day)
        {
            UtilitiesWords words = W;
            WorkTab tab = _facts.Applications;
            for (int i = w.Applications.Count - 1; i >= 0; i--)
            {
                Careers.Application a = w.Applications[i];
                string title = CareersText.Title(a.Post.Prof, a.Post.Rank), employer = CareersText.Employer(a.Post.Employer), step = CareersText.Step(a.At);
                UiRole tone = a.Stage == (int)ApplyStep.Offered || a.Stage == (int)ApplyStep.Interview ? UiRole.Ok : a.Live ? UiRole.Accent : UiRole.Warn;
                var r = new WorkRow { Id = a.Post.Id, Icon = WorkGlyphs.Profession(a.Post.Prof), Name = title, Sub = employer, Chip = step, ChipTone = tone, Status = step, Tone = tone,
                    Cells = new[] { null, CareersText.Day(a.Day), a.Live ? CareersText.Day(a.Answer) : "-", step }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, tone } };
                var steps = new WorkSection { Part = WorkPart.Checks, Title = words.T("reallife_work_where_it_stands", "Where it stands") };
                steps.Checks.Add(new WorkNeed { Words = words.F("reallife_work_sent_on", "Sent on {day}", "day", CareersText.Day(a.Day)), Met = true });
                bool interviewed = a.Stage >= (int)ApplyStep.Interview && a.Stage != (int)ApplyStep.Sent;
                steps.Checks.Add(new WorkNeed { Words = words.T("reallife_work_step_interview", "Interview"), Met = interviewed,
                    Have = a.At == ApplyStep.Interview ? CareersText.Day(a.InterviewDay) : null });
                bool offered = a.Stage >= (int)ApplyStep.Offered;
                steps.Checks.Add(new WorkNeed { Words = words.T("reallife_work_step_offer", "Offer"), Met = offered, Have = a.At == ApplyStep.Offered ? CareersText.Rate(Prices.At(a.Offer)) : null });
                steps.Checks.Add(new WorkNeed { Words = words.T("reallife_work_step_start_desk", "Start at your desk"), Met = a.At == ApplyStep.Accepted });
                r.Sections.Add(steps);
                switch (a.At)
                {
                    case ApplyStep.Sent:
                        r.Acts.Add(Act(CareerAct.Withdraw, a.Post.Id, words.T("reallife_career_button_withdraw", "Withdraw"), UiButtonKind.Destructive, UiIcon.Close));
                        break;
                    case ApplyStep.Interview:
                        r.Acts.Add(Act(CareerAct.Interview, a.Post.Id, words.T("reallife_career_button_interview", "Attend the interview"), UiButtonKind.Primary, UiIcon.Check));
                        r.Acts.Add(Act(CareerAct.Withdraw, a.Post.Id, words.T("reallife_career_button_withdraw", "Withdraw"), UiButtonKind.Destructive, UiIcon.Close));
                        break;
                    case ApplyStep.Offered:
                        r.Acts.Add(Act(CareerAct.Accept, a.Post.Id, words.T("reallife_career_button_accept", "Accept"), UiButtonKind.Primary, UiIcon.Check));
                        if (a.Asks < ProRules.AsksMost)
                        {
                            r.Acts.Add(Act(CareerAct.AskSmall, a.Post.Id, words.T("reallife_career_button_ask_small", "Ask for 5% more"), UiButtonKind.Secondary, UiIcon.TrendUp));
                            r.Acts.Add(Act(CareerAct.AskLarge, a.Post.Id, words.T("reallife_career_button_ask_large", "Ask for 10% more"), UiButtonKind.Secondary, UiIcon.TrendUp));
                        }
                        r.Acts.Add(Act(CareerAct.Decline, a.Post.Id, words.T("reallife_career_button_decline", "Turn it down"), UiButtonKind.Destructive, UiIcon.Close));
                        break;
                }
                r.Acts.Add(Act(CareerAct.ShowPost, a.Post.Id, words.T("reallife_career_button_map", "Show on map"), UiButtonKind.Secondary, UiIcon.Locate, true));
                tab.Rows.Add(r);
                _facts.ApplicationsWaiting += a.Live ? 1 : 0;
            }
        }

        private static WorkAct Act(CareerAct act, int arg, string label, UiButtonKind style, UiIcon icon, bool opens = false, string why = null)
        {
            return new WorkAct { Kind = (int)act, Arg = arg, Label = label, Style = style, Icon = icon, Opens = opens, Why = why };
        }

        private void ReadProfessions(WorkFile w, StudyFile study)
        {
            UtilitiesWords words = W;
            WorkTab tab = _facts.Professions;
            Job job = w.Job;
            for (int i = 0; i < ProCatalogue.All.Length; i++)
            {
                Profession p = ProCatalogue.All[i];
                int open = 0;
                for (int k = 0; k < w.Openings.Count; k++)
                {
                    open += w.Openings[k].Prof == p.Id && w.Openings[k].Open(SaveGameManager.Current.Day) ? 1 : 0;
                }
                bool ready = ProRules.Qualified(study, p, 0);
                string status = ready ? words.T("reallife_work_open_to_you", "Open to you") : words.T("reallife_work_needs_course", "Needs a course");
                int pay = Prices.At(p.Ranks[0].Pay);
                var r = new WorkRow { Id = p.Id, Icon = WorkGlyphs.Profession(p.Id), Name = CareersText.ProName(p.Id),
                    Sub = words.F("reallife_work_first_post", "First post: {post}", "post", CareersText.Title(p.Id, 0)),
                    Cells = new[] { null, CareersText.Hours(p), CareersText.Rate(pay), open.ToString(Inv), status }, Values = new[] { 0, 0, 0, Math.Min(100, open * 14), 0 },
                    Status = status, Tone = ready ? UiRole.Ok : UiRole.Warn, Chip = status, ChipTone = ready ? UiRole.Ok : UiRole.Warn };
                var ladder = new WorkSection { Part = WorkPart.Pairs, Title = words.T("reallife_work_the_ladder", "The ladder") };
                for (int rank = 0; rank < p.Ranks.Length; rank++)
                {
                    bool here = job != null && job.Post.Prof == p.Id && job.Post.Rank == rank;
                    ladder.Pairs.Add(new KeyValuePair<string, string>(CareersText.Title(p.Id, rank) + (here ? " (" + words.T("reallife_work_rung_you", "You") + ")" : string.Empty),
                        CareersText.Rate(Prices.At(p.Ranks[rank].Pay))));
                }
                r.Sections.Add(ladder);
                ProRank first = p.Ranks[0];
                var needs = new WorkSection { Part = WorkPart.Checks, Title = words.T("reallife_work_to_start", "To start") };
                if (first.Quals.Length == 0)
                {
                    needs.Checks.Add(new WorkNeed { Words = words.T("reallife_work_no_qualification", "No qualification is asked"), Met = true });
                }
                else
                {
                    bool held = false;
                    for (int q = 0; q < first.Quals.Length; q++)
                    {
                        held |= study.Holds(first.Quals[q]);
                    }
                    needs.Checks.Add(new WorkNeed { Words = CareersText.Course(first.Quals[0]), Met = held });
                }
                r.Sections.Add(needs);
                r.Acts.Add(new WorkAct { Kind = WorkKinds.GoTab, Arg = CareerPages.Jobs, Label = words.T("reallife_career_button_vacancies",
                    "Vacancies"), Style = UiButtonKind.Primary, Icon = UiIcon.Search, Opens = true });
                r.Acts.Add(Act(CareerAct.OpenCampus, p.Id, words.T("reallife_career_button_see_courses", "See the courses"), UiButtonKind.Secondary, UiIcon.Books, true));
                tab.Rows.Add(r);
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
                bool done = Carry((CareerAct)act.Kind, act.Arg, d, view);
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

        private bool Carry(CareerAct act, int arg, CareersDirector d, WorkView view)
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
                default: return CarryMore(act, arg, d, view);
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
