using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What one outlet wrote of a game: who, what score, on which day, and the line it wrote (a part it praised or a part or the bugs it
    /// blamed, in one of two wordings). Saved with the game.</summary>
    public sealed class GameReview
    {
        public int Outlet, Score, Day;
        public int Part;          // 0 to 4 as the catalogue's Part; StudioRules.PartBugs for the bugs
        public bool Praise;       // it praised its part, else it blamed it
        public int Variant;       // the line's wording, 0 or 1
    }

    public static partial class StudioRules
    {
        private const string Mod = "reallife";   // the keys' start, apart, so the text check reads only whole keys
        /// <summary>The line a review quotes blames or praises the bugs, not a part.</summary>
        public const int PartBugs = (int)Part.Count;

        // How far each outlet stands from the critics' score; the twelve sum to nothing, so the outlets' average is the game's score.
        private static readonly int[] Spread = { 9, 6, 4, 3, 1, 0, -1, -2, -3, -4, -6, -7 };

        /// <summary>
        /// The outlets' reviews of a game on its release: each outlet scores around the critics' score (a seeded share of the spread, so every game has its own order of kind and harsh
        /// outlets), and quotes a line built from the game's real parts: the best part when it scores above the critics' score, the weakest part (or its bugs, where they are many) when it
        /// scores below. Each comes in the first week after launch. Deterministic from the game, the studio's seed and the day, so a host and a guest write the same.
        /// </summary>
        public static void WriteReviews(GameDev g, int seed, int day)
        {
            g.Reviews.Clear();
            if (g.Score < 0)
            {
                return;
            }
            int best = 0, worst = 0;
            for (int p = 1; p < (int)Part.Count; p++)
            {
                best = PartQuality(g, p) > PartQuality(g, best) ? p : best;
                worst = PartQuality(g, p) < PartQuality(g, worst) ? p : worst;
            }
            bool buggy = g.Hidden + g.Found > BugRoom(g) / 6;
            int n = StudioCatalogue.Outlets.Length;
            var order = new int[n];
            for (int i = 0; i < n; i++)
            {
                order[i] = i;
            }
            // each outlet's place among the twelve, by a hash of the game: a stable shuffle
            Array.Sort(order, (a, b) => TechRandom.Hash(seed, g.Id, a, 71).CompareTo(TechRandom.Hash(seed, g.Id, b, 71)));
            for (int rank = 0; rank < n; rank++)
            {
                int outlet = order[rank];
                int score = Clamp(g.Score + Spread[rank], 1, 100);
                bool praise = score >= g.Score;
                uint h = TechRandom.Hash(seed, g.Id, outlet, 73);
                g.Reviews.Add(new GameReview
                {
                    Outlet = outlet, Score = score, Day = (g.Launched >= 0 ? g.Launched : day) + (int)(h % 7), Praise = praise, Variant = (int)((h >> 8) % 2),
                    Part = praise ? best : buggy && ((h >> 4) % 2) == 0 ? PartBugs : worst
                });
            }
            g.Reviews.Sort((a, b) => a.Day != b.Day ? a.Day.CompareTo(b.Day) : a.Outlet.CompareTo(b.Outlet));
        }

        /// <summary>The key of the line a review quotes: reallife_studio_review_{p or c}_{part}_{variant}.</summary>
        public static string QuoteKey(GameReview r)
        {
            string part = r.Part >= (int)Part.Count ? "bugs" : ((Part)Clamp(r.Part, 0, (int)Part.Count - 1)).ToString().ToLowerInvariant();
            return Mod + "_studio_review_" + (r.Praise ? "p" : "c") + "_" + part + "_" + Clamp(r.Variant, 0, 1);
        }

        /// <summary>The average of the outlets' scores, whole; -1 with none.</summary>
        public static int ReviewAverage(GameDev g)
        {
            if (g.Reviews.Count == 0)
            {
                return -1;
            }
            int sum = 0;
            for (int i = 0; i < g.Reviews.Count; i++)
            {
                sum += g.Reviews[i].Score;
            }
            return sum / g.Reviews.Count;
        }

        // ---------------------------------------------------------------- the daily sales ring

        private static int SalesSlot(int day) => ((day % StudioCatalogue.SalesDays) + StudioCatalogue.SalesDays) % StudioCatalogue.SalesDays;

        /// <summary>A day's copies written into the game's 90 day ring; the days since the last one it holds are written as none, so a gap never shows an old day's sales.</summary>
        public static void RecordSales(GameDev g, int day, long units)
        {
            if (g.SalesLast < 0 || day - g.SalesLast >= StudioCatalogue.SalesDays)
            {
                Array.Clear(g.Sales, 0, g.Sales.Length);
            }
            else
            {
                for (int d = g.SalesLast + 1; d < day; d++)
                {
                    g.Sales[SalesSlot(d)] = 0;
                }
            }
            g.Sales[SalesSlot(day)] = day == g.SalesLast ? g.Sales[SalesSlot(day)] + units : units;
            g.SalesLast = Math.Max(g.SalesLast, day);
        }

        /// <summary>The copies the game sold on a day of the last 90, or none for a day the ring does not hold.</summary>
        public static long SalesOn(GameDev g, int day) => g.SalesLast >= 0 && day <= g.SalesLast && day > g.SalesLast - StudioCatalogue.SalesDays ? g.Sales[SalesSlot(day)] : 0;

        // ---------------------------------------------------------------- the rival game's line

        /// <summary>The rival game that sells most in a genre, else the one that sells most; null with none. The game's own page puts its line beside the game's.</summary>
        public static RivalGame BestRival(StudioFile f, int genre)
        {
            RivalGame best = null, any = null;
            for (int i = 0; i < f.Rivals.Count; i++)
            {
                RivalGame r = f.Rivals[i];
                any = any == null || r.Sold > any.Sold ? r : any;
                best = r.Genre == genre && (best == null || r.Sold > best.Sold) ? r : best;
            }
            return best ?? any;
        }

        /// <summary>
        /// A rival game's copies a day from a first day on, written into <paramref name="into"/>: its release's window and the tail after it, run from its release by the rule that sells it
        /// (RivalUnitsDay), so the line a page draws is the one the rival really sold by.
        /// </summary>
        public static void RivalSeries(RivalGame r, int from, float[] into)
        {
            var t = new RivalGame { Launched = r.Launched, Score = r.Score, Units = r.Units };
            long sold = 0;
            int last = from + into.Length - 1;
            for (int d = r.Launched; d <= last; d++)
            {
                t.Sold = sold;
                long units = Math.Min(Math.Max(0L, r.Units - sold), RivalUnitsDay(t, d));
                sold += units;
                if (d >= from)
                {
                    into[d - from] = units;
                }
            }
        }
    }
}
