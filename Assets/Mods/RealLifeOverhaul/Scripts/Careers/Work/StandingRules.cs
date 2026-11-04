using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>The player's standing at work: four figures out of 100, each from what the job measures. Game-free.</summary>
    public struct Standing
    {
        public int Performance;   // the review's own score, worked out now (LifeRules.ReviewScore)
        public int Punctuality;   // shifts worked on time, of the shifts worked
        public int Reliability;   // shifts worked, of the shifts due
        public int Teamwork;      // standing with colleagues, from the office matters answered (Job.Favour)
        public bool Measured;     // false before any shift was due: the figures then say nothing yet
    }

    /// <summary>
    /// What the job measures of a person and what two people's records decide: the standing figures, the weekly ring, the colleague up
    /// for the same rung and the other applicants for a post. Every figure comes from a record the job already keeps. Safety is not
    /// here: the work panel records no fault in a shift, so nothing honest could feed it. Colleagues and the player are judged by the
    /// same rules, with weeks seeded from the save's seed so a load gives the same people the same weeks. Game-free (EconomyCheck runs it).
    /// </summary>
    public static class StandingRules
    {
        public const int PeersMost = 4, OthersMost = 12;
        public const int ShiftsMost = 7;               // worked shifts kept for the recent shifts
        public const int OthersPerPostMost = 3;        // other applicants for one post at most
        public const int WeeksToContest = 4;           // weeks of record before two records can be weighed
        public const int DriftMost = 4;                // points a person's weekly score moves from where their record settles, either way
        public const int LuckMost = 6;                 // points the day of an application's weighing moves a score, either way

        /// <summary>The standing of the job's holder from its record since the last review.</summary>
        public static Standing Of(Job j, int performance)
        {
            var s = new Standing { Performance = TalentCatalogue.Clamp(performance, 0, 100), Measured = j != null && j.Due > 0 };
            if (j == null)
            {
                return s;
            }
            s.Punctuality = j.Worked > 0 ? TalentCatalogue.Clamp(100 * (j.Worked - Math.Min(j.Late, j.Worked)) / j.Worked, 0, 100) : 100;
            s.Reliability = j.Due > 0 ? TalentCatalogue.Clamp(100 * j.Worked / j.Due, 0, 100) : 100;
            s.Teamwork = TeamworkOf(j.Favour);
            return s;
        }

        /// <summary>Adds a worked shift to the ring of the last seven; one record a day (a day recorded again is replaced).</summary>
        public static void Shift(StandingFile f, int day, int minutes, int score, int pay)
        {
            for (int i = f.Shifts.Count - 1; i >= 0; i--)
            {
                if (f.Shifts[i].Day == day)
                {
                    f.Shifts.RemoveAt(i);
                }
            }
            f.Shifts.Add(new ShiftRecord { Day = day, Minutes = Math.Max(0, minutes), Score = TalentCatalogue.Clamp(score, 0, 100), Pay = Math.Max(0, pay) });
            while (f.Shifts.Count > ShiftsMost)
            {
                f.Shifts.RemoveAt(0);
            }
        }

        /// <summary>Teamwork out of 100 from the standing with colleagues (minus fifteen to fifteen), the middle for none.</summary>
        public static int TeamworkOf(int favour) => TalentCatalogue.Clamp(50 + favour * 50 / LifeRules.FavourMost, 0, 100);

        public static int WeekOf(int day) => day / 7;

        /// <summary>True when the ring has not been written for this week.</summary>
        public static bool WeekDue(StandingFile f, int day) => f.WeekOf != WeekOf(day);

        /// <summary>
        /// The week's turn: the player's score goes in the ring and every colleague's moves one step. The step is seeded, so a reload of
        /// the same day gives the same one. Returns 1 when a week was written, 0 when it was not due.
        /// </summary>
        public static int Week(StandingFile f, int score, int day)
        {
            if (!WeekDue(f, day))
            {
                return 0;
            }
            int week = WeekOf(day);
            f.WeekOf = week;
            f.Push(TalentCatalogue.Clamp(score, 0, 100));
            for (int i = 0; i < f.Peers.Count; i++)
            {
                Step(f.Peers[i], f.Seed, week, i);
            }
            return 1;
        }

        /// <summary>One person's week: the score moves toward their record and the three standing figures drift a little.</summary>
        public static void Step(Peer p, int seed, int week, int salt)
        {
            int drift = TechRandom.Range(seed, 7701 + salt, week, p.Post, -DriftMost, DriftMost);
            p.Score = TalentCatalogue.Clamp(p.Score + (p.Skill - p.Score) / 3 + drift, 0, 100);
            p.Push(p.Score);
            p.Punctuality = Drift(p.Punctuality, p.Skill, seed, 7711 + salt, week, p.Post);
            p.Reliability = Drift(p.Reliability, p.Skill, seed, 7712 + salt, week, p.Post);
            p.Teamwork = Drift(p.Teamwork, p.Skill, seed, 7713 + salt, week, p.Post);
        }

        private static int Drift(int value, int skill, int seed, int a, int week, int post)
        {
            return TalentCatalogue.Clamp(value + TechRandom.Range(seed, a, week, post, -2, 2) + (skill - value) / 8, 0, 100);
        }

        /// <summary>A colleague made from where their record settles, with the weeks already behind them.</summary>
        public static Peer Colleague(string name, int rank, int skill, PeerRole role, int day, int seed, int weeks)
        {
            var p = new Peer { Name = name, Rank = rank, Skill = TalentCatalogue.Clamp(skill, 30, 95), Role = (int)role, Day = day };
            p.Score = p.Skill;
            p.Punctuality = TalentCatalogue.Clamp(p.Skill + TechRandom.Range(seed, 7721, rank, 0, -6, 6), 0, 100);
            p.Reliability = TalentCatalogue.Clamp(p.Skill + TechRandom.Range(seed, 7722, rank, 0, -6, 6), 0, 100);
            p.Teamwork = TalentCatalogue.Clamp(p.Skill + TechRandom.Range(seed, 7723, rank, 0, -8, 8), 0, 100);
            for (int w = 0; w < weeks; w++)
            {
                Step(p, seed, 1000 + w, rank);
            }
            return p;
        }

        /// <summary>
        /// Who gets the next rung when the player and the colleague up for it are both ready: the better record over the weeks held.
        /// The player wins a tie, and wins by default while either has too few weeks to weigh.
        /// </summary>
        public static bool PlayerGetsPost(StandingFile f, Peer rival)
        {
            if (rival == null || f.Count < WeeksToContest || rival.Count < WeeksToContest)
            {
                return true;
            }
            return f.Mean() >= rival.Mean();
        }

        /// <summary>Another person for the post, drawn when the player first looks at it: their record settles near the post's own bar.</summary>
        public static Peer Applicant(string name, Opening o, int bar, int day, int seed, int index)
        {
            int skill = TalentCatalogue.Clamp(bar + TechRandom.Range(seed, 7731, o.Id, index, -18, 14), 30, 98);
            var p = new Peer { Name = name, Rank = o.Rank, Skill = skill, Score = skill, Post = o.Id, Role = (int)PeerRole.Applicant,
                Day = day - TechRandom.Range(seed, 7732, o.Id, index, 0, 4) };
            p.Punctuality = TalentCatalogue.Clamp(skill + TechRandom.Range(seed, 7733, o.Id, index, -6, 6), 0, 100);
            p.Reliability = TalentCatalogue.Clamp(skill + TechRandom.Range(seed, 7734, o.Id, index, -6, 6), 0, 100);
            p.Teamwork = TalentCatalogue.Clamp(skill + TechRandom.Range(seed, 7735, o.Id, index, -8, 8), 0, 100);
            p.Push(skill);
            return p;
        }

        /// <summary>How many others apply for a post: none to three, seeded by the post and the save.</summary>
        public static int OthersFor(int seed, Opening o) => TechRandom.Range(seed, 7741, o.Id, 0, 0, OthersPerPostMost);

        /// <summary>The others who applied for a post, in the order they came.</summary>
        public static int OthersOf(StandingFile f, int post, List<Peer> into)
        {
            into.Clear();
            for (int i = 0; i < f.Others.Count; i++)
            {
                if (f.Others[i].Post == post)
                {
                    into.Add(f.Others[i]);
                }
            }
            return into.Count;
        }

        /// <summary>Adds an applicant, the oldest going when the list is full.</summary>
        public static void Add(StandingFile f, Peer p)
        {
            f.Others.Add(p);
            while (f.Others.Count > OthersMost)
            {
                f.Others.RemoveAt(0);
            }
        }

        /// <summary>
        /// The employer's weighing of every applicant: the player's score and each other's, moved by the day's luck for that person.
        /// Returns the index of the other who beats the player (the best of them), or -1 when the player stands best; a tie goes to the player.
        /// </summary>
        public static int Beaten(int playerScore, int playerLuck, IList<Peer> others, int seed, int post)
        {
            int best = -1, bestScore = Math.Max(0, playerScore) + TalentCatalogue.Clamp(playerLuck, -LuckMost, LuckMost);
            for (int i = 0; i < others.Count; i++)
            {
                int score = others[i].Score + TechRandom.Range(seed, 7751, post, i, -LuckMost, LuckMost);
                if (score > bestScore)
                {
                    best = i;
                    bestScore = score;
                }
            }
            return best;
        }
    }
}
