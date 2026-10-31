using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    public static partial class StudioRules
    {
        /// <summary>
        /// A day of the rival studios (NPC parity): each releases a game when its day comes, made by the same rules as the
        /// player's (a genre it likes or the hot one, a team's quality rolled, the critics' bar, the audience, the reach of a
        /// typical budget), then waits its scope's gap; a hit starts a trend in its genre. Every rival game sells its day by
        /// the window and the half-life; the newest are kept. Rolled from the rivals' seed (on a Going Public guest, the host's
        /// file is applied instead and this is not run). Returns how many were released, into <paramref name="released"/>.
        /// </summary>
        public static int RivalDay(StudioFile f, int day, List<RivalGame> released)
        {
            released?.Clear();
            if (f == null)
            {
                return 0;
            }
            int n = 0;
            for (int s = 0; s < StudioCatalogue.RivalNames.Length; s++)
            {
                int scope = StudioCatalogue.RivalScope[s];
                if (f.RivalNext[s] <= 0)
                {
                    f.RivalNext[s] = day + TechRandom.Range(f.Seed, s, 81, 0, 5, StudioCatalogue.RivalGapMost[scope]);   // the first releases spread over a gap
                    continue;
                }
                if (day < f.RivalNext[s])
                {
                    continue;
                }
                RivalGame r = RivalRelease(f, s, day);
                f.Rivals.Add(r);
                released?.Add(r);
                n++;
                f.RivalNext[s] = day + TechRandom.Range(f.Seed, s, 83, day, StudioCatalogue.RivalGapLeast[scope], StudioCatalogue.RivalGapMost[scope]);
                if (r.Score >= StudioCatalogue.HitScore)
                {
                    f.TrendGenre = r.Genre;
                    f.TrendUntil = day + StudioCatalogue.TrendDays;
                }
            }
            while (f.Rivals.Count > StudioCatalogue.RivalGamesKept)
            {
                f.Rivals.RemoveAt(0);   // released in order: the oldest first
            }
            for (int i = 0; i < f.Rivals.Count; i++)
            {
                RivalGame r = f.Rivals[i];
                r.Sold = Math.Min(r.Units, r.Sold + RivalUnitsDay(r, day));
            }
            return n;
        }

        /// <summary>A rival studio's release on a day: its genre, platforms, quality and score, name, and the copies it will sell.</summary>
        public static RivalGame RivalRelease(StudioFile f, int studio, int day)
        {
            int id = f.NextRival++;
            int scope = StudioCatalogue.RivalScope[Clamp(studio, 0, StudioCatalogue.RivalNames.Length - 1)];
            int[] likes = StudioCatalogue.RivalGenres[studio];
            int genre = f.TrendGenre >= 0 && day < f.TrendUntil && TechRandom.Mille(f.Seed, id, 85) < 300 ? f.TrendGenre : likes[TechRandom.Range(f.Seed, id, 86, 0, 0, likes.Length - 1)];
            int platforms;
            if (scope == (int)Scope.Big)
            {
                platforms = (1 << (int)Platform.Pc) | (1 << (int)Platform.Console);
            }
            else if (scope == (int)Scope.Mid)
            {
                platforms = (1 << (int)Platform.Pc) | (TechRandom.Mille(f.Seed, id, 87) < 500 ? 1 << (int)Platform.Console : 0);
            }
            else
            {
                platforms = genre == (int)Genre.Puzzle || genre == (int)Genre.Casual || genre == (int)Genre.Family ? 1 << (int)Platform.Mobile : 1 << (int)Platform.Pc;
            }
            int quality = ScaleRules.QualityRoll(f.Seed, id, StudioCatalogue.RivalQuality[scope]);
            int score = (int)((StudioCatalogue.ReviewBase * 1000L + (long)(quality - Expect(genre, day, false)) * StudioCatalogue.ReviewPerQuality) / 1000);
            score = Clamp(score + TechRandom.Range(f.Seed, id, 88, 0, -StudioCatalogue.CriticsSpread, StudioCatalogue.CriticsSpread), 0, 100);
            var r = new RivalGame
            {
                Id = id, Studio = studio, Genre = genre, Platforms = platforms, Scope = scope, Score = score, Launched = day,
                Name = StudioCatalogue.TitleFirst[TechRandom.Range(f.Seed, id, 89, 0, 0, StudioCatalogue.TitleFirst.Length - 1)] + " "
                       + StudioCatalogue.TitleSecond[TechRandom.Range(f.Seed, id, 90, 0, 0, StudioCatalogue.TitleSecond.Length - 1)]
            };
            r.Units = RivalLife(f, r, day);
            return r;
        }

        /// <summary>
        /// The copies a rival game sells over its life: the world's audience of its genre on its platforms (the documented
        /// world: the same on every machine), seen by its typical budget, reached by its scope's usual marketing, bought by its
        /// score, its usual price, and less piracy as a paid PC game's.
        /// </summary>
        public static long RivalLife(StudioFile f, RivalGame r, int day)
        {
            long budget = ScaleCatalogue.BudgetTypical[Clamp(r.Scope, 0, 2)];
            int vis = Visibility(budget), trend = Trend(f, r.Genre, day);
            long audience = 0, units = 0;
            var markets = new long[StudioCatalogue.GamePlatforms];
            for (int p = 0; p < StudioCatalogue.GamePlatforms; p++)
            {
                if ((r.Platforms & (1 << p)) != 0)
                {
                    markets[p] = StudioBridges.World(r.Genre, p, day) * vis / 1000 * trend / 1000;
                    audience += markets[p];
                }
            }
            int reach = ScaleRules.Reach(budget * ScaleCatalogue.MarketingShare[Clamp(r.Scope, 0, 2)] / 1000, audience);
            for (int p = 0; p < StudioCatalogue.GamePlatforms; p++)
            {
                if (markets[p] > 0)
                {
                    long u = ScaleRules.Units(markets[p], reach, r.Score, 1000, 1000);
                    units += p == (int)Platform.Mobile ? u * StudioCatalogue.F2PPlayers / 1000 : u * (1000 - StudioCatalogue.Piracy[p]) / 1000;
                }
            }
            return Math.Max(0L, units);
        }

        /// <summary>A rival game's copies on a day: its launch window's share front loaded, then what is left halving over its score's half-life.</summary>
        public static long RivalUnitsDay(RivalGame r, int day)
        {
            int k = day - r.Launched;
            if (k < 0)
            {
                return 0;
            }
            if (k < StudioCatalogue.WindowDays)
            {
                long window = r.Units * 1000 / ScaleRules.ByQuality(ScaleCatalogue.Tail, r.Score);
                return window * (StudioCatalogue.WindowDays - k) / StudioCatalogue.WindowWeights;
            }
            long left = Math.Max(0L, r.Units - r.Sold);
            return left * 693 / (ScaleRules.HalfLife(r.Score) * 1000L);
        }

        /// <summary>The rival studio that comes for a game studio's people: the one with the newest hit, else the newest release; -1 with none.</summary>
        public static int Poacher(StudioFile f, int day)
        {
            int newest = -1, hit = -1, newestDay = int.MinValue, hitDay = int.MinValue;
            for (int i = 0; f != null && i < f.Rivals.Count; i++)
            {
                RivalGame r = f.Rivals[i];
                if (r.Launched > newestDay)
                {
                    newest = r.Studio;
                    newestDay = r.Launched;
                }
                if (r.Score >= StudioCatalogue.HitScore && r.Launched > hitDay && day - r.Launched < StudioCatalogue.TrendDays)
                {
                    hit = r.Studio;
                    hitDay = r.Launched;
                }
            }
            return hit >= 0 ? hit : newest;
        }

        /// <summary>The rival games of a genre launched within so many days either side of a day: a release date crowded or clear.</summary>
        public static int Crowd(StudioFile f, int genre, int day, int days, List<RivalGame> into)
        {
            into?.Clear();
            int n = 0;
            for (int i = 0; f != null && i < f.Rivals.Count; i++)
            {
                RivalGame r = f.Rivals[i];
                if (r.Genre == genre && Math.Abs(r.Launched - day) <= days)
                {
                    into?.Add(r);
                    n++;
                }
            }
            return n;
        }

        /// <summary>The day a rival studio is expected to release next (its file's day), for the release date's page; 0 before its first is set.</summary>
        public static int RivalDue(StudioFile f, int studio) => f != null && studio >= 0 && studio < f.RivalNext.Length ? f.RivalNext[studio] : 0;
    }
}
