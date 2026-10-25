using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;
using UI.Notification;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The management ladder at the city company the player's job is with (Careers/Ladder): the company seeded from the save when
    /// the job begins, a day of its stores each game morning, the performance score, the recommendation and the promotion at the
    /// job's review, pay by rung, the player's own requests, and the decisions the Career app's Management tab sends. Its own
    /// line of the save; nothing of the game's businesses is written. Once a game day, and a decision when the player takes one;
    /// nothing per frame.
    /// </summary>
    public sealed partial class CareersDirector
    {
        public const int TabLadder = 6;
        private const int LadderCatchUp = 7;   // days of the company run at most after a gap (a long sleep, a save left)

        private LadderFile _ladder;
        private bool _ladderRead, _ladderGone;   // read from the save this session; let go, so the line goes at the next save

        /// <summary>The ladder of the job now, or null: no job at one of the city's companies, the option off, or the part off.</summary>
        public LadderFile Ladder
        {
            get
            {
                Job j = Work != null ? Work.Job : null;
                return Settings.CareersLadder && _ladder != null && _ladder.Of(j) ? _ladder : null;
            }
        }

        private void LadderRead()
        {
            if (_ladderRead || Save == null)
            {
                return;
            }
            _ladderRead = true;
            _ladderGone = false;
            _ladder = LadderStore.Load(Save.modData, out int version);
            if (_ladder != null)
            {
                Logger?.Info("Careers: the management ladder loaded (version " + version + "): " + LadderRanks.Of(_ladder.Rung).Key + " at " + _ladder.Company.Name + ", "
                             + _ladder.Company.Stores.Count + " store(s), " + _ladder.Company.People.Count + " people, " + _ladder.Tasks.Count + " task(s) waiting.");
            }
        }

        private void LadderWrite(Dictionary<string, string> data)
        {
            if (!_ladderRead)
            {
                return;   // never read this session: its line stays as the save has it
            }
            if (_ladder != null)
            {
                data[LadderStore.Key] = LadderStore.Write(_ladder);
            }
            else if (_ladderGone)
            {
                data.Remove(LadderStore.Key);
            }
        }

        private void LadderForget()
        {
            _ladder = null;
            _ladderRead = false;
            _ladderGone = false;
        }

        private static Func<int, string> _nameOf;
        private static Func<int, string> NameOf => _nameOf ?? (_nameOf = LadderGame.NameOf);

        private int Points(int skill) => StudyRules.Points(_study, skill);

        private Func<int, int> _pointsOf;
        private Func<int, int> PointsOf => _pointsOf ?? (_pointsOf = Points);

        /// <summary>The ladder kept to the job: seeded for a job at one of the city's companies, let go with a job that is not.</summary>
        private void LadderKeep(int day)
        {
            LadderRead();
            Job j = _study.Work.Job;
            if (!Settings.CareersLadder)
            {
                return;   // switched off: kept as it was, nothing runs
            }
            if (j == null || !CareerLadderRules.Holds(j))
            {
                if (_ladder != null)
                {
                    _ladder = null;
                    _ladderGone = true;
                }
                return;
            }
            if (_ladder != null && _ladder.Of(j))
            {
                return;
            }
            if (!LadderGame.Read(j, out string key, out string name, out string rival, out List<StoreSeed> stores))
            {
                if (_ladder != null)
                {
                    _ladder = null;
                    _ladderGone = true;
                }
                return;
            }
            var f = new LadderFile();
            CareerLadderRules.Seed(f, j, key, name, rival, stores, StudySeed, NameOf, day);
            _ladder = f;
            _ladderGone = false;
            Logger?.Info("Careers: the management ladder at " + name + " seeded on day " + day + ": " + f.Company.Stores.Count + " store(s) in " + f.Company.Districts.Count
                         + " district(s), " + f.Company.People.Count + " people; the player starts as " + LadderRanks.Of(f.Rung).Key + ".");
            if (Verbose)
            {
                bool ok = f.Company.Stores.Count >= 1 && f.Company.Stores[0].Street == j.Post.Street && CareerLadderRules.StaffAt(f.Company, 0) >= 1;
                Logger?.Info("Careers check (ladder seeded): the job's own store first and staffed, against wanted: " + (ok ? "PASS" : "FAIL, the job's store is not the first or has nobody"));
            }
        }

        /// <summary>The company's days since the last one, each morning: its stores run, the player's request answered, the score marked, one notice.</summary>
        private void LadderDay(int day)
        {
            LadderKeep(day);
            LadderFile f = Ladder;
            Job j = _study.Work.Job;
            if (f == null || j == null)
            {
                return;
            }
            int left = LadderGame.Keep(f);
            var all = new LadderNews();
            for (int d = Math.Max(f.LastDay + 1, day - LadderCatchUp + 1); d <= day; d++)
            {
                LadderNews news = CareerLadderRules.Day(f, StudySeed, d, NameOf);
                all.NewTasks += news.NewTasks;
                all.Lapsed += news.Lapsed;
                all.Left += news.Left;
                all.Hired += news.Hired;
                CareerLadderRules.Mark(f, ProRules.Attendance(j), d);
            }
            all.Left += left;
            int asked = f.Ask;
            if (CareerLadderRules.AnswerAsk(f, StudySeed, day, out bool granted))
            {
                string boss = LadderBoss(f);
                Tell2(granted ? NotificationType.Success : NotificationType.Info, granted ? "reallife_ladder_note_ask_yes" : "reallife_ladder_note_ask_no",
                    Data("boss", boss, "what", WantText(asked)), TabLadder, null);
                if (granted && asked == (int)StaffWant.Shift)
                {
                    KeepJobShift(j, day, false);   // the start an hour earlier or later, where the opening hours have room
                }
            }
            if (all.NewTasks > 0 || all.Lapsed > 0)
            {
                Tell2(all.Lapsed > 0 ? NotificationType.Warning : NotificationType.Info, all.Lapsed > 0 ? "reallife_ladder_note_lapsed" : "reallife_ladder_note_tasks",
                    Data("count", f.Tasks.Count.ToString(), "lapsed", all.Lapsed.ToString(), "company", CareersText.Employer(f.Company.Name)), TabLadder, null);
            }
            if (Verbose)
            {
                CareerLadderRules.UnitFigures(f, day, out int staffing, out int morale, out int stores);
                Logger?.Info("Careers check (ladder day " + day + "): " + LadderRanks.Of(f.Rung).Key + " at " + f.Company.Name + ", " + stores + " store(s) run, staffing " + staffing
                             + "%, morale " + morale + ", score " + CareerLadderRules.ScoreOf(f) + ", " + f.Tasks.Count + " task(s) waiting, " + all.Left + " left, " + all.Hired
                             + " taken on, " + all.Lapsed + " lapsed: " + (staffing >= 0 && staffing <= 100 && morale >= 0
                                 && morale <= 100 ? "PASS" : "FAIL, a figure out of its range"));
            }
        }

        /// <summary>The name of the player's superior, or the company's owner.</summary>
        internal string LadderBoss(LadderFile f)
        {
            Colleague p = CareerLadderRules.Superior(f);
            return p != null ? p.Name : CareersText.T("reallife_ladder_owner");
        }

        // ---------------------------------------------------------------- the review

        /// <summary>What the performance score adds to the job's review (nothing away from a city company).</summary>
        private int LadderReviewBonus() => CareerLadderRules.ReviewBonus(Ladder);

        /// <summary>True while the player is a manager on the ladder: the profession's own ranks no longer rise there, the ladder's do.</summary>
        private bool LadderManages() => Ladder != null && Ladder.Rung > (int)Rung.Frontline;

        /// <summary>At the job's review: the superior's recommendation, and true if the player is promoted on the ladder (a rating of 3 or more, and every requirement met).</summary>
        private bool LadderReview(int rating, int day)
        {
            LadderFile f = Ladder;
            if (f == null)
            {
                return false;
            }
            bool was = f.Recommended;
            string why = rating >= 3 ? CareerLadderRules.WhyNot(f, day, Year, PointsOf) : "reallife_ladder_why_rating";
            CareerLadderRules.Reviewed(f);
            if (!was && f.Recommended)
            {
                Tell2(NotificationType.Success, "reallife_ladder_note_recommended", Data("boss", LadderBoss(f)), TabLadder, null);
            }
            if (why == null)
            {
                return true;
            }
            if (why == "reallife_ladder_why_recommend" && f.Recommended)
            {
                return true;   // the recommendation this review brought
            }
            return false;
        }

        /// <summary>The player is promoted a rung on the ladder: its pay, its role and every one of its tools at once.</summary>
        private void LadderPromote(Job j, int day)
        {
            LadderFile f = Ladder;
            if (f == null)
            {
                return;
            }
            RungPower before = f.Def.Powers;
            CareerLadderRules.Promote(f, day, CareerLadderRules.SeatAimed(f, PointsOf), StudySeed, NameOf);
            j.Pay = CareerLadderRules.PayFor(f.Rung, (EmployerKind)j.Post.Kind, j.Pay);
            j.Raises = 0;
            j.NonCompete |= f.Rung >= (int)Rung.StoreManager;
            j.Exclusive |= f.Rung >= (int)Rung.Regional;
            string title = LadderTitle(f), employer = CareersText.Employer(j.Post.Employer);
            Tell2(NotificationType.Success, "reallife_ladder_note_promoted", Data("title", title, "employer", employer, "pay", CareersText.Rate(Prices.At(j.Pay))),
                TabLadder, null);
            CareersGame.Mood(ModState.MoodUp);
            if (f.Rung == (int)Rung.Executive)
            {
                Story("reallife_life_news_top", "name", PlayerName(), "title", title + ", " + employer);
            }
            Logger?.Info("Careers: promoted on the ladder at " + f.Company.Name + " on day " + day + " to " + LadderRanks.Of(f.Rung).Key + ", " + j.Pay + " cents an hour at the start's prices.");
            if (Verbose)
            {
                bool ok = (f.Def.Powers & before) == before && f.Def.Powers != before && f.Tasks.Count == 0;
                Logger?.Info("Careers check (ladder promotion): to " + LadderRanks.Of(f.Rung).Key + ", the tools of the rung below kept and its own added: " + (ok ? "PASS" : "FAIL"));
            }
        }

        /// <summary>What a demand or request is for, in words: "a raise", "better equipment".</summary>
        internal static string WantText(int want) => CareersText.T(LadderRanks.Key("want", ((StaffWant)want).ToString().ToLowerInvariant()));

        /// <summary>The player's title on the ladder: the rung's, an executive's by the seat.</summary>
        public static string LadderTitle(LadderFile f) => f != null ? CareersText.T(LadderRanks.TitleKey(f.Rung, (ExecSeat)f.Seat)) : string.Empty;

        /// <summary>The job's title as the Career app heads it: the ladder's for a manager, else the profession's rank.</summary>
        public string JobTitle(Job j)
        {
            LadderFile f = Ladder;
            return f != null && f.Rung > (int)Rung.Frontline ? LadderTitle(f) : CareersText.Title(j.Post.Prof, j.Post.Rank);
        }

        // ---------------------------------------------------------------- what the ladder does to the job

        /// <summary>Hours the job's start moves by a shift request granted (CareersDirector.Hours), 0 away from the ladder.</summary>
        private int LadderPrefer(Job j) => Ladder != null && Ladder.Of(j) ? Ladder.Prefer : 0;

        /// <summary>Thousandths of what an hour's work teaches: more with the equipment asked for.</summary>
        private int LadderLearn() => Ladder != null && Ladder.Equipped ? CareerLadderRules.EquippedLearn : 1000;

        /// <summary>Basis points the employer adds to the pension with the insurance asked for.</summary>
        private int LadderPension() => Ladder != null && Ladder.Insured ? CareerLadderRules.InsuredPensionBp : 0;

        /// <summary>A working day worked at the store: a manager on the floor lifts the staff, and a manager's work teaches the rung's own skills.</summary>
        private void LadderWorked(Job j, int minutes, int day)
        {
            LadderFile f = Ladder;
            if (f == null || f.Rung == (int)Rung.Frontline)
            {
                return;
            }
            if (f.Rung <= (int)Rung.StoreManager && j.TodayDay == day && j.TodayMinutes * 1000 >= j.Def.Hours * 60 * ProRules.CountedShare && f.Company.Stores.Count > 0)
            {
                f.Company.Stores[0].Present = day;
            }
            LadderRanks.SkillsOf(f.Rung, (ExecSeat)f.Seat, out int a, out int b);
            int hours = Math.Max(1, minutes / 60);
            if (a >= 0)
            {
                LearnAtWork(a, hours, day);
                LearnAtWork(b, hours, day);
            }
        }

        // ---------------------------------------------------------------- the Career app's decisions

        private string LadderDo(Func<LadderFile, string> act)
        {
            LadderFile f = Ladder;
            if (f == null)
            {
                return "reallife_ladder_why_none";
            }
            string why = act(f);
            if (why == null)
            {
                StudyRules.Use(_study, TalentCatalogue.Leadership, Save.Day);
                Changed();
            }
            return why;
        }

        /// <summary>A task decided: yes (with the person or candidate picked), no, or passed up.</summary>
        public string LadderAnswer(int task, Verdict choice, int pick) => LadderDo(f => CareerLadderRules.Answer(f, task, choice, pick, StudySeed, Save.Day, NameOf));

        /// <summary>The player's own request to their superior.</summary>
        public string LadderAsk(StaffWant w, int way)
        {
            string why = LadderDo(f => CareerLadderRules.Ask(f, w, way, Save.Day));
            if (why == null)
            {
                Tell2(NotificationType.Info, "reallife_ladder_note_asked", Data("boss", LadderBoss(Ladder)), TabLadder, null);
            }
            return why;
        }

        public string LadderRota(int rota) => LadderDo(f => CareerLadderRules.SetRota(f, rota));

        public string LadderRaise(int person) => LadderDo(f => CareerLadderRules.RaiseOne(f, person, Save.Day));

        public string LadderMove(int person, int to) => LadderDo(f => CareerLadderRules.Move(f, person, to));

        public string LadderPolicy(PolicyField field, int way) => LadderDo(f => CareerLadderRules.SetPolicy(f, field, way));

        public string LadderAiming(bool on) => LadderDo(f =>
        {
            f.Aiming = on;
            return null;
        });

        public string LadderAimSeat(ExecSeat seat) => LadderDo(f =>
        {
            f.Aim = (int)seat;
            return null;
        });

        /// <summary>A candidate's name, the game's own way, for the Career app.</summary>
        public string LadderCandidateName(int task, int k) => NameOf(CareerLadderRules.CandidateName(StudySeed, task, k));

        /// <summary>The seed the candidates are rolled from, for the Career app's figures.</summary>
        public int LadderSeed => StudySeed;

        /// <summary>Why the player cannot be promoted at the next review now, for the Career app; null if they can.</summary>
        public string LadderWhyNot(LadderFile f) => CareerLadderRules.WhyNot(f, Save.Day, Year, PointsOf);

        /// <summary>The next rung's skills against the player's.</summary>
        public void LadderSkills(LadderFile f, out int have, out int need) => CareerLadderRules.SkillsNeeded(f, PointsOf, out have, out need);

        public ExecSeat LadderSeatAimed(LadderFile f) => CareerLadderRules.SeatAimed(f, PointsOf);

        public int LadderDaysNeeded(LadderFile f) => CareerLadderRules.DaysNeeded(f, Year);
    }
}
