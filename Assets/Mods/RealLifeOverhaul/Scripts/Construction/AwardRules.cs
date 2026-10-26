using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The City Ledger's building of the year, game-free: the paper's critic gives each building finished in the city a score
    /// (the player's for their work and its timing, a rival's or a developer's for its builder's standing), the four best of
    /// the year are shortlisted in its last days, the city votes on Flare (the player's own following counts for their
    /// buildings), and the most votes wins on the new year's first day. Seeded by the save's seed and the year.
    /// </summary>
    public static class AwardRules
    {
        public const int Shortlist = 4;                 // buildings shortlisted
        public const int EntriesMost = 16;              // the year's best kept for it, the player's always among them
        public const int VoteLeastDays = 3, VoteMostDays = 7;
        public const int OnTimeBonus = 8, LatePerDay = 2, LateMost = 15, Noise = 8;
        public const int StarsMost = 5;
        public const int VotesBase = 40, VotesPerPoint = 3;
        public const int FollowersPerVote = 50, FollowerVotesMost = 400;
        public const int RepWon = 6, StandingWon = 4, RepShortlisted = 1;
        public const int CriticFrom = 80, CriticBelow = 25;   // a building of the city's the critic writes of: this good, or this poor

        /// <summary>The game year a day is in (day 1 opens year 0).</summary>
        public static int Year(int day, int daysPerYear) => Math.Max(0, day - 1) / Math.Max(1, daysPerYear);

        /// <summary>Days the city votes: an eighth of the year, three to seven.</summary>
        public static int VoteDays(int daysPerYear) => BuildCatalogue.Clamp(daysPerYear / 8, VoteLeastDays, VoteMostDays);

        /// <summary>The day a year's shortlist comes out: its last days, the vote's length before its end.</summary>
        public static int ShortlistDay(int year, int daysPerYear) => (year + 1) * Math.Max(1, daysPerYear) - VoteDays(daysPerYear) + 1;

        /// <summary>The day a year's award is made: the next year's first.</summary>
        public static int AwardDay(int year, int daysPerYear) => (year + 1) * Math.Max(1, daysPerYear) + 1;

        /// <summary>
        /// The critic's score of a finished building, 5 to 98: the work's quality, a few points for being on time or fewer for
        /// each day late, and the critic's own taste of a few points either way.
        /// </summary>
        public static int Score(int quality, int lateDays, int seed, int key)
        {
            BuildRandom random = BuildRandom.Of(seed, key, 503);
            int score = BuildCatalogue.Clamp(quality, 0, 100) + (lateDays <= 0 ? OnTimeBonus : -Math.Min(LateMost, lateDays * LatePerDay)) + random.Range(-Noise, Noise + 1);
            return BuildCatalogue.Clamp(score, 5, 98);
        }

        /// <summary>The critic's stars, one to five.</summary>
        public static int Stars(int score) => BuildCatalogue.Clamp(1 + BuildCatalogue.Clamp(score, 0, 100) / 21, 1, StarsMost);

        /// <summary>A day's votes on Flare for a shortlisted building: by its score, the day's mood, and the player's following for their own.</summary>
        public static int DayVotes(int seed, int year, int entry, int day, int score, int followers, bool players)
        {
            BuildRandom random = BuildRandom.Of(seed, year * 1009 + entry, day * 7 + 509);
            int votes = (VotesBase + BuildCatalogue.Clamp(score, 0, 100) * VotesPerPoint) * random.Range(700, 1301) / 1000;
            return votes + (players ? Math.Min(FollowerVotesMost, Math.Max(0, followers) / FollowersPerVote) : 0);
        }

        /// <summary>The order of the year's entries for the shortlist: the best score first, then the earlier finished.</summary>
        public static void Rank(List<AwardEntry> entries, List<int> into)
        {
            into.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                int at = into.Count;
                while (at > 0 && Better(entries[i], entries[into[at - 1]]))
                {
                    at--;
                }
                into.Insert(at, i);
            }
        }

        private static bool Better(AwardEntry a, AwardEntry b)
        {
            return a.Score > b.Score || (a.Score == b.Score && a.Day < b.Day);
        }

        /// <summary>The winner among the shortlisted: the most votes, then the better score, then the earlier finished; -1 for none.</summary>
        public static int Winner(List<AwardEntry> entries)
        {
            int best = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                AwardEntry e = entries[i];
                if (!e.Short)
                {
                    continue;
                }
                if (best < 0)
                {
                    best = i;
                    continue;
                }
                AwardEntry b = entries[best];
                if (e.Votes > b.Votes || (e.Votes == b.Votes && Better(e, b)))
                {
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// Where a new entry goes when the year's list is full: the place of the worst that is not the player's, or -1 when the
        /// new one is no better than any of those (the player's own are always kept).
        /// </summary>
        public static int Room(List<AwardEntry> entries, int score, bool players)
        {
            if (entries.Count < EntriesMost)
            {
                return entries.Count;
            }
            int worst = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Builder != -1 && (worst < 0 || entries[i].Score < entries[worst].Score))
                {
                    worst = i;
                }
            }
            return worst >= 0 && (players || entries[worst].Score < score) ? worst : -1;
        }
    }
}
