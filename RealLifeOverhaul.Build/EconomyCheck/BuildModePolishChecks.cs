using System;
using System.Collections.Generic;
using RealLifeOverhaul.Construction;

// The owner's test of the map-first build mode (26 Oct 2026, "Fix those", three screenshots): a forgiving pick as Cities Skylines
// takes a drag (along the street the press began on, the squares the tool cannot take left out, a site's widest at most, a press
// on another street starting afresh), Zone, Place and Join only on the player's own land with "Buy these squares first" for land
// for sale, the hint bar on one line where the room allows, the tour's card clear of its part and of the tool panel, the legend
// short and out of the tool panel, and the sources that keep the interface off the game's HUD and its labels at the foot.
internal static partial class Program
{
    private static void BuildModePolishChecks()
    {
        BuildModeReachChecks();
        BuildModeDragChecks();
        BuildModeOwnLandChecks();
        BuildModeFitChecks();
        BuildModeTourCardChecks();
        BuildModePolishSourceChecks();
    }

    private static void BuildModeReachChecks()
    {
        const int ok = CellRules.RunOk, gap = CellRules.RunGap, taken = CellRules.RunTaken;
        int n = CellRules.Reach(new[] { ok, ok, ok, ok, ok }, 5, 7, out int start, out int took);
        Check(n == 5 && start == 0 && took == CellRules.TookAll, "build mode drag: five spaces the tool may take are all taken (" + n + ", " + took + ")");
        n = CellRules.Reach(new[] { ok, ok, ok, ok, ok, ok, ok, ok, ok, ok }, 10, 7, out start, out took);
        Check(n == 7 && start == 0 && took == CellRules.TookWide, "build mode drag: a drag over ten spaces takes a site's widest, seven, and says so (" + n + ", " + took + ")");
        n = CellRules.Reach(new[] { ok, ok, taken, ok }, 4, 7, out start, out took);
        Check(n == 2 && start == 0 && took == CellRules.TookTaken, "build mode drag: the run stops before a space the tool cannot take (" + n + ", " + took + ")");
        n = CellRules.Reach(new[] { taken, taken, ok, ok, ok }, 5, 7, out start, out took);
        Check(n == 3 && start == 2 && took == CellRules.TookTaken, "build mode drag: a press on land the tool cannot take starts the run at the first it can (" + start + ", " + n + ")");
        n = CellRules.Reach(new[] { ok, gap, ok }, 3, 7, out start, out took);
        Check(n == 1 && took == CellRules.TookGap, "build mode drag: a gap in the street's spaces ends the run");
        n = CellRules.Reach(new[] { taken, taken }, 2, 7, out start, out took);
        Check(n == 1 && start == 0, "build mode drag: nothing the tool can take: the pressed space alone, so the panel says why");
        n = CellRules.Reach(new[] { ok }, 1, 7, out start, out took);
        Check(n == 1 && took == CellRules.TookAll && CellRules.Reach(new int[0], 3, 7, out _, out _) == 1 && CellRules.Reach(new[] { ok, ok }, 99, 7, out _, out _) == 2,
            "build mode drag: a click takes its space; an empty or short run is read safely");
        int row = CellRules.RunRow(0, 0, 1, 5, out bool deeper);
        int two = CellRules.RunRow(0, 0, 2, 5, out bool deeperTwo);
        int flat = CellRules.RunRow(0, 0, 1, 1, out bool deeperFlat);
        Check(row == 1 && deeper && two == 0 && !deeperTwo && flat == 0 && !deeperFlat && CellRules.RunRow(2, 7, 3, 5, out _) == 4,
            "build mode drag: one street square alone is taken a square deeper (a site needs two); a wider run, a one-row strip and a deep drag are left as dragged");
    }

    private sealed class RunKinds : CellRules.IRunKind
    {
        public readonly HashSet<int> Taken = new HashSet<int>();

        public int Kind(int column) => Taken.Contains(column) ? CellRules.RunTaken : CellRules.RunOk;
    }

    private static void BuildModeDragChecks()
    {
        List<LotView> lots = BuildModePlan();
        CellGrid grid = CellGrid.Of(lots);
        int first = -1, longest = 0;
        for (int c = 0; c < grid.Columns.Count; c++)
        {
            if (grid.Prev(c) >= 0 || grid.Columns[c].Rows < 3)
            {
                continue;   // a street's first space only
            }
            int at = c, k = 0;
            while (at >= 0 && k < 12)
            {
                k++;
                at = grid.Next(at);
            }
            bool corner = false;   // the block has another street's squares too
            for (int o = 0; o < grid.Columns.Count && !corner && k > longest; o++)
            {
                corner = grid.Columns[o].Block == grid.Columns[c].Block && grid.Columns[o].Strip != grid.Columns[c].Strip;
            }
            if (corner && k > longest)
            {
                longest = k;
                first = c;
            }
        }
        first = longest >= 9 ? first : -1;
        Check(first >= 0, "build mode drag: a street of nine spaces or more from its start, in a block with another street, is found on the plan (" + longest + ")");
        if (first < 0)
        {
            return;
        }
        var depth = new int[grid.Columns.Count];
        var run = new int[64];
        var runColumn = new int[64];
        var kinds = new RunKinds();
        int far = first;
        for (int k = 0; k < longest - 1; k++)
        {
            far = grid.Next(far);
        }
        // The pointer far along the street and deep in the block, then past the street's end and out on the road: always along the first street.
        grid.Rect(far, grid.Columns[far].Rows - 1, 0f, out float x1, out float z1, out float x2, out float z2);
        CellAt to = grid.Project(first, (x1 + x2) * 0.5f, (z1 + z2) * 0.5f);
        Check(to.Column == far && to.Row == grid.Columns[far].Rows - 1, "build mode drag: the pointer over a square of the street is read as that square");
        CellColumn fc = grid.Columns[first];
        bool alongX = fc.Facing == 0 || fc.Facing == 180;
        float lowEnd = fc.A - 40f, road = fc.Front - fc.Inward * 6f, mid = (fc.A + fc.B) * 0.5f;
        CellAt past = grid.Project(first, alongX ? lowEnd : road, alongX ? road : lowEnd);
        Check(past.Column == first && past.Row == 0, "build mode drag: the pointer past the street's end and out on the road is held to its first square, at the street");
        CellAt deep = grid.Project(first, alongX ? mid : fc.Front + fc.Inward * 500f, alongX ? fc.Front + fc.Inward * 500f : mid);
        Check(deep.Column == first && deep.Row == fc.Rows - 1, "build mode drag: the pointer far into the block is held to the street's deepest row");
        int other = -1;
        for (int c = 0; c < grid.Columns.Count && other < 0; c++)
        {
            other = grid.Columns[c].Block == fc.Block && grid.Columns[c].Strip != fc.Strip ? c : -1;
        }
        Check(other >= 0, "build mode drag: the block has another street's squares to drag onto");
        if (other >= 0)
        {
            grid.Rect(other, 0, 0f, out x1, out z1, out x2, out z2);
            CellAt over = grid.Project(first, (x1 + x2) * 0.5f, (z1 + z2) * 0.5f);
            Check(!over.None && grid.Columns[over.Column].Strip == fc.Strip, "build mode drag: the pointer over another street's squares of the block is read along the first street");
        }

        // A drag over nine spaces or more: seven taken from the press, as a site, and said.
        int tookWide = CellRules.DragPick(grid, new CellAt(first, 0), to, kinds, 7, true, Cleared(depth), run, runColumn, out int firstColumn);
        string why = CellRules.Site(grid, Picks(grid, depth), 7, out CellSite site);
        Check(why == null && site.Count == 7 && site.FirstColumn == first && firstColumn == first && tookWide == CellRules.TookWide && site.MinRows == grid.Columns[first].Rows,
            "build mode drag: a drag over " + longest + " spaces to the back of the block picks a site seven wide, every row, and says it was cut (" + (why ?? "site " + site.Count) + ")");
        // A space the tool cannot take in the run: the run stops before it.
        kinds.Taken.Add(grid.Next(grid.Next(grid.Next(first))));
        int tookTaken = CellRules.DragPick(grid, new CellAt(first, 1), to, kinds, 7, true, Cleared(depth), run, runColumn, out _);
        why = CellRules.Site(grid, Picks(grid, depth), 7, out site);
        Check(why == null && site.Count == 3 && tookTaken == CellRules.TookTaken, "build mode drag: land the tool cannot take ends the run, the rest kept as a site (" + (why ?? "site " + site.Count) + ")");
        kinds.Taken.Clear();
        // One street square: taken a square deeper for Buy land and Place; otherwise one square, no site.
        int tookDeeper = CellRules.DragPick(grid, new CellAt(first, 0), new CellAt(first, 0), kinds, 7, true, Cleared(depth), run, runColumn, out _);
        why = CellRules.Site(grid, Picks(grid, depth), 7, out site);
        Check(why == null && site.Cells == 2 && tookDeeper == CellRules.TookDeeper, "build mode drag: a click on a street square makes a site of two squares, and says so");
        CellRules.DragPick(grid, new CellAt(first, 0), new CellAt(first, 0), kinds, 7, false, Cleared(depth), run, runColumn, out _);
        Check(CellRules.Site(grid, Picks(grid, depth), 7, out _) == CellRules.TooFew, "build mode drag: with no deepening (Zone, Join) a click is the one square");

        // The owner's screenshot: squares picked on one street, then a drag on another street of the block.
        if (other >= 0)
        {
            Cleared(depth);
            depth[first] = 1;
            depth[grid.Next(first)] = 1;
            CellAt press = new CellAt(other, 0);
            Check(!CellRules.Touches(grid, depth, press), "build mode drag: a press on another street does not join the squares picked");
            int[] kept = (int[])depth.Clone();
            CellRules.DragPick(grid, press, press, kinds, 7, true, kept, run, runColumn, out _);
            Check(CellRules.Site(grid, Picks(grid, kept), 7, out _) == CellRules.TwoStrips,
                "build mode drag: kept, the first street's squares and the second's make no site (the refusal the owner met)");
            CellRules.DragPick(grid, press, press, kinds, 7, true, Cleared(depth), run, runColumn, out _);
            why = CellRules.Site(grid, Picks(grid, depth), 7, out site);
            Check(why == null && grid.Columns[site.FirstColumn].Strip == grid.Columns[other].Strip, "build mode drag: started afresh, the second street's squares make a site (" + (why ?? "ok") + ")");
        }
        Cleared(depth);
        depth[first] = 0;
        Check(CellRules.Touches(grid, depth, new CellAt(grid.Next(first), 2)) && !CellRules.Touches(grid, depth, new CellAt(grid.Next(grid.Next(grid.Next(first))), 0))
              && CellRules.Touches(grid, Cleared(new int[grid.Columns.Count]), new CellAt(first, 0)),
            "build mode drag: a press beside the squares picked adds to them; one apart starts afresh; with nothing picked any press is the start");
        // Squares added beside a pick past a site's widest: those farthest from the new drag let go.
        Cleared(depth);
        int at2 = first;
        for (int k = 0; k < 9; k++, at2 = grid.Next(at2))
        {
            depth[at2] = 0;
        }
        int keep = grid.Next(grid.Next(grid.Next(grid.Next(grid.Next(grid.Next(grid.Next(grid.Next(first))))))));
        bool cut = CellRules.TrimWide(grid, depth, keep, 7);
        why = CellRules.Site(grid, Picks(grid, depth), 7, out site);
        Check(cut && why == null && site.Count == 7 && depth[first] < 0 && depth[keep] == 0, "build mode drag: nine squares picked are cut to seven, the ones farthest from the drag let go");
    }

    private static int[] Cleared(int[] depth)
    {
        for (int i = 0; i < depth.Length; i++)
        {
            depth[i] = -1;
        }
        return depth;
    }

    private static List<CellAt> Picks(CellGrid grid, int[] depth)
    {
        var picked = new List<CellAt>();
        for (int c = 0; c < depth.Length; c++)
        {
            if (depth[c] >= 0)
            {
                picked.Add(new CellAt(c, depth[c]));
            }
        }
        return picked;
    }

    private static void BuildModeOwnLandChecks()
    {
        var forSale = new BuildModeRules.ColumnFacts { Free = true, Use = -1, MainUse = -1 };
        var mine = new BuildModeRules.ColumnFacts { Mine = true, Use = -1, MainUse = -1 };
        var other = new BuildModeRules.ColumnFacts { Building = true, Use = 1, MainUse = 1 };
        var myBuilding = new BuildModeRules.ColumnFacts { Building = true, MyBuilding = true, Use = 1, MainUse = 1 };
        Check(BuildModeRules.Takes(BuildTool.Land, forSale) && BuildModeRules.Takes(BuildTool.Land, mine) && !BuildModeRules.Takes(BuildTool.Land, other),
            "build mode own land: Buy land takes land for sale and the player's own, not another owner's");
        bool own = true;
        foreach (BuildTool t in new[] { BuildTool.Zone, BuildTool.Place, BuildTool.Join })
        {
            own &= BuildModeRules.Takes(t, mine) && !BuildModeRules.Takes(t, forSale) && !BuildModeRules.Takes(t, other) && !BuildModeRules.Takes(t, myBuilding);
        }
        Check(own, "build mode own land: Zone, Place and Join take only the player's own land");
        Check(BuildModeRules.Takes(BuildTool.Bulldoze, myBuilding) && !BuildModeRules.Takes(BuildTool.Bulldoze, other) && !BuildModeRules.Takes(BuildTool.Refurbish, mine),
            "build mode own land: Bulldoze and Refurbish take only the player's buildings");
        HintFacts Picked(BuildTool tool, bool mineLand, bool sale) => new HintFacts
        {
            State = new BuildModeState { Tool = tool, Step = BuildStep.Picked }, Picked = true, SiteOk = true, PickedMine = mineLand, PickedForSale = sale
        };
        Check(BuildModeHintRules.Hint(Picked(BuildTool.Zone, false, true)) == BuildModeHintRules.BuyFirst
              && BuildModeHintRules.Hint(Picked(BuildTool.Zone, false, false)) == "reallife_bld_hint_zone_not_yours"
              && BuildModeHintRules.Hint(Picked(BuildTool.Zone, true, false)) == "reallife_bld_hint_zone_pick_for",
            "build mode own land: Zone on land for sale says to buy it first, on another owner's land that it is not yours, on the player's to pick a zone");
        HintFacts placeBad = Picked(BuildTool.Place, false, true);
        placeBad.SiteOk = false;
        Check(BuildModeHintRules.Hint(placeBad) == BuildModeHintRules.BuyFirst && BuildModeHintRules.Hint(Picked(BuildTool.Join, false, true)) == BuildModeHintRules.BuyFirst
              && BuildModeHintRules.Hint(Picked(BuildTool.Place, false, false)) == "reallife_bld_hint_place_not_yours",
            "build mode own land: Place and Join on land for sale say to buy it first, before the site rule's words");
        // The words said for what a drag left out and how to put a pick right.
        bool took = true;
        foreach (int t in new[] { CellRules.TookWide, CellRules.TookGap, CellRules.TookTaken, CellRules.TookStreet, CellRules.TookDeeper })
        {
            took &= BuildModeHintRules.TookKey(BuildTool.Land, t) != null && BuildModeHintRules.TookKey(BuildTool.Place, t) != null;
        }
        Check(took && BuildModeHintRules.TookKey(BuildTool.Land, CellRules.TookAll) == null && BuildModeHintRules.TookKey(BuildTool.Land, CellRules.TookTaken) != BuildModeHintRules.TookKey(BuildTool.Zone, CellRules.TookTaken),
            "build mode own land: what a drag left out is said for Buy land and Place, the left out squares in each tool's words");
        bool fixes = true;
        foreach (string why in new[] { CellRules.TwoStrips, CellRules.NotJoined, CellRules.TooWide, CellRules.TooFew, "reallife_bld_mode_refuse_owned", "reallife_bld_answer_lot_taken",
                     "reallife_bld_mode_refuse_mixed", "reallife_bld_mode_refuse_two_sites" })
        {
            fixes &= BuildModeHintRules.FixKey(why) != null;
        }
        Check(fixes && BuildModeHintRules.FixKey(CellRules.NoCells) == null, "build mode own land: every reason squares make no site or cannot be bought has words saying how to put it right");
        // The legend over the views: six entries at most for every view, the zoning view's four uses among them.
        var fills = new int[BuildModeRules.LegendMost];
        var lines = new int[BuildModeRules.LegendMost];
        var keys = new string[BuildModeRules.LegendMost];
        bool legend = true;
        for (int v = 0; v < (int)BuildView.Count; v++)
        {
            int count = BuildModeRules.Legend((BuildView)v, fills, lines, keys);
            int shown = BuildModeRules.LegendShown(count);
            legend &= shown >= 1 && shown <= BuildModeRules.LegendShort && shown <= count;
            if ((BuildView)v == BuildView.Zoning)
            {
                legend &= shown >= 4 && fills[0] == BuildModeRules.Homes && fills[3] == BuildModeRules.Warehouses;
            }
        }
        Check(legend && BuildModeRules.LegendShort <= 6, "build mode legend: every view's legend shows one to six entries, the zoning view's four uses first");
    }

    private static void BuildModeFitChecks()
    {
        BuildModeHintRules.HintFit(800f, 700f, 2000f, out float m, out float o);
        Check(m < 0f && o < 0f, "build mode hint bar: a hint and its quieter line that fit the room stay one line each");
        BuildModeHintRules.HintFit(1100f, 1000f, 2000f, out m, out o);
        Check(m < 0f && Math.Abs(o - 900f) < 0.01f, "build mode hint bar: the quieter line wraps first, the hint kept on one line");
        BuildModeHintRules.HintFit(1800f, 1000f, 2000f, out m, out o);
        Check(Math.Abs(m - 1500f) < 0.01f && Math.Abs(o - 500f) < 0.01f, "build mode hint bar: a hint too long for the room wraps only after the quieter line has a quarter of it");
        BuildModeHintRules.HintFit(2400f, 0f, 2000f, out m, out o);
        Check(Math.Abs(m - 2000f) < 0.01f && o < 0f, "build mode hint bar: a hint alone wraps only past the room");
        // The room on a screen 16 by 9 and 16 by 10, the objectives panel 740 wide at the left (the game's own): a 48 character hint stays one line.
        foreach (float wide in new[] { 3840f, 3456f })
        {
            float half = Math.Min(wide * 0.5f - (740f + 28f), wide * 0.5f - 48f);
            float room = half * 2f - 44f * 2f - 68f - 32f - 32f * 2f - 4f;
            BuildModeHintRules.HintFit(48 * 24f, 55 * 18f, room, out m, out _);
            Check(m < 0f, "build mode hint bar: on a screen " + wide + " units wide a hint of 48 characters keeps to one line (room " + room + ")");
        }
    }

    private static void BuildModeTourCardChecks()
    {
        // The interface as the owner saw it (3840 by 2160, y up from the middle): the top bar and needs to 295 from the top, the
        // objectives at the left from 320, the hint bar under them, the tools, views and demand at the foot, the Zone tool's tall
        // panel over the tools' left end, the legend over the views. Then the same on a screen 16 by 10.
        foreach (float wide in new[] { 3840f, 3456f })
        {
            float half = wide * 0.5f;
            var screen = new UiBox(-half, -1080f, half, 1080f);
            var hint = new UiBox(-1000f, 1080f - 323f - 124f, 1000f, 1080f - 323f);
            var tools = new UiBox(-1172f, -1040f, 196f, -816f);
            var views = new UiBox(224f, -1040f, 844f, -816f);
            var demand = new UiBox(872f, -1040f, 1172f, -816f);
            var panel = new UiBox(-1172f, -784f, -292f, 240f);
            var legend = new UiBox(224f, -784f, 860f, -560f);
            var top = new UiBox(-half + 9f, 1080f - 295f, half - 640f, 1080f);
            var buttons = new UiBox(half - 800f, 1080f - 183f, half, 1080f);
            var tasks = new UiBox(-half, 1080f - 320f - 500f, -half + 740f, 1080f - 320f);
            var parts = new[] { hint, tools, views, demand };
            bool all = true;
            string failed = null;
            for (int p = 0; p < parts.Length; p++)
            {
                var avoid = new List<UiBox> { panel, legend, top, buttons, tasks };
                for (int q = 0; q < parts.Length; q++)
                {
                    if (q != p)
                    {
                        avoid.Add(parts[q]);
                    }
                }
                bool placed = BuildModeHintRules.TourCard(screen, parts[p], avoid.ToArray(), avoid.Count, 1120f, 440f, 48f, p == 0, out UiBox card);
                bool clear = placed && !card.Meets(parts[p]) && card.Inside(screen);
                foreach (UiBox a in avoid)
                {
                    clear &= !card.Meets(a);
                }
                if (!clear)
                {
                    all = false;
                    failed = failed ?? "tip " + (p + 1);
                }
            }
            Check(all, "build mode tour: on a screen " + wide + " units wide each tip's card stands clear of its part, the tool panel, the legend, the other parts and the game's HUD (" + (failed ?? "all") + ")");
        }
        var tiny = new UiBox(0f, 0f, 100f, 100f);
        bool none = BuildModeHintRules.TourCard(tiny, new UiBox(0f, 0f, 100f, 100f), new UiBox[0], 0, 400f, 400f, 10f, true, out _);
        Check(!none, "build mode tour: with no room clear anywhere the card is said not placed (the test then fails)");
    }

    private static void BuildModePolishSourceChecks()
    {
        string kit = ModSource(@"Construction\BuildModeKit.cs");
        string bar = ModSource(@"Construction\BuildModeBar.cs");
        string sheet = ModSource(@"Construction\BuildModeSheet.cs");
        string tour = ModSource(@"Construction\BuildModeTour.cs");
        string mode = ModSource(@"Construction\BuildMode.cs");
        string window = ModSource(@"Construction\BuildMode.Window.cs");
        string game = ModSource(@"Construction\BuildModeGame.cs");
        string tests = ModSource(@"Construction\ConstructionDirector.BuildModeUiTests.cs") + ModSource(@"Construction\ConstructionDirector.BuildModeLayerTests.cs");
        Check(kit.Contains("\"white-rounded-box\"") && kit.Contains("DataUtility.GetPadding") && kit.Contains("!Padded(picker)") && !kit.Contains("kit.White = whiteImage != null ? whiteImage.sprite : null;"),
            "build mode look: a box's picture is a rounded white box with nothing see-through round it, not the colour picker's tight packed white-round (thin lines through the zone rows)");
        Check(!sheet.Contains("public void Legend(") && window.Contains("WriteLegend();") && window.Contains("BuildModeRules.LegendShown(n)") && bar.Contains("Legend = new BuildModeLegend(_kit, _root);"),
            "build mode look: the legend is out of the tool panel, over the views, the view's first entries only");
        Check(sheet.Contains("_kit.Frame(s.Button, BuildModeBar.ButtonGrey, false);") && sheet.Contains("_kit.Frame(a.Side, BuildModeBar.ButtonGrey, false);"),
            "build mode look: the zone rows and Clear are buttons with a face inside an edge");
        Check(MethodText(window, "private void Write(bool top)").Contains("_bar.Hud(BuildModeGame.Hud(_bar.Root));") && game.Contains("container.Find(\"Left Bar\")") && game.Contains("\"Container/Panel\""),
            "build mode look: the hint bar stands under the game's top bar and what hangs under it, clear of the objectives, measured at each writing");
        string open = MethodText(mode, "public bool Open(ConstructionDirector d)");
        string leave = MethodText(mode, "private void Leave(string why)");
        Check(open.Contains("BuildModeGame.HideFootLabels(true);") && leave.Contains("BuildModeGame.HideFootLabels(false);") && game.Contains("\"FPS\"") && game.Contains("FootHeld[i].enabled = FootWas[i];"),
            "build mode look: the game's FPS, address and neighbourhood lines are hidden while build mode is open and given back as they were");
        Check(tour.Contains("BuildModeHintRules.TourCard(") && tour.Contains("_bar.TourAvoid(_step, _avoid, _hud)") && tour.Contains("not inside its padding"),
            "build mode tour: the card placed clear of the tool panel and the other parts, its padding measured by the test");
        string drag = MethodText(mode, "private void Drag()");
        Check(drag.Contains("CellRules.DragPick(") && drag.Contains("CellRules.TrimWide(") && mode.Contains("CellRules.Touches(_d.Cells, _depth, cell)") && mode.Contains("_d.Cells.Project(_from.Column, at.x, at.z)"),
            "build mode drag: the drag is the game-free rules' (along its street, forgiving, a press elsewhere starting afresh)");
        Check(bar.Contains("BuildModeGame.HudParts(hud);") && bar.Contains("Sheet.Swatches()") && bar.Contains("BuildModeKit.Padded(image.sprite)") && bar.Contains("Legend.Check()")
              && tests.Contains("OwnerLooks(owner)") && tests.Contains("FootLabelsHeld") && tests.Contains("DragForTest("),
            "build mode tests: the interface test measures the HUD, the boxes' pictures, the zone rows' squares, the legend, the labels at the foot and the forgiving drag");
    }
    // The fix line for the owner's test, in some row of What's new in every language (a release moves it out of the next row).
    private static void BuildModePolishTextChecks()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("build mode polish texts", words, codes))
        {
            return;
        }
        var fixes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Fixed: in build mode the hint bar no longer covers the game's top bar and needs" }, { "pt", "Corrigido: no modo de construção a barra de dicas não cobre mais" },
            { "zh-cn", "已修复：建造模式下，提示栏不再遮住游戏顶栏和需求" }, { "de", "Behoben: Im Baumodus verdeckt die Hinweisleiste nicht mehr" },
            { "fr", "Corrigé : en mode construction, la barre d'aide ne couvre plus" }, { "es", "Corregido: en el modo construcción la barra de pistas ya no tapa" },
            { "ru", "Исправлено: в режиме строительства строка подсказок больше не закрывает" }, { "nl", "Opgelost: in bouwmodus bedekt de hintbalk" },
            { "tr", "Düzeltildi: inşaat modunda ipucu çubuğu artık" }
        };
        for (int l = 0; l < words.Length; l++)
        {
            Check(fixes.TryGetValue(codes[l], out string fix) && WhatsNewHas(words[l], fix), "build mode polish texts: What's new has the fix of the owner's test in some row in " + codes[l]);
        }
    }
}
