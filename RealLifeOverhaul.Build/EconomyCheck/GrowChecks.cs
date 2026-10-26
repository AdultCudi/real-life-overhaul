using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Uptown;

// Buildings that grow (plans/growables.md; Scripts/Construction: ZoneRules, GrowRules, BuildState.Grow, BuildStore.Grow,
// ConstructionDirector.Zone, .Grow, .LetItGrow, .GrowTests, BuildMode.Zone, .Grow; Scripts/Uptown: DemandRules.PeopleLift,
// LifeDirector.Grow): zoning per square and the tools' picks on the real plan, an older save's site, the council's zoning of free
// land and its rezoning, the developer's pick and the steady cap with the pace simulated on the real plan, the tiers' costs and
// rents and the builder's pick, tenants by demand, condition and its drop, the store's new fields and parts, then the sources
// (nothing a frame, the option, a session rests, the bridges, the tests) and the texts.
internal static partial class Program
{
    private static void GrowChecks()
    {
        GrowZoneChecks();
        GrowToolChecks();
        GrowCouncilChecks();
        GrowPickChecks();
        GrowPaceChecks();
        GrowTierChecks();
        GrowTenantChecks();
        GrowConditionChecks();
        GrowStoreChecks();
        GrowSourceChecks();
        GrowTextChecks();
    }

    // ------------------------------------------------------------------ zoning per square

    private static void GrowZoneChecks()
    {
        int[] rows = { 5, 3 };
        // An older save's site: no digits of its own, every square its paint.
        Check(ZoneRules.At(string.Empty, 0, 0, 2) == 2 && ZoneRules.At(null, 1, 2, 3) == 3 && ZoneRules.At("", 0, 0, 0) == 0 && ZoneRules.Main(string.Empty, 4) == 4,
            "zoning per square: an older save's site (no digits) reads its paint on every square, and its main use is that paint");
        string whole = ZoneRules.Fill(2, rows, 1);
        Check(whole == "11111.111", "zoning per square: a site made whole is a group a column, a digit a row (" + whole + ")");
        string one = ZoneRules.Set(string.Empty, 2, rows, 2, 1, 2, 3);
        Check(one == "22222.223" && ZoneRules.At(one, 1, 2, 2) == 3 && ZoneRules.At(one, 0, 4, 0) == 2 && ZoneRules.At(one, 1, 4, 2) == 2,
            "zoning per square: one square painted leaves every other square its site's paint (" + one + "); a row past the column's digits reads the site's paint");
        string many = ZoneRules.SetMany(one, 2, rows, 2, new[] { 0, 0, 1, 7 }, new[] { 0, 9, 0, 0 }, 4);
        Check(many == "42222.423", "zoning per square: many squares painted at once; a square off the site is passed over (" + many + ")");
        Check(ZoneRules.Main(many, 0) == 2 && ZoneRules.Main("44330.1", 0) == 3 && ZoneRules.Main("00000", 2) == 0 && ZoneRules.Main("1122", 0) == 1,
            "zoning per square: the site's own paint is the use most of its squares carry, ties to the lower use, none when none is painted");
        Check(ZoneRules.Count(many, 4) == 2 && ZoneRules.Cleared(many, 4) == "02222.023" && ZoneRules.Cleared(string.Empty, 2) == string.Empty,
            "zoning per square: a use the council refused taken back to none on each square that carried it");
        Check(ZoneRules.Clean("12340.1", 2) == "12340.1" && ZoneRules.Clean("12340.1", 1) == string.Empty && ZoneRules.Clean("19", 1) == string.Empty
              && ZoneRules.Clean("1a", 1) == string.Empty && ZoneRules.Clean("1234567890", 1) == string.Empty && ZoneRules.Clean(null, 3) == string.Empty,
            "zoning per square: a save's digits are kept only as written: 0 to 4, at most nine a column, no more groups than spaces; anything else reads as none of its own");
        string joined = ZoneRules.Join("12", 1, new[] { 2 }, 0, string.Empty, 2, new[] { 2, 2 }, 3);
        Check(joined == "12.33.33" && ZoneRules.Join(string.Empty, 1, new[] { 5 }, 2, string.Empty, 1, new[] { 5 }, 2) == string.Empty,
            "zoning per square: two sites joined keep each square's zone, each made whole with its own paint (" + joined + "); two older sites of one paint stay none of their own");
        Check(ZoneRules.SplitOne(joined, 1) == "33" && ZoneRules.SplitOne(joined, 0) == "12" && ZoneRules.SplitOne(string.Empty, 0) == string.Empty && ZoneRules.Column("12.33", 2) == null,
            "zoning per square: a site split gives each lot its own column's digits");
        Check(ZoneRules.Covers("222.222", 0, 0, 2, 3, BuildUse.Shops) && !ZoneRules.Covers("222.212", 0, 0, 2, 3, BuildUse.Shops) && ZoneRules.Covers("222.21", 2, 0, 2, 1, BuildUse.Shops)
              && !ZoneRules.Covers("222.222", 0, 0, 0, 3, BuildUse.Shops),
            "zoning per square: a footprint is covered by a use only when every square of it carries the use");
    }

    // A stand-in for the player's land: so many rows held of each column.
    private sealed class HeldColumns : ZoneRules.IHeld
    {
        public readonly Dictionary<int, int> Rows = new Dictionary<int, int>();

        public int Held(int column) => Rows.TryGetValue(column, out int r) ? r : 0;
    }

    private static void GrowToolChecks()
    {
        List<LotView> lots = BuildModePlan();
        CellGrid grid = CellGrid.Of(lots);
        int first = -1;
        for (int c = 0; c < grid.Columns.Count && first < 0; c++)
        {
            int n = 0;
            for (int at = c; at >= 0 && n < 6; at = grid.Next(at))
            {
                n++;
            }
            first = n >= 6 && grid.Prev(c) >= 0 ? c : -1;
        }
        Check(first >= 0, "zone tools: a strip of six spaces or more is found on the real plan");
        if (first < 0)
        {
            return;
        }
        int a = first, b = grid.Next(a), c3 = grid.Next(b);
        var held = new HeldColumns();
        held.Rows[a] = grid.Columns[a].Rows;
        held.Rows[b] = 3;
        held.Rows[c3] = grid.Columns[c3].Rows;
        var cells = new List<CellAt>();
        int filled = ZoneRules.FillSide(grid, b, held, cells);
        bool side = filled == grid.Columns[a].Rows + 3 + grid.Columns[c3].Rows && cells.All(x => held.Held(x.Column) > x.Row) && cells.All(x => grid.Columns[x.Column].Strip == grid.Columns[a].Strip);
        Check(side, "zone tools: Fill side takes every square the player holds along the block side, each column to the rows held (" + filled + " squares), and nothing else");
        int rect = ZoneRules.Rect(grid, new CellAt(c3, 1), new CellAt(a, 4), held, cells);
        Check(rect == 4 + 2 + 4 && cells.All(x => x.Row >= 1 && x.Row <= 4 && held.Held(x.Column) > x.Row),
            "zone tools: a rectangle takes the squares between the press and the pointer, either way along the street, the player's alone (" + rect + ")");
        int other = -1;
        for (int c = 0; c < grid.Columns.Count && other < 0; c++)
        {
            other = grid.Columns[c].Strip != grid.Columns[a].Strip ? c : -1;
        }
        Check(ZoneRules.Rect(grid, new CellAt(a, 0), new CellAt(other, 0), held, cells) == 0 && ZoneRules.Rect(grid, new CellAt(-1, 0), new CellAt(a, 0), held, cells) == 0,
            "zone tools: a rectangle across two streets (the pointer not read onto the press's street) or from nowhere takes nothing");
        Check(ZoneRules.Brush(grid, new CellAt(b, 2), held) && !ZoneRules.Brush(grid, new CellAt(b, 3), held) && !ZoneRules.Brush(grid, new CellAt(other, 0), held),
            "zone tools: the brush takes the square under the pointer only when it is the player's");
        Check(ZoneRules.FillSide(grid, other, held, cells) == 0, "zone tools: Fill side on a street with none of the player's land takes nothing");
    }

    // ------------------------------------------------------------------ the council's zoning of free land

    private static void GrowCouncilChecks()
    {
        Check(ZoneRules.CouncilAt(null, 2, "a", 0) == ZoneRules.CouncilUse("a", 0) + 1 && ZoneRules.CouncilAt("2200", 2, "a", 0) == 0 && ZoneRules.CouncilAt("2200", 1, "a", 3) == 2
              && ZoneRules.CouncilUse("b", 1) == (int)BuildUse.Offices && ZoneRules.CouncilUse("c", 3) == (int)BuildUse.Warehouses,
            "council zoning: a free square the council never changed takes the plan's use for its lot (the developers' own rule); a record's digit otherwise");
        Check(ZoneRules.CouncilColumn(5, 2) == "22222" && ZoneRules.CouncilColumn(0, 1) == "1" && ZoneRules.CouncilColumn(20, 3).Length == ZoneRules.MostRows,
            "council zoning: a column rezoned whole for one use, its rows' digits");
        bool rezone = ZoneRules.Rezone(new[] { 70, 20, 50, 30 }, new[] { 4, 6, 2, 1 }, out int to, out int from);
        Check(rezone && to == 0 && from == 1, "council zoning: the council rezones free land of the weakest idle use (shops at 20) for the strongest wanted one (homes at 70)");
        Check(!ZoneRules.Rezone(new[] { 59, 20, 50, 30 }, new[] { 4, 6, 2, 1 }, out _, out _) && !ZoneRules.Rezone(new[] { 70, 40, 50, 36 }, new[] { 4, 6, 2, 1 }, out _, out _)
              && !ZoneRules.Rezone(new[] { 70, 20, 50, 30 }, new[] { 4, 0, 2, 0 }, out _, out _) && !ZoneRules.Rezone(new[] { -1, -1, -1, -1 }, new[] { 4, 6, 2, 1 }, out _, out _),
            "council zoning: nothing rezoned while no use is wanted enough, none is idle, the idle use has no free land, or demand is not counted (a Going Public session)");
        Check(ZoneRules.RezoneOdds(50) == ZoneRules.RezoneOddsBase && ZoneRules.RezoneOdds(80) > ZoneRules.RezoneOdds(60) && ZoneRules.RezoneOdds(100) <= 1000
              && ZoneRules.RezoneOdds(80) == ZoneRules.RezoneOddsBase + BuildModeRules.CouncilLift(80),
            "council zoning: its odds are the player's own rule's: the base and the demand's lift (3 in 1000 a point over 50)");
    }

    // ------------------------------------------------------------------ the developer's pick

    // The real plan's free land as the growth sees it: free spaces (no size planned), each column zoned as the council's plan gives it.
    private static GrowColumn[] GrowLand(List<LotView> lots, CellGrid grid)
    {
        var byId = lots.ToDictionary(l => l.Id, l => l);
        var cols = new GrowColumn[grid.Columns.Count];
        for (int c = 0; c < cols.Length; c++)
        {
            LotView lot = byId[grid.Columns[c].LotId];
            bool free = lot.Size == null && lot.Spaces <= 1;
            cols[c] = new GrowColumn { Free = free, Use = free ? ZoneRules.CouncilUse(lot.Id, lot.Class) : -1 };
        }
        return cols;
    }

    private static void GrowPickChecks()
    {
        List<LotView> lots = BuildModePlan();
        CellGrid grid = CellGrid.Of(lots);
        GrowColumn[] cols = GrowLand(lots, grid);
        int[] free = new int[4];
        foreach (GrowColumn c in cols)
        {
            free[Math.Max(0, c.Use)] += c.Use >= 0 ? 1 : 0;
        }
        Check(free.Sum() > 40, "growth: the real plan has free land the council zones for the growth: homes " + free[0] + ", shops " + free[1] + ", offices " + free[2] + ", warehouses " + free[3] + " column(s)");
        bool every = true, deterministic = true, run = true;
        string seen = string.Empty;
        for (int use = 0; use < 4; use++)
        {
            if (free[use] < GrowRules.WidthLeast[use])
            {
                continue;
            }
            bool picked = GrowRules.Pick(grid, cols, Only(use), 4711, 40, out _, out int first, out int length, out int count);
            if (!picked)
            {
                continue;   // no run long enough for this use's least width
            }
            seen += (seen.Length > 0 ? ", " : string.Empty) + (BuildUse)use + " " + count + " of " + length;
            GrowRules.Pick(grid, cols, Only(use), 4711, 40, out _, out int again, out _, out int count2);
            deterministic &= again == first && count2 == count;
            int strip = grid.Columns[first].Strip, block = grid.Columns[first].Block;
            int at = first;
            for (int i = 0; i < length; i++, at = grid.Next(at))
            {
                every &= at >= 0 && cols[at].Free && cols[at].Use == use && grid.Columns[at].Strip == strip && grid.Columns[at].Block == block;
            }
            run &= count >= Math.Min(GrowRules.WidthLeast[use], length) && count <= length && length >= GrowRules.WidthLeast[use] && count <= GrowRules.WidthMost[use];
            int start = GrowRules.Take(grid, first, length, count);
            int end = start;
            for (int i = 1; i < count; i++)
            {
                end = grid.Next(end);
            }
            bool inside = true;
            for (int i = 0, x = first; i < length; i++, x = grid.Next(x))
            {
                inside &= true;
            }
            run &= end >= 0 && cols[start].Free && cols[end].Free && cols[start].Use == use;
        }
        Check(seen.Length > 0, "growth: the developer finds land for some use on the real plan (" + seen + ")");
        Check(every, "growth: the run picked is free land the council zones for the use, side by side along one street of one block");
        Check(run, "growth: the site taken is the use's width (its least to its most, never more than the run), from the run's corner end");
        Check(deterministic, "growth: the same seed and day pick the same land on every machine");
        var none = (GrowColumn[])cols.Clone();
        for (int c = 0; c < none.Length; c++)
        {
            none[c].Use = none[c].Use == 2 ? 0 : none[c].Use;
        }
        Check(!GrowRules.Pick(grid, none, Only(2), 4711, 40, out _, out _, out _, out _), "growth: nothing is picked for a use no free land is zoned for");
        // The block filling first: a block with growth under way, then the most built share.
        int pickFirst = -1;
        GrowRules.Pick(grid, cols, Only(0), 4711, 40, out _, out pickFirst, out _, out _);
        if (pickFirst >= 0)
        {
            int block = grid.Columns[pickFirst].Block;
            var busy = (GrowColumn[])cols.Clone();
            int marked = 0;
            for (int c = 0; c < busy.Length && marked < GrowRules.BlockSitesMost; c++)
            {
                if (grid.Columns[c].Block == block && busy[c].Free && busy[c].Use != 0)
                {
                    busy[c].Free = false;
                    busy[c].Growing = true;
                    marked++;
                }
            }
            bool other = !GrowRules.Pick(grid, busy, Only(0), 4711, 40, out _, out int elsewhere, out _, out _) || grid.Columns[elsewhere].Block != block;
            Check(marked < GrowRules.BlockSitesMost || other, "growth: a block with " + GrowRules.BlockSitesMost + " sites going up takes no more at once: the next starts on another block");
        }
        Check(GrowRules.UseToGrow(new[] { 50, 70, 60, 30 }, new[] { 9, 9, 9, 9 }) == 1 && GrowRules.UseToGrow(new[] { 50, 70, 60, 30 }, new[] { 9, 0, 9, 9 }) == 2
              && GrowRules.UseToGrow(new[] { 30, 40, 40, 45 }, new[] { 9, 9, 9, 9 }) == -1 && GrowRules.UseToGrow(new[] { -1, -1, -1, -1 }, new[] { 9, 9, 9, 9 }) == -1
              && GrowRules.UseToGrow(new[] { 80, 10, 10, 10 }, new[] { 0, 9, 9, 9 }) == -1,
            "growth: the use that grows is the one furthest over its threshold with land zoned for it at its least width; none under its threshold or with demand not counted");
        Check(GrowRules.MayStart(GrowRules.SitesMost - 1, 10, 10) && !GrowRules.MayStart(GrowRules.SitesMost, 10, 10) && !GrowRules.MayStart(0, 9, 10),
            "growth: a new site starts only under the cap of " + GrowRules.SitesMost + " under way and once its gap has passed");
    }

    // ------------------------------------------------------------------ the steady pace, simulated on the real plan

    private static void GrowPaceChecks()
    {
        List<LotView> lots = BuildModePlan();
        CellGrid grid = CellGrid.Of(lots);
        GrowColumn[] cols = GrowLand(lots, grid);
        var byId = lots.ToDictionary(l => l.Id, l => l);
        int[] demand = { 60, 60, 60, 60 };
        int seed = 1234567, next = GrowRules.FirstDay, id = 0, mostAtOnce = 0, mostOnBlock = 0, started = 0, ended = 0;
        var under = new List<int[]>();   // first column, count, end day, block
        var blockFirst = new Dictionary<int, int>();
        var blockLast = new Dictionary<int, int>();
        var siteDays = new List<int>();
        for (int day = 1; day <= 720; day++)
        {
            for (int i = under.Count - 1; i >= 0; i--)
            {
                if (under[i][2] <= day)
                {
                    for (int k = 0, at = under[i][0]; k < under[i][1]; k++, at = grid.Next(at))
                    {
                        cols[at].Growing = false;   // stands: no longer free, no longer under way
                    }
                    blockLast[under[i][3]] = day;
                    under.RemoveAt(i);
                    ended++;
                }
            }
            int[] freeOf = new int[4];
            foreach (GrowColumn c in cols)
            {
                freeOf[Math.Max(0, c.Use)] += c.Free && c.Use >= 0 ? 1 : 0;
            }
            if (!GrowRules.MayStart(under.Count, day, next))
            {
                continue;
            }
            if (GrowRules.UseToGrow(demand, freeOf) < 0 || !GrowRules.Pick(grid, cols, demand, seed, day, out int use, out int first, out int length, out int count))
            {
                continue;
            }
            int start = GrowRules.Take(grid, first, length, count);
            int width = 0, depth = byId[grid.Columns[start].LotId].Depth;
            for (int k = 0, at = start; k < count; k++, at = grid.Next(at))
            {
                cols[at].Free = false;
                cols[at].Growing = true;
                width += byId[grid.Columns[at].LotId].Width;
            }
            int cls = count >= 3 ? 3 : count == 2 ? 1 : 0;
            int area = Math.Max(1, LandRules.UsualArea(width, depth, cls));
            int storeys = Math.Max(1, area / Math.Max(1, width * depth));
            int plans = day + GrowRules.PlansDays(seed, ++id);
            int decided = PlanningRules.AfterWorkingDays(plans, PlanningRules.WorkingDays(area));
            int built = PlanningRules.AfterWorkingDays(decided, TenderRules.StandardDays(PlansRules.Hours((BuildUse)use, area, storeys, ProjectKind.Plot)));
            int block = grid.Columns[start].Block;
            under.Add(new[] { start, count, built, block });
            siteDays.Add(built - day);
            if (!blockFirst.ContainsKey(block))
            {
                blockFirst[block] = day;
            }
            started++;
            next = day + GrowRules.GapDays;
            mostAtOnce = Math.Max(mostAtOnce, under.Count);
            mostOnBlock = Math.Max(mostOnBlock, under.GroupBy(u => u[3]).Max(g => g.Count()));
        }
        var fills = blockFirst.Keys.Where(b => blockLast.ContainsKey(b)).Select(b => blockLast[b] - blockFirst[b]).OrderBy(d => d).ToList();
        int median = fills.Count > 0 ? fills[fills.Count / 2] : -1;
        int inMonths = fills.Count(d => d <= 60);
        siteDays.Sort();
        int siteMedian = siteDays.Count > 0 ? siteDays[siteDays.Count / 2] : -1;
        Console.WriteLine("  growth pace on the real plan (demand 60 for every use, seed " + seed + "): " + started + " site(s) begun, " + ended + " standing in 720 days; at most " + mostAtOnce
                          + " under way at once, " + mostOnBlock + " on one block; a site takes " + (siteDays.Count > 0 ? siteDays[0] : 0) + " to " + (siteDays.Count > 0 ? siteDays[siteDays.Count - 1] : 0)
                          + " days (median " + siteMedian + "); " + fills.Count + " block(s) grown, each from its first start to its last building in " + (fills.Count > 0 ? fills[0] : 0) + " to "
                          + (fills.Count > 0 ? fills[fills.Count - 1] : 0) + " days (median " + median + "), " + inMonths + " within 60.");
        Check(started > 10 && mostAtOnce <= GrowRules.SitesMost && mostAtOnce >= 2, "growth pace: never more than " + GrowRules.SitesMost + " sites under way in Uptown at once (" + mostAtOnce + "), " + started + " begun");
        Check(mostOnBlock <= GrowRules.BlockSitesMost, "growth pace: never more than " + GrowRules.BlockSitesMost + " under way on one block (" + mostOnBlock + ")");
        Check(siteMedian >= 14 && siteMedian <= 45, "growth pace: a grown building takes 14 to 45 days from its land to its keys, as the player's own (median " + siteMedian + ")");
        Check(median >= 25 && median <= 60 && inMonths * 3 >= fills.Count * 2, "growth pace: a block fills over one to two months of 30 days (median " + median + " days; " + inMonths + " of " + fills.Count + " within 60)");
    }

    // ------------------------------------------------------------------ Let it grow's tiers

    private static void GrowTierChecks()
    {
        Check(GrowRules.TierCost(1000000L, GrowTier.Basic) < GrowRules.TierCost(1000000L, GrowTier.Standard) && GrowRules.TierCost(1000000L, GrowTier.Standard) == 1000000L
              && GrowRules.TierCost(1000000L, GrowTier.Standard) < GrowRules.TierCost(1000000L, GrowTier.Premium) && GrowRules.TierCost(-5L, GrowTier.Premium) == 0L,
            "tiers: basic costs less than the plans' estimate, standard the estimate, premium more; never below nothing");
        Check(GrowRules.TierQuality[0] < GrowRules.TierQuality[1] && GrowRules.TierQuality[1] < GrowRules.TierQuality[2] && GrowRules.TierQuality.All(q => q >= 0 && q <= 100)
              && GrowRules.TierFirm.All(f => f >= 0 && f < BuildCatalogue.FirmCount) && GrowRules.TierCostPermille.All(c => c >= BuildCatalogue.DesignCostLeast && c <= BuildCatalogue.DesignCostMost),
            "tiers: build quality rises with the tier, each drawn by one of the city's practices, each cost within what a design's cost may be");
        float market = 2.5f;
        Check(Math.Abs(GrowRules.TierRent(market, GrowTier.Basic) - 2.25f) < 0.001f && Math.Abs(GrowRules.TierRent(market, GrowTier.Standard) - 2.5f) < 0.001f
              && Math.Abs(GrowRules.TierRent(market, GrowTier.Premium) - 2.875f) < 0.001f && GrowRules.TierRent(-1f, GrowTier.Premium) == 0f
              && GrowRules.TierRentPermille.All(r => r >= 800 && r <= 1200),
            "tiers: the rent a tier asks is its share of the market's, within the game's own 0.8 to 1.2 for the player's rent");
        Check(GrowRules.TierPick(5, GrowTier.Basic, 50) == 0 && GrowRules.TierPick(5, GrowTier.Standard, 50) == 2 && GrowRules.TierPick(5, GrowTier.Premium, 50) == 4
              && GrowRules.TierPick(5, GrowTier.Standard, 80) == 3 && GrowRules.TierPick(5, GrowTier.Standard, 20) == 1 && GrowRules.TierPick(5, GrowTier.Premium, 90) == 4
              && GrowRules.TierPick(5, GrowTier.Basic, 10) == 0 && GrowRules.TierPick(0, GrowTier.Premium, 50) == -1 && GrowRules.TierPick(1, GrowTier.Premium, 90) == 0,
            "tiers: the builder picks the smallest design for basic, the middle for standard, the largest for premium, one larger for strong demand and one smaller for weak, within the list");
        var tiers = Enumerable.Range(1, 600).Select(i => GrowRules.HoldingTier(99, i)).ToList();
        int basic = tiers.Count(t => t == GrowTier.Basic), premium = tiers.Count(t => t == GrowTier.Premium);
        Check(basic > 60 && basic < 180 && premium > 90 && premium < 210 && GrowRules.HoldingTier(99, 7) == GrowRules.HoldingTier(99, 7),
            "tiers: a rival tycoon's holding builds mostly standard, now and then basic (" + basic + " of 600) or premium (" + premium + "), the same on every machine");
        int holdings = Enumerable.Range(1, 600).Count(i => GrowRules.ByHolding(5, i, true));
        Check(holdings > 150 && holdings < 250 && !Enumerable.Range(1, 50).Any(i => GrowRules.ByHolding(5, i, false)),
            "tiers: a third of the developers' sites go to a rival tycoon's holding while one is in play (" + holdings + " of 600), none without one");
    }

    // ------------------------------------------------------------------ tenants

    private static void GrowTenantChecks()
    {
        bool bands = true;
        for (int id = 1; id < 300; id++)
        {
            int high = GrowRules.LetDays(80, 3, id), good = GrowRules.LetDays(55, 3, id), even = GrowRules.LetDays(42, 3, id), low = GrowRules.LetDays(20, 3, id);
            bands &= high >= 2 && high <= 6 && good >= 5 && good <= 14 && even >= 10 && even <= 24 && low >= 21 && low <= GrowRules.LetDaysMost;
        }
        Check(bands && GrowRules.LetDays(-1, 3, 9) == GrowRules.LetDaysUnknown && GrowRules.LetDays(80, 3, 9) == GrowRules.LetDays(80, 3, 9),
            "tenants: a grown building's unit is let within 2 to 6 days at strong demand, 5 to 14 at good, 10 to 24 at its threshold, 21 to " + GrowRules.LetDaysMost
            + " under it; 21 where demand is not counted; the same on every machine");
        double meanHigh = Enumerable.Range(1, 300).Average(i => GrowRules.LetDays(75, 8, i)), meanLow = Enumerable.Range(1, 300).Average(i => GrowRules.LetDays(30, 8, i));
        Check(meanHigh < meanLow, "tenants: the stronger the demand, the sooner its tenant moves in (" + meanHigh.ToString("0.0") + " against " + meanLow.ToString("0.0") + " days)");
        Check(GrowRules.WeekFill(80, GrowTier.Basic) > GrowRules.WeekFill(80, GrowTier.Standard) && GrowRules.WeekFill(80, GrowTier.Standard) > GrowRules.WeekFill(80, GrowTier.Premium)
              && GrowRules.WeekFill(80, GrowTier.Standard) > GrowRules.WeekFill(45, GrowTier.Standard) && GrowRules.WeekFill(20, GrowTier.Premium) >= 1,
            "tenants: a Let it grow building of the player's fills faster at strong demand and at a lower rent, never not at all");
        int weeks = 0, occupancy = 0;
        while (occupancy < 90 && weeks < 30)
        {
            occupancy += GrowRules.WeekFill(60, GrowTier.Standard);
            weeks++;
        }
        Check(weeks >= 5 && weeks <= GrowRules.FillWeeks, "tenants: a standard building at good demand is let to 90 % by the developer's lettings alone in " + weeks + " weeks, over the following weeks, within its "
                                                          + GrowRules.FillWeeks + " weeks of them");
    }

    // ------------------------------------------------------------------ condition

    private static void GrowConditionChecks()
    {
        Check(GrowRules.Condition(1000, true, GrowRules.EmptyGraceDays) == 1000 && GrowRules.Condition(1000, true, GrowRules.EmptyGraceDays + 1) == 1000 - GrowRules.FallPerWeek
              && GrowRules.Condition(500, false, 90) == 500 + GrowRules.RisePerWeek && GrowRules.Condition(990, false, 0) == 1000 && GrowRules.Condition(30, true, 99) == 0,
            "condition: a building empty within its grace keeps its condition, past it falls each week; let, it is won back to full; never under nothing");
        int c = 1000, empty = 0, week = 0;
        bool down = false;
        while (!down && week < 30)
        {
            empty += 7;
            c = GrowRules.Condition(c, true, empty);
            down = GrowRules.RunDown(c, down);
            week++;
        }
        Check(down && week >= 10 && week <= 14, "condition: a new building left empty is run down in " + week + " weeks (its grace, then " + GrowRules.FallPerWeek + " a week)");
        Check(GrowRules.RunDown(399, false) && !GrowRules.RunDown(450, false) && GrowRules.RunDown(450, true) && !GrowRules.RunDown(650, true),
            "condition: the run down mark comes under 400 and goes only over 600, so it does not flicker");
        Check(GrowRules.Drop(1000) == 0 && GrowRules.Drop(800) == 0 && GrowRules.Drop(700) == 1 && GrowRules.Drop(500) == 3 && GrowRules.Drop(100) == GrowRules.DropMost && GrowRules.DropMost <= 6,
            "condition: its traffic, and with it the rent and value the game asks, drops in steps with its condition, at most " + GrowRules.DropMost + " points");
        Check(DemandRules.PeopleLift(0) == 0 && DemandRules.PeopleLift(1) == 0 && DemandRules.PeopleLift(2) == 1 && DemandRules.PeopleLift(5) == 2 && DemandRules.PeopleLift(40) == DemandRules.PeopleLiftMost,
            "foot traffic: new homes and offices on a block bring a point of traffic to its every door for each two, up to " + DemandRules.PeopleLiftMost);
    }

    // ------------------------------------------------------------------ the store

    private static void GrowStoreChecks()
    {
        var s = new BuildState { Seed = 5, FirstDay = 2, LastDay = 9, NextGrowDay = 33, CityLand = 1234567L, CityLandTax = 8910L };
        s.Lots.Add(new LotFile { Id = "u.2.1.3", Bought = 4, Zones = 3, Spaces = 2, Rows = "53", Paint = 2, Cells = "22211.000" });
        s.Lots.Add(new LotFile { Id = "u.old", Bought = 4, Zones = 1, Spaces = 1, Paint = 1 });
        s.Projects.Add(new ProjectFile { Id = 7, Kind = ProjectKind.Plot, Lot = "u.2.1.3", Grow = 3, CostPermille = 1350 });
        s.Projects.Add(new ProjectFile { Id = 8, Kind = ProjectKind.Plot, Lot = "u.old" });
        s.CouncilCells[CellRules.Key(2001, 4)] = "33333";
        s.Grows.Add(new GrowFile
        {
            Id = 11, Stage = GrowStage.Let, Owner = GrowOwner.Holding, Lot = "u.3.4.5", Spaces = 3, Use = BuildUse.Offices, Rival = 6, Holding = "Vance & Co", Tier = GrowTier.Premium,
            Day = 40, Until = 70, SiteId = 0, Design = "corner_glass", Price = 456789L, Handed = 66, Condition = 520, EmptyDays = 35, RunDown = false, Week = 12, Project = 0
        });
        s.Grows.Add(new GrowFile { Id = 12, Stage = GrowStage.Building, Owner = GrowOwner.Builder, Lot = "u.1.1.1", Spaces = 2, Use = BuildUse.Homes, SiteId = 44, Until = 90 });
        string line = BuildStore.Write(s);
        BuildState r = BuildStore.Read(line, out int version);
        LotFile l = r != null ? r.Lot("u.2.1.3") : null, old = r != null ? r.Lot("u.old") : null;
        Check(r != null && version == BuildStore.Version && l != null && l.Cells == "22211.000" && l.Paint == 2 && old != null && old.Cells == string.Empty && old.Paint == 1,
            "growth store: a site's squares' zones read back, a site without any reads as its paint");
        ProjectFile p = r != null ? r.Project(7) : null, q = r != null ? r.Project(8) : null;
        Check(p != null && p.Grow == 3 && p.CostPermille == 1350 && q != null && q.Grow == 0 && q.Look == string.Empty, "growth store: a Let it grow project keeps its tier; one without has none");
        GrowFile g = r != null ? r.Grows.FirstOrDefault(x => x.Id == 11) : null, h = r != null ? r.Grows.FirstOrDefault(x => x.Id == 12) : null;
        Check(g != null && g.Stage == GrowStage.Let && g.Owner == GrowOwner.Holding && g.Lot == "u.3.4.5" && g.Spaces == 3 && g.Use == BuildUse.Offices && g.Rival == 6 && g.Holding == "Vance & Co"
              && g.Tier == GrowTier.Premium && g.Day == 40 && g.Until == 70 && g.Design == "corner_glass" && g.Price == 456789L && g.Handed == 66 && g.Condition == 520 && g.EmptyDays == 35
              && !g.RunDown && g.Week == 12 && h != null && h.SiteId == 44 && h.Stage == GrowStage.Building && h.UnderWay && g.Standing,
            "growth store: every grown building's field reads back");
        Check(r != null && r.NextGrowDay == 33 && r.CityLand == 1234567L && r.CityLandTax == 8910L && r.CouncilCells.TryGetValue(CellRules.Key(2001, 4), out string council) && council == "33333",
            "growth store: the next start, the city's land money and the council's zoning of free squares read back");
        // A line from before: no squares, no tier, no growth parts.
        var before = new BuildState { Seed = 5, FirstDay = 2, LastDay = 9 };
        before.Lots.Add(new LotFile { Id = "u.x", Spaces = 1, Paint = 3 });
        before.Projects.Add(new ProjectFile { Id = 3, Kind = ProjectKind.Plot, Lot = "u.x" });
        string plain = BuildStore.Write(before);
        BuildState back = BuildStore.Read(plain, out _);
        Check(!plain.Contains("^u:") && !plain.Contains("^x:") && back != null && back.Grows.Count == 0 && back.CouncilCells.Count == 0 && back.NextGrowDay == 0 && back.Lot("u.x").Cells == string.Empty
              && back.Project(3).Grow == 0,
            "growth store: a save that never grew writes its line as before and reads back with nothing grown");
        // An older reader: the new fields sit after every field it knows (it stops before them) and the new parts have letters it passes over.
        string oldLine = Regex.Replace(Regex.Replace(line, @"\^[ux]:[^\^]*", string.Empty), @"\|22211\.000", string.Empty);
        BuildState older = BuildStore.Read(oldLine, out _);
        Check(older != null && older.Lot("u.2.1.3") != null && older.Lot("u.2.1.3").Paint == 2 && older.Lot("u.2.1.3").Cells == string.Empty && older.Grows.Count == 0,
            "growth store: the line without the new parts and fields still reads, as an older line does");
        // The cap: as many grown records as kept at most, the newest kept.
        var many = new BuildState { Seed = 1 };
        for (int i = 0; i < BuildState.GrowsMost + 20; i++)
        {
            many.Grows.Add(new GrowFile { Id = i + 1, Lot = "u." + i, Stage = GrowStage.Let });
        }
        BuildState capped = BuildStore.Read(BuildStore.Write(many), out _);
        Check(capped != null && capped.Grows.Count == BuildState.GrowsMost && capped.Grows[capped.Grows.Count - 1].Id == BuildState.GrowsMost + 20,
            "growth store: at most " + BuildState.GrowsMost + " grown buildings are kept, the newest");
    }

    // ------------------------------------------------------------------ the sources

    private static void GrowSourceChecks()
    {
        string grow = ModSource(@"Construction\ConstructionDirector.Grow.cs"), zone = ModSource(@"Construction\ConstructionDirector.Zone.cs");
        string tool = ModSource(@"Construction\BuildMode.Zone.cs"), ui = ModSource(@"Construction\BuildMode.Grow.cs"), letIt = ModSource(@"Construction\ConstructionDirector.LetItGrow.cs");
        string market = ModSource(@"Construction\ConstructionDirector.Market.cs"), day = ModSource(@"Construction\ConstructionDirector.Day.cs");
        string tests = ModSource(@"Construction\ConstructionDirector.GrowTests.cs"), guide = ModSource(@"Construction\ConstructionGuide.cs");
        string settings = ModSource(@"Core\Settings.cs"), wiring = ModSource(@"Wiring\Wiring.ConstructionLinks.cs"), district = ModSource(@"Uptown\DistrictBuildings.cs");
        string rules = ModSource(@"Construction\GrowRules.cs") + ModSource(@"Construction\ZoneRules.cs");
        Check(grow.Length > 0 && zone.Length > 0 && tool.Length > 0 && ui.Length > 0 && letIt.Length > 0 && tests.Length > 0, "growth sources: every file is where the checks look");
        Check(!Regex.IsMatch(grow + zone + letIt, @"void\s+(Update|LateUpdate|FixedUpdate)\s*\(") && !Regex.IsMatch(tool + ui, @"void\s+(Update|LateUpdate|FixedUpdate)\s*\("),
            "growth sources: nothing of the growth runs a frame: no Update of its own (build mode's tools are pressed, the city's growth is the day's pass)");
        Check(Regex.IsMatch(market, @"(?m)^\s+GrowDay\(day\);") && market.Contains("if (!GoingPublic.InSession)") && grow.Contains("if (GoingPublic.InSession || !Running)")
              && grow.Contains("public bool GrowthOn => Running && Settings.ConstructionGrowth && Settings.ConstructionDevelopers && !GoingPublic.InSession && DistrictLives();"),
            "growth sources: the growth is the day's pass outside a Going Public session alone, with its option and the developers' on and Uptown standing");
        Check(market.Contains("if (GrowthOn)") && market.Contains("the old lot-by-lot turns start nothing more"), "growth sources: while the city grows, the old lot-by-lot developments start nothing new");
        Check(Regex.IsMatch(rules, @"UnityEngine|(?<![A-Za-z])Random\.|DateTime|Environment\.TickCount") == false, "growth sources: the rules are game-free and draw nothing from the clock or Unity's chance");
        Check(grow.Contains("FinishOverLot(") && grow.Contains("FinishLot(") && grow.Contains("StartPlotSite(") && grow.Contains("PlanningRules.Decide(") && grow.Contains("NoteFinished(")
              && grow.Contains("RivalNews(") && grow.Contains("Paper(\"reallife_grow_paper_opened\""),
            "growth sources: a grown building goes through the player's own pipeline: City Hall's rules, a plot site of the programme, the district's one registration step, the builders' news, the City Ledger");
        Check(letIt.Contains("PlaceProject(") && letIt.Contains("DrawPlans(p)") && letIt.Contains("Apply(p)") && letIt.Contains("AskEngineer(p, false)") && letIt.Contains("ConstructionGame.DeedRent("),
            "growth sources: Let it grow places the builder's design by the player's own calls, its practice draws the plans, the builder applies, and the deed asks the tier's rent");
        Check(Regex.IsMatch(day, @"(?m)^\s+GrowProjectsDay\(day\);") && Regex.IsMatch(day, @"(?m)^\s+GrowDeedsDay\(\);"), "growth sources: the player's Let it grow projects are carried on the day's pass");
        Check(settings.Contains("public static bool ConstructionGrowth = true;") && settings.Contains("ConstructionGrowth = Bool(modId, ConstructionGrowthId, ConstructionGrowth);")
              && guide.Contains("AddToggle(Settings.ConstructionGrowthId, \"reallife_grow_option\"") && guide.Contains(".Owns(Settings.ConstructionGrowthId)")
              && ModSource(@"Meta\OptionsTree.cs").Contains(".Opt(\"construction_growth\", \"construction_buildmode\")") && letIt.Contains("if (!Settings.ConstructionGrowth)"),
            "growth sources: its option, on by default, in the construction group of the options and the Guide; off, nothing grows and Let it grow refuses");
        Check(wiring.Contains("ConstructionBridges.LetUnit = LetUnit;") && wiring.Contains("ConstructionBridges.Retraffic = Retraffic;") && wiring.Contains("ConstructionBridges.LetUnit = null;")
              && district.Contains("public static string LetUnit(string lotId)") && district.Contains("public static bool Retraffic(string lotId, int drop)"),
            "growth sources: the district's letting and traffic reached through the bridges, wired and let go");
        foreach (string t in new[] { "TestGrowZoneNow", "TestGrowPassNow", "TestGrowTenantsNow", "TestGrowTiersNow", "TestGrowConditionNow" })
        {
            Check(tests.Contains("public static ") && tests.Contains(t + "()") && guide.Contains("ConstructionDirector." + t + "()"), "growth sources: the test " + t + " is in Options > Tests");
        }
        Check(ModSource(@"TestRun\RunPlan.cs").Contains("\"reallife_grow_test_tenants\""), "growth sources: the test that stands a building for good is left to the player, with its reason");
        string lifeGrow = ModSource(@"Uptown\LifeGame.Grow.cs");
        Check(lifeGrow.Contains("GoingPublic.Foreign(reg)") && lifeGrow.Contains("reg.RentedByPlayer") && ModSource(@"Uptown\LifeDirector.Grow.cs").Contains("if (GoingPublic.InSession)"),
            "growth sources: a tenant is never put in the player's unit or another player's, never in a session");
    }

    // ------------------------------------------------------------------ the texts

    private static void GrowTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string file in new[]
                 {
                     @"Construction\ConstructionDirector.Grow.cs", @"Construction\ConstructionDirector.Zone.cs", @"Construction\ConstructionDirector.LetItGrow.cs", @"Construction\ConstructionDirector.GrowTests.cs",
                     @"Construction\BuildMode.Zone.cs", @"Construction\BuildMode.Grow.cs", @"Construction\BuildMode.Window.cs", @"Construction\GrowRules.cs", @"Construction\BuildModeRules.cs",
                     @"Construction\ConstructionGuide.cs", @"Construction\ConstructionApp.City.cs", @"Construction\ConstructionApp.Projects.cs", @"Construction\ConstructionDirector.BuildMode.cs"
                 })
        {
            foreach (Match m in Regex.Matches(ModSource(file), "\"(reallife_grow_[a-z0-9_]*[a-z0-9])\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        keys.Add("reallife_opt_tip_construction_growth");
        Check(keys.Count >= 60, "growth texts: their keys are found: " + keys.Count);
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("growth texts", words, codes))
        {
            return;
        }
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            string missing = null;
            foreach (string key in keys)
            {
                if (!words[l].TryGetValue(key, out string value) || value.Length == 0 || value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Contains(" - ")
                    || value.Contains("  "))
                {
                    missing = missing ?? key;
                }
                else if (l > 0 && words[0].TryGetValue(key, out string en) && NamesOf(en) != NamesOf(value))
                {
                    missing = missing ?? key + " (its names in curly brackets)";
                }
            }
            Check(missing == null, "growth texts: every key is in " + codes[l] + ".json, in the style, with the English names (" + (missing ?? "all") + ")");
        }
        foreach (string key in keys.Where(k => k.Contains("_notice_") || k.Contains("_answer_")))
        {
            string text = words[0].TryGetValue(key, out string t) ? t : string.Empty;
            Check(text.Length <= 240, "growth texts: " + key + " at most 240 characters (" + text.Length + ")");
        }
        var starts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Buildings that grow" }, { "pt", GrowLineStart("pt") }, { "zh-cn", GrowLineStart("zh-cn") }, { "de", GrowLineStart("de") }, { "fr", GrowLineStart("fr") },
            { "es", GrowLineStart("es") }, { "ru", GrowLineStart("ru") }, { "nl", GrowLineStart("nl") }, { "tr", GrowLineStart("tr") }
        };
        string[] marks = { "Fixed: ", "Corrigido: ", "已修复：", "Behoben: ", "Corrigé : ", "Corregido: ", "Исправлено: ", "Opgelost: ", "Düzeltildi: " };
        for (int l = 0; l < words.Length; l++)
        {
            string start = starts.TryGetValue(codes[l], out string s) ? s : "?";
            Check(start.Length > 3 && WhatsNewHas(words[l], start), "growth texts: What's new has buildings that grow in some row in " + codes[l]);
            if (words[l].TryGetValue("reallife_changes_next", out string next) && next.Contains(start))
            {
                int at = next.IndexOf(start, StringComparison.Ordinal);
                bool fixBefore = marks.Any(mark => { int f = next.IndexOf(mark, StringComparison.Ordinal); return f >= 0 && f < at; });
                Check(next.IndexOf(start, at + 1, StringComparison.Ordinal) < 0 && !fixBefore && !next.Contains("  "),
                    "growth texts: its line once in the next row, among the feature lines (no fix line before it), no double space, in " + codes[l]);
            }
        }
        var bullets = new Dictionary<string, string>(StringComparer.Ordinal) { { "en", "\n- Let it grow: " }, { "pt", GrowGuideStart("pt") }, { "zh-cn", GrowGuideStart("zh-cn") } };
        var heads = new Dictionary<string, string>(StringComparer.Ordinal) { { "en", "\n# Build mode\n" }, { "pt", "\n# Modo de construção\n" }, { "zh-cn", "\n# 建造模式\n" } };
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            string page = words[l].TryGetValue("reallife_guide_construction", out string g) ? g : string.Empty;
            int head = page.IndexOf(heads[codes[l]], StringComparison.Ordinal), bullet = bullets[codes[l]].Length > 4 ? page.IndexOf(bullets[codes[l]], StringComparison.Ordinal) : -1;
            int nextHead = head >= 0 ? page.IndexOf("\n# ", head + 2, StringComparison.Ordinal) : -1;
            Check(head >= 0 && bullet > head && (nextHead < 0 || bullet < nextHead) && page.Contains("{grow_sites_most}") && page.Contains("{grow_cost_premium}"),
                "growth texts: the Guide's build mode section has Let it grow, the city's growth and condition, with their figures, in " + codes[l]);
        }
    }

    // The start of each language's What's new line and Guide bullet, as written (the first words before the colon).
    private static readonly Dictionary<string, string> GrowStarts = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "pt", "Prédios que crescem" }, { "zh-cn", "会生长的建筑" }, { "de", "Wachsende Gebäude" }, { "fr", "Des bâtiments qui poussent" }, { "es", "Edificios que crecen" },
        { "ru", "Растущие здания" }, { "nl", "Gebouwen die groeien" }, { "tr", "Büyüyen binalar" }
    };

    private static readonly Dictionary<string, string> GrowGuideStarts = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "pt", "\n- Deixe crescer: " }, { "zh-cn", "\n- 让它生长：" }
    };

    // A demand that wants one use alone (a test's, or a check's).
    private static int[] Only(int use)
    {
        var d = new[] { -1, -1, -1, -1 };
        d[use] = 100;
        return d;
    }

    private static string GrowLineStart(string code) => GrowStarts.TryGetValue(code, out string s) ? s : "?";

    private static string GrowGuideStart(string code) => GrowGuideStarts.TryGetValue(code, out string s) ? s : "?";
}
