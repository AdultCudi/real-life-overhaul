using System;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>A boss's manner: the saved number of <see cref="Job.Boss"/>.</summary>
    public enum BossKind
    {
        Mentor = 0,        // brings people on: the work teaches a quarter more, and a review is kinder
        Driver = 1,        // wants results: overtime counts twice at a review, missed days hurt more
        Micromanager = 2,  // watches the clock: lateness counts twice, a review is harder
        HandsOff = 3       // leaves people to it: nothing either way
    }

    /// <summary>One office matter: the saved number is its place in <see cref="LifeRules.Matters"/>.</summary>
    public sealed class Matter
    {
        public readonly string Key;        // reallife_life_matter_{Key}: what happened; _{Key}_a and _{Key}_b the two answers
        public readonly int FavourA, FavourB;   // what each answer does to the player's standing with colleagues
        public readonly int BurnoutA;      // thousandths of burnout the first answer costs
        public readonly bool BossMatters;  // the first answer is liked by a mentor or a hands-off boss and disliked by a micromanager

        public Matter(string key, int favourA, int favourB, int burnoutA = 0, bool bossMatters = false)
        {
            Key = key;
            FavourA = favourA;
            FavourB = favourB;
            BurnoutA = burnoutA;
            BossMatters = bossMatters;
        }
    }

    /// <summary>The rules of working life in plain numbers: reviews, promotion, burnout, office matters, search firms, redundancy, options, the pension. Game-free.</summary>
    public static class LifeRules
    {
        // Reviews come every 14 game days in the game's own year of 60, every 28 at most in a longer one (Paced). It was 182:
        // a review a month at 60, against gigs that reach their top level in three days. The careers keep the pace of a
        // year of PaceYearMost days at most, so a longer year (up to 365) is never more than twice as slow as the game's own
        // 60, while gigs and businesses keep theirs.
        public const int ReviewDays = 85;           // between reviews, written for a year of 365 days (four a year)
        public const int PaceYearMost = 120;        // days: the longest year the careers' days are kept to
        public static readonly int[] RatingFrom = { 0, 35, 50, 65, 80 };   // a review's score for each rating, 1 to 5
        public const int RiseBp = 300;              // a rating of 4: a rise of so much; 5: twice it; 3: half of it
        // A bonus is a fifth and two fifths of the fortnight's pay between reviews (it was a week and two, set for two reviews a year).
        public const int BonusDays4 = 2, BonusDays5 = 4;     // a bonus of working days' pay for a 4, a 5
        public const int RaisesMost = 4;            // rises at one rank
        public const int PromoteRating = 4;
        public const int PromoteBp = 1200;          // a promotion pays at least this much more, or the new rank's own rate if higher
        public const int LowRunsToGo = 2;           // ratings of 1 in a row that end the job
        public const int AskRiseBp = 500;           // asking for a rise between reviews asks this much
        public const int AskGapDays = 30;           // ...once in so many days
        public const int MentorLearn = 250;         // thousandths more the work teaches under a mentor

        public const int OvertimeMinutes = 120;     // a stint of overtime
        public const int LongWeekHours = 45;        // hours a week over which burnout builds
        public const int BurnoutPerHour = 12;       // thousandths of burnout an hour over it
        public const int BurnoutRest = 30;          // thousandths it eases on a day not worked
        public const int BurnoutMood = 600;         // over this the player feels it (the mood)
        public const int BurnoutSignedOff = 900;    // over this the doctor signs the player off for a working day
        public const int BurnoutAfterSignedOff = 650;

        public const int MatterGapLeast = 6, MatterGapMost = 12;   // days between office matters
        public const int MatterDays = 2;            // days to answer one; unanswered is the second answer
        public const int FavourMost = 15;           // standing with colleagues, either way
        public const int FavourAtReview = 1000;     // thousandths of the favour a review counts

        public static readonly Matter[] Matters =
        {
            new Matter("credit", 4, -2, 0, true),        // a colleague takes the credit for your work: speak up, or let it go
            new Matter("weekend", 5, -3, 120),           // the boss asks you to cover a weekend: agree, or say no
            new Matter("rumour", 2, 0),                  // talk of cuts: ask around, or keep your head down
            new Matter("help", 3, -1, 60),               // a colleague is drowning: help, or keep to your own work
            new Matter("win", 4, 2),                     // a big win: share the credit, or take it
            new Matter("clash", 1, 3, 0, true),          // a clash in a meeting: smooth it over, or stand firm
            new Matter("party", 2, -1, 40),              // the office party: go, or go home
            new Matter("shortcut", -2, 3, 0, true)       // a shortcut your manager wants: take it, or do it properly
        };

        public const int PoachLeastRank = 2;        // search firms come for the third rung and up
        public const int PoachChance = 5;           // per mille a day, by the last rating and the player's name
        public const int PoachGapDays = 21;
        public const int PoachPremiumBp = 1800;     // what a search firm's offer pays over the player's rate
        public static readonly int[] RedundancyByCycle = { 0, 0, 3, 8 };   // per mille a day in a slowdown, a recession (never the public sector)
        public const int SeveranceWeeksPerYear = 1, SeveranceLeastWeeks = 2;
        public const int BenefitWeeks = 6;
        public const int BenefitShare = 400;        // thousandths of the last week's pay
        public const int BenefitMost = 600;         // whole money a week at most, at the start's prices

        public const int OptionsPerRank = 2000;     // options granted at a tech company for each rung above the first
        public const int VestQuarterDays = 91;      // a quarter of them is the player's every so many days (a year of 365)
        public static readonly int[] OptionCents = { 520, 380, 240, 120 };   // what one option comes to when it vests, by the city's economy

        public const int PensionOwnBp = 500;        // of pay into the pension, the player's own
        public const int PensionEmployerBp = 300;   // ...and the employer's on top
        public const int PensionPublicBp = 800;     // the public sector's employer pays this instead
        public const int PensionGrowthBp = 400;     // what the pot grows by a year
        public const int RetireAge = 65, RetireEarlyAge = 55;
        public const int PensionYears = 20;         // the pot is paid out over so many years
        public const int EarlyCutBp = 2500;         // retiring early pays this much less

        public const int ProfitShareChiefBp = 100;  // a chief executive's share of a rival's week of takings, basis points
        public const int ProfitShareExecBp = 40;    // ...the other seats at the top
        public const int ConflictChance = 3;        // per mille a day an undeclared conflict comes out, for each rung
        public const int ConflictIntegrity = -4;    // what it does to the player's integrity

        /// <summary>The year the careers' days are kept to: the game's, from 60 days to <see cref="PaceYearMost"/>.</summary>
        public static int PaceYear(int year) => TalentCatalogue.Clamp(year, 60, PaceYearMost);

        /// <summary>Game days for a number of days written for a year of 365, at the careers' pace (<see cref="PaceYear"/>): reviews, time at a rank, experience.</summary>
        public static int Paced(int days, int year) => TalentCatalogue.Scaled(days, PaceYear(year));

        /// <summary>Days between reviews in a year of the game's length.</summary>
        public static int ReviewEvery(int year) => Paced(ReviewDays, year);

        /// <summary>A review's score, 0 to 100: half for attendance, skills against the rank, overtime, standing with colleagues, the boss's manner and burnout.</summary>
        public static int ReviewScore(Job j, int points, int need, BossKind boss)
        {
            int attended = ProRules.Attendance(j);
            int late = j.Due > 0 ? j.Late * 1000 / j.Due : 0;
            double s = 50.0 * attended / 1000.0 - (boss == BossKind.Micromanager ? 2 : 1) * 10.0 * late / 1000.0;
            int fit = points - need;
            s += fit >= 0 ? Math.Min(20, fit) : fit * 3;
            int overtimeHours = j.Overtime / 60;
            s += Math.Min(boss == BossKind.Driver ? 16 : 8, overtimeHours * (boss == BossKind.Driver ? 2 : 1));
            s += j.Favour * FavourAtReview / 1000;
            s += boss == BossKind.Mentor ? 5 : boss == BossKind.Micromanager ? -3 : 0;
            s -= boss == BossKind.Driver && attended < 900 ? 6 : 0;
            s -= j.Burnout > BurnoutMood ? 8 : 0;
            s += 12;   // (a steady worker who turns up and is just up to the job lands in the middle)
            return TalentCatalogue.Clamp((int)Math.Round(s), 0, 100);
        }

        /// <summary>The rating, 1 to 5, a score gives.</summary>
        public static int Rating(int score)
        {
            int r = 1;
            for (int i = 1; i < RatingFrom.Length; i++)
            {
                r = score >= RatingFrom[i] ? i + 1 : r;
            }
            return r;
        }

        /// <summary>The rise a rating brings, basis points: half a step for a 3, a step for a 4, two for a 5; none once a rank has had its rises.</summary>
        public static int RiseFor(int rating, int raises)
        {
            if (raises >= RaisesMost)
            {
                return 0;
            }
            return rating >= 5 ? RiseBp * 2 : rating == 4 ? RiseBp : rating == 3 ? RiseBp / 2 : 0;
        }

        /// <summary>The bonus a rating brings, in working days' pay.</summary>
        public static int BonusDays(int rating) => rating >= 5 ? BonusDays5 : rating == 4 ? BonusDays4 : 0;

        /// <summary>A working day's pay of a job, cents at the start's prices.</summary>
        public static long DayPay(Job j) => (long)j.Pay * j.Def.Hours;

        /// <summary>A week's pay of a job, cents at the start's prices.</summary>
        public static long WeekPay(Job j) => (long)j.Pay * j.Def.Hours * Days(j.Def.Days);

        private static int Days(int mask)
        {
            int n = 0;
            for (int i = 0; i < 7; i++)
            {
                n += (mask >> i) & 1;
            }
            return n;
        }

        /// <summary>True if a review can bring a promotion: a good rating, time at the rank, the skills and the qualifications of the next rung, and room above.</summary>
        public static bool CanPromote(StudyFile f, Job j, int rating, int day, int year)
        {
            Profession p = j.Def;
            int next = j.Post.Rank + 1;
            if (rating < PromoteRating || next >= p.Ranks.Length)
            {
                return false;
            }
            ProRank r = p.Ranks[next];
            if (day - j.RankDay < Paced(r.Days, year))
            {
                return false;
            }
            if (r.Exec && j.Declared)
            {
                return false;   // a declared conflict keeps the player off the top seats of that employer
            }
            return ProRules.Qualified(f, p, next) && ProRules.Points(f, p, next) >= ProRules.Need(f, p, next);
        }

        /// <summary>What a promotion pays: the old rate and a promotion's rise, or the new rank's own rate at this employer if that is more.</summary>
        public static int PromotedPay(Job j, int nextRank)
        {
            int up = (int)((long)j.Pay * (10000 + PromoteBp) / 10000);
            int own = ProRules.PostPay(j.Def, nextRank, (EmployerKind)j.Post.Kind, 1000);
            return Math.Max(up, own);
        }

        /// <summary>The chance (per cent) an ask for a rise between reviews is granted, by the last rating.</summary>
        public static int AskRiseChance(int rating) => rating >= 5 ? 75 : rating == 4 ? 55 : rating == 3 ? 25 : 5;

        /// <summary>A week's hours worked add burnout over the long-week mark; returns the thousandths added for the minutes just worked.</summary>
        public static int BurnoutFor(int weekMinutesBefore, int minutes)
        {
            int mark = LongWeekHours * 60;
            int over = Math.Max(0, weekMinutesBefore + minutes - Math.Max(mark, weekMinutesBefore));
            return over * BurnoutPerHour / 60;
        }

        /// <summary>What an answer to an office matter does to the player's favour, with the boss's manner where it counts.</summary>
        public static int FavourOf(Matter m, bool first, BossKind boss)
        {
            if (!first)
            {
                return m.FavourB;
            }
            if (m.BossMatters)
            {
                return boss == BossKind.Micromanager ? -Math.Abs(m.FavourA) : boss == BossKind.Mentor || boss == BossKind.HandsOff ? m.FavourA + 1 : m.FavourA;
            }
            return m.FavourA;
        }

        /// <summary>The chance (per mille) a search firm comes this day, by the rung, the last rating and the player's name.</summary>
        public static int PoachChanceOf(int rank, int rating, int reputation)
        {
            if (rank < PoachLeastRank || rating < 4)
            {
                return 0;
            }
            return Math.Max(0, PoachChance + (rating - 4) * 3 + Math.Max(0, reputation) / 2);
        }

        /// <summary>The chance (per mille) of being made redundant this day, by the city's economy and the last rating; never the public sector.</summary>
        public static int RedundancyChance(int cycle, EmployerKind kind, int rating)
        {
            if (kind == EmployerKind.Public)
            {
                return 0;
            }
            int c = RedundancyByCycle[TalentCatalogue.Clamp(cycle, 0, 3)];
            return rating >= 5 ? c / 3 : rating == 4 ? c / 2 : c;
        }

        /// <summary>Severance for a redundancy, cents at the start's prices: a week's pay for each year of service, two weeks at least.</summary>
        public static long Severance(Job j, int day, int year)
        {
            int years = Math.Max(0, day - j.Hired) / Math.Max(60, year);
            return WeekPay(j) * Math.Max(SeveranceLeastWeeks, years * SeveranceWeeksPerYear);
        }

        /// <summary>Unemployment benefit a week, whole money at the start's prices: a share of the last week's pay, no more than the most.</summary>
        public static int BenefitWeek(long weekPayCents)
        {
            return (int)Math.Min(BenefitMost, weekPayCents * BenefitShare / 1000 / 100);
        }

        /// <summary>Options granted on joining a tech company at a rung (none at the first).</summary>
        public static int OptionsFor(EmployerKind kind, int rank) => kind == EmployerKind.TechFirm ? OptionsPerRank * Math.Max(0, rank) : 0;

        /// <summary>A quarter vests: the options it is and what they come to, cents.</summary>
        public static long Vest(int options, int cycle, out int vested)
        {
            vested = options / 4;
            return (long)vested * OptionCents[TalentCatalogue.Clamp(cycle, 0, 3)];
        }

        /// <summary>What a pay goes into the pension, cents: the player's own share (taken from it) and the employer's.</summary>
        public static void Pension(long payCents, EmployerKind kind, out long own, out long employer)
        {
            own = payCents * PensionOwnBp / 10000;
            employer = payCents * (kind == EmployerKind.Public ? PensionPublicBp : PensionEmployerBp) / 10000;
        }

        /// <summary>A day's growth of the pension pot, cents.</summary>
        public static long PensionGrowth(long pot, int year) => pot <= 0 ? 0 : pot * PensionGrowthBp / 10000 / Math.Max(60, year);

        /// <summary>What the pension pays a day once retired, cents: the pot over the years it is paid for, less for retiring early.</summary>
        public static long PensionDay(long pot, int year, bool early)
        {
            long day = pot / Math.Max(1, PensionYears * Math.Max(60, year));
            return early ? day * (10000 - EarlyCutBp) / 10000 : day;
        }

        /// <summary>A seat at the top of a rival's company: the share of its week's takings it pays, basis points (0 elsewhere).</summary>
        public static int ProfitShareBp(Job j)
        {
            if (j == null || j.Post.Kind != (int)EmployerKind.Rival || !j.Rank.Exec)
            {
                return 0;
            }
            return j.Post.Rank == j.Def.Ranks.Length - 1 ? ProfitShareChiefBp : ProfitShareExecBp;
        }

        /// <summary>The chance (per mille) an undeclared conflict of interest comes out this day: more the higher the rung.</summary>
        public static int ConflictChanceOf(int rank) => ConflictChance * (1 + rank);
    }
}
