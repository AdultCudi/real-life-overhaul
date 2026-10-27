using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Uptown;

// No ground of a block left unexplained (the owner, 27 Oct 2026, two screenshots of build mode's Zoning view: "There are empty spots in the
// block with no grids"): the wholesale store's and the filling station's ground, in no lot of the plan, never had its building (the
// declaration read the place's lot through UptownLots.Get, which is null for a place), so it showed as bare green with no squares. The
// plan's ground is walked metre by metre: every spot is a square, a held ground (a special place, a park, the campus, the district's
// edge) or the plan's thin margin; every column of squares is a building, for sale or held; and each rule is made to fail once.
internal static partial class Program
{
    private static void BuildGroundChecks()
    {
        BuildGroundPlanChecks();
        BuildGroundFailChecks();
        BuildGroundSourceChecks();
        BuildGroundTextChecks();
    }

    // The plan as the game reads it at a new game's load (LifeGame.Lots): its lots, then the big places' ground.
    private static List<BuildingsLot> BuildGroundPlan()
    {
        var plan = new List<BuildingsLot>();
        foreach (Lot l in LayoutRules.Lots.Concat(LayoutRules.Places))
        {
            plan.Add(new BuildingsLot
            {
                Id = l.Id, Street = l.Street, Number = l.Number, Block = l.Block, First = l.First, Avenue = BuildingsRules.FrontsAvenue(l.Facing), X = l.X, Z = l.Z, Facing = l.Facing,
                Width = l.Width, Depth = l.Depth, Class = (BuildingsClass)(int)l.Class, Sizes = l.Sizes, Version = l.Version, Spaces = l.Spaces, Strip = l.Strip, Index = l.Index, Open = l.Open
            });
        }
        return plan;
    }

    // The grid over the plan's lots with the held grounds the plan names, and what each column is at a new game's load: built from the first
    // day (standing), an open run's (for sale), a landmark's block left open (held), the lot something stands into (kept clear).
    private static CellGrid BuildGroundGrid(out Func<int, BuildModeRules.ColumnFacts> facts, out List<BlockView> blocks, bool placesStand = true, bool dropPlaces = false)
    {
        List<LotView> lots = BuildModePlan();
        for (int i = 0; i < lots.Count; i++)
        {
            LotView v = lots[i];
            v.Kept = !BuildingsRules.Clear(v.Id);
            lots[i] = v;
        }
        CellGrid grid = CellGrid.Of(lots);
        foreach (HeldGround h in HeldGroundRules.All)
        {
            if (dropPlaces && h.Kind == HeldGroundKind.Place)
            {
                continue;
            }
            grid.Held.Add(new HeldView
            {
                Kind = (HeldWhat)(int)h.Kind, Block = h.Block, District = DistrictSplit.OfBlock(h.Block), Lot = h.Lot, Street = h.Street, Number = h.Number, Name = h.Name, NameKey = h.NameKey,
                Stands = h.Kind != HeldGroundKind.Place || placesStand, X1 = h.X1, Z1 = h.Z1, X2 = h.X2, Z2 = h.Z2
            });
        }
        List<BuildingsLot> plan = BuildGroundPlan();
        var built = new List<int>();
        BuildingsRules.Built(plan, GroundSurvey.IsLandmarkBlock, 0f, 1f, built);
        var standing = new HashSet<string>(built.Select(i => plan[i].Id));
        var byLot = lots.ToDictionary(l => l.Id);
        facts = c =>
        {
            LotView lot = byLot[grid.Columns[c].LotId];
            var f = new BuildModeRules.ColumnFacts { Use = -1, MainUse = -1, CouncilUse = -1 };
            if (lot.Kept)
            {
                f.Held = f.Kept = true;
            }
            else if (GroundSurvey.IsLandmarkBlock(lot.Block) && !standing.Contains(lot.Id) && lot.Size != null && !LayoutRules.ById[lot.Id].Open)
            {
                f.Held = true;   // a landmark's block: every lot not built from the first day is the landmark's site
            }
            else if (LayoutRules.ById[lot.Id].Open)
            {
                f.Free = true;
            }
            else if (standing.Contains(lot.Id))
            {
                f.Building = f.Stands = true;
            }
            return f;
        };
        blocks = new List<BlockView>();
        for (int row = 1; row <= LayoutRules.Rows + 1; row++)
        {
            for (int column = 0; column <= LayoutRules.Columns + 1; column++)
            {
                if ((row <= LayoutRules.Rows && column >= 1 && column <= LayoutRules.Columns) || LayoutRules.IsRing(row, column))
                {
                    LayoutRules.Block(row, column, out float x0, out float z0, out float x1, out float z1);
                    blocks.Add(new BlockView { Block = row * 100 + column, X1 = x0, Z1 = z0, X2 = x1, Z2 = z1 });
                }
            }
        }
        return grid;
    }

    private static void BuildGroundPlanChecks()
    {
        CellGrid grid = BuildGroundGrid(out Func<int, BuildModeRules.ColumnFacts> facts, out List<BlockView> blocks);
        BareReport r = BuildGroundRules.Audit(grid, blocks, facts, 1f);
        Check(r.Unclassified == 0 && r.Classes[(int)ColumnClass.Standing] + r.Classes[(int)ColumnClass.ForSale] + r.Classes[(int)ColumnClass.Landmark] + r.Classes[(int)ColumnClass.Kept] == grid.Columns.Count,
            "bare ground: every column of squares of every block is a building, for sale, held for a landmark or kept clear (" + grid.Columns.Count + " columns, " + r.Unclassified + " accounted for by nothing"
            + (r.FirstColumn != null ? ", the first " + r.FirstColumn : string.Empty) + ")");
        Check(r.BareM2 <= 0.0 && r.Blocks == 24 + 3 * 18 && r.MarginM2 > 0.0 && r.MarginM2 < r.CellM2 * 0.02,
            "bare ground: every metre of every one of " + r.Blocks + " blocks is a square, a held ground or the plan's margin (" + r.BareM2.ToString("0") + " m2 bare, " + r.MarginM2.ToString("0") + " m2 margin"
            + (r.FirstBare != null ? ", the first " + r.FirstBare : string.Empty) + ")");
        Check(r.Ok && r.Line().EndsWith("PASS", StringComparison.Ordinal) && r.Line().StartsWith("Build mode check (bare ground): ", StringComparison.Ordinal),
            "bare ground: the load's line says PASS on the plan (" + r.Line() + ")");
        Check(r.Classes[(int)ColumnClass.Kept] >= 1 && r.Classes[(int)ColumnClass.ForSale] > 60 && r.Classes[(int)ColumnClass.Landmark] > 20 && r.Classes[(int)ColumnClass.Standing] > 1000,
            "bare ground: the tree's lot is kept clear, the open runs' spaces are for sale, the landmarks' blocks are held and the buildings of the first day stand (" + string.Join(", ", r.Classes) + ")");

        // What holds each piece: two special places, two parks, the campus and the ring's edges, each with a name.
        Check(r.HeldKinds[(int)HeldWhat.Place] == 2 && r.HeldKinds[(int)HeldWhat.Park] == 2 && r.HeldKinds[(int)HeldWhat.Campus] == 1 && r.HeldKinds[(int)HeldWhat.Edge] == 24,
            "bare ground: held ground is 2 special places, 2 parks, the campus and 24 edges (" + string.Join(", ", r.HeldKinds) + ")");
        Check(grid.Held.All(h => !string.IsNullOrEmpty(h.Name) || !string.IsNullOrEmpty(h.NameKey)) && grid.Held.Where(h => h.Kind == HeldWhat.Place).All(h => !string.IsNullOrEmpty(h.Name) && h.Street != null),
            "bare ground: every held ground is named, a special place by its business and its address");
        HeldView store = grid.Held.First(h => h.Lot == "uptown_place_wholesale"), fuel = grid.Held.First(h => h.Lot == "uptown_place_fuel");
        Check(store.Name == "Northgate Wholesale" && fuel.Name == "Summit Fuel" && store.Block == 108 && fuel.Block == 206 && store.District == DistrictSplit.CedarHeights && fuel.District == DistrictSplit.CedarHeights,
            "bare ground: the wholesale store's ground is block 108's, the filling station's block 206's, both in Cedar Heights");
        // The filling station's building takes 32 m of its avenue strip's 39: the 7 m behind it is its ground too, so no strip of bare grass stands behind it.
        Check(Math.Abs((fuel.Z2 - fuel.Z1) - LayoutRules.AvenueDepth) < 0.01f && Math.Abs((store.Z2 - store.Z1) - 41f) < 0.6f,
            "bare ground: the filling station holds its avenue strip's whole 39 m, 7 behind its own 32, and the wholesale store its 41 (" + (fuel.Z2 - fuel.Z1) + ", " + (store.Z2 - store.Z1) + ")");
        bool apart = true;
        for (int i = 0; i < grid.Held.Count && apart; i++)
        {
            for (int k = i + 1; k < grid.Held.Count && apart; k++)
            {
                HeldView a = grid.Held[i], b = grid.Held[k];
                apart = !(a.X1 < b.X2 && b.X1 < a.X2 && a.Z1 < b.Z2 && b.Z1 < a.Z2);
            }
            for (int c = 0; c < grid.Columns.Count && apart; c++)
            {
                if (grid.Columns[c].Block != grid.Held[i].Block)
                {
                    continue;
                }
                grid.Rect(c, 0, 0.01f, out float x1, out float z1, out float x2, out float z2);
                HeldView h = grid.Held[i];
                apart = !(x1 < h.X2 && h.X1 < x2 && z1 < h.Z2 && h.Z1 < z2);
            }
        }
        Check(apart, "bare ground: no held ground overlaps another or a square, so a spot is one thing");

        // The plan's own survey agrees with the grid: a spot it calls a lot is under a square, a place's, a park's, the campus's and the ring's under a held ground, and none is "between".
        int between = 0, lotNoSquare = 0, heldNoGround = 0, points = 0;
        foreach (BlockView b in blocks)
        {
            for (float z = b.Z1 + 0.5f; z < b.Z2; z += 1f)
            {
                for (float x = b.X1 + 0.5f; x < b.X2; x += 1f)
                {
                    GroundKind kind = GroundSurvey.At(x, z).Kind;
                    bool square = !grid.At(x, z).None, held = grid.HeldAt(x, z) >= 0;
                    points++;
                    between += kind == GroundKind.Between ? 1 : 0;
                    lotNoSquare += (kind == GroundKind.Lot || kind == GroundKind.OpenRun || kind == GroundKind.LandmarkSite) && !square && !held ? 1 : 0;
                    heldNoGround += (kind == GroundKind.BigPlace || kind == GroundKind.ParkSquare || kind == GroundKind.Campus || kind == GroundKind.Ring) && !held && !square
                                    && GroundSurvey.Gap(LayoutRules.Lots, b.Block, x, z) > GroundSurvey.MarginMost ? 1 : 0;
                }
            }
        }
        Check(points > 500000 && between == 0 && lotNoSquare == 0 && heldNoGround == 0,
            "bare ground: the plan's own survey of " + points + " spots has none between buildings, none of a lot with no square and none of a place, park, campus or edge with no held ground (" + between + ", " + lotNoSquare + ", " + heldNoGround + ")");

        // The thin margin is no wider than GroundSurvey says, on the real plan (SpacesRules' whole metres leave under a metre a strip end).
        float widest = 0f;
        foreach (BlockView b in blocks)
        {
            if (LayoutRules.IsRing(b.Block / 100, b.Block % 100) || LayoutRules.IsSquare(b.Block / 100, b.Block % 100))
            {
                continue;
            }
            for (float z = b.Z1 + 0.25f; z < b.Z2; z += 0.5f)
            {
                for (float x = b.X1 + 0.25f; x < b.X2; x += 0.5f)
                {
                    if (grid.At(x, z).None && grid.HeldAt(x, z) < 0)
                    {
                        widest = Math.Max(widest, GroundSurvey.Gap(LayoutRules.Lots, b.Block, x, z));
                    }
                }
            }
        }
        Check(widest > 0f && widest <= GroundSurvey.MarginMost && GroundSurvey.MarginMost == BuildGroundRules.MarginMost, "bare ground: the plan's margin is at most " + GroundSurvey.MarginMost + " m (" + widest.ToString("0.00") + ")");

        // Held ground is never for sale: the grid's columns, a landmark's lots and a kept one, are not free for the player or for the city's developers and car-park builders alike (all ask one thing).
        var held2 = new BuildModeRules.ColumnFacts { Held = true, Free = true };
        Check(BuildGroundRules.ClassOf(held2) == ColumnClass.Unclassified && BuildGroundRules.ClassOf(new BuildModeRules.ColumnFacts { Held = true }) == ColumnClass.Landmark
              && BuildGroundRules.ClassOf(new BuildModeRules.ColumnFacts { Held = true, Kept = true }) == ColumnClass.Kept,
            "bare ground: a column both held and for sale is found out, so the player and the city's builders never see held ground differently");
        for (int v = 0; v < (int)BuildView.Count; v++)
        {
            BuildModeRules.Look(new BuildModeRules.ColumnFacts { Held = true, Building = true, Use = 1 }, 0, 5, (BuildView)v, out int fill, out int line);
            Check(fill == BuildModeRules.HeldFill && line == BuildModeRules.LineHeld, "bare ground: a held square is drawn the one way in the " + (BuildView)v + " view");
        }
        BuildModeRules.Look(new BuildModeRules.ColumnFacts { Mine = true, MineRows = 5, Held = true }, 0, 5, BuildView.Zoning, out int mineFill, out _);
        Check(mineFill != BuildModeRules.HeldFill, "bare ground: the player's own land is never drawn as held");
        var fills = new int[16];
        var lines = new int[16];
        var keys = new string[16];
        int zoning = BuildModeRules.Legend(BuildView.Zoning, fills, lines, keys), status = BuildModeRules.Legend(BuildView.Status, fills, lines, keys);
        var zk = new string[16];
        BuildModeRules.Legend(BuildView.Zoning, fills, lines, zk);
        var sk = new string[16];
        BuildModeRules.Legend(BuildView.Status, fills, lines, sk);
        Check(zk.Contains("reallife_bld_mode_key_held") && sk.Contains("reallife_bld_mode_key_held") && zoning <= BuildModeRules.LegendMost && status <= BuildModeRules.LegendMost,
            "bare ground: the Zoning and Status legends say what the held colour is, within the legend's " + BuildModeRules.LegendMost + " entries");
        int bright = BuildModeRules.Apart(BuildModeRules.Palette[BuildModeRules.HeldFill], BuildModeRules.Palette[BuildModeRules.ForSale]);
        Check(BuildModeRules.Palette.Length == BuildModeRules.PaletteSize && bright > 9000 && BuildModeRules.Apart(BuildModeRules.Palette[BuildModeRules.LineHeld], BuildModeRules.Palette[BuildModeRules.LineForSale]) > 9000,
            "bare ground: the held colour and its edge read apart from land for sale (" + bright + ")");
        Check(grid.LayerCells == grid.CellCount + grid.Held.Count && grid.HeldAt(store.X1 + 1f, store.Z1 + 1f) >= 0 && grid.HeldAt(store.X1 - 20f, store.Z1 + 1f) < 0,
            "bare ground: the layer draws one square for each held ground after the grid's, and the pointer finds the held ground under it");
    }

    // Each rule made to fail once, on a changed copy of the plan.
    private static void BuildGroundFailChecks()
    {
        // 1. A place left out of the held grounds is bare ground: the wholesale store's 70 by 41 m.
        CellGrid none = BuildGroundGrid(out Func<int, BuildModeRules.ColumnFacts> f1, out List<BlockView> b1, true, true);
        BareReport lost = BuildGroundRules.Audit(none, b1, f1, 1f);
        Check(!lost.Ok && lost.BareM2 > 2800.0 && lost.WorstBlock == "108" && lost.Line().EndsWith("FAIL", StringComparison.Ordinal),
            "bare ground, fails on purpose: with the special places' ground left out the check finds the wholesale store's bare ground in block 108 (" + lost.BareM2.ToString("0") + " m2, worst " + lost.WorstBlock + ")");
        // 2. A place whose building does not stand fails, though its ground is held.
        CellGrid down = BuildGroundGrid(out Func<int, BuildModeRules.ColumnFacts> f2, out List<BlockView> b2, false);
        BareReport empty = BuildGroundRules.Audit(down, b2, f2, 2f);
        Check(!empty.Ok && empty.PlacesDown == 2 && empty.BareM2 <= 0.0, "bare ground, fails on purpose: a place whose building did not stand fails the check though its ground is held (" + empty.PlacesDown + ")");
        // 3. A column of squares that says it has a building where none stands is found.
        CellGrid g3 = BuildGroundGrid(out Func<int, BuildModeRules.ColumnFacts> f3, out List<BlockView> b3);
        int built = Enumerable.Range(0, g3.Columns.Count).First(c => f3(c).Stands);
        BareReport lie = BuildGroundRules.Audit(g3, b3, c => { BuildModeRules.ColumnFacts f = f3(c); f.Stands = f.Stands && c != built; return f; }, 4f);
        Check(!lie.Ok && lie.Unclassified == 1 && lie.FirstColumn != null && lie.FirstColumn.Contains(g3.Columns[built].LotId), "bare ground, fails on purpose: a lot declared built with no building standing is found, by its lot (" + lie.FirstColumn + ")");
        // 4. A column that is not a building, not for sale, not held and not the developers' is found.
        BareReport nothing = BuildGroundRules.Audit(g3, b3, c => { BuildModeRules.ColumnFacts f = f3(c); return c == built ? new BuildModeRules.ColumnFacts() : f; }, 4f);
        Check(!nothing.Ok && nothing.Unclassified == 1, "bare ground, fails on purpose: a column that is nothing in particular is found");
        // 5. A park left out of the held grounds, and a square block grown bare, are found.
        CellGrid g5 = BuildGroundGrid(out Func<int, BuildModeRules.ColumnFacts> f5, out List<BlockView> b5);
        g5.Held.RemoveAll(h => h.Kind == HeldWhat.Park && h.Block == 117);
        BareReport park = BuildGroundRules.Audit(g5, b5, f5, 2f);
        Check(!park.Ok && park.WorstBlock == "117" && park.BareM2 > 8000.0, "bare ground, fails on purpose: a park left out of the held grounds is 8,400 m2 of bare ground in its block (" + park.BareM2.ToString("0") + ", " + park.WorstBlock + ")");
        // 6. A gap wider than the margin between strips is found: a column dropped from the grid.
        List<LotView> lots = BuildModePlan();
        lots.RemoveAt(lots.FindIndex(l => l.Block == 203 && l.Spaces >= 3));
        CellGrid g6 = CellGrid.Of(lots);
        foreach (HeldView h in g3.Held)
        {
            g6.Held.Add(h);
        }
        var plain = new BuildModeRules.ColumnFacts { Building = true, Stands = true };
        BareReport hole = BuildGroundRules.Audit(g6, b3, c => plain, 2f);
        Check(!hole.Ok && hole.WorstBlock == "203" && hole.BareM2 > 150.0, "bare ground, fails on purpose: a lot of the plan with no squares leaves bare ground in its block (" + hole.BareM2.ToString("0") + " m2, block " + hole.WorstBlock + ")");
        // 7. The survey's own rule: a point between buildings farther than the margin from any lot is "between", not margin.
        LayoutRules.Block(2, 3, out float x0, out float z0, out _, out _);
        Check(GroundSurvey.Gap(LayoutRules.Lots, 203, x0 + 200f, z0 + 200f) > GroundSurvey.MarginMost && GroundSurvey.Gap(LayoutRules.Lots, 203, x0 + 3f, z0 + 3f) == 0f,
            "bare ground, fails on purpose: ground over a metre and a half from any lot is not the margin");
    }

    private static void BuildGroundSourceChecks()
    {
        string big = ModSource(@"Uptown\LifeDirector.BigPlaces.cs"), lots = ModSource(@"Uptown\UptownLots.cs"), life = ModSource(@"Uptown\LifeDirector.cs");
        Check(big.Contains("UptownLots.PlaceOf(place.Lot)") && !big.Contains("UptownLots.Get(place.Lot)") && big.Contains("private static void BigCensus(string when)") && big.Contains("Uptown check (big places)"),
            "bare ground: a big place is declared wanted by reading its own ground (UptownLots.PlaceOf), never through Get, which is null for a place, and the load says whether each stands");
        Check(life.Contains("BigCensus(\"the load\")"), "bare ground: the load runs the big places' check after the special places'");
        var places = LayoutRules.Places;
        Check(places.Count == 2 && places.All(p => UptownLots.Get(p.Id) == null && UptownLots.PlaceOf(p.Id) == p && UptownLots.PlaceOf(p.Id).District == DistrictSplit.CedarHeights)
              && UptownLots.PlaceOf("uptown_1_09_0004") == null && UptownLots.PlaceOf(null) == null && lots.Contains("public static Lot PlaceOf(string id)")
              && ModSource(@"Uptown\DistrictBuildings.cs").Contains("UptownLots.Get(lotId) ?? UptownLots.PlaceOf(lotId)"),
            "bare ground: each place's ground is found by its id (PlaceOf) where Get finds none, its district Cedar Heights', and no lot of the plan is taken for a place");
        string director = ModSource(@"Construction\ConstructionDirector.BareGround.cs"), cd = ModSource(@"Construction\ConstructionDirector.cs"), bm = ModSource(@"Construction\ConstructionDirector.BuildMode.cs");
        Check(ModSource(@"Construction\BuildGroundRules.cs").Contains("Build mode check (bare ground): ") && director.Contains("BuildGroundRules.Audit(grid, _blocks, FactsOf, BareStep)")
              && director.Contains("!Verbose") && cd.Contains("BareGroundSecond();") && cd.Contains("_bareChecked = false;"),
            "bare ground: the load writes 'Build mode check (bare ground)' once, a second after the district stands, with Developer diagnostics on");
        Check(bm.Contains("ReadHeld(_cells);") && bm.Contains("f.Held = true;") && bm.Contains("LandmarkHeld(lot.Id)") && bm.Contains("public bool HeldCard(int index") && bm.Contains("f.Stands = LotBuilt(lot.Id)"),
            "bare ground: the grid reads the held grounds, a landmark's lot and a kept one are held whoever asks, and held ground has its card");
        string mode = ModSource(@"Construction\BuildMode.cs"), game = ModSource(@"Construction\BuildModeGame.cs"), wiring = ModSource(@"Wiring\Wiring.BuildModeLinks.cs"), wlinks = ModSource(@"Wiring\Wiring.ConstructionLinks.cs");
        Check(mode.Contains("_d.Cells.HeldAt(at.x, at.z)") && mode.Contains("_d.HeldCard(held") && mode.Contains("grid.LayerCells") && game.Contains("grid.Columns.Count + grid.Held.Count")
              && wiring.Contains("BuildModeBridges.Held = HeldInto;") && wiring.Contains("BuildModeBridges.LandmarkOf = LandmarksDirector.SiteIdOfLot;") && wiring.Contains("BuildModeBridges.Held = null;")
              && wlinks.Contains("Kept = !BuildingsRules.Clear(l.Id)"),
            "bare ground: build mode draws and names the held grounds, and the wiring gives and takes back what it asks for");
        Check(ModSource(@"Landmarks\LandmarksDirector.Works.cs").Contains("public static string SiteIdOfLot(string lot)") && ModSource(@"Uptown\GroundSurvey.cs").Contains("Margin = 13")
              && ModSource(@"Uptown\GroundDirector.Survey.cs").Contains("the place does not stand at this load"),
            "bare ground: a held lot says which landmark holds it and 'What is this ground?' says whether a place stands");
    }

    private static void BuildGroundTextChecks()
    {
        string[] keys =
        {
            "reallife_bld_mode_key_held", "reallife_bld_held_landmark_name", "reallife_bld_held_kept_name", "reallife_bld_held_landmark", "reallife_bld_held_kept", "reallife_bld_hover_held_head",
            "reallife_bld_held_place_name", "reallife_bld_held_place", "reallife_bld_held_place_down", "reallife_bld_held_park_name", "reallife_bld_held_park", "reallife_bld_held_campus",
            "reallife_bld_held_edge_name", "reallife_bld_held_edge"
        };
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("bare ground texts", words, codes))
        {
            return;
        }
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            string missing = null;
            foreach (string key in keys)
            {
                if (!words[l].TryGetValue(key, out string value) || value.Length == 0 || value.Length > 240 || value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Contains(" - "))
                {
                    missing = missing ?? key;
                }
                else if (l > 0 && words[0].TryGetValue(key, out string en) && NamesOf(en) != NamesOf(value))
                {
                    missing = missing ?? key + " (its names in curly brackets)";
                }
            }
            Check(missing == null, "bare ground texts: every key is in " + codes[l] + ".json, in the style, with the English names (" + (missing ?? "all") + ")");
        }
        string src = string.Concat(ModSource(@"Construction\ConstructionDirector.BuildMode.cs"), ModSource(@"Construction\BuildModeRules.cs"), ModSource(@"Uptown\HeldGroundRules.cs"));
        Check(keys.All(k => src.Contains("\"" + k + "\"") || k.EndsWith("_down") || k.EndsWith("_place") || k.EndsWith("_park") || k.EndsWith("_campus") || k.EndsWith("_edge")),
            "bare ground texts: every key is read by the code");
        for (int l = 0; l < words.Length; l++)
        {
            Check(words[l].TryGetValue("reallife_changes_next", out string next) && WhatsNewFixed(codes[l], next, "bare") , "bare ground texts: the fix line is in What's new's next row in " + codes[l]);
        }
    }

    // The next row carries a fix line with its language's mark (and is not the empty row).
    private static bool WhatsNewFixed(string code, string next, string _)
    {
        var marks = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Fixed: " }, { "pt", "Corrigido: " }, { "zh-cn", "已修复：" }, { "de", "Behoben: " }, { "fr", "Corrigé : " }, { "es", "Corregido: " }, { "ru", "Исправлено: " },
            { "nl", "Opgelost: " }, { "tr", "Düzeltildi: " }, { "pl", "Naprawiono: " }, { "ko", "수정: " }
        };
        return marks.TryGetValue(code, out string mark) && next.StartsWith(mark, StringComparison.Ordinal) && next.Length > 40;
    }
}
