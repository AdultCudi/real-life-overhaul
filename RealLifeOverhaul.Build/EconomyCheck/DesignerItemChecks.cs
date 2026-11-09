using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Designs;
using RealLifeOverhaul.Ui;

// What the interior designer's catalogue and cost lines gained: the "RLO fit-out" tab and the RLO badge, the facts on a tile (where a piece goes, what it holds,
// its size in the game's grid cells), and the old floor sold back as a line of its own. The tab lists the pieces the mod's fit-outs are made of that the designer
// can place, and a piece pressed there is placed by the call every other tab's is. The facts come from the item's own placement flags, cargo capacity and prefab
// colliders, as the game's grid filler reads them. The refund is the game's (InteriorElementTool.CalculateInteriorElementChangePrice): checked against that formula
// copied out of the decompiled game. Run alone with --designer-items, and in the main run.
internal static partial class Program
{
    private static void DesignerItemChecks()
    {
        int before = _run, failedBefore = _failed;
        var watch = Stopwatch.StartNew();
        DesignerFitTabChecks();
        DesignerFactChecks();
        DesignerRefundChecks();
        DesignerItemSourceChecks();
        DesignerItemScreenChecks();
        Console.WriteLine("  designer items: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // The tab lists only the mod's pieces, and every one of them the designer offers.
    private static void DesignerFitTabChecks()
    {
        string p = DesignCatalogue.ItemPrefix;
        var offered = new List<string> { p + "armchair1", p + "fridgeofthegame", p + "cashregister", p + "sofa_not_ours", p + "glassbarrier", p + "ceilinglampround" };
        var shown = new List<string>();
        int n = DesignerFitOut.Pick(offered, shown);
        Check(n == 4 && shown.Count == 4 && shown[0] == p + "armchair1" && shown[1] == p + "cashregister" && shown[2] == p + "glassbarrier" && shown[3] == p + "ceilinglampround",
            "designer fit-out: the tab lists the pieces the mod's fit-outs are made of from what the designer offers, in its order, and nothing else (" + string.Join(", ", shown) + ")");
        // A showroom piece and a bank branch piece are both the mod's; a piece the game offers that no design of the mod keeps is not.
        Check(DesignerFitOut.Is(p + "officedesk1") && DesignerFitOut.Is(p + "beltbarrier") && !DesignerFitOut.Is(p + "fridgeofthegame") && !DesignerFitOut.Is(null)
            && !DesignerFitOut.Is("armchair1"),
            "designer fit-out: a piece is the mod's by the game's item name (its prefix included) if a showroom or branch design keeps it, and a null or bare name is not");
        // All of them that are placeable: offer every piece the designs keep and the tab shows every one, once.
        var every = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        bool unique = true;
        foreach (string bare in DesignCatalogue.ShowroomKeeps)
        {
            unique &= seen.Add(bare);
            every.Add(p + bare);
        }
        foreach (string bare in DesignCatalogue.BranchExtras)
        {
            if (seen.Add(bare))
            {
                every.Add(p + bare);
            }
        }
        every.Insert(3, p + "not_a_design_piece");
        var all = new List<string>();
        int most = DesignerFitOut.Pick(every, all);
        Check(unique && most == every.Count - 1 && most == DesignerFitOut.Size && !all.Contains(p + "not_a_design_piece"),
            "designer fit-out: offered every piece the designs keep, the tab shows every one of them (" + most + " of " + DesignerFitOut.Size + ") and not the stray");
        // A business that can place none of them gets no tab: an empty or null offer picks nothing.
        Check(DesignerFitOut.Pick(new List<string>(), shown) == 0 && shown.Count == 0 && DesignerFitOut.Pick(null, shown) == 0,
            "designer fit-out: with nothing the designer can place there is nothing to list, and no tab (the live code adds the tab only for a count above nothing)");
        // The tab narrows the game's list and leaves the press alone.
        string cat = ModSource(@"Ui\Designer\DesignerGame.Catalogue.cs");
        string place = MethodText(cat, "public static bool PlaceItem(int index)");
        string fit = MethodText(cat, "public static bool FitOut()");
        string press = MethodText(cat, "public static bool Tab(int tab)");
        Check(place.Contains("data.onClickItemName(null, data.itemName)") && place.Contains("View[index]") && !place.Contains("_fitOn") && !place.Contains("DesignerFitOut"),
            "designer fit-out: a piece pressed on the new tab is placed by the call every other tab's tile makes"
            + " (the game's onClickItemName on the same list the tiles are laid from), the tab adding no call of its own");
        Check(cat.Contains("!fitOnly || DesignerFitOut.Is(Listed[i].itemName)") && cat.Contains("View.Add(Listed[i]);") && fit.Contains("SwitchOf(0), true")
            && fit.Contains("DesignerFavToggle")
              && !fit.Contains("onClickItemName") && press.Contains("tab == _fitAt"),
            "designer fit-out: the tab is the game's list on all items cut to the mod's pieces, from the game's own tile data; opening it puts the game on all items with no favourites");
        Check(cat.Contains("DesignerFitOut.Pick(names, FitList)") && cat.Contains("GameContract.DesignerAvailable") && cat.Contains("_fitAt = fit > 0 ? most + 1 : -1;"),
            "designer fit-out: the pieces come from the game's own list of what the designer can place, and the tab is there only when one can be");
        Check(UiDesignerCatalogue.TabsMost >= 14 && cat.Contains("UiDesignerCatalogue.TabsMost - 2"),
            "designer fit-out: the tabs keep room for all of the game's categories (twelve), All items and the new tab");
        // The sample (and so the screen checks and the previews) shows the tab up with only the mod's pieces and the badge on each.
        UiDesignerModel m = UiSamples.Designer(DesignerWords, "furniture_fit");
        UiDesignerCatalogue c = m.Catalogue;
        bool onlyOurs = c.CardCount > 0;
        for (int i = 0; i < c.CardCount; i++)
        {
            onlyOurs &= c.Cards[i].Rlo;
        }
        Check(c.FitTab == c.TabCount - 1 && c.Tab == c.FitTab && c.Tabs[c.FitTab] == "RLO fit-out" && onlyOurs && UiSamples.Designer(DesignerWords, "furniture").Catalogue.Tab == 0,
            "designer fit-out: the sample shows the new tab last and chosen, with only the mod's pieces, each badged");
    }

    // The facts a tile shows are right for a floor, a wall and a ceiling piece, and absent where the piece has none.
    private static void DesignerFactChecks()
    {
        const string holds = "holds {n}", sizeWords = "{w} x {d} tiles";
        string kind, size;
        // A floor piece: neither flag, no cargo, a footprint of two cells by one: a size and no kind.
        DesignerRules.FactWords(DesignerRules.PlaceOf(false, false), 0, 2, 1, holds, "ceiling", "wall", sizeWords, out kind, out size);
        Check(kind == null && size == "2 x 1 tiles", "designer facts: a floor piece shows its size and no kind (" + kind + ", " + size + ")");
        // A wall piece, a ceiling piece, and one that holds things.
        DesignerRules.FactWords(DesignerRules.PlaceOf(true, false), 0, 3, 1, holds, "ceiling", "wall", sizeWords, out kind, out size);
        Check(kind == "wall" && size == "3 x 1 tiles", "designer facts: a wall piece says wall (" + kind + ", " + size + ")");
        DesignerRules.FactWords(DesignerRules.PlaceOf(false, true), 0, 1, 1, holds, "ceiling", "wall", sizeWords, out kind, out size);
        Check(kind == "ceiling" && size == "1 x 1 tiles", "designer facts: a ceiling piece says ceiling (" + kind + ", " + size + ")");
        DesignerRules.FactWords(DesignerRules.PlaceOf(false, false), 24, 2, 2, holds, "ceiling", "wall", sizeWords, out kind, out size);
        Check(kind == "holds 24" && size == "2 x 2 tiles", "designer facts: a piece with cargo capacity says what it holds (" + kind + ", " + size + ")");
        DesignerRules.FactWords(DesignerRules.PlaceOf(true, false), 12, 2, 1, holds, "ceiling", "wall", sizeWords, out kind, out size);
        Check(kind == "wall, holds 12", "designer facts: a wall piece that holds things says both, where it goes first (" + kind + ")");
        Check(DesignerRules.PlaceOf(true, true) == DesignerRules.Place.Ceiling && DesignerRules.PlaceOf(false, false) == DesignerRules.Place.None && DesignerRules.PlaceOf(true,
            false) == DesignerRules.Place.Wall,
            "designer facts: a piece that snaps to the ceiling is a ceiling piece whatever its wall flag says");
        // Absent: no footprint, no flags, no cargo, so no kind and no size, and cargo of nothing or less holds nothing.
        DesignerRules.FactWords(DesignerRules.PlaceOf(false, false), 0, 0, 0, holds, "ceiling", "wall", sizeWords, out kind, out size);
        Check(kind == null && size == null, "designer facts: a piece with no placement flag, no cargo and no footprint shows no fact at all");
        DesignerRules.FactWords(DesignerRules.Place.None, -3, 0, 1, holds, "ceiling", "wall", sizeWords, out kind, out size);
        Check(kind == null && size == null, "designer facts: a negative capacity and a footprint with no width show nothing");
        // The footprint in cells is the game's grid filler's count: each side to the centimetre, over the cell, rounded up.
        Check(DesignerRules.Tiles(1.0f, 0.5f, 0.5f, out int w, out int d) && w == 2 && d == 1, "designer facts: one metre by half a metre on half-metre cells is two by one");
        Check(DesignerRules.Tiles(0.55f, 0.5f, 0.5f, out w, out d) && w == 2 && d == 1, "designer facts: a piece a little over a cell takes the next one");
        Check(DesignerRules.Tiles(1.2f, 0.3f, 0.25f, out w, out d) && w == 5 && d == 2, "designer facts: on quarter-metre cells 1.2 by 0.3 metres is five by two (" + w + " by " + d + ")");
        Check(DesignerRules.Tiles(0.4f, 0.4f, 0f, out w, out d) && w == 1 && d == 1, "designer facts: a piece with no placement step is counted on the default half metre");
        Check(!DesignerRules.Tiles(0f, 1f, 0.5f, out w, out d) && w == 0 && d == 0 && !DesignerRules.Tiles(1f, -1f, 0.5f, out _, out _) && !DesignerRules.Tiles(float.NaN, 1f, 0.5f,
            out _, out _),
            "designer facts: a piece with no footprint has no size");
        // Several colliders merge into the box all of them lie in; a single one is its own size.
        var one = new DesignerRules.Extent();
        one.Add(1f, 0.5f, 0.3f, 0f);
        var two = new DesignerRules.Extent();
        two.Add(1f, 1f, 0f, 0f);
        two.Add(1f, 1f, 1f, 0f);
        two.Add(0.2f, 0.2f, 0f, 0.8f);
        var none = new DesignerRules.Extent();
        Check(Math.Abs(one.Width - 1f) < 1e-4f && Math.Abs(one.Depth - 0.5f) < 1e-4f && Math.Abs(two.Width - 2f) < 1e-4f && Math.Abs(two.Depth - 1.4f) < 1e-4f && none.Width == 0f
            && none.Depth == 0f,
            "designer facts: the colliders of a piece merge into one footprint as the game merges them, and a piece with none has none");
        // The sample's tiles carry the facts: a piece with each, and a piece with none that shows nothing.
        UiDesignerCatalogue cat = UiSamples.Designer(DesignerWords, "furniture").Catalogue;
        bool wall = false, ceiling = false, holding = false, sized = false, bare = false, rlo = false;
        for (int i = 0; i < cat.CardCount; i++)
        {
            UiDesignerCard card = cat.Cards[i];
            wall |= card.Kind == "wall";
            ceiling |= card.Kind == "ceiling";
            holding |= card.Kind != null && card.Kind.StartsWith("holds", StringComparison.Ordinal);
            sized |= card.Size != null && card.Size.Contains(" x ") && !card.Size.Contains("tiles");
            bare |= card.Kind == null && card.Size == null;
            rlo |= card.Rlo;
        }
        Check(wall && ceiling && holding && sized && bare && rlo,
            "designer facts: the sample's tiles show a wall, a ceiling and a holding piece, sizes, an RLO badge and a piece with no fact");
        UiDesignerModel chosen = UiSamples.Designer(DesignerWords, "furniture");
        bool sizeRow = false;
        for (int i = 0; i < chosen.Catalogue.DetailCount; i++)
        {
            sizeRow |= chosen.Catalogue.DetailLabels[i] == "Size"
                       && chosen.Catalogue.DetailValues[i] == chosen.Catalogue.Cards[chosen.Catalogue.Chosen].Size + " tiles";
        }
        Check(sizeRow, "designer facts: the chosen piece's detail has a size row that says what its tile says, in words");
    }

    // The game's price of changing a piece, copied from the decompiled InteriorElementTool.CalculateInteriorElementChangePrice (the branch outside the blueprint creator).
    private static float GameChangePrice(string selectedId, int selectedColour, string startId, int startColour, string nowId, int nowColour, float nowPrice, float newPrice)
    {
        if (startId == selectedId && startColour == selectedColour)
        {
            return -nowPrice;
        }
        float num = 0f;
        if (nowId != startId || nowColour != startColour)
        {
            num -= nowPrice;
        }
        return num + newPrice;
    }

    private static float PriceOfMaterial(string id) => id == "tile" ? 28f : id == "oak" ? 32f : 35f;

    // The old floor sold back is what the game refunds: the price of the material a piece has from this edit, never the room's own.
    private static void DesignerRefundChecks()
    {
        string[] ids = { "tile", "oak", "walnut" };
        int cases = 0, wrong = 0, wrongRefund = 0;
        foreach (string newId in ids)
        {
            foreach (string startId in ids)
            {
                foreach (string nowId in ids)
                {
                    for (int newC = 0; newC < 2; newC++)
                    {
                        for (int startC = 0; startC < 2; startC++)
                        {
                            for (int nowC = 0; nowC < 2; nowC++)
                            {
                                if (nowId == newId && nowC == newC)
                                {
                                    continue;   // the game leaves a piece that is already the material out of the sum
                                }
                                cases++;
                                float cost = DesignerRules.PieceCost(newId, newC, startId, startC, nowId, nowC, PriceOfMaterial(nowId), PriceOfMaterial(newId));
                                float charge = DesignerRules.PieceMoney(newId, newC, startId, startC, nowId, nowC, PriceOfMaterial(nowId), PriceOfMaterial(newId), out float refund);
                                wrong += Math.Abs(cost - GameChangePrice(newId, newC, startId, startC, nowId, nowC, PriceOfMaterial(nowId), PriceOfMaterial(newId))) < 1e-4f ? 0 : 1;
                                // The refund is the old material's price, and only if the piece's material came from this edit or the piece goes home.
                                bool home = startId == newId && startC == newC;
                                bool fromEdit = nowId != startId || nowC != startC;
                                float expect = home || fromEdit ? PriceOfMaterial(nowId) : 0f;
                                wrongRefund += Math.Abs(refund - expect) < 1e-4f && Math.Abs(cost - (charge - refund)) < 1e-4f ? 0 : 1;
                            }
                        }
                    }
                }
            }
        }
        Check(cases == 3 * 3 * 3 * 8 - 3 * 3 * 4 && wrong == 0, "designer refund: a piece's price is the game's own formula in every case of three materials and two shades (" + cases
            + " cases, " + wrong + " wrong)");
        Check(wrongRefund == 0, "designer refund: the refund is the old material's price when it came from this edit or the piece goes home, and nothing otherwise (" + wrongRefund
            + " wrong)");
        // A first laying over the room's own floor is paid back nothing, so the line is not there; laid again in the same edit it pays back what the first laying cost.
        DesignerRules.PieceMoney("oak", 0, "tile", 0, "tile", 0, 28f, 32f, out float first);
        DesignerRules.PieceMoney("walnut", 0, "tile", 0, "oak", 0, 32f, 35f, out float again);
        DesignerRules.PieceMoney("tile", 0, "tile", 0, "oak", 0, 32f, 28f, out float home2);
        DesignerRules.PieceMoney("oak", 0, "tile", 0, "oak", 0, 32f, 32f, out float same);
        Check(first == 0f && again == 32f && home2 == 32f && same == 0f,
            "designer refund: the room's own floor is never paid back (nothing the first time), a floor laid over one laid this edit pays it back (32), going home pays back the one on it");
        // The tally tells the two apart and they add up to the figure the game charges.
        var tally = new DesignerRules.Tally();
        tally.Add(DesignerRules.Cause.Floor, 3f + 32f, 4);
        tally.Add(DesignerRules.Cause.FloorBack, -32f, 4);
        tally.Area += 4f;
        tally.AreaBack += 4f;
        Check(tally.Total() == 3f && tally.Money[(int)DesignerRules.Cause.FloorBack] == -32f && tally.Count[(int)DesignerRules.Cause.FloorBack] == 4 && (int)DesignerRules.Cause.Count == 5,
            "designer refund: the floor laid and the old floor sold back are two lines of the tally that add to the game's one figure for the action");
        tally.Clear();
        Check(tally.AreaBack == 0f && tally.Money[(int)DesignerRules.Cause.FloorBack] == 0f, "designer refund: a new edit starts with nothing paid back");
        // The sample shows the line in the cost lines and in the floor's detail; the wall's detail has none (a wall has no lay-the-whole button).
        UiDesignerModel costed = UiSamples.Designer(DesignerWords, "floors"), floors = UiSamples.Designer(DesignerWords, "floors"), walls = UiSamples.Designer(DesignerWords, "walls");
        bool costLine = false, floorRow = false, wallRow = false;
        for (int i = 0; i < costed.Info.CostCount; i++)
        {
            costLine |= costed.Info.Cost[i].Label.StartsWith("Old floor sold back", StringComparison.Ordinal) && costed.Info.Cost[i].Value.StartsWith("-", StringComparison.Ordinal);
        }
        for (int i = 0; i < floors.Materials.DetailCount; i++)
        {
            floorRow |= floors.Materials.DetailLabels[i] == "Old floor sold back";
        }
        for (int i = 0; i < walls.Materials.DetailCount; i++)
        {
            wallRow |= walls.Materials.DetailLabels[i] == "Old floor sold back";
        }
        Check(costLine && floorRow && !wallRow, "designer refund: the cost lines and the floor's detail show the old floor sold back as its own line, and the wall's detail does not");
    }

    // What the live code does, read from its source: the refund is told from the game's own action, the sizes are read a few a look, the switch off leaves the game's designer.
    private static void DesignerItemSourceChecks()
    {
        string edit = ModSource(@"Ui\Designer\DesignerGame.Edit.cs");
        string mat = ModSource(@"Ui\Designer\DesignerGame.Materials.cs");
        string cat = ModSource(@"Ui\Designer\DesignerGame.Catalogue.cs");
        string read = ModSource(@"Ui\Designer\DesignerGame.Read.cs");
        string face = ModSource(@"Ui\Designer\DesignerFace.cs");
        string kits = ModSource(@"Ui\Designer\DesignerKits.cs");
        string director = ModSource(@"Ui\UiDirector.cs");
        string repay = MethodText(edit, "private static float Repayment(");
        Check(repay.Contains("DesignerLaidOldIds") && repay.Contains("DesignerLaidNewId") && repay.Contains("DesignerRules.PieceMoney(") && repay.Contains("Originals.TryGetValue")
              && edit.Contains("Tally.Add(DesignerRules.Cause.FloorBack, -repaid, pieces)") && edit.Contains("cost + repaid"),
            "designer refund: the line is worked out from the game's own laid-floor action (the materials it replaced and the one it laid)"
            + " against what the room began with, and the floor line is the rest of the game's figure");
        Check(mat.Contains("DesignerRules.PieceMoney(") && mat.Contains("refund += back;") && mat.Contains("reallife_ui_designer_sold_back") && mat.Contains("refund > 0.5f"),
            "designer refund: the lay button's detail shows the old floor sold back from the same rule, and only when something would be paid back");
        string footprint = MethodText(cat, "private static bool Footprint(");
        string sizeOf = MethodText(cat, "private static string SizeOf(");
        Check(footprint.Contains("PrefabHelper.LoadItemControllerFromPrefab(itemName)") && footprint.Contains("prefab.Colliders") && footprint.Contains("BoxCollider")
            && footprint.Contains("SphereCollider")
              && footprint.Contains("CapsuleCollider") && footprint.Contains("MeshCollider") && footprint.Contains("DesignerRules.Tiles("),
            "designer facts: a piece's size is read from its prefab's colliders, the way the game's own grid filler reads them, and counted in the game's cells");
        Check(sizeOf.Contains("budget <= 0") && sizeOf.Contains("_sizesPending = true;") && sizeOf.Contains("Sized[itemName]") && cat.Contains("SizesPerLook = 3")
            && read.Contains("CatalogueSignature()")
              && face.Contains("DesignerGame.CatalogueDrawn("),
            "designer facts: sizes are read three prefabs a look, remembered for the session, and the face looks again while a tile in view is waiting, so no run of loads stalls a frame");
        string kind = MethodText(cat, "private static string KindOf(");
        Check(kind.Contains("item.wallMounted") && kind.Contains("item.snapToCeiling") && kind.Contains("item.cargoCapacity") && kind.Contains("Kinds[itemName]"),
            "designer facts: where a piece goes and what it holds are its own placement flags and cargo capacity, worked out once for each piece");
        // The switch off: the kit's face (and so all of this) is only installed while its switch is on; nothing here runs elsewhere.
        Check(kits.Contains("UiScreenSwitches.On(DesignerFace.ScreenId)") && director.Contains("DesignerKits.Install();") && director.Contains("DesignerKits.Remove(why);"),
            "designer items: the face (and so the tab, the facts and the line) exists only while the designer's switch is on, and with it off the game's own designer is what is on screen");
        string allSource = cat + edit + mat + read;
        Check(!allSource.Contains("Harmony") && !cat.Contains("SetActive(false)") && MethodText(cat, "private static void BuildTabs(").Contains("GetComponentsInChildren")
              && !MethodText(cat, "private static bool FillCatalogue(").Contains("GetComponentsInChildren") && !sizeOf.Contains("GetComponentsInChildren")
                  && !footprint.Contains("GetComponentsInChildren"),
            "designer items: no Harmony patch, and GetComponentsInChildren only where the catalogue is built on open, never in a look at the game");
    }

    // A screen holds at most 48 targets: the catalogue with the new tab in every state, at every frame, in every language of the width checks.
    private static void DesignerItemScreenChecks()
    {
        int most = 0;
        string where = null;
        foreach (float aspect in UiAspects)
        {
            UiFrame f = UiFrame.FullMenu(aspect, "Interior designer");
            foreach (string state in UiSamples.DesignerStates)
            {
                var scene = new UiScene();
                UiSamples.DesignerScene(scene, f, true, UiDefaultKeys.Keyboard, DesignerWords, state);
                if (scene.NodeCount > most)
                {
                    most = scene.NodeCount;
                    where = state + " at " + aspect.ToString("0.00");
                }
            }
        }
        Check(most > 0 && most <= 48, "designer items: the catalogue with the new tab, its badges and its facts holds at most 48 targets in every state at every frame (" + most + " in "
            + where + ")");
        // The tab strip is still one target however many tabs it has, so the new tab cost no target.
        UiDesignerModel model = UiSamples.Designer(DesignerWords, "furniture_fit");
        var withTab = new UiScene();
        UiDesignerPanels.Lay(withTab, model, UiFrame.FullMenu(16f / 9f, "Interior designer"), true, UiDefaultKeys.Keyboard);
        model.Catalogue.TabCount--;
        var without = new UiScene();
        UiDesignerPanels.Lay(without, model, UiFrame.FullMenu(16f / 9f, "Interior designer"), true, UiDefaultKeys.Keyboard);
        Check(withTab.NodeCount == without.NodeCount, "designer items: the row of tabs is one target, so the tab for the mod's fit-out adds none (" + withTab.NodeCount + " and "
            + without.NodeCount + ")");
        // The wheel still moves the tiles a whole row at a time through the tab's pieces, never past the last.
        int cols = UiDesignerPanels.Columns(UiDesignerPanels.CatalogueWidth);
        int pieces = DesignerFitOut.Size;
        float step = UiDesignerPanels.RowStep;
        float last = ((pieces + cols - 1) / cols - UiDesignerPanels.Rows) * step;
        UiDesignerPanels.Visible(pieces, cols, 1e6f, out int first, out int lastShown);
        Check(UiDesignerPanels.Scrolled(pieces, cols, step * 1000f) == last && lastShown == pieces - 1 && first <= lastShown && UiDesignerPanels.Scrolled(pieces, cols, -5f) == 0f,
            "designer items: the whole fit-out list (" + pieces + " pieces) pages in whole rows to its last tile");
    }
}
