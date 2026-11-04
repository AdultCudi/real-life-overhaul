using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    // The people beside the player: the standing at work with its colleagues and the other applicants, and the campus's cohorts and tutors.
    // Each has its own line of the save (StandingStore, RosterStore), read when first asked for and written with the careers' own, so an
    // older save loads with both empty and a build without them leaves them as they were. This part is game-free (EconomyCheck runs it as the
    // director's own code: the day turn's weekly write, a worked shift, a review's contest, an interview's weighing, a seat and its refusal);
    // what reaches the game (the colleagues' names, the player's employees, the notices) stands in CareersDirector.Standing.cs.
    public sealed partial class CareersDirector
    {
        private StandingFile _standing;
        private RosterFile _roster;
        private bool _standingRead, _rosterRead;

        // ------------------------------------------------------------------------------------------------- the save

        private void StandingLoad()
        {
            if (_standingRead || Save == null)
            {
                return;
            }
            _standingRead = true;
            _standing = StandingStore.Load(Save.modData, out int _) ?? new StandingFile { Seed = StudySeed, Hired = -1 };
        }

        private void RosterLoad()
        {
            if (_rosterRead || Save == null)
            {
                return;
            }
            _rosterRead = true;
            _roster = RosterStore.Load(Save.modData, out int _) ?? new RosterFile { Seed = StudySeed };
        }

        private void PeopleWrite(Dictionary<string, string> data)
        {
            if (_standingRead && _standing != null)
            {
                data[StandingStore.Key] = StandingStore.Write(_standing);
            }
            if (_rosterRead && _roster != null)
            {
                data[RosterStore.Key] = RosterStore.Write(_roster);
            }
        }

        private void PeopleForget()
        {
            _standing = null;
            _roster = null;
            _standingRead = _rosterRead = false;
            PeopleForgotten();
        }

        // ------------------------------------------------------------------------------------------------- standing at work

        /// <summary>The player's standing and the people beside them,
        /// tied to the job now (a new job starts a new record and keeps the applicants seen).</summary>
        public StandingFile Standing
        {
            get
            {
                StandingLoad();
                Job j = Work != null ? Work.Job : null;
                if (_standing == null || j == null)
                {
                    return null;
                }
                if (!_standing.Of(j))
                {
                    var f = new StandingFile { Seed = StudySeed, Hired = j.Hired, Street = j.Post.Street, Number = j.Post.Number, Employer = j.Post.Employer };
                    f.Others.AddRange(_standing.Others);
                    _standing = f;
                    SeedColleagues(f, j, Save != null ? Save.Day : 0);
                }
                return _standing;
            }
        }

        /// <summary>The standing figures now: the review's own score and what the job measured since the last review.</summary>
        public Standing StandingNow()
        {
            Job j = Work != null ? Work.Job : null;
            return StandingRules.Of(j, j != null ? PerformanceNow(j) : 0);
        }

        /// <summary>The score a review would give today, 0 to 100, as the review works it out.</summary>
        public int PerformanceNow(Job j)
        {
            Profession p = j.Def;
            int score = LifeRules.ReviewScore(j, ProRules.Points(Study, p, j.Post.Rank), ProRules.Need(Study, p, j.Post.Rank),
                (BossKind)j.Boss) + LadderReviewBonus();
            return TalentCatalogue.Clamp(score, 0, 100);
        }

        // Three colleagues from the shift: a lead a rank above, one up for the same next rung, and one beside the player. Their records settle
        // near the player's own, so the contest is a real one.
        private void SeedColleagues(StandingFile f, Job j, int day)
        {
            StandingRules.Seed(f, j.Post.Rank, Math.Max(0, j.Def.Ranks.Length - 1), PerformanceNow(j), day, StudySeed + j.Hired, Names);
            TagPeople(f);
        }

        // The pool people the names were picked from, by name, until the colleagues made with them are tagged with their ids.
        private readonly Dictionary<string, int> _pickedIds = new Dictionary<string, int>(8);

        private void TagPeople(StandingFile f)
        {
            for (int i = 0; i < f.Peers.Count; i++)
            {
                if (f.Peers[i].PersonId == 0 && _pickedIds.TryGetValue(f.Peers[i].Name, out int id))
                {
                    f.Peers[i].PersonId = id;
                }
            }
            _pickedIds.Clear();
        }

        // A worked shift goes into the ring of the last seven with its score and pay (whole money at today's prices, as the job pays it).
        private void RecordShift(Job j, int day, int minutes)
        {
            StandingFile f = Standing;
            if (f != null)
            {
                StandingRules.Shift(f, day, minutes, PerformanceNow(j), (int)((long)Prices.At(j.Pay) * minutes / 6000));
            }
        }

        // Each day: the week's score goes in the ring once a week. Runs on the day turn only.
        private void StandingDay(int day)
        {
            StandingFile f = Standing;
            Job j = Work != null ? Work.Job : null;
            if (f != null && j != null && StandingRules.WeekDue(f, day))
            {
                StandingRules.Week(f, PerformanceNow(j), day);
            }
        }

        // The review's promotion goes to the better record when a colleague is up for the same rung. A colleague who is passed over stays;
        // the one who wins the post leaves the shift and another takes their place.
        private bool StandingDecides(Job j, int day)
        {
            StandingFile f = Standing;
            if (f == null)
            {
                return true;
            }
            Standing now = StandingNow();
            if (now.Measured && now.Teamwork < PostFilterRules.PromotionTeamwork)
            {
                return false;   // an employer does not promote someone the shift does not get on with, whoever else is up for the post
            }
            bool mine = StandingRules.Contest(f, j.Post.Rank, Math.Max(0, j.Def.Ranks.Length - 1), day, StudySeed + day, Names, out Peer rival);
            TagPeople(f);
            if (!mine)
            {
                PassedOver(rival.Name, j.Post.Employer);
            }
            return mine;
        }

        // ------------------------------------------------------------------------------------------------- other applicants

        /// <summary>
        /// The others who applied for a post, made from the post itself (StandingRules.OthersDerive), so every post the board shows has the same
        /// others every time it is read and the one weighed against the player at an interview's end is one the player could see. Others an older
        /// save kept are used as they were.
        /// </summary>
        public int AlsoApplied(Opening o, List<Peer> into)
        {
            into.Clear();
            StandingLoad();
            if (_standing == null || o == null || Save == null)
            {
                return 0;
            }
            if (StandingRules.OthersOf(_standing, o.Id, into) == 0)
            {
                StandingRules.OthersDerive(StudySeed, o, Math.Max(40, Math.Min(85, ProRules.OfferPass)), into);
            }
            for (int i = 0; i < into.Count; i++)
            {
                into[i].Day = Math.Min(into[i].Day, Save.Day);   // they cannot have applied tomorrow
            }
            return into.Count;
        }

        // The employer's weighing at the end of an interview: another applicant who stands better gets the post. Null when the player does.
        private string OtherGotIt(Application a)
        {
            var others = new List<Peer>(StandingRules.OthersPerPostMost);
            if (AlsoApplied(a.Post, others) == 0)
            {
                return null;
            }
            int luck = ProRules.Luck(StudySeed, a.Post.Id, 3, StandingRules.LuckMost);
            int beat = StandingRules.Beaten(a.Score, luck, others, StudySeed, a.Post.Id);
            return beat >= 0 ? others[beat].Name : null;
        }

        // ------------------------------------------------------------------------------------------------- the campus

        /// <summary>The campus's people, read when first asked for.</summary>
        public RosterFile Roster
        {
            get
            {
                RosterLoad();
                return _roster;
            }
        }

        /// <summary>The cohort a course has today, made when first asked for and the same every time (a course with no classes has none).</summary>
        public Intake IntakeOf(CourseDef c)
        {
            RosterFile r = Roster;
            return r == null || c == null || !c.Timetabled || Save == null ? null : RosterRules.Ensure(r, c.Id, Save.Day);
        }

        /// <summary>The cohort the player's enrolment sat in, if it is still kept.</summary>
        public Intake IntakeOf(Enrolment e)
        {
            RosterFile r = Roster;
            return r == null || e == null ? null : r.Find(e.Course, RosterRules.StartOf(e.Start));
        }

        /// <summary>The tutor of a course, made when first asked for.</summary>
        public Tutor TutorOf(CourseDef c)
        {
            RosterFile r = Roster;
            return r == null || c == null ? null : RosterRules.TutorFor(r, c.Id);
        }

        /// <summary>Why a course cannot be enrolled in for the campus's own reasons (a skill floor, no seat), a reason key, or null.</summary>
        private string CampusWhy(CourseDef c)
        {
            if (RosterRules.Floor(c, out int skill, out int need) && Points(skill) < need)
            {
                return "reallife_study_why_floor";
            }
            Intake i = IntakeOf(c);
            return i != null && !RosterRules.CanJoin(i) ? "reallife_study_why_seats" : null;
        }

        // The player has a seat, and an employee of theirs may follow the same course.
        private void CampusEnrolled(CourseDef c, int day)
        {
            Intake i = IntakeOf(c);
            if (i == null)
            {
                return;
            }
            RosterRules.Join(i);
            string who = RosterRules.EmployeeFollows(Roster, i, EmployeeNames(), day);
            if (who != null)
            {
                EmployeeFollowed(who, c.Id);
            }
        }

        /// <summary>The marks of the player's finished courses, for the grade point average and the credits.</summary>
        public int Marks(List<Mark> into)
        {
            into.Clear();
            StudyFile f = Study;
            for (int i = 0; f != null && i < f.Results.Count; i++)
            {
                Enrolment e = f.Results[i];
                if (e.Def != null && (e.Stage == (int)EnrolStage.Passed || e.Stage == (int)EnrolStage.Failed))
                {
                    into.Add(new Mark { Credits = e.Stage == (int)EnrolStage.Passed ? RosterRules.CreditsOf(e.Def) : 0,
                        Points = RosterRules.PointsOf(e.Score), Start = e.Start });
                }
            }
            return into.Count;
        }
    }
}
