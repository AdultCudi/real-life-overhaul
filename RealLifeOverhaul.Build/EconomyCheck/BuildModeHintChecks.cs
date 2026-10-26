using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Construction;

// Build mode's guidance (Scripts/Construction/BuildModeHintRules.cs; the owner, 26 Oct 2026: "when a new user sees it they must
// not feel where to click"): which tools can be used yet and why not, the hint bar's line in every state build mode can reach,
// what Esc does there, the tour's order and the demand bars' words; then their texts.
internal static partial class Program
{
    private static void BuildModeHintChecks()
    {
        BuildModeToolChecks();
        BuildModeHintLineChecks();
        BuildModeTourChecks();
        BuildModeCatalogueChecks();
        BuildModeBarSourceChecks();
        BuildModeHintTextChecks();
    }

    private static void BuildModeCatalogueChecks()
    {
        bool[] corner = { true, false, false, true, false };
        bool[] fits = { true, true, false, false, true };
        Check(BuildModeCatalogue.Count(BuildModeCatalogue.Fits, corner, fits, 5) == 3 && BuildModeCatalogue.Count(BuildModeCatalogue.Corners, corner, fits, 5) == 2
              && BuildModeCatalogue.Count(BuildModeCatalogue.City, corner, fits, 5) == 3 && BuildModeCatalogue.FilterKeys.Length == BuildModeCatalogue.FilterCount,
            "build mode designs: Fits here shows those that can be built, Corner and City designs their kind, built or not");
        Check(BuildModeCatalogue.First(corner, fits, 5) == BuildModeCatalogue.Fits && BuildModeCatalogue.First(new[] { true }, new[] { false }, 1) == BuildModeCatalogue.Corners
              && BuildModeCatalogue.First(new bool[0], new bool[0], 0) == BuildModeCatalogue.Fits, "build mode designs: the window opens on Fits here, else on the first filter that shows anything");
        long[] costs = { 900, 300, 100, 50, 300 };
        var order = new int[5];
        BuildModeCatalogue.Order(costs, fits, 5, order);
        Check(order[0] == 1 && order[1] == 4 && order[2] == 0 && order[3] == 3 && order[4] == 2, "build mode designs: those that can be built first, then cheapest first, ties as found (" + string.Join(",", order) + ")");
        Check(BuildModeCatalogue.Why(0) == null && BuildModeCatalogue.Why(BuildModeRules.WarnTall | BuildModeRules.WarnParking) == null
              && BuildModeCatalogue.Why(BuildModeRules.WarnTall | BuildModeRules.WarnCells) == "reallife_bld_mode_warn_cells" && BuildModeCatalogue.Why(BuildModeRules.WarnZone) == "reallife_bld_mode_warn_zone",
            "build mode designs: a card greyed only by a warning that stops Build here, with that warning as its reason");
        Check(BuildModeCatalogue.Valid(0, 3) && !BuildModeCatalogue.Valid(3, 3) && !BuildModeCatalogue.Valid(-1, 3) && !BuildModeCatalogue.Valid(BuildModeCatalogue.Most, 99),
            "build mode designs: a card's index checked against the designs held");
    }

    // The interface the owner chose (26 Oct 2026): the map in full view, the toolbar, views, demand and a small tool panel at the foot, the
    // hint bar at the top, the big window only for designs, a tour once; all the game's own parts, nothing a frame of its own.
    private static void BuildModeBarSourceChecks()
    {
        string bar = ModSource(@"Construction\BuildModeBar.cs");
        string sheet = ModSource(@"Construction\BuildModeSheet.cs");
        string tour = ModSource(@"Construction\BuildModeTour.cs");
        string kit = ModSource(@"Construction\BuildModeKit.cs");
        string tip = ModSource(@"Construction\BuildModeTip.cs");
        string panel = ModSource(@"Construction\BuildModePanel.cs");
        string mode = ModSource(@"Construction\BuildMode.cs");
        string window = ModSource(@"Construction\BuildMode.Window.cs");
        string game = ModSource(@"Construction\BuildModeGame.cs");
        string parts = bar + sheet + tour + kit + tip + panel;
        Check(kit.Contains("BuildModeGame.Designer()") && kit.Contains("\"Topbar/Section\"") && kit.Contains("SelectedBackground") && game.Contains("uis.interiorDesignerUI")
              && tip.Contains(": TooltipTarget") && game.Contains("Tooltip.TooltipSystem.Show()"),
            "build mode interface: its toolbar, buttons and labels copies of the game's interior designer's; its tooltips and the square's card the game's TooltipSystem");
        Check(!Regex.IsMatch(parts, @"new Canvas|AddComponent<Canvas>|(?<![A-Za-z])Input\.|EventSystem|void Update\(|void LateUpdate\("),
            "build mode interface: no canvas of its own, no input read, nothing a frame of its own");
        Check(bar.Contains("ContentSizeFitter") && sheet.Contains("ContentSizeFitter") && !sheet.Contains("ScrollRect") && !Regex.IsMatch(parts, @"TextOverflowModes\.(Ellipsis|Truncate)")
              && kit.Contains("t.overflowMode = TextOverflowModes.Overflow;"), "build mode interface: the tool panel as tall as what it says (no scroll), texts never cut short by an ellipsis");
        Check(window.Contains("BuildModeHintRules.Hint(f)") && window.Contains("BuildModeHintRules.ToolWhy((BuildTool)t, _holdings, _state.ViewOnly)")
              && MethodText(window, "private void ToolPicked(int tool)").Contains("BuildModeHintRules.ToolWhy((BuildTool)tool, _holdings, _state.ViewOnly) != null"),
            "build mode interface: the hint bar and the greyed tools from the game-free rules; a greyed tool cannot be chosen");
        string update = MethodText(mode, "private void OnUpdate()");
        Check(update.Contains("_tour.Skip();") && update.Contains("CatalogueBack();") && update.IndexOf("_tour.Skip();", StringComparison.Ordinal) < update.IndexOf("CatalogueBack();", StringComparison.Ordinal)
              && update.Contains("if (tour || window)"), "build mode interface: Esc ends the tour, then shuts the design window, then clears; the tour and the window hold the map");
        Check(tour.Contains("\"reallife.buildmode.tour\"") && mode.Contains("BuildModeHintRules.TourWanted(BuildModeTour.Seen, _state.ViewOnly)"), "build mode interface: the tour shown once, remembered");
        Check(!window.Contains("reallife_bld_mode_status_picked") && !mode.Contains("reallife_bld_mode_status_picked") && !panel.Contains("Fold(") && !window.Contains("reallife_bld_mode_help_")
              && !panel.Contains("OptionRows.Take("), "build mode interface: the stray help text, the arrow that sent the window aside and the header count without context are gone");
        Check(MethodText(mode, "private void Leave(string why)").Contains("BuildModeGame.HideHover();") && MethodText(mode, "private void Leave(string why)").Contains("_tour?.Hide();")
              && MethodText(mode, "private void Leave(string why)").Contains("_catalogue?.Show(false);"), "build mode interface: leaving takes the square's card, the tour and the design window away");
        Check(MethodText(window, "private void AfterPick()").Contains("OpenCatalogue();") && MethodText(window, "private void CataloguePlace()").Contains("StandGhost();"),
            "build mode interface: squares of the player's picked for a building open the design window; Place it on the map stands the ghost");
    }

    private static BuildModeRules.ColumnFacts HintColumn(bool mine, int zones, int paint, bool waits, bool project, bool myBuilding)
    {
        return new BuildModeRules.ColumnFacts { Mine = mine, Zones = zones, Paint = paint, PaintWaits = waits, HasProject = project, MyBuilding = myBuilding, Use = -1, MainUse = -1 };
    }

    private static void BuildModeToolChecks()
    {
        var none = new BuildHoldings();
        Check(BuildModeHintRules.ToolOpen(BuildTool.Land, none, false) && BuildModeHintRules.ToolWhy(BuildTool.Zone, none, false) == BuildModeHintRules.WhyBuyFirst
              && BuildModeHintRules.ToolWhy(BuildTool.Place, none, false) == BuildModeHintRules.WhyBuyZoneFirst && BuildModeHintRules.ToolWhy(BuildTool.Join, none, false) == BuildModeHintRules.WhyNoSites
              && BuildModeHintRules.ToolWhy(BuildTool.Bulldoze, none, false) == BuildModeHintRules.WhyNoBuilding && BuildModeHintRules.ToolWhy(BuildTool.Refurbish, none, false) == BuildModeHintRules.WhyNoBuilding,
            "build mode tools: with nothing owned only Buy land is open; Zone says buy land first, Place buy and zone land first, Bulldoze and Refurbish no building");
        // The holdings from the columns' facts: bare land with a use in force can take a building; a use asked of City Hall, or
        // zoned for nothing, needs zoning first; land with a project on it is used; a building of the player's counts apart.
        var facts = new[]
        {
            HintColumn(true, 6, 0, false, false, false),   // the council's zoning in force
            HintColumn(true, 6, 3, false, false, false),   // a use painted and allowed
            HintColumn(true, 1, 4, true, false, false),    // a use asked of City Hall
            HintColumn(true, 0, 0, false, false, false),   // zoned for nothing
            HintColumn(true, 6, 0, false, true, false),    // a project on it
            HintColumn(false, 6, 0, false, false, true),   // a building of the player's
            HintColumn(false, 6, 0, false, false, false)   // somebody else's
        };
        BuildHoldings h = BuildModeHintRules.Holdings(facts, facts.Length);
        Check(h.Land == 5 && h.Placeable == 2 && h.Unzoned == 2 && h.Buildings == 1, "build mode tools: the holdings read from the columns: land 5 (" + h.Land + "), free for a building 2 (" + h.Placeable
            + "), to zone 2 (" + h.Unzoned + "), buildings 1 (" + h.Buildings + ")");
        Check(BuildModeHintRules.Holdings(facts, 1).Land == 1 && BuildModeHintRules.Holdings(null, 3).Land == 0 && BuildModeHintRules.Holdings(facts, 99).Land == 5,
            "build mode tools: the holdings read only the columns asked for, none from none, never past the facts");
        Check(BuildModeHintRules.ToolOpen(BuildTool.Place, h, false) && BuildModeHintRules.ToolOpen(BuildTool.Zone, h, false) && BuildModeHintRules.ToolOpen(BuildTool.Bulldoze, h, false)
              && BuildModeHintRules.ToolOpen(BuildTool.Refurbish, h, false) && BuildModeHintRules.ToolOpen(BuildTool.Join, h, false), "build mode tools: with zoned land and a building every tool is open");
        var unzoned = new BuildHoldings { Land = 2, Unzoned = 2 };
        var used = new BuildHoldings { Land = 2 };
        Check(BuildModeHintRules.ToolWhy(BuildTool.Place, unzoned, false) == BuildModeHintRules.WhyZoneFirst && BuildModeHintRules.ToolWhy(BuildTool.Place, used, false) == BuildModeHintRules.WhyAllUsed
              && BuildModeHintRules.ToolOpen(BuildTool.Zone, unzoned, false), "build mode tools: Place with land not zoned says zone it first; with every site already taken by a project, that it is all used");
        bool session = true;
        for (int t = 0; t < (int)BuildTool.Count; t++)
        {
            string why = BuildModeHintRules.ToolWhy((BuildTool)t, h, true);
            session &= t == (int)BuildTool.Land ? why == null : why == BuildModeHintRules.WhySession;
        }
        Check(session, "build mode tools: in a Going Public session only Buy land opens, to look at prices; every other tool says why");
        Check(BuildModeHintRules.FirstTool(none, false) == BuildTool.Land && BuildModeHintRules.FirstTool(unzoned, false) == BuildTool.Zone && BuildModeHintRules.FirstTool(h, false) == BuildTool.Place
              && BuildModeHintRules.FirstTool(h, true) == BuildTool.Land && BuildModeHintRules.ToolOpen(BuildModeHintRules.FirstTool(used, false), used, false),
            "build mode tools: it opens on the next step not done (Buy land, Zone, Place), always on a tool that is open");
        Check(BuildModeHintRules.ToolKeys.Length == (int)BuildTool.Count && BuildModeHintRules.ToolTips.Length == (int)BuildTool.Count && BuildModeHintRules.ViewTips.Length == (int)BuildView.Count
              && BuildModeHintRules.ToolKeys[0] == "reallife_bld_mode_tool_land" && BuildModeHintRules.ToolKeys[5] == "reallife_bld_mode_tool_refurbish",
            "build mode tools: a name and a tooltip line for each tool and view, the toolbar in the steps' order");
    }

    private static HintFacts HintAt(BuildTool tool, BuildStep step, bool viewOnly = false)
    {
        return new HintFacts { State = new BuildModeState { Tool = tool, Step = step, ViewOnly = viewOnly } };
    }

    private static string HintOf(BuildTool tool, BuildStep step, Func<HintFacts, HintFacts> set = null)
    {
        HintFacts f = HintAt(tool, step);
        return BuildModeHintRules.Hint(set != null ? set(f) : f);
    }

    private static void BuildModeHintLineChecks()
    {
        // The owner's lines (26 Oct 2026), each in its state.
        Check(HintOf(BuildTool.Land, BuildStep.Idle) == "reallife_bld_hint_land_idle" && HintOf(BuildTool.Land, BuildStep.Dragging) == "reallife_bld_hint_land_drag"
              && HintOf(BuildTool.Land, BuildStep.Picked, f => { f.Picked = true; return f; }) == "reallife_bld_hint_land_bad"
              && HintOf(BuildTool.Land, BuildStep.Picked, f => { f.Picked = f.SiteOk = true; return f; }) == "reallife_bld_hint_land_blocked"
              && HintOf(BuildTool.Land, BuildStep.Picked, f => { f.Picked = f.SiteOk = f.CanBuy = true; return f; }) == "reallife_bld_hint_land_buy",
            "build mode hints: Buy land: click squares by a street, drag, then Buy the squares picked, or why they cannot be bought");
        Check(HintOf(BuildTool.Zone, BuildStep.Idle) == "reallife_bld_hint_zone_pick" && HintOf(BuildTool.Zone, BuildStep.Idle, f => { f.ZoneChosen = true; return f; }) == "reallife_bld_hint_zone_click"
              && HintOf(BuildTool.Zone, BuildStep.Picked, f => { f.Picked = true; return f; }) == "reallife_bld_hint_zone_not_yours"
              && HintOf(BuildTool.Zone, BuildStep.Picked, f => { f.Picked = f.PickedMine = true; return f; }) == "reallife_bld_hint_zone_pick_for",
            "build mode hints: Zone: pick a zone, then click your land; land not the player's said");
        Check(HintOf(BuildTool.Place, BuildStep.Idle) == "reallife_bld_hint_place_idle" && HintOf(BuildTool.Place, BuildStep.Picked, f => { f.Picked = true; return f; }) == "reallife_bld_hint_place_bad"
              && HintOf(BuildTool.Place, BuildStep.Picked, f => { f.Picked = f.SiteOk = true; return f; }) == "reallife_bld_hint_place_not_yours"
              && HintOf(BuildTool.Place, BuildStep.Picked, f => { f.Picked = f.SiteOk = f.PickedMine = true; return f; }) == "reallife_bld_hint_place_none"
              && HintOf(BuildTool.Place, BuildStep.Picked, f => { f.Picked = f.SiteOk = f.PickedMine = true; f.Designs = 3; return f; }) == "reallife_bld_hint_place_pick"
              && HintOf(BuildTool.Place, BuildStep.Ghost, f => { f.Picked = f.SiteOk = f.PickedMine = f.MayBuild = true; f.Designs = 3; return f; }) == "reallife_bld_hint_place_build"
              && HintOf(BuildTool.Place, BuildStep.Ghost, f => { f.Picked = f.SiteOk = f.PickedMine = true; f.Designs = 3; return f; }) == "reallife_bld_hint_place_fix",
            "build mode hints: Place: drag over your land, pick a design, then Build here, or what is in red first");
        Check(HintOf(BuildTool.Bulldoze, BuildStep.Idle) == "reallife_bld_hint_bulldoze_idle" && HintOf(BuildTool.Bulldoze, BuildStep.Picked, f => { f.Picked = true; return f; }) == "reallife_bld_hint_building_not_yours"
              && HintOf(BuildTool.Bulldoze, BuildStep.Picked, f => { f.Picked = f.PickedBuilding = true; return f; }) == "reallife_bld_hint_bulldoze_go"
              && HintOf(BuildTool.Refurbish, BuildStep.Idle) == "reallife_bld_hint_refurbish_idle" && HintOf(BuildTool.Refurbish, BuildStep.Picked, f => { f.Picked = f.PickedBuilding = true; return f; }) == "reallife_bld_hint_refurbish_go"
              && HintOf(BuildTool.Join, BuildStep.Idle) == "reallife_bld_hint_join_idle" && HintOf(BuildTool.Join, BuildStep.Picked, f => { f.Picked = f.PickedMine = true; return f; }) == "reallife_bld_hint_join_go",
            "build mode hints: Bulldoze, Refurbish and Join: click a building or land of yours, then press the button");
        Check(HintOf(BuildTool.Place, BuildStep.Confirming) == "reallife_bld_hint_confirming" && BuildModeHintRules.Hint(HintAt(BuildTool.Place, BuildStep.Picked, true)) == "reallife_bld_hint_view_only"
              && BuildModeHintRules.Hint(HintAt(BuildTool.Land, BuildStep.Closed)) == null, "build mode hints: the question box, a Going Public session and closed each said (closed: nothing)");
        Check(BuildModeHintRules.Esc(new BuildModeState { Step = BuildStep.Idle }, false) == "reallife_bld_hint_esc_leave" && BuildModeHintRules.Esc(new BuildModeState { Step = BuildStep.Picked }, true) == "reallife_bld_hint_esc_clear"
              && BuildModeHintRules.Esc(new BuildModeState { Step = BuildStep.Ghost }, true) == "reallife_bld_hint_esc_design" && BuildModeHintRules.Esc(new BuildModeState { Step = BuildStep.Confirming }, true) == "reallife_bld_hint_esc_answer"
              && BuildModeHintRules.Esc(new BuildModeState { Step = BuildStep.Dragging }, false) == "reallife_bld_hint_esc_clear" && BuildModeHintRules.Esc(default(BuildModeState), false) == null,
            "build mode hints: Esc said as it acts: the question box's, a design let go, the squares cleared, else build mode left");
        // The step number follows the path from bare ground to a building; the tools off that path have none.
        Check(BuildModeHintRules.HintStep(HintAt(BuildTool.Land, BuildStep.Idle)) == 1 && BuildModeHintRules.HintStep(new HintFacts { State = new BuildModeState { Tool = BuildTool.Land, Step = BuildStep.Picked }, Picked = true }) == 2
              && BuildModeHintRules.HintStep(HintAt(BuildTool.Zone, BuildStep.Idle)) == 3 && BuildModeHintRules.HintStep(HintAt(BuildTool.Place, BuildStep.Picked)) == 4
              && BuildModeHintRules.HintStep(HintAt(BuildTool.Place, BuildStep.Ghost)) == 5 && BuildModeHintRules.HintStep(HintAt(BuildTool.Bulldoze, BuildStep.Idle)) == 0
              && BuildModeHintRules.HintStep(HintAt(BuildTool.Land, BuildStep.Idle, true)) == 0 && BuildModeHintRules.HintStep(HintAt(BuildTool.Land, BuildStep.Closed)) == 0,
            "build mode hints: the step number 1 to 5 from picking land to Build here; none off that path, in view only or closed");
        Check(BuildModeHintRules.HintExtra(new HintFacts { State = new BuildModeState { Tool = BuildTool.Land, Step = BuildStep.Picked }, Picked = true }) == "reallife_bld_hint_extra_drop"
              && BuildModeHintRules.HintExtra(HintAt(BuildTool.Land, BuildStep.Idle)) == "reallife_bld_hint_extra_drag" && BuildModeHintRules.HintExtra(HintAt(BuildTool.Zone, BuildStep.Idle)) == "reallife_bld_hint_extra_own"
              && BuildModeHintRules.HintExtra(HintAt(BuildTool.Place, BuildStep.Ghost)) == "reallife_bld_hint_extra_ghost" && BuildModeHintRules.HintExtra(HintAt(BuildTool.Join, BuildStep.Idle)) == null
              && BuildModeHintRules.HintExtra(HintAt(BuildTool.Place, BuildStep.Confirming)) == null,
            "build mode hints: the second line's tip: right click drops a square, only your own land is zoned, the ghost stands where it is built; none with the question box up");
        // Every state the machine reaches with every mix of the facts has a line and an Esc line, and the line is one of the hint keys.
        var reached = new HashSet<string>();
        var queue = new Queue<BuildModeState>();
        queue.Enqueue(new BuildModeState());
        int states = 0, mixes = 0;
        string wrong = null;
        while (queue.Count > 0 && states < 2000)
        {
            BuildModeState at = queue.Dequeue();
            if (!reached.Add(at.Step + "/" + at.Tool + "/" + at.ViewOnly))
            {
                continue;
            }
            states++;
            for (int bits = 0; bits < 256; bits++)
            {
                var f = new HintFacts
                {
                    State = at, Picked = (bits & 1) != 0, SiteOk = (bits & 2) != 0, CanBuy = (bits & 4) != 0, PickedMine = (bits & 8) != 0, PickedBuilding = (bits & 16) != 0, ZoneChosen = (bits & 32) != 0,
                    Designs = (bits & 64) != 0 ? 4 : 0, MayBuild = (bits & 128) != 0
                };
                string hint = BuildModeHintRules.Hint(f), esc = BuildModeHintRules.Esc(at, f.Picked);
                mixes++;
                if (at.Open && (hint == null || !hint.StartsWith("reallife_bld_hint_", StringComparison.Ordinal) || esc == null))
                {
                    wrong = wrong ?? at.Step + "/" + at.Tool + "/" + bits;
                }
                if (!at.Open && (hint != null || esc != null))
                {
                    wrong = wrong ?? "closed " + bits;
                }
            }
            foreach (BuildInput input in Enum.GetValues(typeof(BuildInput)))
            {
                for (int t = 0; t < (int)BuildTool.Count; t++)
                {
                    queue.Enqueue(BuildModeRules.Next(at, input, (BuildTool)t, false));
                    queue.Enqueue(BuildModeRules.Next(at, input, (BuildTool)t, true));
                }
            }
        }
        Check(wrong == null && states > 20, "build mode hints: each of " + states + " states with each of 256 mixes of facts (" + mixes + ") has a hint and an Esc line while open, none closed (" + (wrong ?? "all") + ")");
        // Determinism: the same facts, the same line.
        HintFacts same = HintAt(BuildTool.Place, BuildStep.Ghost);
        same.Picked = same.SiteOk = same.PickedMine = same.MayBuild = true;
        Check(BuildModeHintRules.Hint(same) == BuildModeHintRules.Hint(same), "build mode hints: the same state gives the same line");
    }

    private static void BuildModeTourChecks()
    {
        TourStep s = TourStep.HintBar;
        int steps = 0;
        var order = new List<TourStep>();
        while (s != TourStep.Done && steps < 10)
        {
            order.Add(s);
            s = BuildModeHintRules.TourNext(s, false);
            steps++;
        }
        Check(steps == 4 && order[0] == TourStep.HintBar && order[1] == TourStep.Toolbar && order[2] == TourStep.Views && order[3] == TourStep.Demand && BuildModeHintRules.TourTitles.Length == 4 && BuildModeHintRules.TourBodies.Length == 4,
            "build mode tour: four steps, the hint bar, the toolbar, the views and the demand, then done");
        Check(BuildModeHintRules.TourNext(TourStep.Toolbar, true) == TourStep.Done && BuildModeHintRules.TourNext(TourStep.Done, false) == TourStep.Done,
            "build mode tour: skipping ends it at any step, and done stays done");
        Check(BuildModeHintRules.TourWanted(false, false) && !BuildModeHintRules.TourWanted(true, false) && !BuildModeHintRules.TourWanted(false, true),
            "build mode tour: shown once, never again once seen, never in a Going Public session");
        Check(BuildModeHintRules.DemandWords(-1) == "reallife_bld_tip_demand_unknown" && BuildModeHintRules.DemandWords(0) == "reallife_bld_tip_demand_low"
              && BuildModeHintRules.DemandWords(BuildModeRules.DeveloperDemandLeast - 1) == "reallife_bld_tip_demand_low" && BuildModeHintRules.DemandWords(BuildModeRules.DeveloperDemandLeast) == "reallife_bld_tip_demand_some"
              && BuildModeHintRules.DemandWords(64) == "reallife_bld_tip_demand_some" && BuildModeHintRules.DemandWords(65) == "reallife_bld_tip_demand_high" && BuildModeHintRules.DemandWords(100) == "reallife_bld_tip_demand_high",
            "build mode demand bars: their words by the developers' own threshold (" + BuildModeRules.DeveloperDemandLeast + ") and a high mark of 65");
        Check(BuildModeHintRules.DemandFill(-5) == 0f && BuildModeHintRules.DemandFill(0) == 0f && Math.Abs(BuildModeHintRules.DemandFill(40) - 0.4f) < 0.0001f && BuildModeHintRules.DemandFill(140) == 1f,
            "build mode demand bars: filled by the demand, empty when none is known, never past full");
    }

    private static void BuildModeHintTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string file in new[] { @"Construction\BuildModeHintRules.cs", @"Construction\BuildModeBar.cs", @"Construction\BuildModeTour.cs", @"Construction\BuildModePanel.cs",
                     @"Construction\BuildMode.Window.cs", @"Construction\BuildModeCatalogue.cs", @"Construction\ConstructionDirector.BuildMode.cs" })
        {
            foreach (Match m in Regex.Matches(ModSource(file), "\"(reallife_bld_(?:hint|tip|tour|bar|sheet|cat|hover|zone)_[a-z0-9_]+[a-z0-9])\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        Check(keys.Count >= 40, "build mode hint texts: their keys are found: " + keys.Count);
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("build mode hint texts", words, codes))
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
            Check(missing == null, "build mode hint texts: every key is in " + codes[l] + ".json, in the style, with the English names (" + (missing ?? "all") + ")");
        }
        string longest = null;
        foreach (string key in keys)
        {
            string text = words[0].TryGetValue(key, out string t) ? t : string.Empty;
            int most = key.StartsWith("reallife_bld_tour_", StringComparison.Ordinal) && key.EndsWith("_body", StringComparison.Ordinal) ? 200 : 90;   // a hint, a tooltip or a heading one short line; a tour step's words two sentences
            if (text.Length > most)
            {
                longest = longest ?? key + " (" + text.Length + " of " + most + ")";
            }
        }
        Check(longest == null, "build mode hint texts: each hint and tooltip line short, each tour step at most two sentences (" + (longest ?? "all") + ")");
        // The fix line for the new interface, in some row of What's new in every language (a release moves it out of the next row).
        var fixes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Fixed: build mode was hard to find your way in." }, { "pt", "Corrigido: era difícil se orientar no modo de construção." },
            { "zh-cn", "已修复：建造模式不容易上手。" }, { "de", "Behoben: Im Baumodus fand man sich schwer zurecht." },
            { "fr", "Corrigé : il était difficile de s'y retrouver dans le mode construction." }, { "es", "Corregido: costaba orientarse en el modo construcción." },
            { "ru", "Исправлено: в режиме строительства было трудно разобраться." }, { "nl", "Opgelost: in de bouwmodus was de weg moeilijk te vinden." },
            { "tr", "Düzeltildi: inşa modunda yolunuzu bulmak zordu." }
        };
        for (int l = 0; l < words.Length; l++)
        {
            Check(fixes.TryGetValue(codes[l], out string fix) && WhatsNewHas(words[l], fix), "build mode hint texts: What's new has the new interface's fix in some row in " + codes[l]);
        }
        // The Guide's build mode section says the new layout.
        var guide = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "The tools sit at the foot in the order you use them" }, { "pt", "As ferramentas ficam embaixo na ordem de uso" }, { "zh-cn", "工具按使用顺序排在底部" }
        };
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            string page = words[l].TryGetValue("reallife_guide_construction", out string g) ? g : null;
            Check(page == null || page.Contains(guide[codes[l]]) && !page.Contains("window's arrow") && !page.Contains("seta da janela") && !page.Contains("窗口的箭头"),
                "build mode hint texts: the Guide's build mode section says the toolbar at the foot, not the old window's arrow, in " + codes[l]);
        }
    }
}
