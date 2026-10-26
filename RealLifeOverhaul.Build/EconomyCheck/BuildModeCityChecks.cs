using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Construction;

// Build mode over the whole city (the owner, 26 Oct 2026: "Can you make the cursor a '+' like how it is in CS when it in build
// mode? Also can you make it grids in all district?", then "Grid + buildings"; Scripts/Construction: BuildModeCityRules,
// BuildMode.Plots, BuildModeGame.Plots, ConstructionDirector.BuildModeCityTests): every plot's status and colour in every view,
// the tools by where the map looks, the pointer by what it is over, what can be done with a plot and why not, a building's
// outline from its box and its turn, the layer's squares; then the sources (made once an opening, nothing a frame, put back on
// leaving) and the texts.
internal static partial class Program
{
    private static void BuildModeCityChecks()
    {
        BuildModeCityStatusChecks();
        BuildModeCityToolChecks();
        BuildModeCityCursorChecks();
        BuildModeCityActionChecks();
        BuildModeCityOutlineChecks();
        BuildModeCitySourceChecks();
        BuildModeCityTextChecks();
    }

    private static void BuildModeCityStatusChecks()
    {
        var sale = new PlotFacts { ForSale = true, Use = 1 };
        var owned = new PlotFacts { Owned = true, Rented = true, Use = 2 };
        var rented = new PlotFacts { Rented = true, Use = 1 };
        var rival = new PlotFacts { Rival = true, Use = 2 };
        var toLet = new PlotFacts { ToLet = true, Use = 0 };
        var taken = new PlotFacts { Use = 3 };
        var kept = new PlotFacts { Kept = true, ForSale = true, Use = -1 };
        var foreign = new PlotFacts { Foreign = true, Rented = true, Use = 1 };
        Check(BuildModeCityRules.Status(sale) == PlotStatus.ForSale && BuildModeCityRules.Status(owned) == PlotStatus.Yours && BuildModeCityRules.Status(rented) == PlotStatus.Rented
              && BuildModeCityRules.Status(rival) == PlotStatus.Rival && BuildModeCityRules.Status(toLet) == PlotStatus.ToLet && BuildModeCityRules.Status(taken) == PlotStatus.Taken
              && BuildModeCityRules.Status(kept) == PlotStatus.Kept && BuildModeCityRules.Status(foreign) == PlotStatus.Kept
              && BuildModeCityRules.Status(new PlotFacts { Rival = true, ForSale = true }) == PlotStatus.ForSale && BuildModeCityRules.Status(new PlotFacts { Kept = true, Owned = true }) == PlotStatus.Yours,
            "build mode plots: each building's status: for sale, yours, rented by you, a rival's, empty to let, let, kept; another player's and the kept places nobody's to buy, a rival's on sale for sale");
        int[] wanted = { BuildModeRules.ForSale, BuildModeRules.YourBuilding, BuildModeRules.YourLand, BuildModeRules.Rival, BuildModeRules.Vacant, BuildModeRules.Taken, BuildModeRules.Yard };
        PlotFacts[] each = { sale, owned, rented, rival, toLet, taken, kept };
        bool apart = true;
        var fills = new HashSet<int>();
        for (int i = 0; i < each.Length; i++)
        {
            BuildModeCityRules.Look(each[i], BuildView.Status, out int fill, out int line);
            apart &= fill == wanted[i] && fills.Add(fill);
            apart &= (i == 1 || i == 2) == (line == BuildModeRules.LineYours) && (i == 0) == (line == BuildModeRules.LineForSale);
        }
        Check(apart, "build mode plots: the land view gives each status its own fill, the player's edge on theirs and the for-sale edge on land for sale");
        var works = new PlotFacts { Rival = true, UnderWay = true, Progress = 70, Use = 1 };
        BuildModeCityRules.Look(works, BuildView.Status, out int worksFill, out _);
        BuildModeCityRules.Look(works, BuildView.UnderWay, out int lateFill, out _);
        works.Progress = 20;
        BuildModeCityRules.Look(works, BuildView.UnderWay, out int earlyFill, out _);
        BuildModeCityRules.Look(taken, BuildView.UnderWay, out int noneFill, out _);
        Check(worksFill == BuildModeRules.UnderWay && lateFill == BuildModeRules.UnderWayDone && earlyFill == BuildModeRules.UnderWay && noneFill == BuildModeRules.Nothing,
            "build mode plots: works under way (a rival's as well as the player's) show as under construction first, by their share done");
        bool zoning = true;
        for (int u = -1; u < 4; u++)
        {
            BuildModeCityRules.Look(new PlotFacts { Use = u }, BuildView.Zoning, out int fill, out _);
            zoning &= fill == (u >= 0 ? BuildModeRules.Homes + u : BuildModeRules.NoZone);
        }
        Check(zoning, "build mode plots: the zoning view shows each building's type in the game's colour for it, any other use apart");
        BuildModeCityRules.Look(new PlotFacts { ValueStep = 0 }, BuildView.Value, out int low, out _);
        BuildModeCityRules.Look(new PlotFacts { ValueStep = 99 }, BuildView.Value, out int high, out _);
        Check(low == BuildModeRules.Value && high == BuildModeRules.Value + BuildModeRules.ValueSteps - 1 && BuildModeCityRules.ValueStep(100f, 100f, 900f) == 0
              && BuildModeCityRules.ValueStep(900f, 100f, 900f) == BuildModeRules.ValueSteps - 1 && BuildModeCityRules.ValueStep(5f, 5f, 5f) == BuildModeRules.ValueSteps / 2,
            "build mode plots: land value by the game's price a square metre among the city's, lowest to highest");
        BuildModeCityRules.Look(toLet, BuildView.Occupancy, out int roomFill, out _);
        BuildModeCityRules.Look(taken, BuildView.Occupancy, out int letFill, out _);
        BuildModeCityRules.Look(kept, BuildView.Occupancy, out int keptFill, out _);
        Check(roomFill == BuildModeRules.Vacant && letFill == BuildModeRules.Let && keptFill == BuildModeRules.Nothing, "build mode plots: occupancy as let or empty to let, a kept place not drawn");
        bool inPalette = true;
        foreach (PlotFacts f in each.Concat(new[] { works, foreign, new PlotFacts { Use = 7, ValueStep = -4 } }))
        {
            for (int v = 0; v < (int)BuildView.Count; v++)
            {
                BuildModeCityRules.Look(f, (BuildView)v, out int fill, out int line);
                inPalette &= fill >= 0 && fill < BuildModeRules.PaletteSize && line >= BuildModeRules.LinePlain && line <= BuildModeRules.LinePicked;
            }
        }
        Check(inPalette && BuildModeRules.Palette.Length == BuildModeRules.PaletteSize && BuildModeRules.Rival < BuildModeRules.PaletteSize
              && BuildModeRules.Apart(BuildModeRules.Palette[BuildModeRules.Rival], BuildModeRules.Palette[BuildModeRules.Taken]) >= BuildModeRules.ColourApart
              && BuildModeRules.Apart(BuildModeRules.Palette[BuildModeRules.Rival], BuildModeRules.Palette[BuildModeRules.YourBuilding]) >= BuildModeRules.ColourApart,
            "build mode plots: every look of every plot in the palette, a rival's colour apart from another owner's and the player's");
        var lf = new int[BuildModeRules.LegendMost];
        var ll = new int[BuildModeRules.LegendMost];
        var lk = new string[BuildModeRules.LegendMost];
        bool legends = true;
        for (int v = 0; v < (int)BuildView.Count; v++)
        {
            int n = BuildModeCityRules.Legend((BuildView)v, lf, ll, lk);
            legends &= n > 1 && n <= BuildModeRules.LegendMost && lk.Take(n).All(k => k != null && k.StartsWith("reallife_bld", StringComparison.Ordinal))
                       && lf.Take(n).All(f => f >= -1 && f < BuildModeRules.PaletteSize);
            for (int i = 0; i < each.Length && v == 0; i++)
            {
                BuildModeCityRules.Look(each[i], BuildView.Status, out int fill, out _);
                legends &= lf.Take(n).Contains(fill);
            }
        }
        Check(legends, "build mode plots: each view's legend over the city in the palette, the land view's naming every status's fill");
    }

    private static void BuildModeCityToolChecks()
    {
        BuildHoldings[] cases = { new BuildHoldings(), new BuildHoldings { Land = 3, Placeable = 2 }, new BuildHoldings { Land = 2, Unzoned = 2 }, new BuildHoldings { Buildings = 1 } };
        bool same = true;
        foreach (BuildHoldings h in cases)
        {
            for (int t = 0; t < (int)BuildTool.Count; t++)
            {
                same &= BuildModeCityRules.ToolWhyAt((BuildTool)t, h, false, true) == BuildModeHintRules.ToolWhy((BuildTool)t, h, false)
                        && BuildModeCityRules.ToolWhyAt((BuildTool)t, h, true, true) == BuildModeHintRules.ToolWhy((BuildTool)t, h, true);
            }
        }
        Check(same, "build mode tools by district: over Uptown each tool as before");
        bool city = true;
        foreach (BuildHoldings h in cases)
        {
            city &= BuildModeCityRules.ToolWhyAt(BuildTool.Land, h, false, false) == null && BuildModeCityRules.ToolWhyAt(BuildTool.Refurbish, h, false, false) == null
                    && BuildModeCityRules.ToolWhyAt(BuildTool.Zone, h, false, false) == BuildModeCityRules.WhyUptownOnly
                    && BuildModeCityRules.ToolWhyAt(BuildTool.Place, h, false, false) == BuildModeCityRules.WhyUptownOnly
                    && BuildModeCityRules.ToolWhyAt(BuildTool.Join, h, false, false) == BuildModeCityRules.WhyUptownOnly
                    && BuildModeCityRules.ToolWhyAt(BuildTool.Bulldoze, h, false, false) == "reallife_bld_answer_not_district";
        }
        Check(city, "build mode tools by district: over the rest of the city Zone, Place and Join greyed as Uptown's alone, Bulldoze with the demolition's own refusal, Buy land and Refurbish open, whatever is held");
        bool session = BuildModeCityRules.ToolWhyAt(BuildTool.Land, cases[1], true, false) == null;
        for (int t = 1; t < (int)BuildTool.Count; t++)
        {
            session &= BuildModeCityRules.ToolWhyAt((BuildTool)t, cases[1], true, false) == BuildModeHintRules.WhySession;
        }
        Check(session, "build mode tools by district: in a Going Public session only Buy land opens over the city too, to look");
        var f = new HintFacts { State = new BuildModeState { Step = BuildStep.Idle, Tool = BuildTool.Land }, City = true };
        string idle = BuildModeHintRules.Hint(f);
        int step = BuildModeHintRules.HintStep(f);
        string extra = BuildModeHintRules.HintExtra(f);
        f.PlotPicked = true;
        f.State.Step = BuildStep.Picked;
        string picked = BuildModeHintRules.Hint(f);
        string esc = BuildModeHintRules.Esc(f.State, true);
        f.City = false;
        f.PlotPicked = false;
        f.State.Step = BuildStep.Idle;
        Check(idle == "reallife_bld_hint_city_idle" && step == 0 && extra == "reallife_bld_hint_extra_city" && picked == "reallife_bld_hint_city_picked" && esc == "reallife_bld_hint_esc_clear"
              && BuildModeHintRules.Hint(f) == "reallife_bld_hint_land_idle" && BuildModeHintRules.HintStep(f) == 1,
            "build mode hints over the city: click a building, then choose in the panel, no step number, the tip that squares are Uptown's; over Uptown as before");
        Check(BuildModeHintRules.TourMore.Length == BuildModeHintRules.TourBodies.Length && BuildModeHintRules.TourMore[(int)TourStep.Toolbar] == "reallife_bld_tour_tools_city",
            "build mode tour: the tools' tip says the rest of the city is built up");
    }

    private static void BuildModeCityCursorChecks()
    {
        bool ok = BuildModeCityRules.Cursor(true, false, false, false) == BuildCursor.Cross;
        ok &= BuildModeCityRules.Cursor(true, true, false, false) == BuildCursor.Normal && BuildModeCityRules.Cursor(true, false, true, false) == BuildCursor.Normal
              && BuildModeCityRules.Cursor(true, false, false, true) == BuildCursor.Normal && BuildModeCityRules.Cursor(false, false, false, false) == BuildCursor.Normal;
        Check(ok, "build mode pointer: a crosshair over the map; the game's own over a panel, a question box, the tour or the design window, and once build mode is shut");
    }

    private static void BuildModeCityActionChecks()
    {
        var sale = new PlotFacts { ForSale = true };
        var owned = new PlotFacts { Owned = true };
        var rented = new PlotFacts { Rented = true };
        var toLet = new PlotFacts { ToLet = true };
        var busy = new PlotFacts();
        var kept = new PlotFacts { Kept = true, ToLet = true };
        var foreign = new PlotFacts { Foreign = true, Rented = true };
        Check(BuildModeCityRules.BuyWhy(sale, false) == null && BuildModeCityRules.BuyWhy(busy, false) == null && BuildModeCityRules.BuyWhy(owned, false) == BuildModeCityRules.WhyYours
              && BuildModeCityRules.BuyWhy(kept, false) == BuildModeCityRules.WhyKept && BuildModeCityRules.BuyWhy(foreign, false) == BuildModeCityRules.WhyForeign
              && BuildModeCityRules.BuyWhy(sale, true) == BuildModeHintRules.WhySession,
            "build mode plots: Buy open on any building but the player's, a kept place or another player's (the game's offer decides the price), never in a session");
        Check(BuildModeCityRules.RentWhy(toLet, false) == null && BuildModeCityRules.RentWhy(owned, false) == null && BuildModeCityRules.RentWhy(busy, false) == BuildModeCityRules.WhyNotToLet
              && BuildModeCityRules.RentWhy(rented, false) == BuildModeCityRules.WhyRentedAlready && BuildModeCityRules.RentWhy(kept, false) == BuildModeCityRules.WhyKept
              && BuildModeCityRules.RentWhy(foreign, false) == BuildModeCityRules.WhyRentedAlready && BuildModeCityRules.RentWhy(toLet, true) == BuildModeHintRules.WhySession,
            "build mode plots: Rent open where the unit is to let or the building is the player's, else why not");
        Check(BuildModeCityRules.RefurbishWhy(owned, false) == null && BuildModeCityRules.RefurbishWhy(rented, false) == null && BuildModeCityRules.RefurbishWhy(sale, false) == BuildModeCityRules.WhyNotYours
              && BuildModeCityRules.RedevelopWhy(owned, false) == null && BuildModeCityRules.RedevelopWhy(rented, false) == BuildModeCityRules.WhyNotOwned
              && BuildModeCityRules.RefurbishWhy(owned, true) == BuildModeHintRules.WhySession && BuildModeCityRules.RedevelopWhy(owned, true) == BuildModeHintRules.WhySession,
            "build mode plots: Refurbish on what the player owns or rents, Redevelop (the rebuild) on what they own, then the construction game's own checks");
        Check(BuildModeCityRules.ActionKeys.Length == (int)PlotAction.Count && BuildModeCityRules.StatusKeys.Length == (int)PlotStatus.Count, "build mode plots: a name for each action and each status");
    }

    private static void BuildModeCityOutlineChecks()
    {
        var x = new float[4];
        var z = new float[4];
        bool upright = BuildModeCityRules.Outline(10f, 20f, 50f, 40f, 0f, x, z) && x[0] == 10f && z[0] == 20f && x[2] == 50f && z[2] == 40f && BuildModeCityRules.Inside(x, z, 30f, 30f)
                       && !BuildModeCityRules.Inside(x, z, 60f, 30f);
        Check(upright, "build mode plots: an unturned building's outline is its box, a point in it inside, one beside it outside");
        // A 40 by 20 m building turned 30 degrees: its upright box is solved back to its own outline.
        double turn = 30.0 * Math.PI / 180.0;
        float w = (float)(40.0 * Math.Cos(turn) + 20.0 * Math.Sin(turn)), d = (float)(40.0 * Math.Sin(turn) + 20.0 * Math.Cos(turn));
        bool turned = BuildModeCityRules.Outline(-w / 2f, -d / 2f, w / 2f, d / 2f, 30f, x, z);
        float side = (float)Math.Sqrt((x[1] - x[0]) * (x[1] - x[0]) + (z[1] - z[0]) * (z[1] - z[0])), other = (float)Math.Sqrt((x[3] - x[0]) * (x[3] - x[0]) + (z[3] - z[0]) * (z[3] - z[0]));
        float maxX = Math.Max(Math.Max(x[0], x[1]), Math.Max(x[2], x[3]));
        Check(turned && Math.Abs(side - 40f) < 0.05f && Math.Abs(other - 20f) < 0.05f && Math.Abs(maxX - w / 2f) < 0.05f && BuildModeCityRules.Inside(x, z, 0f, 0f) && !BuildModeCityRules.Inside(x, z, w / 2f - 0.2f, d / 2f - 0.2f),
            "build mode plots: a turned building's outline solved from its upright box and its turn (40 by 20 m at 30 degrees: " + side.ToString("0.00") + " by " + other.ToString("0.00") + ")");
        bool half = BuildModeCityRules.Outline(0f, 0f, 30f, 30f, 45f, x, z) && x[0] == 0f && x[2] == 30f;
        bool bad = !BuildModeCityRules.Outline(0f, 0f, 900f, 20f, 0f, x, z) && !BuildModeCityRules.Outline(0f, 0f, 0.5f, 20f, 0f, x, z);
        Check(half && bad, "build mode plots: at 45 degrees the upright box itself; a batch's box or a sliver is no building");
        Check(BuildModeCityRules.Tile(10f, 10f) == BuildModeCityRules.Tile(250f, 1f) && BuildModeCityRules.Tile(-1f, 10f) != BuildModeCityRules.Tile(1f, 10f)
              && BuildModeCityRules.Tile(10f, -1f) != BuildModeCityRules.Tile(10f, 1f) && BuildModeCityRules.Tile(300f, 10f) != BuildModeCityRules.Tile(10f, 10f),
            "build mode plots: the layer's squares by a point, the same everywhere, apart either side of the axes");
        Check(BuildModeCityRules.InRect(5f, 5f, 0f, 0f, 10f, 10f) && !BuildModeCityRules.InRect(11f, 5f, 0f, 0f, 10f, 10f) && !BuildModeCityRules.InRect(0f, 0f, float.MaxValue, float.MaxValue, float.MinValue, float.MinValue),
            "build mode plots: the map over Uptown by its squares' rectangle, none with no squares");
    }

    private static void BuildModeCitySourceChecks()
    {
        string mode = ModSource(@"Construction\BuildMode.cs");
        string window = ModSource(@"Construction\BuildMode.Window.cs");
        string plots = ModSource(@"Construction\BuildMode.Plots.cs");
        string game = ModSource(@"Construction\BuildModeGame.Plots.cs");
        string rules = ModSource(@"Construction\BuildModeCityRules.cs");
        string tests = ModSource(@"Construction\ConstructionDirector.BuildModeCityTests.cs");
        string director = ModSource(@"Construction\ConstructionDirector.BuildMode.cs");
        Check(!Regex.IsMatch(plots + game + rules, @"using System\.Linq|\.Where\(|\.Select\(|\.OrderBy\(|FindObjectsOfType|FindObjectsByType|GameObject\.Find\b|Physics\.")
              && !Regex.IsMatch(rules, @"UnityEngine|SaveGameManager|GameManager"), "build mode plots sources: no LINQ, no scene search, no physics query of the mod's; the rules game-free");
        Check(MethodText(game, "public static Plots ReadPlots(").Contains("GameDlc.Open(city)") && MethodText(game, "public static Plots ReadPlots(").Contains("ModStreet")
              && MethodText(game, "public static Plots ReadPlots(").Contains("uptown.Contains(key)"),
            "build mode plots sources: the city's buildings read through GameDlc.Open (nothing a DLC keeps), Uptown's left to its squares");
        string open = MethodText(mode, "public bool Open(ConstructionDirector d)");
        string leave = MethodText(mode, "private void Leave(string why)");
        string update = MethodText(mode, "private void OnUpdate()");
        Check(open.Contains("MakePlots();") && leave.Contains("DropPlots();") && leave.Contains("BuildModeGame.Pointer(BuildCursor.Normal);")
              && MethodText(plots, "private void MakePlots()").Contains("Stopwatch.StartNew()") && MethodText(plots, "private void MakePlots()").Contains("Logger?.Info("),
            "build mode plots sources: the plots read and made once an opening, timed and said in Player.log, destroyed at leaving, the game's pointer put back on every way out");
        Check(update.Contains("CursorTick(now);") && !update.Contains("ColourPlots(") && !update.Contains("MakePlots(") && MethodText(plots, "private void CursorTick(float now)").Contains("_nextCursor = now + CursorEvery;")
              && MethodText(game, "public static void Pointer(BuildCursor wanted)").Contains("if (wanted == _cursor)") && MethodText(game, "private static Texture2D Cross()").Contains("if (_cross != null)"),
            "build mode plots sources: nothing made or coloured a frame; the pointer looked at twenty times a second and set only when it changes, its crosshair made once");
        Check(MethodText(game, "public static int PlotPointed(").Contains("MouseController.currentTargetEntity") && MethodText(mode, "private void Hover()").Contains("HoverPlot()")
              && MethodText(game, "public static void PutCardAway(").Contains("buildingResume.Close()"),
            "build mode plots sources: the building under the pointer as the game's own map finds it, its card in the game's tooltip, the game's map card put away when build mode takes the click");
        Check(MethodText(game, "public static bool MakePlots(").Contains("BuildModeCityRules.Tile(") && MethodText(game, "public static bool MakePlots(").Contains("renderer.sharedMaterial = p.Material;")
              && MethodText(game, "public static bool OpenBizMan(").Contains("bizMan.Open(") && MethodText(plots, "private void PlotWhys()").Contains("_d.DemolishBlock(street, number)")
              && MethodText(plots, "private void PlotWhys()").Contains("_d.RebuildBlock(street, number)") && MethodText(plots, "private void PlotWhys()").Contains("_d.RefitBlock(street, number)"),
            "build mode plots sources: one mesh a square of the city on the land layer's material, the map culls the rest; buy and rent the game's own BizMan page; refit, rebuild and demolition the existing calls and checks");
        Check(window.Contains("BuildModeCityRules.ToolWhyAt((BuildTool)t, _holdings, _state.ViewOnly, _inUptown)")
              && MethodText(window, "private void ToolPicked(int tool)").Contains("BuildModeCityRules.ToolWhyAt((BuildTool)tool, _holdings, _state.ViewOnly, _inUptown) != null")
              && MethodText(window, "private void Act(int id)").Contains("if (ActPlot(id))"), "build mode plots sources: the toolbar greyed by where the map looks; a greyed tool not chosen; a plot's buttons its own");
        Check(director.Contains("ConstructionBridges.AllSites") && ModSource(@"Construction\ConstructionBridges.cs").Contains("AllSites = null;")
              && ModSource(@"Wiring\Wiring.ConstructionLinks.cs").Contains("ConstructionBridges.AllSites = AllSites;") && ModSource(@"Wiring\Wiring.ConstructionLinks.cs").Contains("ConstructionBridges.AllSites = null;"),
            "build mode plots sources: the city's works under way read through a bridge wired in and out (a rival's and the city's as well as the player's)");
        string guide = ModSource(@"Construction\ConstructionGuide.cs");
        foreach (string t in new[] { "TestBuildModeCityNow", "TestBuildModeCursorNow", "TestBuildModePlotNow" })
        {
            Check(guide.Contains("ConstructionDirector." + t + "()") && tests.Contains("public static void " + t + "()"), "build mode plots sources: the test " + t + " is in the feature list and a public static method");
        }
        Check(Regex.Matches(tests, "ModeLine\\(").Count >= 5, "build mode plots sources: each test writes its check line");
    }

    private static void BuildModeCityTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string file in new[] { @"Construction\BuildModeCityRules.cs", @"Construction\BuildMode.Plots.cs", @"Construction\ConstructionDirector.BuildModeCityTests.cs", @"Construction\BuildModeHintRules.cs" })
        {
            foreach (Match m in Regex.Matches(ModSource(file), "\"(reallife_bld_(?:plot|hint_city|hint_extra_city|hint_why_uptown|tour_tools_city)[a-z0-9_]*[a-z0-9])\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        foreach (string t in new[] { "city", "cursor", "plot" })
        {
            keys.Add("reallife_bld_mode_test_" + t);
        }
        Check(keys.Count >= 50, "build mode plots texts: their keys are found: " + keys.Count);
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("build mode plots texts", words, codes))
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
            Check(missing == null, "build mode plots texts: every key is in " + codes[l] + ".json, in the style, with the English names (" + (missing ?? "all") + ")");
        }
        foreach (string key in keys)
        {
            string text = words[0].TryGetValue(key, out string t) ? t : string.Empty;
            Check(text.Length <= 240, "build mode plots texts: " + key + " at most 240 characters (" + text.Length + ")");
        }
        var starts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Build mode over the whole city" }, { "pt", "Modo de construção na cidade toda" }, { "zh-cn", "建造模式覆盖整座城市" }, { "de", "Baumodus über die ganze Stadt" },
            { "fr", "Le mode construction couvre toute la ville" }, { "es", "Modo construcción en toda la ciudad" }, { "ru", "Режим строительства на весь город" },
            { "nl", "Bouwmodus over de hele stad" }, { "tr", "Tüm şehirde inşa modu" }
        };
        var parks = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Uptown's two other squares are parks now" }, { "pt", "As outras duas praças do Uptown agora são parques" }, { "zh-cn", "上城另外两个广场现在是公园" },
            { "de", "Die beiden anderen Plätze von Uptown sind jetzt Parks" }, { "fr", "Les deux autres places d'Uptown sont désormais des parcs" },
            { "es", "Las otras dos plazas de Uptown son ahora parques" }, { "ru", "Две другие площади Uptown теперь парки" }, { "nl", "De twee andere pleinen van Uptown zijn nu parken" },
            { "tr", "Uptown'ın diğer iki meydanı artık park" }
        };
        string[] marks = { "Fixed: ", "Corrigido: ", "已修复：", "Behoben: ", "Corrigé : ", "Corregido: ", "Исправлено: ", "Opgelost: ", "Düzeltildi: " };
        for (int l = 0; l < words.Length; l++)
        {
            string start = starts.TryGetValue(codes[l], out string s) ? s : "?";
            Check(WhatsNewHas(words[l], start), "build mode plots texts: What's new has build mode over the whole city in some row in " + codes[l]);
            if (words[l].TryGetValue("reallife_changes_next", out string next) && next.Contains(start))
            {
                int at = next.IndexOf(start, StringComparison.Ordinal), park = next.IndexOf(parks[codes[l]], StringComparison.Ordinal);
                bool fixBetween = park > at && marks.Any(mark => { int f = next.IndexOf(mark, at, StringComparison.Ordinal); return f >= 0 && f < park; });
                Check(next.IndexOf(start, at + 1, StringComparison.Ordinal) < 0 && (park < 0 || park > at) && !fixBetween && !next.Contains("  "),
                    "build mode plots texts: its line once in the next row, among the feature lines before the parks' line where it is there (no fix between), no double space, in " + codes[l]);
            }
        }
        var bullets = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "\n- The rest of the city: " }, { "pt", "\n- O resto da cidade: " }, { "zh-cn", "\n- 城市其他地方：" }
        };
        var heads = new Dictionary<string, string>(StringComparer.Ordinal) { { "en", "\n# Build mode\n" }, { "pt", "\n# Modo de construção\n" }, { "zh-cn", "\n# 建造模式\n" } };
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            string page = words[l].TryGetValue("reallife_guide_construction", out string g) ? g : string.Empty;
            int head = page.IndexOf(heads[codes[l]], StringComparison.Ordinal), bullet = page.IndexOf(bullets[codes[l]], StringComparison.Ordinal);
            int nextHead = head >= 0 ? page.IndexOf("\n# ", head + 2, StringComparison.Ordinal) : -1;
            Check(head >= 0 && bullet > head && (nextHead < 0 || bullet < nextHead), "build mode plots texts: the Guide's build mode section has the rest of the city and the pointer in " + codes[l]);
        }
    }
}
