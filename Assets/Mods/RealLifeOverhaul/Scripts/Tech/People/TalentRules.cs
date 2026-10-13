using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What a professional's day was, as the game layer read it: handed to <see cref="TalentRules.Day"/>.</summary>
    public struct WorkDay
    {
        public int Day, Year;        // today, and the game's year in days
        public int Hours;            // hours worked today
        public int WeekHours;        // hours worked this week so far
        public int MainPoints;       // the main skill in whole points
        public bool Mentored;        // a senior of the family works at the same place and has room for one more
        public int Paid, Going;      // cents an hour: what they are paid and the going rate for their role and skill
        public int Esteem;           // the player's standing as an employer, -10 to 10
        public bool OnCall;          // their business promises uptime and they keep it running
        public bool Wanted;          // the arrangement they would like is the one they have
    }

    /// <summary>
    /// The rules of professionals: who a newcomer is, how much they get done, how they learn, how they feel, how they
    /// are reviewed and promoted, what leaving costs, and when a rival comes for them. Game-free and checked by
    /// EconomyCheck; every roll is <see cref="TechRandom"/>'s, so the same save gives the same people.
    /// </summary>
    public static class TalentRules
    {
        // ---------------------------------------------------------------- who they are

        /// <summary>The level a main skill is held at for a role: the highest whose least it reaches, within the role's levels.</summary>
        public static int LevelFor(int mainPoints, RoleDef role)
        {
            int level = role.LeastLevel;
            for (int l = role.LeastLevel; l <= role.MostLevel; l++)
            {
                if (mainPoints >= TalentCatalogue.LevelSkillLeast[l])
                {
                    level = l;
                }
            }
            return level;
        }

        /// <summary>
        /// A role for somebody hired under a game skill: the first of the business's wanted roles their skill can be,
        /// else, by a roll, one of the skill's roles (the first twice as likely). -1 if the skill has no professional roles.
        /// </summary>
        public static int Pick(string gameSkill, int[] wanted, int wantedCount, int seed, int n)
        {
            for (int i = 0; wanted != null && i < wantedCount && i < wanted.Length; i++)
            {
                RoleDef r = TalentCatalogue.Role(wanted[i]);
                if (r.GameSkill == gameSkill && r.InGame)
                {
                    return r.Id;
                }
            }
            var roles = new int[TalentCatalogue.RoleCount];
            int count = TalentCatalogue.RolesOf(gameSkill, roles);
            if (count == 0)
            {
                return -1;
            }
            int roll = TechRandom.Range(seed, n, 11, 0, 0, count);   // count + 1 faces: the first has two
            return roles[roll >= count ? 0 : roll];
        }

        /// <summary>
        /// A newcomer's file for a role at a level: specialisms within the level's band (the second a little lower),
        /// years of experience, a school (more often a degree for the data and design roles, a bootcamp for some
        /// engineers) and, from senior, a certificate in their field now and then. Main is the band's for a professional
        /// of the mod's payroll; for the game's employees the game's skill stands instead.
        /// </summary>
        public static Profile Make(int seed, int n, int role, int level, int day)
        {
            RoleDef r = TalentCatalogue.Role(role);
            level = TalentCatalogue.Clamp(level, r.LeastLevel, r.MostLevel);
            int least = TalentCatalogue.LevelSkillLeast[level], most = TalentCatalogue.LevelSkillMost[level];
            var p = new Profile
            {
                Role = r.Id,
                Level = level,
                Main = TechRandom.Range(seed, n, 1, 0, least, most) * 1000,
                A = TechRandom.Range(seed, n, 2, 0, least, most) * 1000,
                B = Math.Max(5, TechRandom.Range(seed, n, 3, 0, least - 10, most - 5)) * 1000,
                YearsDays = TechRandom.Range(seed, n, 4, 0, TalentCatalogue.LevelYearsLeast[level] * 365, TalentCatalogue.LevelYearsMost[level] * 365),
                School = School(seed, n, r, level),
                Morale = TalentCatalogue.MoraleStart,
                Joined = day,
                LevelDay = day,
                Female = TechRandom.Mille(seed, n, 5) < 500,
                Record = TechRandom.Mille(seed, n, 6) < 40
            };
            p.Age = 21 + p.YearsDays / 365 + TechRandom.Range(seed, n, 7, 0, 0, 6);
            if (level >= TalentCatalogue.Senior)
            {
                for (int c = 0; c < TalentCatalogue.Certs.Length; c++)
                {
                    TalentCatalogue.CertDef cert = TalentCatalogue.Certs[c];
                    int score = p.ScoreOf(cert.Skill);
                    if (score >= cert.Needs && TechRandom.Mille(seed, n, 20 + c) < 350)
                    {
                        p.Certs |= 1 << c;
                    }
                }
            }
            return p;
        }

        private static int School(int seed, int n, RoleDef r, int level)
        {
            int roll = TechRandom.Mille(seed, n, 8);
            bool degree = r.Family == Family.Data || r.Family == Family.Corporate || r.Family == Family.Built || r.Id == TalentCatalogue.ResearchScientist;
            if (r.Id == TalentCatalogue.ResearchScientist)
            {
                return roll < 600 ? TalentCatalogue.Doctorate : TalentCatalogue.Master;
            }
            if (degree)
            {
                return roll < 120 && level >= TalentCatalogue.Senior ? TalentCatalogue.Doctorate : roll < 450 ? TalentCatalogue.Master : TalentCatalogue.Bachelor;
            }
            if (r.Family == Family.Support)
            {
                return roll < 450 ? TalentCatalogue.Diploma : roll < 750 ? TalentCatalogue.SelfTaught : TalentCatalogue.Bachelor;
            }
            return roll < 120 ? TalentCatalogue.SelfTaught : roll < 300 ? TalentCatalogue.Bootcamp : roll < 420 ? TalentCatalogue.Diploma : roll < 850 ? TalentCatalogue.Bachelor : TalentCatalogue.Master;
        }

        // ---------------------------------------------------------------- what they get done

        /// <summary>
        /// A professional's output in thousandths of a mid-level's in good heart: the level's, by the average of the main
        /// skill and the two specialisms (at 50 as written, 0.6 at 0, 1.4 at 100), by morale (0.7 at none, 1.1 at full),
        /// less half the burnout. Nothing while they are off sick (a course is taken outside working hours).
        /// </summary>
        public static int Output(Profile p, int mainPoints, int day)
        {
            if (p.Sick)
            {
                return 0;
            }
            int skill = (mainPoints * 1000 + p.A + p.B) / 3;           // thousandths of a point
            long o = TalentCatalogue.LevelOutput[TalentCatalogue.Clamp(p.Level, 0, TalentCatalogue.Levels - 1)];
            o = o * (600 + skill * 8 / 1000) / 1000;
            o = o * (700 + p.Morale * 400 / 1000) / 1000;
            o = o * (2000 - p.Burnout) / 2000;
            return (int)Math.Max(0, o);
        }

        /// <summary>How often their work needs doing again, thousandths: 300 at a skill of 0, 30 at 100 (the bugs a release ships with, the client's complaints).</summary>
        public static int Faults(Profile p, int mainPoints)
        {
            int skill = (mainPoints * 1000 + p.A * 2) / 3 / 1000;
            return Math.Max(30, 300 - skill * 27 / 10) * (1000 + p.Burnout / 2) / 1000;
        }

        // ---------------------------------------------------------------- learning

        /// <summary>
        /// A day's learning on the job: each specialism grows by the hours worked, less as it nears 100, faster from a
        /// better school and with a mentor. With a mentor the main skill of the game's own grows too (on top of the
        /// game's growth): the thousandths of a point it adds are handed back.
        /// </summary>
        public static int Learn(Profile p, int hours, bool mentored)
        {
            if (hours <= 0)
            {
                return 0;
            }
            int school = TalentCatalogue.SchoolLearning[TalentCatalogue.Clamp(p.School, 0, TalentCatalogue.Schools - 1)];
            int mentor = mentored ? 1000 + TalentCatalogue.MentorBonus : 1000;
            p.A = Grow(p.A, hours, school, mentor);
            p.B = Grow(p.B, hours, school, mentor);
            if (!mentored)
            {
                return 0;
            }
            return hours * TalentCatalogue.MainLearnPerHour;
        }

        private static int Grow(int score, int hours, int school, int mentor)
        {
            long gain = (long)hours * TalentCatalogue.LearnPerHour * (100000 - score) / 100000;
            gain = gain * school / 1000 * mentor / 1000;
            return (int)Math.Min(100000, score + Math.Max(0, gain));
        }

        // ---------------------------------------------------------------- how they feel

        /// <summary>
        /// The day's move of morale and burnout. Burnout builds for each hour worked over a day's share of the long week
        /// (and more for whoever is on call), and eases on a lighter day and more on a day off. Morale moves an eighth of
        /// the way a day to where pay, standing, burnout, recognition and the arrangement put it.
        /// </summary>
        public static void Mood(Profile p, in WorkDay d)
        {
            int dayShare = TalentCatalogue.BurnoutLong / 5;
            if (d.Hours <= 0)
            {
                p.Burnout -= TalentCatalogue.BurnoutRest * 2;
            }
            else if (d.Hours > dayShare || d.WeekHours > TalentCatalogue.BurnoutLong)
            {
                p.Burnout += Math.Max(1, d.Hours - dayShare) * TalentCatalogue.BurnoutPerHour;
            }
            else
            {
                p.Burnout -= TalentCatalogue.BurnoutRest;
            }
            if (d.OnCall)
            {
                p.Burnout += 6;
            }
            p.Burnout = TalentCatalogue.Clamp(p.Burnout, 0, 1000);
            int target = MoraleTarget(p, d);
            p.Morale = TalentCatalogue.Clamp(p.Morale + (target - p.Morale) / 8, 0, 1000);
        }

        /// <summary>Where morale is heading, thousandths: 600 as a start, moved by pay against the going rate, standing, burnout, a recent rise and the arrangement.</summary>
        public static int MoraleTarget(Profile p, in WorkDay d)
        {
            int target = 600;
            if (d.Going > 0)
            {
                target += TalentCatalogue.Clamp((int)((long)(d.Paid - d.Going) * 2000 / d.Going), -250, 200);
            }
            target += TalentCatalogue.Clamp(d.Esteem, -10, 10) * 15;
            target -= p.Burnout / 3;
            if (d.Day - p.RecognisedDay < TalentCatalogue.Scaled(60, d.Year))
            {
                target += 80;
            }
            target += d.Wanted ? 40 : -30;
            if (p.Rating == 1)
            {
                target -= 60;
            }
            if (p.NoticeEnd > 0)
            {
                target -= 150;
            }
            return TalentCatalogue.Clamp(target, 0, 1000);
        }

        /// <summary>The game's satisfaction a day moves by because of morale: a few points towards it at most (the game's own figure stays the game's).</summary>
        public static float GameMood(Profile p, float satisfaction)
        {
            float want = p.Morale / 10f;
            float step = TalentCatalogue.MoodToGame;
            return Math.Max(-step, Math.Min(step, (want - satisfaction) / 4f));
        }

        /// <summary>A burnt-out professional goes off sick for the day now and then: 0 to 999 against this.</summary>
        public static int SickChance(Profile p)
        {
            return p.Burnout <= TalentCatalogue.BurnoutSick ? 0 : (p.Burnout - TalentCatalogue.BurnoutSick) / 2;
        }

        /// <summary>True if burnout makes them hand in their notice today.</summary>
        public static bool BurnsOut(int seed, Profile p, int day)
        {
            return p.Burnout > TalentCatalogue.BurnoutQuit && p.NoticeEnd == 0 && TechRandom.Mille(seed, TechRandom.Of(p.Id), day, 31) < 30;
        }

        // ---------------------------------------------------------------- reviews and promotion

        /// <summary>The first review: half a year after joining.</summary>
        public static int FirstReview(int day, int year)
        {
            return day + Math.Max(7, year / TalentCatalogue.ReviewsAYear);
        }

        /// <summary>
        /// The review: a rating of 1 to 5 from how the main skill and the specialisms stand against the level's band,
        /// morale and burnout, and a little of the reviewer's mood (the roll). A 4 asks for a rise, a 5 for twice that.
        /// Ratings of 1 in a row are counted: two make a dismissal fair.
        /// </summary>
        public static int Review(Profile p, int seed, int day, int year, int mainPoints)
        {
            int level = TalentCatalogue.Clamp(p.Level, 0, TalentCatalogue.Levels - 1);
            int least = TalentCatalogue.LevelSkillLeast[level], most = TalentCatalogue.LevelSkillMost[level];
            int skill = (mainPoints * 2 + p.A / 1000 + p.B / 1000) / 4;
            int place = most > least ? (skill - least) * 1000 / (most - least) : 500;   // 0 at the band's foot, 1000 at its top
            int score = place / 2 + p.Morale / 4 - p.Burnout / 4 + TechRandom.Range(seed, TechRandom.Of(p.Id), day, 41, -120, 120) + 125;
            int rating = score < 150 ? 1 : score < 330 ? 2 : score < 560 ? 3 : score < 760 ? 4 : 5;
            p.Rating = rating;
            p.LowRuns = rating == 1 ? p.LowRuns + 1 : 0;
            p.RaiseAsk = rating == 4 ? TalentCatalogue.ReviewRiseBp : rating == 5 ? TalentCatalogue.ReviewRiseBp * 2 : 0;
            p.AskEnd = p.RaiseAsk > 0 ? day + TalentCatalogue.AskDays : 0;
            p.ReviewDay = day + Math.Max(7, year / TalentCatalogue.ReviewsAYear);
            return rating;
        }

        /// <summary>True if a promotion is open to them: a rating of 4 or more, long enough at the level, the main skill near the next level's band and a level left in the role.</summary>
        public static bool CanPromote(Profile p, int day, int year, int mainPoints)
        {
            RoleDef r = p.Def;
            return p.Rating >= TalentCatalogue.PromoteRating && p.Level < r.MostLevel && p.Level + 1 < TalentCatalogue.Levels && p.NoticeEnd == 0
                   && day - p.LevelDay >= TalentCatalogue.Scaled(TalentCatalogue.PromoteDaysInLevel, year)
                   && mainPoints >= TalentCatalogue.LevelSkillLeast[p.Level + 1] - 5;
        }

        public static void Promote(Profile p, int day)
        {
            p.Level = Math.Min(p.Level + 1, p.Def.MostLevel);
            p.LevelDay = day;
            p.RecognisedDay = day;
            p.RaiseAsk = 0;
            p.Morale = Math.Min(1000, p.Morale + 120);
        }

        /// <summary>A rise given: recognised, and what the last review asked for is settled.</summary>
        public static void Raised(Profile p, int day, int bp)
        {
            p.RecognisedDay = day;
            p.Morale = Math.Min(1000, p.Morale + Math.Min(150, bp / 10));
            if (bp >= p.RaiseAsk)
            {
                p.RaiseAsk = 0;
            }
        }

        /// <summary>A request for a rise refused, or left unanswered until it lapsed: morale falls.</summary>
        public static void Refused(Profile p)
        {
            p.RaiseAsk = 0;
            p.AskEnd = 0;
            p.Morale = Math.Max(0, p.Morale - TalentCatalogue.RefuseMorale);
        }

        /// <summary>The arrangement a professional would like: on site, hybrid or from home (hybrid where the role cannot be done from home).</summary>
        public static Arrangement Wants(Profile p, int seed)
        {
            int roll = TechRandom.Mille(seed, TechRandom.Of(p.Id), 71);
            if (roll < 200)
            {
                return Arrangement.OnSite;
            }
            return roll < 750 || !p.Def.Remote ? Arrangement.Hybrid : Arrangement.Remote;
        }

        /// <summary>True if the arrangement they have is the one they want; hybrid will do for someone who would rather be at home but whose job is in the building.</summary>
        public static bool Content(Profile p, int seed)
        {
            Arrangement want = Wants(p, seed), have = (Arrangement)p.Work;
            return want == have || (want == Arrangement.Remote && have == Arrangement.Hybrid && !p.Payroll);
        }

        /// <summary>A role changed within the skill: the level is kept inside the new role's.</summary>
        public static void Move(Profile p, int role)
        {
            RoleDef r = TalentCatalogue.Role(role);
            p.Role = r.Id;
            p.Level = TalentCatalogue.Clamp(p.Level, r.LeastLevel, r.MostLevel);
        }

        // ---------------------------------------------------------------- notice, probation and severance

        public static bool OnProbation(Profile p, int day, int year)
        {
            return day - p.Joined < TalentCatalogue.Scaled(TalentCatalogue.ProbationDays, year);
        }

        /// <summary>Whole years of service, counted in the game's years.</summary>
        public static int ServiceYears(Profile p, int day, int year)
        {
            return Math.Max(0, day - p.Joined) / Math.Max(60, year);
        }

        /// <summary>The notice either side gives: a week on probation, else a week for each year of service, between a week and twelve.</summary>
        public static int NoticeDays(Profile p, int day, int year)
        {
            if (OnProbation(p, day, year))
            {
                return TalentCatalogue.ProbationNotice;
            }
            return TalentCatalogue.Clamp(TalentCatalogue.NoticePerYear * Math.Max(1, ServiceYears(p, day, year)), TalentCatalogue.NoticeLeast, TalentCatalogue.NoticeMost);
        }

        /// <summary>Severance on a lay-off: a week's pay for each year of service, two weeks at least; none on probation, for a fair dismissal or for whoever resigns.</summary>
        public static long Severance(Profile p, int day, int year, int weekCents, LeaveKind why)
        {
            if (why != LeaveKind.LaidOff || OnProbation(p, day, year))
            {
                return 0;
            }
            int weeks = Math.Max(TalentCatalogue.SeveranceLeastWeeks, ServiceYears(p, day, year) * TalentCatalogue.SeveranceWeeksPerYear);
            return (long)weeks * Math.Max(0, weekCents);
        }

        /// <summary>True if a dismissal would be fair: still on probation, or two ratings of 1 in a row.</summary>
        public static bool FairDismissal(Profile p, int day, int year)
        {
            return OnProbation(p, day, year) || p.LowRuns >= TalentCatalogue.LowRatingsToGo;
        }

        /// <summary>Notice is served: the day they go and why.</summary>
        public static void Notice(Profile p, int day, int year, LeaveKind why)
        {
            p.Leaving = (int)why;
            p.NoticeEnd = day + (why == LeaveKind.Dismissed ? 0 : NoticeDays(p, day, year));
        }

        // ---------------------------------------------------------------- courses and certificates

        /// <summary>Points a course adds to a specialism held at a score: eight from nothing, fewer near the top, two at least.</summary>
        public static int CourseGain(int scorePoints)
        {
            return Math.Max(2, TalentCatalogue.CourseGain * (100 - TalentCatalogue.Clamp(scorePoints, 0, 100)) / 100);
        }

        /// <summary>A course's fee at the start's prices: by the points it adds, dearer the better they are already.</summary>
        public static int CourseFee(int scorePoints)
        {
            return CourseGain(scorePoints) * TalentCatalogue.CourseFee * (1000 + TalentCatalogue.Clamp(scorePoints, 0, 100) * 10) / 1000;
        }

        public static void StartCourse(Profile p, int slot, int day, int exam)
        {
            p.CourseSkill = slot == 0 ? 0 : 1;
            p.CourseEnd = day + (exam >= 0 ? TalentCatalogue.ExamDays : TalentCatalogue.CourseDays);
            p.Exam = exam;
        }

        /// <summary>The per cent chance of passing a certificate's exam: 55 at the score it needs, two more a point over, between 5 and 95; none under it.</summary>
        public static int ExamChance(Profile p, int cert)
        {
            TalentCatalogue.CertDef c = TalentCatalogue.Certs[TalentCatalogue.Clamp(cert, 0, TalentCatalogue.CertCount - 1)];
            int score = p.ScoreOf(c.Skill);
            if (score < c.Needs)
            {
                return 0;
            }
            return TalentCatalogue.Clamp(TalentCatalogue.ExamPassBase + 2 * (score - c.Needs), 5, 95);
        }

        /// <summary>
        /// A course or exam that has ended today: a course adds its points to the specialism, an exam is passed or not by
        /// the roll. 1 for a course done, 2 for an exam passed, 3 for one failed; 0 if nothing ended.
        /// </summary>
        public static int EndCourse(Profile p, int seed, int day)
        {
            if (p.CourseSkill < 0 || day < p.CourseEnd)
            {
                return 0;
            }
            int result;
            if (p.Exam >= 0)
            {
                bool pass = TechRandom.Range(seed, TechRandom.Of(p.Id), day, 51, 1, 100) <= ExamChance(p, p.Exam);
                if (pass)
                {
                    p.Certs |= 1 << p.Exam;
                    p.Morale = Math.Min(1000, p.Morale + TalentCatalogue.ExamMoraleUp);
                }
                result = pass ? 2 : 3;
            }
            else
            {
                int score = p.Slot(p.CourseSkill) / 1000;
                int gained = Math.Min(100000, p.Slot(p.CourseSkill) + CourseGain(score) * 1000);
                if (p.CourseSkill == 0)
                {
                    p.A = gained;
                }
                else
                {
                    p.B = gained;
                }
                result = 1;
            }
            p.CourseSkill = -1;
            p.Exam = -1;
            return result;
        }

        /// <summary>What the certificates held add to a professional's going rate, basis points.</summary>
        public static int CertPayBp(Profile p)
        {
            int bp = 0;
            for (int c = 0; c < TalentCatalogue.CertCount; c++)
            {
                bp += p.Has(c) ? TalentCatalogue.Certs[c].PayBp : 0;
            }
            return Math.Min(1500, bp);
        }

        // ---------------------------------------------------------------- the going rate

        /// <summary>
        /// The going rate for a professional, cents an hour: the game's own wage for their main skill (handed in), by the
        /// role's pay, the labour market's shortage of the family (thousandths), and their certificates; at least the floor.
        /// </summary>
        public static int Going(int gameRateCents, RoleDef role, int shortage, int certBp, int levelBp = 0)
        {
            long rate = (long)gameRateCents * role.Pay / 1000;
            rate = rate * Math.Max(500, shortage) / 1000;
            rate = rate * (10000 + certBp + levelBp) / 10000;
            return (int)Math.Max(TalentCatalogue.RateLeast, rate);
        }

        // ---------------------------------------------------------------- rivals

        /// <summary>
        /// Whether a rival comes for a professional today: seniors and above, not already going or approached, by the
        /// family's shortage, low morale, pay under the going rate, a restraint clause (half) and the player's standing.
        /// </summary>
        public static bool Approached(int seed, Profile p, int day, int shortage, int esteem, int paid, int going)
        {
            if (p.Level < TalentCatalogue.PoachLevel || p.NoticeEnd > 0 || p.PoachDay > day)
            {
                return false;
            }
            long chance = TalentCatalogue.PoachChance * 1000L;   // millionths
            chance = chance * Math.Max(500, shortage) / 1000;
            chance = chance * (1500 - p.Morale) / 1000;
            if (going > 0 && paid < going)
            {
                chance = chance * (1000 + (going - paid) * 3000L / going) / 1000;
            }
            if (p.NonCompete)
            {
                chance = chance * TalentCatalogue.NonCompetePoach / 1000;
            }
            chance = chance * (1000 - TalentCatalogue.Clamp(esteem, -10, 10) * 25) / 1000;
            return TechRandom.Hash(seed, TechRandom.Of(p.Id), day, 61) % 1000000u < (uint)Math.Max(0, chance);
        }

        public static void Offer(Profile p, string rival, int day)
        {
            p.PoachBy = rival ?? string.Empty;
            p.PoachBp = TalentCatalogue.PoachPremiumBp;
            p.PoachDay = day + TalentCatalogue.PoachAnswerDays;
        }

        /// <summary>True while a rival's offer waits for an answer.</summary>
        public static bool OfferOpen(Profile p, int day)
        {
            return p.PoachBp > 0 && day <= p.PoachDay;
        }

        /// <summary>The rival's offer answered: matched (the rise is the offer's), or not (they hand in their notice and go to the rival).</summary>
        public static void Answer(Profile p, bool match, int day, int year)
        {
            if (match)
            {
                Raised(p, day, p.PoachBp);
            }
            else
            {
                Notice(p, day, year, LeaveKind.Poached);
            }
            p.PoachBp = 0;
            p.PoachDay = day + TalentCatalogue.PoachRestDays;   // (no second approach while this one is fresh)
        }
    }
}
