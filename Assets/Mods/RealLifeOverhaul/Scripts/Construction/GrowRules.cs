using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>A Let it grow building's budget (plans/growables.md section 3): what it costs, how well it is built, the rent it asks.</summary>
    public enum GrowTier
    {
        Basic = 0,
        Standard = 1,
        Premium = 2,
        Count = 3
    }

    /// <summary>What one column of the district's land is to the city's growth: free to take, and the council's use on every row of it.</summary>
    public struct GrowColumn
    {
        public bool Free;      // free land nobody holds, builds on or develops
        public int Use;        // the council's use on every row (BuildUse), -1 when its rows differ or it is unzoned
        public bool Growing;   // a site of the city's growth under way on it
    }

    /// <summary>
    /// The city's growth (plans/growables.md section 2; the owner chose a steady pace, 26 Oct 2026): once a game day, for a use
    /// whose demand is over its threshold and while fewer than <see cref="SitesMost"/> grown sites are under way, a developer
    /// takes free land the council zones for the use along one street of the block that is filling (the one most built, a
    /// block with growth under way first), as many columns as the use's buildings want, and puts a building through the
    /// pipeline the player's goes through; once it stands its unit is let to a tenant within days or weeks by the demand, and
    /// a building left mostly empty for long falls in condition (lower rent, then run down) until it is let or refurbished.
    /// The tiers of Let it grow on the player's own land are data here too. Every answer from the arguments alone, drawn from
    /// the save's seed and the day (BuildRandom): the same on every machine. Game-free (EconomyCheck's GrowChecks).
    /// COST: nothing per frame; a pass over the district's columns once a game day.
    /// </summary>
    public static class GrowRules
    {
        // ---- the pace (plans/growables.md, "Caps and balance")
        public const int SitesMost = 4;           // grown sites under way in Uptown at once: a few, as the owner chose (steady)
        public const int BlockSitesMost = 3;      // ... of them on one block: the block filling keeps most, never all, so the next one starts
        public const int GapDays = 2;             // days at least between two starts: one in a day's pass at most, then a pause
        public const int FirstDay = 3;            // days after a save first meets the growth before its first start: the market is counted first
        public static readonly int[] Threshold = { 40, 45, 45, 50 };   // demand (0 to 100) each use needs before it grows: homes, shops, offices, warehouses
        public static readonly int[] WidthLeast = { 1, 1, 1, 1 };      // columns a grown site of each use takes, at least: Uptown's free land is mostly single spaces between buildings
        public static readonly int[] WidthMost = { 2, 2, 3, 3 };       // ... at most, where the run is longer (designs fill one to seven)

        // ---- the stages
        public const int PlansDaysLeast = 3, PlansDaysSpread = 3;   // the developer's architect: 3 to 5 days
        public const int PatienceDays = 30;       // days past a stage's end it waits on the district before it is given up
        public const int Quality = 70;            // a developer's plans and works before City Hall and the critic: a practice's, as the redevelopments'

        // ---- the letting (the owner's plan: "units let to NPC tenants by demand over the following weeks")
        public const int LetDaysUnknown = 21;     // days to a tenant where demand is not counted (as in a Going Public session)
        public const int LetDaysMost = 42;        // ... at most, for a use under its threshold: six weeks

        // ---- condition instead of levels
        public const int ConditionFull = 1000;
        public const int EmptyGraceDays = 28;     // days a unit may stand empty before its building starts to fall
        public const int FallPerWeek = 80;        // condition lost each week past the grace while mostly empty
        public const int RisePerWeek = 40;        // ... won back each week while let
        public const int RunDownBelow = 400;      // under this a building is marked run down
        public const int RunDownClears = 600;     // ... and over this the mark comes off again
        public const int MostlyEmptyBelow = 40;   // % of a building of the player's let under which it counts as mostly empty
        public const int DropMost = 6;            // traffic points a building in poor condition loses at most (its rent and value follow)

        // ---- Let it grow's tiers (data: cost, build quality, rent level)
        public static readonly int[] TierCostPermille = { 850, 1000, 1350 };   // the build's cost against the plans' estimate
        public static readonly int[] TierQuality = { 55, 72, 90 };             // the plans' quality (the architect's), the inspector's and the critic's eye
        public static readonly int[] TierRentPermille = { 900, 1000, 1150 };   // the deed's rent against the market's (the game holds the player's rent within 0.8 to 1.2)
        public static readonly int[] TierFirm = { 0, 1, 2 };                   // the practice that draws its plans (BuildCatalogue.FirmCount)
        public static readonly string[] TierKeys = { "reallife_grow_tier_basic", "reallife_grow_tier_standard", "reallife_grow_tier_premium" };
        public const int FillWeeks = 16;          // weeks after the keys the developer still finds tenants for the player's building

        /// <summary>The use that grows now: the one with the strongest demand over its threshold among those with free land zoned for it, or -1.</summary>
        public static int UseToGrow(int[] demand, int[] freeColumns)
        {
            int best = -1;
            for (int u = 0; demand != null && u < demand.Length && u < Threshold.Length; u++)
            {
                bool land = freeColumns != null && u < freeColumns.Length && freeColumns[u] >= WidthLeast[u];
                if (land && demand[u] >= Threshold[u] && (best < 0 || demand[u] - Threshold[u] > demand[best] - Threshold[best]))
                {
                    best = u;
                }
            }
            return best;
        }

        /// <summary>True when a new grown site may start today: under the cap, the gap since the last kept, and the growth's first days passed.</summary>
        public static bool MayStart(int underWay, int day, int nextDay)
        {
            return underWay < SitesMost && day >= nextDay;
        }

        /// <summary>The columns a grown site of a use wants: from its least to its most, drawn from the save's seed and the day.</summary>
        public static int Width(int use, int seed, int day)
        {
            int u = BuildCatalogue.Clamp(use, 0, WidthLeast.Length - 1);
            BuildRandom r = BuildRandom.Of(seed, day, 0x6721);
            return WidthLeast[u] + r.Range(0, WidthMost[u] - WidthLeast[u] + 1);
        }

        /// <summary>
        /// Where the city grows next and with what: the block that is filling (a block with growth under way and room for more first,
        /// then the most built share, ties by the seed), and on it the use furthest over its threshold (<paramref name="demand"/>, by
        /// BuildUse; -1 not counted) that has a run of free columns the council zones whole for it along one of its streets (a run at
        /// the block's corner first, so the corner buildings stand); <paramref name="count"/> as many of the run as the use's width
        /// wanted, at most the run (<see cref="Take"/> gives where they start in it). So a block's free land fills, use by use, before
        /// the next block's. False when no wanted use has such land. Returns the use, the run's first column and its length.
        /// </summary>
        public static bool Pick(CellGrid grid, GrowColumn[] cols, int[] demand, int seed, int day, out int use, out int first, out int run, out int count)
        {
            use = first = -1;
            run = count = 0;
            if (grid == null || cols == null || demand == null)
            {
                return false;
            }
            var blocks = new Dictionary<int, int[]>(64);   // block: columns, taken, growing
            for (int c = 0; c < grid.Columns.Count && c < cols.Length; c++)
            {
                int block = grid.Columns[c].Block;
                if (!blocks.TryGetValue(block, out int[] b))
                {
                    blocks[block] = b = new int[3];
                }
                b[0]++;
                b[1] += cols[c].Free ? 0 : 1;
                b[2] += cols[c].Growing ? 1 : 0;
            }
            int bestBlock = int.MinValue, bestFirst = -1, bestRun = 0, bestUse = -1;
            long bestScore = long.MinValue;
            foreach (KeyValuePair<int, int[]> kv in blocks)
            {
                int[] b = kv.Value;
                if (b[2] >= BlockSitesMost)
                {
                    continue;   // the block has as many going up as it takes at once
                }
                int blockUse = -1, blockFirst = -1, blockRun = 0;
                for (int u = 0; u < Threshold.Length && u < demand.Length; u++)
                {
                    if (demand[u] < Threshold[u] || (blockUse >= 0 && demand[u] - Threshold[u] <= demand[blockUse] - Threshold[blockUse]))
                    {
                        continue;   // not wanted, or wanted less than one found already
                    }
                    if (Run(grid, cols, u, kv.Key, WidthLeast[u], out int runFirst, out int runLength))
                    {
                        blockUse = u;
                        blockFirst = runFirst;
                        blockRun = runLength;
                    }
                }
                if (blockUse < 0)
                {
                    continue;
                }
                long score = (b[2] > 0 ? 2000L : 0L) + b[1] * 1000L / Math.Max(1, b[0]);
                long tie = (uint)BuildRandom.Mix(BuildRandom.Mix(seed, kv.Key), 0x51);
                score = score * 4294967296L + tie;
                if (score > bestScore || (score == bestScore && kv.Key < bestBlock))
                {
                    bestScore = score;
                    bestBlock = kv.Key;
                    bestFirst = blockFirst;
                    bestRun = blockRun;
                    bestUse = blockUse;
                }
            }
            if (bestFirst < 0)
            {
                return false;
            }
            use = bestUse;
            first = bestFirst;
            run = bestRun;
            count = Math.Max(1, Math.Min(bestRun, Math.Max(WidthLeast[use], Width(use, seed, day))));
            return true;
        }

        // The block's first run for a use along its streets: a corner run first (from the corner end), else the first in strip order.
        private static bool Run(CellGrid grid, GrowColumn[] cols, int use, int block, int least, out int first, out int length)
        {
            first = -1;
            length = 0;
            int plainFirst = -1, plainLength = 0;
            for (int c = 0; c < grid.Columns.Count && c < cols.Length; c++)
            {
                CellColumn col = grid.Columns[c];
                if (col.Block != block || !Takes(cols[c], use) || (grid.Prev(c) >= 0 && Takes(cols[grid.Prev(c)], use)))
                {
                    continue;   // not this block's, not the use's free land, or not a run's first
                }
                int n = 0, last = c;
                for (int at = c; at >= 0 && Takes(cols[at], use) && n < BuildCatalogue.SiteSpacesMost; at = grid.Next(at))
                {
                    n++;
                    last = at;
                }
                if (n < least)
                {
                    continue;
                }
                bool lowCorner = CellRules.Corner(grid, c, 1, out bool low, out _) && low;
                bool highCorner = CellRules.Corner(grid, last, 1, out bool lowEnd, out _) && !lowEnd;
                if (lowCorner || highCorner)
                {
                    first = c;   // a run at a corner: the site is taken from its corner end (Take)
                    length = n;
                    return true;
                }
                if (plainFirst < 0)
                {
                    plainFirst = c;
                    plainLength = n;
                }
            }
            first = plainFirst;
            length = plainLength;
            return first >= 0;
        }

        private static bool Takes(GrowColumn c, int use) => c.Free && !c.Growing && c.Use == use;

        // The column n - 1 back along the strip from one: the start of n columns ending there.
        private static int Back(CellGrid grid, int last, int n)
        {
            int at = last;
            for (int i = 1; i < n && grid.Prev(at) >= 0; i++)
            {
                at = grid.Prev(at);
            }
            return at;
        }

        /// <summary>The first of <paramref name="count"/> columns taken from a run (at <paramref name="first"/>, <paramref name="run"/> long): from a high corner its end, else its start.</summary>
        public static int Take(CellGrid grid, int first, int run, int count)
        {
            int last = first;
            for (int i = 1; i < run && grid.Next(last) >= 0; i++)
            {
                last = grid.Next(last);
            }
            bool highCorner = CellRules.Corner(grid, last, 1, out bool low, out _) && !low && !(CellRules.Corner(grid, first, 1, out bool lowFirst, out _) && lowFirst);
            return highCorner ? Back(grid, last, count) : first;
        }

        public const int RunsTried = 6;   // free runs one day's pass looks at before it waits: a run nothing of the city's fills is left out, so the pass goes on to the next

        /// <summary>
        /// The widths to try for a site on a free run, best first, into <paramref name="into"/> (at least <see cref="BuildCatalogue.SiteSpacesMost"/> long):
        /// from the width wanted (or the run, if shorter) down to one; with <paramref name="roomy"/> then the widths above the wanted one up to the whole
        /// run, which leave less over beside the site. Returns how many.
        /// </summary>
        public static int Counts(int want, int run, bool roomy, int[] into)
        {
            int n = 0;
            int most = Math.Min(Math.Min(run, BuildCatalogue.SiteSpacesMost), into.Length);
            for (int count = Math.Min(Math.Max(1, want), most); count >= 1; count--)
            {
                into[n++] = count;
            }
            for (int count = Math.Max(1, want) + 1; roomy && count <= most; count++)
            {
                into[n++] = count;
            }
            return n;
        }

        /// <summary>
        /// The free columns a site of <paramref name="count"/> columns from <paramref name="start"/> leaves in its run (at <paramref name="first"/>,
        /// <paramref name="run"/> long): how many before it and where they start, how many after it and where they start (-1 where none).
        /// </summary>
        public static void Rest(CellGrid grid, int first, int run, int start, int count, out int leftAt, out int left, out int rightAt, out int right)
        {
            left = 0;
            for (int at = first; at >= 0 && at != start && left < run; at = grid.Next(at))
            {
                left++;
            }
            right = Math.Max(0, run - left - count);
            leftAt = left > 0 ? first : -1;
            rightAt = -1;
            int last = start;
            for (int i = 1; i < count && last >= 0; i++)
            {
                last = grid.Next(last);
            }
            if (right > 0 && last >= 0)
            {
                rightAt = grid.Next(last);
            }
            right = rightAt >= 0 ? right : 0;
        }

        /// <summary>
        /// Leaves a free run out of the day's pass (its columns no longer free for growth until the next read of the land): a run no design of
        /// the city's fills for its use would be picked again and again, and the growth stood still on it. Returns the columns left out.
        /// </summary>
        public static int Leave(CellGrid grid, GrowColumn[] cols, int first, int run)
        {
            int n = 0;
            for (int i = 0, at = first; i < run && at >= 0 && at < cols.Length; i++, at = grid.Next(at))
            {
                if (cols[at].Free)
                {
                    cols[at].Free = false;
                    n++;
                }
            }
            return n;
        }

        /// <summary>The days the developer's architect takes to draw a grown site's plans: 3 to 5, by its id.</summary>
        public static int PlansDays(int seed, int id)
        {
            return PlansDaysLeast + BuildRandom.Of(seed, id, 0x6722).Range(0, PlansDaysSpread);
        }

        /// <summary>
        /// The days after its keys a grown building's unit finds its tenant: the stronger the demand for its use, the sooner (70 or
        /// more: 2 to 6 days; 50 or more: 5 to 14; at its threshold: 10 to 24; under it: 21 to 42; not counted: 21).
        /// </summary>
        public static int LetDays(int demand, int seed, int id)
        {
            if (demand < 0)
            {
                return LetDaysUnknown;
            }
            BuildRandom r = BuildRandom.Of(seed, id, 0x6723);
            if (demand >= 70)
            {
                return 2 + r.Range(0, 5);
            }
            if (demand >= 50)
            {
                return 5 + r.Range(0, 10);
            }
            if (demand >= 40)
            {
                return 10 + r.Range(0, 15);
            }
            return 21 + r.Range(0, LetDaysMost - 21 + 1);
        }

        /// <summary>
        /// The occupancy points a week a Let it grow building of the player's gains from the developer's lettings in its first weeks
        /// (beside the game's own daily coming and going): by the demand for its use, a basic one's lower rent filling it sooner, a
        /// premium one's higher rent later.
        /// </summary>
        public static int WeekFill(int demand, GrowTier tier)
        {
            int points = demand < 0 ? 6 : demand >= 70 ? 14 : demand >= 50 ? 10 : demand >= 40 ? 7 : 3;
            int t = BuildCatalogue.Clamp((int)tier, 0, (int)GrowTier.Count - 1);
            return Math.Max(1, points * (t == 0 ? 1200 : t == 1 ? 1000 : 800) / 1000);
        }

        /// <summary>A building's condition after a week: let, it is won back toward full; mostly empty past its grace, it falls; never under 0.</summary>
        public static int Condition(int condition, bool mostlyEmpty, int emptyDays)
        {
            int c = BuildCatalogue.Clamp(condition, 0, ConditionFull);
            if (!mostlyEmpty)
            {
                return Math.Min(ConditionFull, c + RisePerWeek);
            }
            return emptyDays > EmptyGraceDays ? Math.Max(0, c - FallPerWeek) : c;
        }

        /// <summary>Whether a building is marked run down: under the line it is; over the higher line it is not; between, as it was.</summary>
        public static bool RunDown(int condition, bool was)
        {
            return condition < RunDownBelow || (was && condition < RunDownClears);
        }

        /// <summary>The traffic points a building's condition takes off (its rent, value and passers-by follow): none from 800, then 1, 3, and 6 when run down.</summary>
        public static int Drop(int condition)
        {
            return condition >= 800 ? 0 : condition >= 600 ? 1 : condition >= RunDownBelow ? 3 : DropMost;
        }

        /// <summary>
        /// Which of the designs that fit a Let it grow site the builder picks, by tier and demand, from those ordered smallest floor area
        /// first: basic the smallest, standard the middle, premium the largest; a use in strong demand (70 or more) one larger, a
        /// weak one (under 40) one smaller. -1 when none fits.
        /// </summary>
        public static int TierPick(int count, GrowTier tier, int demand)
        {
            if (count <= 0)
            {
                return -1;
            }
            int t = BuildCatalogue.Clamp((int)tier, 0, (int)GrowTier.Count - 1);
            int at = t == 0 ? 0 : t == 2 ? count - 1 : (count - 1) / 2;
            at += demand >= 70 ? 1 : demand >= 0 && demand < 40 ? -1 : 0;
            return BuildCatalogue.Clamp(at, 0, count - 1);
        }

        /// <summary>A tier's estimate against the plans': the build's cost times its tier's share.</summary>
        public static long TierCost(long estimate, GrowTier tier)
        {
            return Math.Max(0L, estimate) * TierCostPermille[BuildCatalogue.Clamp((int)tier, 0, (int)GrowTier.Count - 1)] / 1000L;
        }

        /// <summary>A tier's rent a square metre a day against the market's, held within the game's own 0.8 to 1.2 of it.</summary>
        public static float TierRent(float market, GrowTier tier)
        {
            float share = TierRentPermille[BuildCatalogue.Clamp((int)tier, 0, (int)GrowTier.Count - 1)] / 1000f;
            return Math.Max(0f, market) * Math.Max(0.8f, Math.Min(1.2f, share));
        }

        /// <summary>The tier a rival tycoon's holding builds to, drawn from the seed and its site: mostly standard, now and then basic or premium.</summary>
        public static GrowTier HoldingTier(int seed, int id)
        {
            int roll = BuildRandom.Of(seed, id, 0x6724).Range(0, 100);
            return roll < 20 ? GrowTier.Basic : roll < 75 ? GrowTier.Standard : GrowTier.Premium;
        }

        /// <summary>True when a developer's site goes to a rival tycoon's holding rather than a builder's own arm (a third of them, while one is in play).</summary>
        public static bool ByHolding(int seed, int id, bool holdings)
        {
            return holdings && BuildRandom.Of(seed, id, 0x6725).Range(0, 3) == 0;
        }
    }
}
