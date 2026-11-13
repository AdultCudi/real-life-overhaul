using System;
using System.Collections.Generic;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Ui;

// Build mode on the kit (Scripts/Ui/BuildMode): every state laid out at every frame in every language (the screen registry's checks), then the rules it stands on:
// the Rents and Traffic layers' colours and legends, the wanted badge, the favourites and the search, the panels' places (none over another, all on the screen), and the
// wiring that keeps the map and the camera the game's. Run alone with --buildmode-ui.
internal static partial class Program
{
    private static readonly int[] BuildModeCommands = UiBuildPanels.Commands;

    private static void UiCasesBuildMode(UiScreenDef def, List<UiScreenCase> cases)
    {
        foreach (string state in UiSamples.BuildModeStates)
        {
            string st = state;
            cases.Add(new UiScreenCase
            {
                Def = def, Name = "build_mode (" + st + ")", Title = "Build mode", Commands = BuildModeCommands, Overlay = true,
                Lay = (s, f, d, k, t) => UiSamples.BuildModeScene(s, f, d, k, t, st)
            });
        }
    }

    private static void BuildModeKitAll()
    {
        if (Environment.GetEnvironmentVariable("BM_DEBUG") != null)
        {
            BuildModeDebug();
        }
        BuildModeLayerChecks();
        BuildModeCardChecks();
        BuildModePlaceChecks();
        BuildModeWiringChecks();
        BuildModeFilterChecks();
    }

    // What the layout checks found, said for one state at one frame: BM_DEBUG=<state> shows each rule's findings.
    private static void BuildModeDebug()
    {
        foreach (string state in UiSamples.BuildModeStates)
        {
            if (Environment.GetEnvironmentVariable("BM_DEBUG") != "all" && Environment.GetEnvironmentVariable("BM_DEBUG") != state)
            {
                continue;
            }
            var s = new UiScene();
            UiFrame frame = UiFrame.FullMenu(16f / 9f, "Build mode");
            UiSamples.BuildModeScene(s, frame, true, UiDefaultKeys.Keyboard, BmWords, state);
            Action<string> say = line => Console.WriteLine("  [" + state + "] " + line);
            Console.WriteLine(state + ": overlaps " + UiCheckRules.Overlaps(s, say) + ", spills " + UiCheckRules.Spills(s, say) + ", offgrid " + UiCheckRules.OffGrid(s, say) + ", unfit " + UiCheckRules.Unfit(s, say)
                + ", small " + UiCheckRules.SmallTargets(s, say) + ", crowded " + UiCheckRules.Crowded(s, say) + ", dead " + UiCheckRules.DeadSpace(s, say) + ", contrast " + UiCheckRules.LowContrast(s, say));
        }
    }

    // Rents and Traffic: real figures only, a step of five, no tint where nothing is read, the legend for each, the city's plots by the same steps.
    private static void BuildModeLayerChecks()
    {
        Check((int)BuildView.Count == 7 && BuildModeRules.ViewKeys.Length == (int)BuildView.Count && BuildModeHintRules.ViewTips.Length == (int)BuildView.Count,
            "build mode kit layers: seven views, each with its name and its tip");
        Check(BuildModeRules.RampStepOf(100, 100, 600) == 0 && BuildModeRules.RampStepOf(600, 100, 600) == BuildModeRules.RampSteps - 1 && BuildModeRules.RampStepOf(350, 100, 600) == 2
              && BuildModeRules.RampStepOf(9, 5, 5) == BuildModeRules.RampSteps / 2 && BuildModeRules.RampStepOf(-1, 5, 50) == -1,
            "build mode kit layers: a figure's step among the least and most of its kind, the middle where all are alike, none where it is not read");
        Check(BuildModeRules.TrafficStepOf(0) == 0 && BuildModeRules.TrafficStepOf(100) == BuildModeRules.RampSteps - 1 && BuildModeRules.TrafficStepOf(50) == 2 && BuildModeRules.TrafficStepOf(-1) == -1,
            "build mode kit layers: the game's traffic index of 0 to 100 in five steps, none where it is not known");
        var unknown = new BuildModeRules.ColumnFacts { RentStep = -1, TrafficStep = -1, Use = -1, MainUse = -1 };
        var known = new BuildModeRules.ColumnFacts { RentStep = 3, TrafficStep = 4, Use = -1, MainUse = -1, Free = true };
        BuildModeRules.Look(unknown, 0, 4, BuildView.Rents, out int noRent, out _);
        BuildModeRules.Look(unknown, 0, 4, BuildView.Traffic, out int noTraffic, out _);
        BuildModeRules.Look(known, 0, 4, BuildView.Rents, out int rent, out int rentLine);
        BuildModeRules.Look(known, 0, 4, BuildView.Traffic, out int traffic, out int trafficLine);
        Check(noRent == BuildModeRules.Nothing && noTraffic == BuildModeRules.Nothing && rent == BuildModeRules.RentRamp + 3 && traffic == BuildModeRules.TrafficRamp + 4
              && rentLine == BuildModeRules.LineForSale && trafficLine == BuildModeRules.LineForSale,
            "build mode kit layers: a cell with nothing read is not tinted, one with a step takes its ramp's colour, land for sale keeps its bright edge");
        bool ramps = BuildModeRules.Palette.Length == BuildModeRules.PaletteSize && BuildModeRules.RentRamp + BuildModeRules.RampSteps == BuildModeRules.TrafficRamp
                     && BuildModeRules.TrafficRamp + BuildModeRules.RampSteps == BuildModeRules.PaletteSize;
        for (int i = 1; i < BuildModeRules.RampSteps && ramps; i++)
        {
            ramps &= BuildModeRules.Palette[BuildModeRules.RentRamp + i][1] >= BuildModeRules.Palette[BuildModeRules.RentRamp + i - 1][1];   // dearer is greener
            ramps &= BuildModeRules.Palette[BuildModeRules.TrafficRamp + i][0] >= BuildModeRules.Palette[BuildModeRules.TrafficRamp + i - 1][0];   // busier is redder
        }
        Check(ramps, "build mode kit layers: the two ramps sit after the held ground's colours, five steps each, dearer greener and busier redder");
        var fills = new int[BuildModeRules.LegendMost];
        var lines = new int[BuildModeRules.LegendMost];
        var keys = new string[BuildModeRules.LegendMost];
        int n = BuildModeRules.Legend(BuildView.Rents, fills, lines, keys);
        int c = BuildModeCityRules.Legend(BuildView.Traffic, fills, lines, keys);
        Check(n >= 2 && c >= 2 && keys[0] == "reallife_bld_mode_key_traffic_low" && keys[1] == "reallife_bld_mode_key_traffic_high", "build mode kit layers: each layer has its legend, over Uptown and over the city");
        var plot = new PlotFacts { RentStep = 2, TrafficStep = 1 };
        BuildModeCityRules.Look(plot, BuildView.Rents, out int plotRent, out _);
        BuildModeCityRules.Look(plot, BuildView.Traffic, out int plotTraffic, out _);
        plot.RentStep = -1;
        BuildModeCityRules.Look(plot, BuildView.Rents, out int plotNone, out _);
        Check(plotRent == BuildModeRules.RentRamp + 2 && plotTraffic == BuildModeRules.TrafficRamp + 1 && plotNone == BuildModeRules.Nothing,
            "build mode kit layers: the city's plots by the same steps, none tinted where their use has no rent read");
        string director = ModSource(@"Construction\ConstructionDirector.BuildMode.cs");
        Check(MethodText(director, "public void RentAndTraffic(").Contains("RentRange(") && director.Contains("EstateDirector.Instance.Rents") && director.Contains("lot.Traffic")
              && !MethodText(director, "private void RentRange(").Contains("Random"),
            "build mode kit layers: rents from the estate's own rent map and traffic from each lot's index, nothing made up");
    }

    // The design window's rules: the wanted badge, the weeks, the search, the favourites.
    private static void BuildModeCardChecks()
    {
        Check(BuildModeCatalogue.Wanted(new[] { 72, 41, 63, 18 }, 4) == 0 && BuildModeCatalogue.Wanted(new[] { 30, 41, 54, 18 }, 4) == -1 && BuildModeCatalogue.Wanted(new[] { -1, -1, -1, -1 }, 4) == -1
              && BuildModeCatalogue.Wanted(new[] { 60, 60, 20, 10 }, 4) == 0 && BuildModeCatalogue.Wanted(new[] { 10, 10, 90, 70 }, 4) == 2,
            "build mode kit designs: the badge goes to the use the district wants most, only where that is a lot, none where demand is not known");
        Check(BuildModeCatalogue.Weeks(0) == 1 && BuildModeCatalogue.Weeks(7) == 1 && BuildModeCatalogue.Weeks(8) == 2 && BuildModeCatalogue.Weeks(49) == 7 && BuildModeCatalogue.Weeks(50) == 8,
            "build mode kit designs: the build time in whole weeks, at least one");
        Check(BuildModeCatalogue.Matches("Walk-up flats", "flats") && BuildModeCatalogue.Matches("Walk-up flats", "FLAT walk") && !BuildModeCatalogue.Matches("Walk-up flats", "tower")
              && BuildModeCatalogue.Matches("Walk-up flats", "  ") && !BuildModeCatalogue.Matches(null, "a"), "build mode kit designs: the search wants every word, in any case, and an empty search holds all");
        string list = string.Empty;
        list = BuildFavouriteRules.Toggled(list, BuildModeCatalogue.FavouriteId(0, null, "Walk-up flats"));
        list = BuildFavouriteRules.Toggled(list, BuildModeCatalogue.FavouriteId(1, null, "Shop row"));
        bool both = BuildFavouriteRules.Has(list, "0:Walk-up flats") && BuildFavouriteRules.Has(list, "1:Shop row") && BuildFavouriteRules.Count(list) == 2;
        list = BuildFavouriteRules.Toggled(list, "0:Walk-up flats");
        Check(both && !BuildFavouriteRules.Has(list, "0:Walk-up flats") && BuildFavouriteRules.Has(list, "1:Shop row") && BuildFavouriteRules.Toggled("a", "x|y") == "a" && BuildFavouriteRules.Toggled(null, string.Empty) == string.Empty,
            "build mode kit designs: a star puts a design on the list and takes it off, a design's use is part of its id, an id that would break the list is refused");
        string many = string.Empty;
        for (int i = 0; i < BuildFavouriteRules.Most + 5; i++)
        {
            many = BuildFavouriteRules.Toggled(many, "0:d" + i);
        }
        Check(BuildFavouriteRules.Count(many) == BuildFavouriteRules.Most && !BuildFavouriteRules.Has(many, "0:d0") && BuildFavouriteRules.Has(many, "0:d" + (BuildFavouriteRules.Most + 4)),
            "build mode kit designs: the list keeps the newest stars, the oldest goes");
        string favs = ModSource(@"Construction\BuildFavourites.cs");
        Check(favs.Contains("PlayerPrefs") && !favs.Contains("SaveGameManager") && !favs.Contains("Network"), "build mode kit designs: the stars are kept on this computer with the game's settings, never in the save, never sent");
        // The window on the kit holds the cards it is given, the search narrows them, the favourites switch narrows them to the starred.
        var c = UiSamples.BuildMode(BmWords, "catalogue").Catalogue;
        int all = UiBuildPanels.Shown(c);
        c.Search = "flats";
        int flats = UiBuildPanels.Shown(c);
        c.Search = string.Empty;
        c.FavouritesOnly = true;
        int starred = UiBuildPanels.Shown(c);
        Check(all == c.CardCount && flats > 0 && flats < all && starred > 0 && starred < all, "build mode kit designs: the cards, narrowed by the search, narrowed by the favourites (" + all + ", " + flats + ", " + starred + ")");
    }

    private static readonly UiWords BmWords = (key, english) => english;

    // The design window's filters (Fits here, Corner buildings, City designs), a tab's count that matches its cards, a design hidden by the search never placed, and the tool panel scrolling
    // when it is taller than the room instead of running under the bar.
    private static void BuildModeFilterChecks()
    {
        UiBuildCatalogueModel c = UiSamples.BuildMode(BmWords, "catalogue").Catalogue;
        UiFrame f = UiFrame.FullMenu(16f / 9f, "Build mode");
        UiBuildPanels.CatalogueBox(c, f, 600f, out _, out _, out _, out float withFilters);
        var bare = new UiBuildCatalogueModel();
        UiBuildPanels.CatalogueBox(bare, f, 600f, out _, out _, out _, out float without);
        Check(c.Filters.Length == BuildModeCatalogue.FilterCount && withFilters > without, "build mode kit filters: the window has a row for the three filters and grows by it");
        var scene = new UiScene();
        UiBuildPanels.Lay(scene, UiSamples.BuildMode(BmWords, "catalogue"), f, true, UiDefaultKeys.Keyboard);
        int pressed = 0;
        for (int i = 0; i < scene.NodeCount; i++)
        {
            pressed += scene.Nodes[i].Command == UiBuildPanels.FilterCommand ? 1 : 0;
        }
        Check(pressed == BuildModeCatalogue.FilterCount && Array.IndexOf(UiBuildPanels.Commands, UiBuildPanels.FilterCommand) >= 0, "build mode kit filters: each filter is a target the window answers");
        // The chosen design the search has hidden is not offered: nothing to place, and the pick line replaces the summary of a design out of sight.
        c.Search = "zzz-no-such-design";
        Check(UiBuildPanels.ChosenShown(c) < 0, "build mode kit filters: a chosen design the search hides is not among those shown");
        c.Search = string.Empty;
        Check(UiBuildPanels.ChosenShown(c) >= 0, "build mode kit filters: with no search the chosen design is shown");
        string face = ModSource(@"Ui\BuildMode\BuildFace.cs"), parts = ModSource(@"Ui\BuildMode\BuildFace.Parts.cs"), mode = ModSource(@"Construction\BuildMode.cs");
        string window = ModSource(@"Construction\BuildMode.Window.cs");
        Check(MethodText(face, "public bool Press(").Contains("_calls.Filter?.Invoke(arg)") && MethodText(face, "public bool Press(").Contains("UiBuildPanels.ChosenShown(_m.Catalogue) < 0")
              && mode.Contains("Filter = FilterPicked") && parts.Contains("Array.Copy(filterNames, c.Filters"), "build mode kit filters: a filter pressed reaches BuildMode's own FilterPicked, and Place refuses a design out of sight");
        Check(MethodText(window, "private void FilterPicked(int filter)").Contains("_card = -1;"), "build mode kit filters: a filter that hides the chosen design lets it go");
        string counts = MethodText(window, "private void ReadUseCounts()");
        Check(counts.Contains("BuildModeCatalogue.Count(BuildModeCatalogue.First(") && !counts.Contains("_useCount[u] = _designs.Count;") && window.IndexOf("ReadDesignFacts();", StringComparison.Ordinal) < window.IndexOf("ReadUseCounts();", StringComparison.Ordinal),
            "build mode kit filters: a use's tab counts the cards it opens on, not every design of the use");
        Check(BuildModeCatalogue.Count(BuildModeCatalogue.First(new[] { false, true }, new[] { false, false }, 2), new[] { false, true }, new[] { false, false }, 2) == 1
              && BuildModeCatalogue.Count(BuildModeCatalogue.Fits, new[] { false, true, false }, new[] { true, false, true }, 3) == 2, "build mode kit filters: a tab opens on the first filter that shows anything, and counts it");
        // The tool panel: past the room it scrolls, and its lines are never cut.
        UiBuildModel tall = UiSamples.BuildMode(BmWords, "zone");
        int need = 0;
        while (UiBuildPanels.SheetHeight(tall) <= f.BandY && need++ < 200)
        {
            if (tall.LineCount == tall.Lines.Length)
            {
                Array.Resize(ref tall.Lines, tall.Lines.Length * 2);
            }
            tall.Lines[tall.LineCount++] = new UiBuildLine { Kind = UiBuildLineKind.Fact, Label = "a", Value = "b", Rgb = -1 };
        }
        var tallScene = new UiScene();
        UiBuildPanels.Lay(tallScene, tall, f, true, UiDefaultKeys.Keyboard);
        bool inside = true;
        for (int i = 0; i < tallScene.ClipCount; i++)
        {
            inside &= tallScene.Clips[i].Y + tallScene.Clips[i].H <= f.BandY;
        }
        Check(tallScene.ClipCount > 0 && inside && UiBuildPanels.SheetRoom(f, 100f) < f.BandY, "build mode kit panel: a tool panel taller than the room scrolls in a clip that ends above the bar");
        Check(MethodText(parts, "private ref UiBuildLine Next(").Contains("Array.Resize(ref m.Lines") && !parts.Contains("Math.Min(m.LineCount, m.Lines.Length - 1)"), "build mode kit panel: a panel with many lines grows its list instead of writing over the last line");
    }

    // The panels' places at every frame the game gives: the left panel, the tool panel, the design window and the tour's card, none over another, all on the screen.
    private static void BuildModePlaceChecks()
    {
        bool apart = true, inside = true;
        string where = null;
        foreach (float aspect in UiAspects)
        {
            UiFrame f = UiFrame.FullMenu(aspect, "Build mode");
            foreach (string state in UiSamples.BuildModeStates)
            {
                UiBuildModel m = UiSamples.BuildMode(BmWords, state);
                UiBuildPanels.LeftBox(m, f, out float lx, out float ly, out float lw, out float lh);
                float right = f.Width - UiTokens.S4, sx = right - UiBuildPanels.SheetWidth, sy = UiTokens.S4 + UiTokens.Control + UiTokens.S2, sh = UiBuildPanels.SheetHeight(m);
                bool sheet = m.Catalogue == null && m.LineCount > 0;
                if (sheet && (lx + lw > sx || sy + sh > f.BandY - UiTokens.S3))
                {
                    apart = false;
                    where = where ?? state + " at " + aspect.ToString("0.00") + ": the tool panel meets the left panel or the bar";
                }
                if (m.Catalogue != null)
                {
                    UiBuildPanels.CatalogueBox(m.Catalogue, f, lx + lw, out float cx, out float cy, out float cw, out float ch);
                    UiBuildPanels.Grid(cw, out int cols, out int rows);
                    bool onScreen = cx >= lx + lw && cx + cw <= f.Width && cy + ch <= f.BandY && cols >= 2 && rows >= 2;
                    inside &= onScreen;
                    if (!onScreen)
                    {
                        where = where ?? state + " at " + aspect.ToString("0.00") + ": the design window runs off the screen or holds under two columns";
                    }
                    // The panels' tops are above the window's top by the left panel's foot: the window stands to the right of the left panel, so they never meet.
                }
                inside &= ly + lh <= f.BandY;
            }
        }
        Check(apart && inside, "build mode kit places: no panel over another and all on the screen at every frame" + (where != null ? " (" + where + ")" : string.Empty));
    }

    // What keeps the map and the camera the game's, and what puts the kit's face in only while its switch is on.
    private static void BuildModeWiringChecks()
    {
        string face = ModSource(@"Ui\BuildMode\BuildFace.cs");
        string kits = ModSource(@"Ui\BuildMode\BuildKits.cs");
        string mode = ModSource(@"Construction\BuildMode.cs");
        string status = ModSource(@"Ui\Hud\HudStatusGame.cs");
        string box = ModSource(@"Ui\Host\UiMinimapBox.cs");
        string director = ModSource(@"Ui\UiDirector.cs");
        Check(MethodText(face, "public void Show(bool shown)").Contains("_host.PointerOnly = true;") && face.Contains("public bool Back() => false;") && face.Contains("public bool Act(UiAction action) => false;"),
            "build mode kit wiring: the piece takes the pointer over its own targets and no key, so the map's camera and Esc stay the game's");
        Check(kits.Contains("UiScreenSwitches.On(BuildFace.ScreenId)") && kits.Contains("UiScreenSwitches.On(\"hud\")") && kits.Contains("BuildMode.KitOn = On;") && director.Contains("BuildKits.Install();")
              && director.Contains("BuildKits.Remove(why);"), "build mode kit wiring: the kit's face is asked for only while the interface, the HUD and its own switch are on, and given back when the interface goes");
        Check(mode.Contains("BuildModeBar.Make(canvas, _kit, ToolPicked, ViewPicked, Picked, Close)") && mode.Contains("KitMake(Calls())") && mode.Contains("kitWanted"),
            "build mode kit wiring: the old panels are still there to go back to when the switch is off");
        Check(status.Contains("ToolRow != null") && status.Contains("m.Apps = null;") && status.Contains("m.Middle = UiToolRow.Width(ToolRow);") && face.Contains("HudStatusGame.ToolRow = _m.Tools;")
              && face.Contains("HudStatusGame.ToolRow = null;"), "build mode kit wiring: the tool row stands on the bar in the apps' place and the bar keeps its width clear, then gives the apps back");
        Check(box.Contains("BuildFace.Showing") && box.Contains("UiBuildPanels.LeftBox("), "build mode kit wiring: the objectives keep clear of the left panel as they do of the map's dock");
        string ask = ModSource(@"Construction\BuildMode.Window.cs");
        Check(MethodText(ask, "private static void Ask(").Contains("GameConfirm.Show(") && MethodText(ask, "private void Refresh(").Contains("Notifications.Show("),
            "build mode kit wiring: every confirm is the game's question box (drawn by the kit's question card) and every answer a notice card");
        Check(MethodText(face, "public bool Press(").Contains("reallife_ui_build_tool_off") && !face.Contains("Input.") && !face.Contains("Camera.") && !face.Contains("CityMapCam"),
            "build mode kit wiring: a greyed tool says why in a notice; the face reads no input and touches no camera");
    }
}
