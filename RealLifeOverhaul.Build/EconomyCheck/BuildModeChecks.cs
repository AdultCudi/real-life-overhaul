using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Uptown;

// Build mode (Scripts/Construction: CellRules, BuildModeRules, BuildMode, BuildModeGame, ConstructionDirector.BuildMode and its
// tests; Scripts/Uptown: CompleteRules, DemandRules, LifeDirector.Complete and .Market; plans/build-mode.md): the zoning cells
// over the real plan, the cell under a point, the site the cells picked make, prices by cells, the rows kept as digits, a site's
// ground cut to its rows, the state machine, the cells' colours, a placed design's warnings, the ghost's ground, the district's
// demand and market level, the whole-building check and the owner rule, the store's new fields; then the sources and the texts.
internal static partial class Program
{
    private static void BuildModeChecks()
    {
        BuildModeGridChecks();
        BuildModeSiteChecks();
        BuildModeStateChecks();
        BuildModeColourChecks();
        BuildModeDemandChecks();
        BuildModeCompleteChecks();
        BuildModeStoreChecks();
        BuildModeSourceChecks();
        BuildModeTextChecks();
    }

    private static List<LotView> BuildModePlan()
    {
        var lots = new List<LotView>(LayoutRules.Lots.Count);
        foreach (Lot l in LayoutRules.Lots)
        {
            lots.Add(new LotView
            {
                Id = l.Id, Street = l.Street, Number = l.Number, X = l.X, Z = l.Z, Yaw = l.Yaw, Width = l.Width, Depth = l.Depth, Class = (int)l.Class, Block = l.Block, Strip = l.Strip,
                Index = l.Index, Spaces = l.Spaces, Size = l.Size, Open = true
            });
        }
        return lots;
    }

    private static void BuildModeGridChecks()
    {
        List<LotView> lots = BuildModePlan();
        CellGrid grid = CellGrid.Of(lots);
        int spaces = lots.Sum(l => Math.Max(1, l.Spaces));
        Check(grid.Columns.Count == spaces && grid.CellCount > 8000 && grid.CellCount < 20000, "build mode cells: one column a space of the plan (" + grid.Columns.Count + " of " + spaces + "), " + grid.CellCount + " cells");
        Check(CellRules.RowsFor(39) == 5 && CellRules.RowsFor(35) == 5 && CellRules.RowsFor(32) == 4 && CellRules.RowsFor(3) == 1, "build mode cells: rows of about 7.5 m, 39 m 5, 35 m 5, 32 m 4, never none");
        // The cells cover each lot's ground exactly: their areas sum to the lots' and none of a block overlaps another.
        double cellArea = 0, lotArea = lots.Sum(l => (double)l.Width * l.Depth);
        bool square = true;
        for (int c = 0; c < grid.Columns.Count; c++)
        {
            for (int r = 0; r < grid.Columns[c].Rows; r++)
            {
                grid.Rect(c, r, 0f, out float x1, out float z1, out float x2, out float z2);
                cellArea += (x2 - x1) * (double)(z2 - z1);
                float w = x2 - x1, d = z2 - z1;
                square &= w > 5.5f && d > 5.5f && w < 10.5f && d < 10.5f;
            }
        }
        Check(Math.Abs(cellArea - lotArea) < lotArea * 0.001, "build mode cells: the cells cover the lots' ground exactly (" + cellArea.ToString("0") + " of " + lotArea.ToString("0") + " m2)");
        Check(square, "build mode cells: every cell a square of about 7 to 9 m (none under 5.5 m or over 10.5 m a side)");
        bool overlap = false;
        foreach (var block in Enumerable.Range(0, grid.Columns.Count).GroupBy(c => grid.Columns[c].Block))
        {
            var rects = new List<float[]>();
            foreach (int c in block)
            {
                for (int r = 0; r < grid.Columns[c].Rows; r++)
                {
                    grid.Rect(c, r, 0.01f, out float x1, out float z1, out float x2, out float z2);
                    rects.Add(new[] { x1, z1, x2, z2 });
                }
            }
            for (int i = 0; i < rects.Count && !overlap; i++)
            {
                for (int j = i + 1; j < rects.Count && !overlap; j++)
                {
                    overlap = rects[i][0] < rects[j][2] && rects[j][0] < rects[i][2] && rects[i][1] < rects[j][3] && rects[j][1] < rects[i][3];
                }
            }
        }
        Check(!overlap, "build mode cells: no two cells of a block overlap");
        // The cell under each cell's middle is that cell; the middle of a street is none.
        bool found = true;
        for (int c = 0; c < grid.Columns.Count && found; c++)
        {
            for (int r = 0; r < grid.Columns[c].Rows && found; r++)
            {
                grid.Rect(c, r, 0f, out float x1, out float z1, out float x2, out float z2);
                CellAt at = grid.At((x1 + x2) * 0.5f, (z1 + z2) * 0.5f);
                found = at.Column == c && at.Row == r;
            }
        }
        Check(found && grid.At(LayoutRules.StreetX(4), LayoutRules.AvenueZ(1) + 60f).None, "build mode cells: the cell under every cell's middle is itself, and a street's middle has none");
        // Row 0 is at the street: for every column its first row's middle is nearer its front line than its last row's.
        bool street = true;
        for (int c = 0; c < grid.Columns.Count && street; c++)
        {
            CellColumn col = grid.Columns[c];
            grid.Rect(c, 0, 0f, out float ax1, out float az1, out float ax2, out float az2);
            grid.Rect(c, col.Rows - 1, 0f, out float bx1, out float bz1, out float bx2, out float bz2);
            bool alongX = col.Facing == 0 || col.Facing == 180;
            float first = alongX ? (az1 + az2) * 0.5f : (ax1 + ax2) * 0.5f, last = alongX ? (bz1 + bz2) * 0.5f : (bx1 + bx2) * 0.5f;
            street = Math.Abs(first - col.Front) < Math.Abs(last - col.Front);
        }
        Check(street, "build mode cells: every column's first row is the one at its street");
        // The ids are stable and unique.
        var ids = new HashSet<string>();
        bool unique = true;
        for (int c = 0; c < grid.Columns.Count; c++)
        {
            for (int r = 0; r < grid.Columns[c].Rows; r++)
            {
                unique &= ids.Add(CellRules.Id(grid.Columns[c], r));
            }
        }
        Check(unique && CellGrid.Of(lots).CellCount == grid.CellCount, "build mode cells: every cell's id unique, the grid the same made twice");
    }

    private static void BuildModeSiteChecks()
    {
        List<LotView> lots = BuildModePlan();
        CellGrid grid = CellGrid.Of(lots);
        // An open run's first space: five single spaces along one strip.
        int first = -1;
        for (int c = 0; c < grid.Columns.Count && first < 0; c++)
        {
            int at = c, n = 0;
            while (at >= 0 && n < 8)
            {
                n++;
                at = grid.Next(at);
            }
            first = n >= 8 ? c : -1;
        }
        Check(first >= 0, "build mode sites: a strip of eight spaces or more is found");
        if (first < 0)
        {
            return;
        }
        int second = grid.Next(first), third = grid.Next(second);
        string why = CellRules.Site(grid, new[] { new CellAt(first, 3) }, 7, out CellSite one);
        Check(why == null && one.Count == 1 && one.Rows[0] == 4 && one.Cells == 4 && one.MinRows == 4, "build mode sites: one cell four rows back picks the cells between it and the street (4 cells)");
        why = CellRules.Site(grid, new[] { new CellAt(first, 1), new CellAt(third, 0) }, 7, out _);
        Check(why == CellRules.NotJoined, "build mode sites: two columns with one between them unpicked make no site (" + why + ")");
        why = CellRules.Site(grid, new[] { new CellAt(first, 1), new CellAt(second, 0), new CellAt(third, 2) }, 7, out CellSite three);
        Check(why == null && three.Count == 3 && three.Cells == 2 + 1 + 3 && three.MinRows == 1, "build mode sites: three neighbouring columns of 2, 1 and 3 rows make one site of 6 cells");
        int other = -1;
        for (int c = 0; c < grid.Columns.Count && other < 0; c++)
        {
            other = grid.Columns[c].Strip != grid.Columns[first].Strip ? c : -1;
        }
        Check(CellRules.Site(grid, new[] { new CellAt(first, 1), new CellAt(other, 1) }, 7, out _) == CellRules.TwoStrips, "build mode sites: cells of two strips make no site");
        Check(CellRules.Site(grid, new[] { new CellAt(first, 0) }, 7, out _) == CellRules.TooFew, "build mode sites: one cell alone is too few");
        var eight = new List<CellAt>();
        for (int at = first, k = 0; k < 8; k++, at = grid.Next(at))
        {
            eight.Add(new CellAt(at, 0));
        }
        Check(CellRules.Site(grid, eight, 7, out _) == CellRules.TooWide, "build mode sites: eight columns are wider than a site takes");
        Check(CellRules.Site(grid, new CellAt[0], 7, out _) == CellRules.NoCells, "build mode sites: nothing picked is no site");
        // Prices by cells: a whole column is the space's price; rows bought deeper later cost the difference, to the last unit.
        Check(CellRules.ColumnPrice(1000, 5, 5) == 1000 && CellRules.ColumnPrice(1000, 2, 5) == 400 && CellRules.ColumnPrice(1000, 9, 5) == 1000 && CellRules.ColumnPrice(0, 2, 5) == 0,
            "build mode prices: a column's rows at their share of the space's price, every row the space's");
        bool sums = true;
        for (int had = 1; had <= 5; had++)
        {
            for (int rows = had; rows <= 5; rows++)
            {
                sums &= CellRules.ColumnPrice(98765, had, 5) + CellRules.DeeperPrice(98765, had, rows, 5) == CellRules.ColumnPrice(98765, rows, 5);
            }
        }
        Check(sums, "build mode prices: rows bought and then bought deeper cost what the deeper rows cost at once, to the last unit");
        // The rows kept as digits, joined, split, read back; anything odd reads as every row.
        Check(CellRules.RowsText(new[] { 5, 5 }, 2, 5) == "" && CellRules.RowsText(new[] { 3, 5 }, 2, 5) == "35" && CellRules.RowsAt("35", 0, 5) == 3 && CellRules.RowsAt("35", 1, 5) == 5
              && CellRules.RowsAt("", 4, 5) == 5, "build mode rows: digits a column, none for every row");
        Check(CellRules.JoinRows("3", 1, "", 1, 5) == "35" && CellRules.JoinRows("", 1, "", 2, 5) == "" && CellRules.SplitRows("35", 0, 5) == "3" && CellRules.SplitRows("35", 1, 5) == ""
              && CellRules.CleanRows("3x", 2) == "" && CellRules.CleanRows("34", 3) == "" && CellRules.CleanRows("34", 2) == "34" && CellRules.MinRows("534", 3, 5) == 3,
            "build mode rows: joined, split, cleaned and the shallowest read back");
        // A site's ground cut to its rows keeps its front on the street, for every way a strip faces.
        bool front = true;
        foreach (int yaw in new[] { 0, 90, 180, 270 })
        {
            var lot = new LotView { X = 100, Z = 200, Yaw = yaw, Width = 14, Depth = 35 };
            LotView cut = CellRules.Trim(lot, 3, 5);
            int facing = (yaw + 180) % 360;
            float before = facing == 0 ? lot.Z + lot.Depth * 0.5f : facing == 180 ? lot.Z - lot.Depth * 0.5f : facing == 90 ? lot.X + lot.Depth * 0.5f : lot.X - lot.Depth * 0.5f;
            float after = facing == 0 ? cut.Z + cut.Depth * 0.5f : facing == 180 ? cut.Z - cut.Depth * 0.5f : facing == 90 ? cut.X + cut.Depth * 0.5f : cut.X - cut.Depth * 0.5f;
            front &= cut.Depth == 21 && Math.Abs(before - after) < 0.01f && cut.Width == 14;
        }
        Check(front && CellRules.Trim(new LotView { Depth = 35, Yaw = 0 }, 5, 5).Depth == 35, "build mode ground: a site bought three rows of five deep is 21 m, its front still on its street, every way it faces");
        Check(CellRules.RowsNeeded(22.5f, 7.8f) == 3 && CellRules.RowsNeeded(22.5f, 7f) == 4 && CellRules.RowsNeeded(21f, 7f) == 3, "build mode footprints: the rows a design's depth needs");
        CellRules.FootprintLot(grid, first, 2, out float fx, out float fz, out float ff, out float fw, out float fd);
        CellColumn a = grid.Columns[first], b = grid.Columns[second];
        Check(Math.Abs(fw - ((a.B - a.A) + (b.B - b.A))) < 0.01f && ff == a.Facing && Math.Abs(fd - a.Depth) < 0.01f, "build mode ghost: a footprint's ground is its columns' frontage together, facing their street");
    }

    private static void BuildModeStateChecks()
    {
        var closed = new BuildModeState();
        BuildModeState s = BuildModeRules.Next(closed, BuildInput.Open, BuildTool.Land, false);
        Check(s.Step == BuildStep.Idle && s.Tool == BuildTool.Land && !s.ViewOnly, "build mode states: the button opens it on the land tool");
        Check(BuildModeRules.Next(closed, BuildInput.OpenInSession, BuildTool.Land, false).ViewOnly, "build mode states: in a Going Public session it opens view only");
        Check(BuildModeRules.Next(closed, BuildInput.Press, BuildTool.Land, true).Step == BuildStep.Closed, "build mode states: closed, a press does nothing");
        s = BuildModeRules.Next(s, BuildInput.Press, BuildTool.Land, false);
        Check(s.Step == BuildStep.Idle, "build mode states: a press off the cells pans, nothing picked");
        s = BuildModeRules.Next(BuildModeRules.Next(s, BuildInput.Press, BuildTool.Land, true), BuildInput.Release, BuildTool.Land, false);
        Check(s.Step == BuildStep.Picked, "build mode states: a press on a cell and its release pick");
        Check(BuildModeRules.Next(s, BuildInput.Design, BuildTool.Land, false).Step == BuildStep.Picked, "build mode states: a design is only the place tool's");
        BuildModeState place = BuildModeRules.Next(BuildModeRules.Next(s, BuildInput.Tool, BuildTool.Place, false), BuildInput.Press, BuildTool.Place, true);
        place = BuildModeRules.Next(BuildModeRules.Next(place, BuildInput.Release, BuildTool.Place, false), BuildInput.Design, BuildTool.Place, false);
        Check(place.Step == BuildStep.Ghost && place.Tool == BuildTool.Place, "build mode states: a footprint and a design stand the ghost");
        BuildModeState asking = BuildModeRules.Next(place, BuildInput.Confirm, BuildTool.Place, false);
        Check(asking.Step == BuildStep.Confirming && BuildModeRules.Next(asking, BuildInput.Press, BuildTool.Place, true).Step == BuildStep.Confirming,
            "build mode states: Build here asks the question box, which holds the map");
        Check(BuildModeRules.Next(asking, BuildInput.No, BuildTool.Place, false).Step == BuildStep.Ghost, "build mode states: no goes back to the ghost");
        BuildModeState yes = BuildModeRules.Next(asking, BuildInput.Yes, BuildTool.Place, false);
        Check(yes.Step == BuildStep.Picked && yes.Tool == BuildTool.Land, "build mode states: yes makes the project and picks its site under the land tool");
        BuildModeState view = BuildModeRules.Next(BuildModeRules.Next(closed, BuildInput.OpenInSession, BuildTool.Land, false), BuildInput.Tool, BuildTool.Place, false);
        view.Step = BuildStep.Ghost;
        Check(BuildModeRules.Next(view, BuildInput.Confirm, BuildTool.Place, false).Step == BuildStep.Ghost, "build mode states: view only never reaches the question box");
        Check(BuildModeRules.Next(place, BuildInput.Clear, BuildTool.Place, false).Step == BuildStep.Picked && BuildModeRules.Next(s, BuildInput.Clear, BuildTool.Land, false).Step == BuildStep.Idle,
            "build mode states: Clear lets the ghost go, then what is picked");
        // Every input in every state reached: never an unknown step or tool, and the map shut always closes it.
        var seen = new HashSet<string>();
        var queue = new Queue<BuildModeState>();
        queue.Enqueue(closed);
        bool sound = true;
        int states = 0;
        while (queue.Count > 0 && states < 2000)
        {
            BuildModeState at = queue.Dequeue();
            if (!seen.Add(at.Step + "/" + at.Tool + "/" + at.ViewOnly))
            {
                continue;
            }
            states++;
            foreach (BuildInput input in Enum.GetValues(typeof(BuildInput)))
            {
                for (int t = 0; t < (int)BuildTool.Count; t++)
                {
                    foreach (bool onCell in new[] { false, true })
                    {
                        BuildModeState next = BuildModeRules.Next(at, input, (BuildTool)t, onCell);
                        sound &= Enum.IsDefined(typeof(BuildStep), next.Step) && Enum.IsDefined(typeof(BuildTool), next.Tool) && next.Tool != BuildTool.Count;
                        sound &= input != BuildInput.MapClosed || next.Step == BuildStep.Closed;
                        sound &= !(next.ViewOnly && next.Step == BuildStep.Confirming);
                        queue.Enqueue(next);
                    }
                }
            }
        }
        Check(sound && states > 20, "build mode states: every input in each of " + states + " states reached keeps a known step and tool, the map shut always closes it, view only never confirms");
    }

    private static void BuildModeColourChecks()
    {
        // The owner's first test (26 Oct 2026): for sale, the player's land and their building, another owner's, each apart; edges green when zoned, red when demolished.
        var free = new BuildModeRules.ColumnFacts { Free = true, Use = -1, MainUse = 0, Zones = 3 };
        var mine = new BuildModeRules.ColumnFacts { Mine = true, MineRows = 3, Use = -1, MainUse = 0, Zones = 3 };
        int fill, line;
        BuildModeRules.Look(free, 0, 5, BuildView.Status, out fill, out line);
        bool forSale = fill == BuildModeRules.ForSale && line == BuildModeRules.LineForSale;
        BuildModeRules.Look(mine, 2, 5, BuildView.Status, out fill, out line);
        bool yours = fill == BuildModeRules.YourLand && line == BuildModeRules.LineYours;
        BuildModeRules.Look(mine, 3, 5, BuildView.Status, out fill, out line);
        bool behind = fill == BuildModeRules.ForSale && line == BuildModeRules.LineForSale;
        Check(forSale && yours && behind, "build mode colours: for sale white with its bright edge, your land in your colour, the rows behind yours for sale to you");
        var building = new BuildModeRules.ColumnFacts { Building = true, MyBuilding = true, Use = 2, MainUse = 2, Zones = 6 };
        BuildModeRules.Look(building, 0, 5, BuildView.Status, out int myFill, out int myLine);
        building.MyBuilding = false;
        BuildModeRules.Look(building, 0, 5, BuildView.Status, out int theirFill, out int theirLine);
        Check(myFill == BuildModeRules.YourBuilding && theirFill == BuildModeRules.Taken && myFill != BuildModeRules.YourLand && myLine == BuildModeRules.LineYours && theirLine == BuildModeRules.LinePlain,
            "build mode colours: your building solid in your colour, apart from your bare land and from another owner's building");
        mine.Paint = (int)BuildUse.Offices + 1;
        BuildModeRules.Look(mine, 0, 5, BuildView.Status, out fill, out line);
        Check(fill == BuildModeRules.Offices && line == BuildModeRules.LineZoned, "build mode colours: your land zoned in its use's colour, its edge green");
        mine.PaintWaits = true;
        BuildModeRules.Look(mine, 0, 5, BuildView.Status, out fill, out line);
        Check(fill == BuildModeRules.YourLand && line == BuildModeRules.LineWaits, "build mode colours: a use waiting on the council, its edge amber, not green");
        var cleared = new BuildModeRules.ColumnFacts { Mine = true, MineRows = 5, Cleared = true, Use = -1, MainUse = 0 };
        var falling = new BuildModeRules.ColumnFacts { Building = true, Demolishing = true, Use = 1, MainUse = 1 };
        BuildModeRules.Look(cleared, 0, 5, BuildView.Status, out _, out int clearedLine);
        BuildModeRules.Look(falling, 0, 5, BuildView.Status, out _, out int fallingLine);
        BuildModeRules.Look(falling, 0, 5, BuildView.Zoning, out _, out int fallingZoned);
        Check(clearedLine == BuildModeRules.LineDemolished && fallingLine == BuildModeRules.LineDemolished && fallingZoned == BuildModeRules.LineDemolished,
            "build mode colours: ground cleared and a building coming down edged red in every view");
        var site = new BuildModeRules.ColumnFacts { Mine = true, MineRows = 5, UnderWay = true, Progress = 40, Use = -1 };
        int built = 0;
        for (int r = 0; r < 5; r++)
        {
            BuildModeRules.Look(site, r, 5, BuildView.UnderWay, out fill, out _);
            built += fill == BuildModeRules.UnderWayDone ? 1 : 0;
        }
        BuildModeRules.Look(new BuildModeRules.ColumnFacts { Developer = true, Free = true }, 0, 5, BuildView.Status, out int devFill, out _);
        Check(built == 2 && devFill == BuildModeRules.Developer, "build mode colours: a site under way shows its share built by rows (40% of 5 rows: 2), the developers' their own");
        // The zoning view: every cell by its council zoning, the plan's main use where zoned, else the one use, else mixed or none; for sale keeps its edge.
        Check(BuildModeRules.ZoneFill(1 | 2, 0, 0) == BuildModeRules.Homes && BuildModeRules.ZoneFill(4 | 2, 0, 2) == BuildModeRules.Offices && BuildModeRules.ZoneFill(8, 0, 0) == BuildModeRules.Warehouses
              && BuildModeRules.ZoneFill(1 | 4, 0, 3) == BuildModeRules.Mixed && BuildModeRules.ZoneFill(0, 0, 0) == BuildModeRules.NoZone && BuildModeRules.ZoneFill(0, 2, 0) == BuildModeRules.Shops,
            "build mode zoning view: the main use, the one use, mixed, none, and a painted use first");
        BuildModeRules.Look(free, 0, 5, BuildView.Zoning, out fill, out line);
        bool zoningFree = fill == BuildModeRules.Homes && line == BuildModeRules.LineForSale;
        BuildModeRules.Look(building, 0, 5, BuildView.Zoning, out fill, out line);
        bool zoningBuilding = fill == BuildModeRules.Offices;
        Check(zoningFree && zoningBuilding && BuildModeRules.ViewFor(BuildTool.Land) == BuildView.Zoning && BuildModeRules.ViewFor(BuildTool.Zone) == BuildView.Zoning
              && BuildModeRules.ViewFor(BuildTool.Place) == BuildView.Status, "build mode zoning view: free land in its zoning with the for-sale edge, a building in its use; Buy land and Zone open on it");
        // Land value and occupancy.
        Check(BuildModeRules.ValueStepOf(100, 100, 600) == 0 && BuildModeRules.ValueStepOf(600, 100, 600) == BuildModeRules.ValueSteps - 1 && BuildModeRules.ValueStepOf(5, 5, 5) == BuildModeRules.ValueSteps / 2
              && BuildModeRules.ValueStepOf(350, 100, 600) == 2 && BuildModeRules.ValueStepOf(9999, 100, 600) == BuildModeRules.ValueSteps - 1, "build mode land value: the lowest, the highest, the middle, all alike");
        var vacant = new BuildModeRules.ColumnFacts { Building = true, Vacant = true, Use = 0 };
        BuildModeRules.Look(vacant, 0, 5, BuildView.Occupancy, out int vacantFill, out _);
        BuildModeRules.Look(building, 0, 5, BuildView.Occupancy, out int letFill, out _);
        BuildModeRules.Look(free, 0, 5, BuildView.Occupancy, out int noneFill, out int noneLine);
        Check(vacantFill == BuildModeRules.Vacant && letFill == BuildModeRules.Let && noneFill == BuildModeRules.Nothing && noneLine == BuildModeRules.LineForSale,
            "build mode occupancy: room to let and full apart, land with no building only its edge");
        // Every view's every cell has a fill and an edge in the palette, and an edge of its own.
        bool inPalette = true;
        foreach (BuildModeRules.ColumnFacts f in new[] { free, mine, building, cleared, falling, site, vacant, new BuildModeRules.ColumnFacts { Use = -1, MainUse = -1 } })
        {
            for (int v = 0; v < (int)BuildView.Count; v++)
            {
                for (int r = 0; r < 5; r++)
                {
                    BuildModeRules.Look(f, r, 5, (BuildView)v, out fill, out line);
                    inPalette &= fill >= 0 && fill < BuildModeRules.LinePlain && line >= BuildModeRules.LinePlain && line < BuildModeRules.PaletteSize;
                }
            }
        }
        Check(inPalette && BuildModeRules.Palette.Length == BuildModeRules.PaletteSize && BuildModeRules.Palette.All(p => p.Length == 4)
              && BuildModeRules.Palette[BuildModeRules.LineZoned][1] > 180 && BuildModeRules.Palette[BuildModeRules.LineZoned][0] < 120
              && BuildModeRules.Palette[BuildModeRules.LineDemolished][0] > 200 && BuildModeRules.Palette[BuildModeRules.LineDemolished][1] < 100
              && BuildModeRules.Palette[BuildModeRules.ForSale][3] >= 100 && BuildModeRules.Palette[BuildModeRules.LineForSale][3] >= 170,
            "build mode colours: every view's cells in the palette, a fill inside and a line round; zoned green, demolished red, for sale readable on the dark map");
        // The legend names every fill and edge each view draws.
        var fills = new int[BuildModeRules.LegendMost];
        var lines = new int[BuildModeRules.LegendMost];
        var keys = new string[BuildModeRules.LegendMost];
        bool legends = true;
        for (int v = 0; v < (int)BuildView.Count; v++)
        {
            int n = BuildModeRules.Legend((BuildView)v, fills, lines, keys);
            legends &= n > 1 && n <= BuildModeRules.LegendMost && keys.Take(n).All(k => k != null && k.StartsWith("reallife_bld_", StringComparison.Ordinal));
        }
        int zn = BuildModeRules.Legend(BuildView.Zoning, fills, lines, keys);
        legends &= Enumerable.Range(BuildModeRules.Homes, 4).All(u => fills.Take(zn).Contains(u)) && fills.Take(zn).Contains(BuildModeRules.Mixed) && lines.Take(zn).Contains(BuildModeRules.LineZoned)
                   && lines.Take(zn).Contains(BuildModeRules.LineDemolished);
        int sn = BuildModeRules.Legend(BuildView.Status, fills, lines, keys);
        legends &= fills.Take(sn).Contains(BuildModeRules.YourLand) && fills.Take(sn).Contains(BuildModeRules.YourBuilding) && fills.Take(sn).Contains(BuildModeRules.ForSale) && fills.Take(sn).Contains(BuildModeRules.Taken);
        Check(legends && BuildModeRules.ViewKeys.Length == (int)BuildView.Count, "build mode legend: each view's, the four uses, mixed, your land and your building, the green and red lines");
        // The mixed zoning's colour is none of the game's: given the game's own (office blue among them), it stands apart from each.
        byte[][] game = { new byte[] { 46, 204, 113 }, new byte[] { 241, 196, 15 }, new byte[] { 52, 152, 219 }, new byte[] { 230, 126, 34 }, new byte[] { 255, 255, 255 }, new byte[] { 155, 89, 182 } };
        byte[] mixed = BuildModeRules.MixedColour(game);
        Check(game.All(g => BuildModeRules.Apart(mixed, g) >= BuildModeRules.ColourApart) && BuildModeRules.MixedColour(new[] { BuildModeRules.MixedChoices[0] }) != BuildModeRules.MixedChoices[0],
            "build mode colours: the mixed zoning takes a colour none of the game's types or the player's is near");
        // Picks let go: a picked square pressed again goes with those behind it; adding takes a column to the deeper row.
        var depth = new[] { -1, 2, 4, -1 };
        bool toggled = BuildModeRules.Toggle(depth, 1, 1, true) && depth[1] == 0 && BuildModeRules.Toggle(depth, 1, 0, true) && depth[1] == -1
                       && BuildModeRules.Toggle(depth, 0, 3, false) && depth[0] == 3 && !BuildModeRules.Toggle(depth, 2, 2, false) && depth[2] == 4
                       && !BuildModeRules.Toggle(depth, 3, 1, true) && !BuildModeRules.Toggle(depth, 9, 1, false) && !BuildModeRules.Toggle(depth, 0, -1, false);
        Check(toggled, "build mode picks: a picked square let go with those behind it, a deeper add, nothing for an unpicked square or one off the grid");
        // A placed design's warnings: each alone, and which stop Build here.
        var ok = new PlaceAsk { Mine = true, HasDesign = true, Columns = 2, SiteColumns = 2, RowsNeeded = 3, RowsHad = 5, Storeys = 6, Limit = 12, ParkingNeeded = 0, Zoned = true };
        Check(BuildModeRules.Warnings(ok) == 0 && BuildModeRules.MayBuild(0), "build mode warnings: none on a design that fits");
        PlaceAsk tall = ok;
        tall.Storeys = 13;
        PlaceAsk park = ok;
        park.ParkingNeeded = 4;
        PlaceAsk zone = ok;
        zone.Zoned = false;
        PlaceAsk cells = ok;
        cells.RowsNeeded = 6;
        PlaceAsk theirs = ok;
        theirs.Mine = false;
        PlaceAsk session = ok;
        session.Session = true;
        Check(BuildModeRules.Warnings(tall) == BuildModeRules.WarnTall && BuildModeRules.MayBuild(BuildModeRules.WarnTall), "build mode warnings: too tall is said in red and left to the planners");
        Check(BuildModeRules.Warnings(park) == BuildModeRules.WarnParking && BuildModeRules.MayBuild(BuildModeRules.WarnParking), "build mode warnings: parking asked is said in red");
        park.ParkingUnder = true;
        Check(BuildModeRules.Warnings(park) == 0, "build mode warnings: parking under the building answers it");
        Check(BuildModeRules.Warnings(zone) == BuildModeRules.WarnZone && !BuildModeRules.MayBuild(BuildModeRules.WarnZone), "build mode warnings: the wrong zone stops it");
        Check(BuildModeRules.Warnings(cells) == BuildModeRules.WarnCells && BuildModeRules.Warnings(theirs) == BuildModeRules.WarnNotYours && BuildModeRules.Warnings(session) == BuildModeRules.WarnSession
              && !BuildModeRules.MayBuild(BuildModeRules.WarnCells | BuildModeRules.WarnTall), "build mode warnings: too few squares, not yours and a session each stop it");
        Check(BuildModeRules.WarnKeys.Length == BuildModeRules.WarnCount, "build mode warnings: a text for each");
        Check(BuildModeRules.PaintOf(2) == 3 && BuildModeRules.PaintOf(-1) == 0 && BuildModeRules.PaintOf(4) == 0, "build mode zones: a use's paint, none for none");
    }

    private static DistrictMarket BuildModeMarket(int homes, int homesVacant, int shops, int shopsVacant, int offices, int officesVacant, int warehouses, int warehousesVacant, int traffic)
    {
        return new DistrictMarket
        {
            Units = new[] { homes, shops, offices, warehouses }, Vacant = new[] { homesVacant, shopsVacant, officesVacant, warehousesVacant }, Traffic = traffic
        };
    }

    private static void BuildModeDemandChecks()
    {
        // Demand rises as a use's vacancies fall, every use.
        bool falls = true;
        for (int use = 0; use < DemandRules.Uses; use++)
        {
            int last = int.MaxValue;
            for (int vacant = 0; vacant <= 40; vacant += 5)
            {
                var v = new int[4];
                v[use] = vacant;
                DistrictMarket m = BuildModeMarket(200, v[0], 60, v[1], 20, v[2], 10, v[3], 50);
                int d = DemandRules.Demand(m, use);
                falls &= d <= last;
                last = d;
            }
            falls &= DemandRules.Demand(BuildModeMarket(200, 0, 60, 0, 20, 0, 10, 0, 50), use) > DemandRules.Demand(BuildModeMarket(200, use == 0 ? 60 : 0, 60, use == 1 ? 30 : 0, 20, use == 2 ? 12 : 0, 10, use == 3 ? 6 : 0, 50), use);
        }
        Check(falls, "build mode demand: each use's demand rises as its vacancies fall");
        Check(DemandRules.Demand(BuildModeMarket(400, 0, 20, 0, 20, 0, 10, 0, 50), 1) > DemandRules.Demand(BuildModeMarket(100, 0, 20, 0, 20, 0, 10, 0, 50), 1),
            "build mode demand: more people living there want more shops");
        Check(DemandRules.Demand(BuildModeMarket(200, 0, 60, 0, 20, 0, 10, 0, 90), 1) > DemandRules.Demand(BuildModeMarket(200, 0, 60, 0, 20, 0, 10, 0, 20), 1),
            "build mode demand: more traffic past the doors wants more shops");
        Check(DemandRules.Demand(BuildModeMarket(100, 0, 100, 0, 60, 0, 10, 0, 50), 0) > DemandRules.Demand(BuildModeMarket(100, 0, 10, 0, 5, 0, 10, 0, 50), 0),
            "build mode demand: more jobs want more homes");
        Check(DemandRules.Level(0) == DemandRules.LevelLeast && DemandRules.Level(100) == DemandRules.LevelMost && DemandRules.Level(50) == 1000 && DemandRules.Level(30) < DemandRules.Level(70),
            "build mode market: oversupply lowers rents and values, shortage raises them, within an eighth either way");
        DistrictMarket glut = BuildModeMarket(200, 120, 60, 40, 20, 15, 10, 8, 30);
        DistrictMarket short_ = BuildModeMarket(200, 0, 60, 0, 20, 0, 10, 0, 80);
        Check(DemandRules.DistrictLevel(glut) < 1000 && DemandRules.DistrictLevel(short_) > 1000 && DemandRules.DistrictLevel(glut) >= DemandRules.LevelLeast && DemandRules.DistrictLevel(short_) <= DemandRules.LevelMost,
            "build mode market: a glut of empty units lowers the district's level, a shortage raises it, inside the caps");
        Check(DemandRules.Step(1000, 1120) == 1000 + DemandRules.LevelStep && DemandRules.Step(1000, 880) == 1000 - DemandRules.LevelStep && DemandRules.Step(1005, 1008) == 1008 && DemandRules.Step(0, 1100) == 1010,
            "build mode market: the level moves a step a day at most");
        Check(DemandRules.VacancyPermille(BuildModeMarket(0, 0, 10, 5, 0, 0, 0, 0, 50), 1) == 500 && DemandRules.VacancyPermille(BuildModeMarket(0, 0, 0, 0, 0, 0, 0, 0, 50), 2) == 0,
            "build mode market: vacancies in thousandths, none with no units");
        Check(DemandRules.DensityLift(0, 5) == 0 && DemandRules.DensityLift(5, 5) == DemandRules.DensityLiftMost && DemandRules.DensityLift(9, 5) == DemandRules.DensityLiftMost && DemandRules.DensityLift(3, 0) == 0
              && DemandRules.DensityLift(2, 5) > 0 && DemandRules.DensityLift(2, 5) < DemandRules.DensityLiftMost, "build mode values: a block's free land built lifts its lots' traffic, up to the cap, none on a block that had none");
        Check(BuildModeRules.DeveloperMay(-1) && !BuildModeRules.DeveloperMay(20) && BuildModeRules.DeveloperMay(BuildModeRules.DeveloperDemandLeast), "build mode developers: they build where a use is wanted, as before where demand is not known");
        Check(BuildModeRules.CouncilLift(50) == 0 && BuildModeRules.CouncilLift(100) == 150 && BuildModeRules.CouncilLift(0) == -150 && BuildModeRules.CouncilLift(-1) == 0, "build mode council: a wanted use's request passes more often");
        Check(DemandRules.UseOfType("ba:buildingtype_residential") == 0 && DemandRules.UseOfType("ba:buildingtype_warehouse") == 3 && DemandRules.UseOfType("ba:buildingtype_parking") == -1
              && Enumerable.Range(0, 4).All(u => DemandRules.UseOfType(BuildCatalogue.UseTypes[u]) == u), "build mode demand: the game's types in the construction game's use order");
        // The owner's first test (26 Oct 2026, day 47): Uptown a third empty by design (its third kept free to let) read as demand 0 for
        // every use and the rents on their floor at once. Against the share the district keeps free to let, the same counts are balanced.
        DistrictMarket third = new DistrictMarket
        {
            Units = new[] { 300, 90, 40, 20 }, Vacant = new[] { 102, 29, 12, 7 }, Filling = new int[4], Normal = new[] { 330, 330, 330, 450 }, Traffic = 55
        };
        DistrictMarket target = third;
        target.Normal = null;
        Check(Enumerable.Range(0, 3).All(u => DemandRules.Demand(target, u) == 0) && Enumerable.Range(0, 4).All(u => DemandRules.Demand(third, u) >= 30),
            "build mode demand: a third empty read against an 8% target is 0 everywhere; against the third the district keeps free to let it is a working market ("
            + string.Join(", ", Enumerable.Range(0, 4).Select(u => DemandRules.Demand(third, u))) + ")");
        Check(Math.Abs(DemandRules.VacancyPull(third, 0)) <= 1 && DemandRules.VacancyPull(target, 0) < -50 && DemandRules.NormalPermille(third, 3) == 450 && DemandRules.NormalPermille(target, 3) == DemandRules.TargetVacancyPermille,
            "build mode demand: inside the band round the normal share only a lean, far past it a strong pull; the normal share the district's own, else the target");
        DistrictMarket filling = third;
        filling.Vacant = new[] { 180, 29, 12, 7 };
        filling.Filling = new[] { 120, 0, 0, 0 };
        DistrictMarket glutted = filling;
        glutted.Filling = new int[4];
        Check(DemandRules.SettledVacancyPermille(filling, 0) == 200 && DemandRules.Demand(filling, 0) >= 50 && DemandRules.Demand(glutted, 0) < DemandRules.Demand(filling, 0) - 30,
            "build mode demand: a new building's units still being let are not oversupply; the same empty units settled are");
        Check(DemandRules.Step(0, DemandRules.LevelLeast) == 1000 - DemandRules.LevelStep && DemandRules.Step(1000, DemandRules.LevelLeast) == 990,
            "build mode market: from the first day the level starts even and moves one step, never straight to the floor");
        var kept = new LifeState { MarketLevel = 950, MarketDay = 47 };
        LifeState back = LifeStore.Read(LifeStore.Write(kept), out _);
        LifeState before = LifeStore.Read("v=1^f:^g:2", out _);
        LifeState odd = LifeStore.Read("v=1^f:^g:2^m:abc", out _);
        Check(back != null && back.MarketLevel == 950 && back.MarketDay == 47 && before != null && before.MarketLevel == 0 && odd != null && odd.MarketLevel == 0
              && !LifeStore.Write(new LifeState()).Contains("^m:"), "build mode market: the level kept in the district's line and read back; a line without it reads none, a bad one none");
    }

    private static CompleteFacts BuildModeWhole()
    {
        return new CompleteFacts
        {
            Street = "reallife:street_uptown_maple", Number = 2, DistrictStreet = true, PlanNumber = 2, AtAddress = 1, HasBusiness = true, BusinessName = "Maple Stores", DisplayName = "Maple Stores",
            Type = "ba:buildingtype_retail", Size = "ba:buildingsize_c", RecordType = "ba:buildingtype_retail", RecordSize = "ba:buildingsize_c", RivalOwner = "rival_3", ForRent = false,
            TypeWords = true, ScheduleDays = 7, InRegistrations = true, InLookup = true, InCityList = true, DoorOnMesh = true
        };
    }

    private static void BuildModeCompleteChecks()
    {
        CompleteFacts whole = BuildModeWhole();
        int all = CompleteRules.Passed(whole);
        Check(CompleteRules.All(all) && CompleteRules.FirstFailed(all) == null, "whole buildings: a building with every item passes");
        var broken = new List<KeyValuePair<string, CompleteFacts>>();
        CompleteFacts f;
        f = whole; f.Number = 4; broken.Add(new KeyValuePair<string, CompleteFacts>("address", f));
        f = whole; f.AtAddress = 2; broken.Add(new KeyValuePair<string, CompleteFacts>("address", f));
        f = whole; f.BusinessName = ""; broken.Add(new KeyValuePair<string, CompleteFacts>("name", f));
        f = whole; f.Type = "ba:buildingtype_office"; broken.Add(new KeyValuePair<string, CompleteFacts>("type", f));
        f = whole; f.RivalOwner = ""; broken.Add(new KeyValuePair<string, CompleteFacts>("owner", f));
        f = whole; f.TypeWords = false; broken.Add(new KeyValuePair<string, CompleteFacts>("card", f));
        f = whole; f.ScheduleDays = 0; broken.Add(new KeyValuePair<string, CompleteFacts>("card", f));
        f = whole; f.InLookup = false; broken.Add(new KeyValuePair<string, CompleteFacts>("BizMan", f));
        f = whole; f.InCityList = false; broken.Add(new KeyValuePair<string, CompleteFacts>("filter", f));
        f = whole; f.DoorOnMesh = false; broken.Add(new KeyValuePair<string, CompleteFacts>("door", f));
        f = whole; f.HasBusiness = false; f.ForRent = false; broken.Add(new KeyValuePair<string, CompleteFacts>("market", f));
        bool each = true;
        foreach (var b in broken)
        {
            int passed = CompleteRules.Passed(b.Value);
            each &= !CompleteRules.All(passed) && CompleteRules.FirstFailed(passed) == b.Key;
        }
        Check(each, "whole buildings: each item fails alone when its fact is missing (the 2 Maple Street warehouse's filter, Birchwood's card and owner among them)");
        f = whole; f.HasBusiness = false; f.BusinessName = null; f.ForRent = true;
        Check(CompleteRules.All(CompleteRules.Passed(f)), "whole buildings: an empty unit for rent is whole: named by its address, listed, to be let by the game");
        f = whole; f.RivalOwner = null; f.PlayerRecord = true; f.PlayerDeed = true; f.HasBusiness = false; f.BusinessName = null;
        Check(CompleteRules.All(CompleteRules.Passed(f)), "whole buildings: the player's own building with its deed is whole");
        f.OnSale = true;
        Check(!CompleteRules.All(CompleteRules.Passed(f)), "whole buildings: the player's building on the city's sale list is not");
        // The owner as BizMan writes it.
        f = whole; f.RivalOwner = null;
        Check(CompleteRules.Owner(f) == null, "whole buildings: no deed, rival, sale or government type: BizMan's Undefined");
        f.OnSale = true;
        Check(CompleteRules.Owner(f) == "city", "whole buildings: on the sale list, the city's");
        f.Government = true;
        Check(CompleteRules.Owner(f) == "government" && CompleteRules.Owner(whole) == "rival", "whole buildings: the government's type first, then the deed, a rival, the sale list");
        var rivals = new List<string>();
        CompleteRules.Rivals(new[] { "r9", "r1", "", "r9", null, "r3" }, rivals);
        Check(string.Join(",", rivals) == "r1,r3,r9", "whole buildings: the rivals owning buildings, sorted, no repeats");
        string pick = CompleteRules.PickOwner(rivals, "reallife:street_uptown_maple", 2);
        Check(pick != null && rivals.Contains(pick) && pick == CompleteRules.PickOwner(new List<string>(rivals), "reallife:street_uptown_maple", 2) && CompleteRules.PickOwner(new List<string>(), "x", 1) == null,
            "whole buildings: the owner given is one of them, the same every time for the same address, none with no rival");
        string line = CompleteRules.Line("its stand", "2 Maple Street", whole, all);
        Check(line.StartsWith("Uptown check (a whole building, its stand, 2 Maple Street): address PASS") && line.EndsWith(": PASS") && line.Contains("occupants PASS"), "whole buildings: the line names every item");
    }

    private static void BuildModeStoreChecks()
    {
        var s = new BuildState();
        s.Lots.Add(new LotFile { Id = "uptown_2_09_0001", Bought = 3, Paid = 1234, Spaces = 2, Rows = "35", Paint = 3 });
        s.Lots.Add(new LotFile { Id = "uptown_2_09_0005", Bought = 4, Paid = 99, Spaces = 1 });
        string line = BuildStore.Write(s);
        BuildState back = BuildStore.Read(line, out _);
        Check(back != null && back.Lots.Count == 2 && back.Lots[0].Rows == "35" && back.Lots[0].Paint == 3 && back.Lots[1].Rows == "" && back.Lots[1].Paint == 0 && BuildStore.Write(back) == line,
            "build mode store: a site's rows and paint read back, and the line written again the same");
        string older = Regex.Replace(line, @"\|35\|3(?=;)", string.Empty);
        older = Regex.Replace(older, @"\|\|0(?=\^)", string.Empty);
        BuildState old = BuildStore.Read(older, out _);
        Check(old != null && old.Lots.Count == 2 && old.Lots[0].Rows == "" && old.Lots[0].Paint == 0 && old.Lots[0].Spaces == 2 && older.Length < line.Length,
            "build mode store: a line from before build mode reads every row and no paint");
    }

    private static void BuildModeSourceChecks()
    {
        string mode = ModSource(@"Construction\BuildMode.cs");
        string window = ModSource(@"Construction\BuildMode.Window.cs");
        string game = ModSource(@"Construction\BuildModeGame.cs");
        string director = ModSource(@"Construction\ConstructionDirector.BuildMode.cs");
        string tests = ModSource(@"Construction\ConstructionDirector.BuildModeTests.cs");
        string panel = ModSource(@"Construction\BuildModePanel.cs");
        string layerTests = ModSource(@"Construction\ConstructionDirector.BuildModeLayerTests.cs");
        string rules = ModSource(@"Construction\CellRules.cs") + ModSource(@"Construction\BuildModeRules.cs") + ModSource(@"Uptown\CompleteRules.cs") + ModSource(@"Uptown\DemandRules.cs");
        string all = mode + window + game + director + tests + rules + panel + layerTests;
        Check(!Regex.IsMatch(all, @"using System\.Linq|\.Where\(|\.Select\(|\.OrderBy\(|FindObjectsOfType|FindObjectsByType|GameObject\.Find\b|Physics\."), "build mode sources: no LINQ, no scene search, no physics query");
        Check(!Regex.IsMatch(rules, @"UnityEngine|SaveGameManager|GameManager"), "build mode sources: the rules are game-free");
        // The game's GetBuildingRegistration throws for a free lot and adds a blank registration for one it has not met; the
        // owner's first open of the fixed window died on it (26 Oct 2026). Build mode asks through HeldUnitsGame.Registration.
        Check(!(mode + window + game + director + panel).Contains("BuildingHelper.GetBuildingRegistration(") && game.Contains("HeldUnitsGame.Registration(")
              && MethodText(mode, "private void ReadFacts()").Contains("catch (Exception e)"),
            "build mode sources: a lot's registration is read only where a building stands, and one lot that fails does not keep the window shut");
        // The palette is made unreadable by Apply(false, true); reading it back threw on every tool pressed (26 Oct 2026).
        Check(!Regex.IsMatch(mode + window + game + panel, @"\.GetPixels?\(") && MethodText(game, "public static Color32 Swatch(").Contains("layer.Colours"),
            "build mode sources: the legend's colours come from the palette kept in memory, never read back from the unreadable texture");
        Check(MethodText(mode, "private void Awake()").Contains("enabled = false;") && mode.Contains("Guard.For(\"construction\", \"BuildMode\", typeof(BuildMode))")
              && MethodText(mode, "private void Update()").Contains("Guard.Fail(FrameGuard, e, this)") && MethodText(mode, "private void Update()").Contains("HotSpots.Time(HotUpdate)"),
            "build mode sources: its one Update guarded and timed, and not enabled until it opens: nothing a frame while closed");
        Check(!Regex.IsMatch(mode + window + director, @"\bCityMap\b|(?<![A-Za-z])Input\.|EventSystem|CityMapCam") && game.Contains("cam.enabled = !held") && game.Contains("DisableTimeControl(locked)"),
            "build mode sources: the map, the mouse and the time controls touched from BuildModeGame alone; the map's camera held and the time locked there");
        Check(MethodText(mode, "private void Leave(string why)").Contains("BuildModeGame.HoldCamera(false);") && MethodText(mode, "private void Leave(string why)").Contains("BuildModeGame.LockTime(false);")
              && MethodText(mode, "private void Leave(string why)").Contains("DropGhost();") && MethodText(mode, "private void OnUpdate()").Contains("if (!BuildModeGame.MapOpen)"),
            "build mode sources: every way out gives the camera and the time back and takes the ghost away, the map shut by any means among them");
        Check(MethodText(director, "public string BuyCells(").Contains("ConstructionGame.Take(price, \"reallife_transaction_bld_land\"") && MethodText(director, "public string CellsBlock(").Contains("FreeSpace(lot)")
              && MethodText(director, "public string BuyCells(").Contains("Join(first)"), "build mode sources: cells bought by the Land tab's own transaction and checks, joined to the player's site beside them");
        Check(MethodText(director, "public string PaintSite(").Contains("AskZone(siteId, (BuildUse)use)") && MethodText(director, "public string PaintSite(").Contains("SetUse(p, (BuildUse)use)"),
            "build mode sources: a use the council does not allow goes to City Hall through the existing request; an allowed one is the plans' use");
        string place = MethodText(director, "public int PlaceProject(");
        Check(place.Contains("NewPlot(siteId, out why)") && place.Contains("SetUse(p, use)") && place.Contains("SetDesign(p, i)") && place.Contains("SetParking(p, parkingUnder)") && place.Contains("Split(siteId)")
              && place.Contains("BuildModeRules.MayBuild(warnings)"), "build mode sources: Build here makes the project through the app's own calls, the footprint split and joined to its own site first");
        Check(ModSource(@"Construction\ConstructionDirector.Market.cs").Contains("&& DemandWants(use))") && ModSource(@"Construction\ConstructionDirector.City.cs").Contains("+ DemandLift(use)"),
            "build mode sources: the developers start where their use is wanted, and the council weighs the demand");
        Check(ModSource(@"Construction\ConstructionDirector.Projects.cs").Contains("lot = WithRows(id, lot);") && ModSource(@"Construction\ConstructionDirector.Join.cs").Contains("CellRules.JoinRows(a.Rows, a.Spaces, b.Rows, b.Spaces, stripRows)"),
            "build mode sources: a site's ground follows its rows, and joining keeps each lot's rows");
        string links = ModSource(@"Wiring\Wiring.BuildModeLinks.cs");
        string bridges = ModSource(@"Construction\BuildModeBridges.cs");
        foreach (Match m in Regex.Matches(bridges, @"public static (?:[A-Za-z<>,\[\] ]+) ([A-Z][A-Za-z]+);"))
        {
            string name = m.Groups[1].Value;
            Check(bridges.Contains(name + " = null;") && links.Contains("BuildModeBridges." + name + " = ") && links.Contains("BuildModeBridges." + name + " = null;"), "build mode sources: the bridge " + name + " is wired in and out and put back");
        }
        string wiring = ModSource(@"Wiring\Wiring.ConstructionLinks.cs");
        Check(wiring.Contains("BuildModeLinks.Connect();") && wiring.Contains("BuildModeLinks.Disconnect();"), "build mode sources: wired with the construction game");
        string settings = ModSource(@"Core\Settings.cs");
        string guide = ModSource(@"Construction\ConstructionGuide.cs");
        Check(settings.Contains("public static bool ConstructionBuildMode = true;") && settings.Contains("ConstructionBuildMode = Bool(modId, ConstructionBuildModeId, ConstructionBuildMode);")
              && ModSource(@"Meta\OptionsTree.cs").Contains("S(\"buildmode\").Guide(\"construction\").Need(\"construction\").Opts(\"construction_buildmode\")") && guide.Contains(".Owns(Settings.ConstructionBuildModeId)")
              && guide.Contains("AddToggle(Settings.ConstructionBuildModeId, \"reallife_bld_mode_option\"") && director.Contains("if (!Settings.ConstructionBuildMode)")
              && ModSource(@"Construction\ConstructionApp.Overview.cs").Contains("if (!Settings.ConstructionBuildMode)"), "build mode sources: its option on by default, read, in the options' section; off, no button and no opening");
        foreach (string t in new[] { "TestBuildModeOpenNow", "TestBuildModePriceNow", "TestBuildModeBuyNow", "TestBuildModeZoneNow", "TestBuildModePlaceNow", "TestBuildModeBulldozeNow", "TestBuildModeCompleteNow", "TestBuildModeStandNow",
                     "TestBuildModeLayerNow", "TestBuildModeZoningNow", "TestBuildModeWindowNow" })
        {
            Check(guide.Contains("ConstructionDirector." + t + "()") && ((tests + layerTests).Contains("public static void " + t + "()") || (tests + layerTests).Contains("public static bool " + t + "()")),
                "build mode sources: the test " + t + " is in the feature list and a public static method");
        }
        // The owner's first test (26 Oct 2026): the layer copied the map's zone material, which draws only where something hides it.
        string layer = MethodText(game, "public static Layer MakeLayer(");
        Check(layer.Contains("material.SetFloat(ZTestTransparent, (float)CompareFunction.LessEqual);") && layer.Contains("material.SetFloat(ZTestForward, (float)CompareFunction.LessEqual);")
              && game.Contains("Shader.PropertyToID(\"_ZTestTransparent\")") && game.Contains("Shader.PropertyToID(\"_ZTestDepthEqualForOpaque\")") && layer.Contains("go.layer = zone.gameObject.layer;")
              && Regex.IsMatch(game, @"LayerUp = 0\.[2-4]\d*f;"), "build mode layer: the copied zone material's depth test set back to the ordinary one, so the cells draw over empty ground, on the zones' own layer, over the ground's grass and paving");
        Check(MethodText(game, "public static byte[][] GameColours()").Contains("data.mapFilterColor") && MethodText(game, "public static byte[][] GameColours()").Contains("filters.forSaleFilterColor")
              && MethodText(game, "public static byte[][] GameColours()").Contains("filters.playerFilterColor") && MethodText(game, "public static byte[][] GameColours()").Contains("BuildModeRules.MixedColour(taken)"),
            "build mode colours: the uses, for sale and the player's read from the game's own map filters; mixed a colour apart from them");
        Check(mode.Contains("[DefaultExecutionOrder(-31000)]") && MethodText(mode, "private void OnUpdate()").Contains("BuildModeGame.TakeCancel();") && game.Contains("PlayerAction.Cancel.Reset()")
              && MethodText(mode, "private void OnUpdate()").IndexOf("BuildModeGame.CancelPressed && (HasPicks", StringComparison.Ordinal) >= 0,
            "build mode picks: Esc clears what is picked first and is taken before the game's own key handling sees it; with nothing picked the game's Esc leaves");
        Check(panel.Contains("HelpFrame.Make(") && panel.Contains("OptionRows.Take(") && panel.Contains("GameBar.Make(") && panel.Contains("_frame.Search.gameObject.SetActive(false);")
              && !Regex.IsMatch(panel, @"new Canvas|AddComponent<Canvas>|(?<![A-Za-z])Input\.|EventSystem") && MethodText(mode, "private void OnUpdate()").Contains("_panel.Fold(true);"),
            "build mode window: the game's help screen in the middle with the settings menu's rows and the top bar's bars, no search field, no canvas of its own; it steps aside while squares are picked");
        Check(MethodText(mode, "private void Awake()").Contains("enabled = false;") && !panel.Contains("void Update(") && !panel.Contains("void LateUpdate("),
            "build mode window: nothing a frame of its own; the page written on a pick or an action");
        Check(Regex.Matches(tests, "ModeLine\\(").Count >= 16 && tests.Contains("\"Construction check (build mode: \""), "build mode sources: each test writes its check line");
        string stand = ModSource(@"Uptown\LifeDirector.Stand.cs");
        string load = ModSource(@"Uptown\LifeDirector.cs");
        Check(stand.Contains("if (!CompleteOne(made, f, \"its stand\"))") && load.Contains("CompleteAll(\"the load\");"), "whole buildings sources: checked at every stand and at every load");
        string complete = ModSource(@"Uptown\LifeDirector.Complete.cs");
        Check(complete.Contains("f.Owner == FinishOwner.City && !GoingPublic.InSession") && complete.Contains("LifeGame.MendOwner(made, OwnerRivals)"), "whole buildings sources: a city's building with no owner given one, never in a session");
        string market = ModSource(@"Uptown\LifeDirector.Market.cs");
        Check(market.Contains("int wanted = GoingPublic.InSession ? 1000 : DemandRules.DistrictLevel(m);") && ModSource(@"Uptown\LifeGame.Market.cs").Contains("_record.rentMultiplierPerSqmPrice = f.RentPerPrice * level / 1000f;")
              && ModSource(@"Uptown\DistrictBuildings.cs").Contains("* LifeDirector.MarketLevel / 1000f"), "build mode market: the level feeds Uptown's record and its land price, even in a session");
    }

    private static void BuildModeTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string name in new[] { @"Construction\BuildMode.cs", @"Construction\BuildMode.Window.cs", @"Construction\CellRules.cs", @"Construction\BuildModeRules.cs",
                     @"Construction\ConstructionDirector.BuildMode.cs", @"Construction\ConstructionDirector.BuildModeTests.cs", @"Construction\BuildModePanel.cs",
                     @"Construction\ConstructionDirector.BuildModeLayerTests.cs" })
        {
            foreach (Match m in Regex.Matches(ModSource(name), "\"(reallife_bld_mode_[a-z0-9_]+[a-z0-9])\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        keys.Add("reallife_bld_button_build_mode");
        keys.Add("reallife_opt_tip_construction_buildmode");
        keys.Add("reallife_uptown_incomplete");
        foreach (string t in new[] { "open", "price", "buy", "zone", "place", "bulldoze", "complete", "stand", "layer", "zoning", "window" })
        {
            keys.Add("reallife_bld_mode_test_" + t);
        }
        Check(keys.Count > 90, "build mode texts: their keys are found: " + keys.Count);
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("build mode texts", words, codes))
        {
            return;
        }
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            string missing = null;
            foreach (string key in keys)
            {
                if (!words[l].TryGetValue(key, out string value) || value.Length == 0 || value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Contains(" - "))
                {
                    missing = missing ?? key;
                }
                else if (l > 0 && words[0].TryGetValue(key, out string en) && NamesOf(en) != NamesOf(value))
                {
                    missing = missing ?? key + " (its names in curly brackets)";
                }
            }
            Check(missing == null, "build mode texts: every key is in " + codes[l] + ".json, in the style, with the English names (" + (missing ?? "all") + ")");
        }
        foreach (string key in keys)
        {
            string text = words[0].TryGetValue(key, out string t) ? t : string.Empty;
            Check(text.Length <= 240, "build mode texts: " + key + " at most 240 characters (" + text.Length + ")");
        }
        var starts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Build mode, from the Construction app" }, { "pt", "Modo de construção, no app Construção" }, { "zh-cn", "建造模式，在建筑公司应用里" }, { "de", "Baumodus in der Bau-App" },
            { "fr", "Mode construction, depuis l'application Construction" }, { "es", "Modo construcción, desde la aplicación Construcción" }, { "ru", "Режим строительства в приложении" },
            { "nl", "Bouwmodus, vanuit de Bouw-app" }, { "tr", "İnşaat uygulamasından inşa modu" }
        };
        for (int l = 0; l < words.Length; l++)
        {
            Check(starts.TryGetValue(codes[l], out string start) && WhatsNewHas(words[l], start), "build mode texts: What's new has build mode in some row in " + codes[l]);
            if (codes[l] == "en" && words[l].TryGetValue("reallife_changes_next", out string next) && next.Contains(start))
            {
                // Among the feature lines: before the parks' line, which stays the last feature line (NavCheck's UptownParksCheck), no fix between.
                int at = next.IndexOf(start, StringComparison.Ordinal), parks = next.IndexOf("Uptown's two other squares are parks now", StringComparison.Ordinal);
                int fix = next.IndexOf("Fixed: ", at, StringComparison.Ordinal);
                Check(parks > at && (fix < 0 || fix > parks), "build mode texts: its line comes before the parks' line with no fix between them, in en");
            }
        }
        var fixes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Fixed: build mode's squares now show on empty land too." }, { "pt", "Corrigido: os quadrados do modo de construção agora aparecem também no terreno vazio." },
            { "zh-cn", "已修复：建造模式的方格现在在空地上也会显示。" }, { "de", "Behoben: Die Felder des Baumodus erscheinen jetzt auch auf leerem Land." },
            { "fr", "Corrigé : les cases du mode construction s'affichent maintenant aussi sur le terrain vide." }, { "es", "Corregido: las casillas del modo construcción ahora se ven también en el terreno vacío." },
            { "ru", "Исправлено: клетки режима строительства теперь видны и на пустой земле." }, { "nl", "Opgelost: de vakjes van de bouwmodus staan nu ook op lege grond." },
            { "tr", "Düzeltildi: inşa modunun kareleri artık boş arazide de görünüyor." }
        };
        for (int l = 0; l < words.Length; l++)
        {
            Check(fixes.TryGetValue(codes[l], out string fix) && WhatsNewHas(words[l], fix), "build mode texts: What's new has the fix of the squares on empty land in some row in " + codes[l]);
        }
        var heads = new Dictionary<string, string>(StringComparer.Ordinal) { { "en", "\n# Build mode\n" }, { "pt", "\n# Modo de construção\n" }, { "zh-cn", "\n# 建造模式\n" } };
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            string page = words[l].TryGetValue("reallife_guide_construction", out string g) ? g : null;
            Check(page == null || page.Contains(heads[codes[l]]), "build mode texts: the Guide's construction page in " + codes[l] + " has its build mode section");
        }
    }
}
