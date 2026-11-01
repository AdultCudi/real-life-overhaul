using System;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// A week's content plan from Flare's desk session: the posts it plans and their days, the holder's strength (a manager makes some of them by
    /// their skill), and whether a storm this week is answered honestly. One at a time, for the whole account. Game-free; saved.
    /// </summary>
    public sealed class FlarePlan
    {
        public int Day = -1;          // the day it was made; -1 none yet
        public int Posts;             // posts it plans
        public int Gap = 1;           // days between two of them
        public int Done;              // posts whose day has come (made or missed)
        public int Made;              // ...of them made
        public int Next = -1;         // the day of the next, -1 none left
        public int Strength = 1000;   // thousandths: the holder's (SessionRules.Strength: the player all of it, a manager half to all by skill)
        public bool Answer;           // a storm this week gets an honest answer
        public bool Manager;          // a manager made it
        public int Turn;              // which of the player's businesses the next post is about, in their order
    }

    /// <summary>
    /// Flare in the city by rule, the desk session (the brief's eleventh step: "a social media session at an office desk (a content plan for the
    /// week, answering a crisis), delegable to a hired social media manager"): once a week at the owner's desk in one of the player's offices, or by
    /// its manager, the week is planned: a post a day or three strong posts about the player's businesses (each the player's own announcement, by
    /// Flare's rules), polite answers to the poor reviews now, and an honest answer to a storm the day it breaks. A manager makes each post at
    /// their strength; the player makes them all. Game-free. COST: a few operations on the hour of a planned day.
    /// </summary>
    public static partial class FlareCityRules
    {
        public const string SessionId = "flare_content_plan";   // saved in each session record: never renamed
        public const int PlanWeek = 7;                           // days a plan runs, and between two sessions
        public const int PlanHour = 10;                          // the hour of a planned day its post goes up, and a manager holds the session
        public const int PlanDaily = 0, PlanFew = 1, PlanNone = 2;   // the first step's choices: a post a day, three strong posts, none
        public const int PlanDailyPosts = 7, PlanFewPosts = 3, PlanFewGap = 2;
        public const int ReviewsAnswer = 0, ReviewsLeave = 1;   // the second step's
        public const int StormHonest = 0, StormQuiet = 1;       // the third step's
        private const int SaltPlan = 111, SaltPlanAnswer = 112;

        /// <summary>A week's plan made on a day by its choices and the holder's strength (thousandths).</summary>
        public static FlarePlan MakePlan(int day, int posts, bool answerStorm, int strength, bool manager)
        {
            var p = new FlarePlan { Day = day, Answer = answerStorm, Strength = Math.Max(0, Math.Min(1000, strength)), Manager = manager };
            if (posts == PlanDaily)
            {
                p.Posts = PlanDailyPosts;
                p.Gap = 1;
            }
            else if (posts == PlanFew)
            {
                p.Posts = PlanFewPosts;
                p.Gap = PlanFewGap;
            }
            p.Next = p.Posts > 0 ? day : -1;
            return p;
        }

        /// <summary>Whether a plan of this week stands on a day: another office's session then waits for the next week.</summary>
        public static bool Planned(FlarePlan p, int day) => p != null && p.Day >= 0 && day >= p.Day && day - p.Day < PlanWeek;

        /// <summary>Whether a planned post is due on a day: its day has come, within the plan's week.</summary>
        public static bool PlanDue(FlarePlan p, int day) => Planned(p, day) && p.Next >= 0 && p.Done < p.Posts && day >= p.Next;

        /// <summary>A planned post's day passed (made or missed): the next one's day, or none.</summary>
        public static void PlanPosted(FlarePlan p, int day)
        {
            p.Done++;
            p.Next = p.Done < p.Posts ? day + p.Gap : -1;
        }

        /// <summary>Whether the holder makes the planned post due on a day: the player always, a manager at their strength (a seeded roll).</summary>
        public static bool PlanMakes(FlarePlan p, int seed, int day) => !p.Manager || Mille(seed, day, SaltPlan, p.Done) < p.Strength;

        /// <summary>Whether a storm on a day is answered by the plan: one that said so, within its week.</summary>
        public static bool PlanAnswers(FlarePlan p, int day) => Planned(p, day) && p.Answer;

        /// <summary>Whether a poor review's bad talk at a business is answered at a session: the player always, a manager at their strength.</summary>
        public static bool AnswerMade(bool manager, int strength, int seed, int day, int business) => !manager || Mille(seed, day, SaltPlanAnswer, business) < strength;
    }
}
