using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Corners;
using RealLifeOverhaul.Uptown;

// The corner buildings of build mode (Scripts/Corners: CornerRules, CornerLook, CornerSigns; Construction's CellRules.Corner and
// ConstructionDirector.Corners; plans/build-mode.md section 12): the corner site rule on the real plan, the four ways a model is
// turned to a block's two streets, its fit and the ground the game's building beneath it takes, its door and boxes, the sign
// anchors against the models' export, what each sign says and its colour, the models in the second bundle, the records' new
// fields, the sources (nothing a frame, the bridges, the one choke point for the building's own look) and the texts.
internal static partial class Program
{
    private static void CornerChecks()
    {
        CornerSiteChecks();
        CornerPoseChecks();
        CornerFitChecks();
        CornerSignChecks();
        CornerBundleChecks();
        CornerStoreChecks();
        CornerSourceChecks();
        CornerTextChecks();
    }

    // The plan's lots as the construction game reads them, with the block's corners on each avenue strip (as Wiring.ConstructionLinks).
    private static List<LotView> CornerPlan()
    {
        var lots = new List<LotView>(LayoutRules.Lots.Count);
        foreach (Lot l in LayoutRules.Lots)
        {
            bool corners = LayoutRules.StreetEnds(l.Row, l.Column, (int)l.Facing, out float low, out float high);
            lots.Add(new LotView
            {
                Id = l.Id, Street = l.Street, Number = l.Number, X = l.X, Z = l.Z, Yaw = l.Yaw, Width = l.Width, Depth = l.Depth, Class = (int)l.Class, Block = l.Block, Strip = l.Strip,
                Index = l.Index, Spaces = l.Spaces, Size = l.Size, Open = true, StreetLow = corners, StreetHigh = corners, EdgeLow = low, EdgeHigh = high
            });
        }
        return lots;
    }

    // ---------------------------------------------------------------- the corner site rule on the real plan

    private static void CornerSiteChecks()
    {
        List<LotView> lots = CornerPlan();
        CellGrid grid = CellGrid.Of(lots);
        int corners = 0, street = 0, middle = 0, wrongStrip = 0, edgeOff = 0, agree = 0, pairs = 0;
        var blocks = new HashSet<int>();
        for (int c = 0; c < grid.Columns.Count; c++)
        {
            CellColumn col = grid.Columns[c];
            bool one = CellRules.Corner(grid, c, 1, out bool low, out float edge);
            bool rules = CornerRules.End(col.A, col.B, col.StreetLow, col.EdgeLow, col.StreetHigh, col.EdgeHigh, out bool low2, out float edge2);
            agree += one == rules && (!one || (low == low2 && Math.Abs(edge - edge2) < 0.001f)) ? 1 : 0;
            pairs++;
            if (!one)
            {
                continue;
            }
            corners++;
            blocks.Add(col.Block);
            wrongStrip += col.Facing == 0 || col.Facing == 180 ? 0 : 1;
            LayoutRules.Block(col.Block / 100, col.Block % 100, out float x0, out _, out float x1, out _);
            edgeOff += Math.Abs(edge - (low ? x0 : x1)) < 0.001f && Math.Abs((low ? col.A : col.B) - edge) <= CellRules.CornerSlack ? 0 : 1;
            street += col.Facing == 90 || col.Facing == 270 ? 1 : 0;
        }
        for (int c = 0; c < grid.Columns.Count; c++)
        {
            CellColumn col = grid.Columns[c];
            if ((col.Facing == 0 || col.Facing == 180) && grid.ColumnOf(col.Strip, col.Space - 1) >= 0 && grid.ColumnOf(col.Strip, col.Space + 1) >= 0)
            {
                middle += CellRules.Corner(grid, c, 1, out _, out _) ? 1 : 0;   // a column with a neighbour each side is never at a corner
            }
        }
        Check(corners >= 60, "corner sites: the plan has " + corners + " corner columns over " + blocks.Count + " blocks");
        Check(wrongStrip == 0 && street == 0, "corner sites: every corner column is an avenue strip's (" + wrongStrip + " not), none a street strip's (" + street + ")");
        Check(edgeOff == 0, "corner sites: every corner is at the block's west or east edge, the cross street's building line, its column within " + CellRules.CornerSlack + " m of it (" + edgeOff + " off)");
        Check(middle == 0, "corner sites: no column between two others of its strip is a corner (" + middle + ")");
        Check(agree == pairs, "corner sites: the construction game's rule and the corner buildings' agree on every column (" + agree + " of " + pairs + ")");
        // Every band block with lots has its avenue strips' two ends as corners: four in rows 2 and 3, two in row 1 (no south strip there).
        int blocksWithCorners = 0, fullBlocks = 0;
        for (int row = 1; row <= LayoutRules.Rows; row++)
        {
            for (int column = 1; column <= LayoutRules.Columns; column++)
            {
                if (LayoutRules.IsSquare(row, column))
                {
                    continue;
                }
                int block = row * 100 + column;
                int count = grid.Columns.Count(col => col.Block == block && CellRules.Corner(grid, grid.Columns.IndexOf(col), 1, out _, out _));
                blocksWithCorners += count > 0 ? 1 : 0;
                fullBlocks += count >= (row == 1 ? 2 : 4) ? 1 : 0;
            }
        }
        Check(blocksWithCorners > 30 && fullBlocks >= blocksWithCorners - 6, "corner sites: " + blocksWithCorners + " blocks have corners, " + fullBlocks + " all of theirs (a big place may hold one)");
        // A site of the corner column and its neighbours is a corner site; one starting a column in, or on a street strip, is not.
        int site = -1;
        for (int c = 0; c < grid.Columns.Count && site < 0; c++)
        {
            site = CellRules.Corner(grid, c, 1, out bool low, out _) && low && grid.Next(c) >= 0 && grid.Next(grid.Next(c)) >= 0 ? c : -1;
        }
        Check(site >= 0 && CellRules.Corner(grid, site, 3, out bool lowRun, out _) && lowRun && !CellRules.Corner(grid, grid.Next(site), 2, out _, out _),
            "corner sites: three columns from a low corner make a corner site; the same run a column in does not");
        int streetColumn = grid.Columns.FindIndex(col => col.Facing == 90 || col.Facing == 270);
        Check(streetColumn >= 0 && !CellRules.Corner(grid, streetColumn, 1, out _, out _), "corner sites: a street strip's column is never a corner");
        // A plan lot at a corner reads the same as its columns (the developers' and the city's lots).
        int lotCorners = 0, lotAgree = 0;
        foreach (LotView l in lots)
        {
            if (!CellRules.Corner(l, out bool low, out float edge))
            {
                continue;
            }
            lotCorners++;
            int first = grid.ColumnOf(l.Strip, l.Index);
            lotAgree += first >= 0 && CellRules.Corner(grid, first, Math.Max(1, l.Spaces), out bool lowC, out float edgeC) && lowC == low && Math.Abs(edgeC - edge) < 0.001f ? 1 : 0;
        }
        Check(lotCorners > 0 && lotAgree == lotCorners, "corner sites: each of the " + lotCorners + " corner lots of the plan is a corner by its columns too (" + lotAgree + ")");
        Check(!LayoutRules.StreetEnds(2, 5, 90, out _, out _) && !LayoutRules.StreetEnds(4, 5, 180, out _, out _) && LayoutRules.StreetEnds(2, 5, 0, out _, out _) == !LayoutRules.IsSquare(2, 5),
            "corner sites: only an avenue strip of a band block has corners (never a street strip or a ring block's)");
        Check(!CellRules.CornerEnd(10f, 30f, true, 8.5f, true, 50f, out _, out _) && CellRules.CornerEnd(9f, 30f, true, 8.5f, true, 50f, out bool lowEnd, out _) && lowEnd
              && CellRules.CornerEnd(20f, 49.5f, true, 0f, true, 50f, out bool lowHigh, out float edgeHigh) && !lowHigh && edgeHigh == 50f
              && !CellRules.CornerEnd(0.2f, 49.8f, false, 0f, false, 50f, out _, out _),
            "corner sites: a run's end within a metre of the block's edge is at the corner, the low end first; no street, no corner");
    }

    // ---------------------------------------------------------------- the four turns

    private static void CornerPoseChecks()
    {
        Check(CornerRules.YawFor(1, 1) == 0 && CornerRules.YawFor(1, -1) == 90 && CornerRules.YawFor(-1, -1) == 180 && CornerRules.YawFor(-1, 1) == 270,
            "corner turns: the model's +x +z quarter into each of the four quarters of a block from its corner");
        bool turns = true;
        foreach (int yaw in new[] { 0, 90, 180, 270 })
        {
            CornerRules.Turn(yaw, 1f, 0f, out float ax, out float az);
            CornerRules.Turn(yaw, 0f, 1f, out float bx, out float bz);
            double a = yaw * Math.PI / 180.0;
            // Unity's Quaternion.Euler(0, yaw, 0): right to (cos, -sin), forward to (sin, cos).
            turns &= Math.Abs(ax - Math.Cos(a)) < 1e-4 && Math.Abs(az + Math.Sin(a)) < 1e-4 && Math.Abs(bx - Math.Sin(a)) < 1e-4 && Math.Abs(bz - Math.Cos(a)) < 1e-4;
        }
        Check(turns, "corner turns: a way in the model's frame turns into the world as Unity turns it");
        Check(Math.Abs(CornerRules.Degrees(0f, 1f)) < 0.01f && Math.Abs(CornerRules.Degrees(1f, 0f) - 90f) < 0.01f && Math.Abs(CornerRules.Degrees(0f, -1f) - 180f) < 0.01f
              && Math.Abs(CornerRules.Degrees(-1f, 0f) - 270f) < 0.01f, "corner turns: a way as the game's degrees (0 north, 90 east)");
        // A real block's four corners: the north strip's two ends and the south strip's, each a turn of its own, the model inside the block,
        // its two fronts on the two streets' building lines, its door outside its body facing out.
        LayoutRules.Block(2, 5, out float x0, out float z0, out float x1, out float z1);
        CornerModel m = CornerRules.Models[0];
        var yaws = new HashSet<int>();
        bool inside = true, fronts = true, doors = true;
        foreach (int facing in new[] { 0, 180 })
        {
            foreach (bool low in new[] { true, false })
            {
                float depth = 39f, width = 21f;
                float frontZ = facing == 0 ? z1 : z0;
                var g = new CornerGround
                {
                    Facing = facing, Width = width, Depth = depth, Low = low, Edge = low ? x0 : x1, X = low ? x0 + width * 0.5f : x1 - width * 0.5f,
                    Z = facing == 0 ? frontZ - depth * 0.5f : frontZ + depth * 0.5f
                };
                CornerPose p = CornerRules.Pose(g);
                yaws.Add(p.Yaw);
                inside &= Math.Abs(p.X - (low ? x0 : x1)) < 0.001f && Math.Abs(p.Z - frontZ) < 0.001f && Math.Abs(p.Along - width) < 0.001f && Math.Abs(p.Deep - depth) < 0.001f;
                foreach (float[] corner in new[] { new[] { 0f, 0f }, new[] { m.Wide, 0f }, new[] { 0f, m.Deep }, new[] { m.Wide, m.Deep } })
                {
                    CornerRules.ToWorld(p.X, p.Z, p.Yaw, corner[0], corner[1], out float wx, out float wz);
                    inside &= wx >= x0 - 0.001f && wx <= x1 + 0.001f && wz >= z0 - 0.001f && wz <= z1 + 0.001f;
                }
                // Front A (its -z face) and front B (its -x face) look out of the block, onto the avenue and the cross street.
                CornerRules.Turn(p.Yaw, 0f, -1f, out float ax, out float az);
                CornerRules.Turn(p.Yaw, -1f, 0f, out float bx, out float bz);
                float avenueOut = facing == 0 ? 1f : -1f, streetOut = low ? -1f : 1f;
                fronts &= (Math.Abs(az - avenueOut) < 0.001f && Math.Abs(bx - streetOut) < 0.001f) || (Math.Abs(bz - avenueOut) < 0.001f && Math.Abs(ax - streetOut) < 0.001f);
                CornerRules.Door(m, p, out float dx, out float dz, out float doorFacing);
                double f = doorFacing * Math.PI / 180.0;
                CornerRules.ToWorld(p.X, p.Z, p.Yaw, m.Wide * 0.5f, m.Deep * 0.5f, out float cx, out float cz);
                doors &= (dx - cx) * Math.Sin(f) + (dz - cz) * Math.Cos(f) > 0;   // the door faces away from the building's middle
            }
        }
        Check(yaws.Count == 4, "corner turns: a block's four corners turn a model four ways (" + string.Join(", ", yaws) + ")");
        Check(inside, "corner turns: at each corner the root is the street corner and the model stands inside the block");
        Check(fronts, "corner turns: at each corner the model's two fronts face the avenue and the cross street");
        Check(doors, "corner turns: at each corner the door faces out of the building");
        // The extents follow the turn: front A along the avenue at two corners, along the cross street at the other two.
        int wideAlong = 0;
        foreach (int yaw in new[] { 0, 90, 180, 270 })
        {
            CornerRules.Extents(m, new CornerPose { Yaw = yaw, AlongX = true }, out float along, out float deep);
            wideAlong += Math.Abs(along - m.Wide) < 0.001f && Math.Abs(deep - m.Deep) < 0.001f ? 1 : 0;
        }
        Check(wideAlong == 2, "corner turns: front A runs along the avenue at two of a block's corners, front B at the other two");
    }

    // ---------------------------------------------------------------- the fit, the ground beneath, the door and the boxes

    private static void CornerFitChecks()
    {
        Check(CornerRules.Models.Length == 7 && CornerRules.Models.Select(m => m.Id).Distinct().Count() == 7, "corner fit: seven corner buildings, each its own id");
        var g = new CornerGround { Facing = 0, Width = 21f, Depth = 39f, Low = true, Edge = 0f, X = 10.5f, Z = -19.5f };
        CornerPose p = CornerRules.Pose(g);
        var boxes = new List<CornerBox>(2);
        bool all = true, copyOk = true, doorFree = true, uses = true, heights = true;
        foreach (CornerModel m in CornerRules.Models)
        {
            all &= CornerRules.Fits(m, p);
            CornerRules.CopyGround(m, g, p, out float x, out float z, out float width, out float depth);
            // The ground beneath: within the site, clear of the corner's square, as deep as the site.
            copyOk &= Math.Abs(width + m.Clear - p.Along) < 0.001f && Math.Abs(depth - g.Depth) < 0.001f && x - width * 0.5f >= g.Edge + m.Clear - 0.001f
                      && x + width * 0.5f <= g.X + g.Width * 0.5f + 0.001f && Math.Abs(z - g.Z) < 0.001f;
            CornerRules.Boxes(m, boxes);
            foreach (CornerBox b in boxes)
            {
                doorFree &= !CornerRules.InCarve(b, m.DoorX, m.DoorZ);
            }
            uses &= m.Uses != 0 && (m.Uses & ~15) == 0;
            heights &= m.Height > 5f && m.Top >= m.Height && m.Wide > 10f && m.Deep > 10f && m.Wide <= 25f && m.Deep <= 25f;
        }
        Check(all, "corner fit: every corner building fits a corner site three spaces wide and five rows deep");
        Check(copyOk, "corner fit: the game's building beneath stands within the site, off the corner's square, as deep as the site");
        Check(doorFree, "corner fit: each entrance is outside every carving box of its own model");
        Check(uses, "corner fit: each corner building is offered for at least one use, and only the four");
        Check(heights, "corner fit: each model's size and height are a building's");
        // Too short or too shallow a site holds none of the bigger ones; the glass tower needs three spaces and three rows.
        CornerModel glass;
        CornerRules.Find("corner_glass", out glass);
        var narrow = new CornerGround { Facing = 0, Width = 14f, Depth = 39f, Low = true, Edge = 0f, X = 7f, Z = -19.5f };
        var shallow = new CornerGround { Facing = 0, Width = 28f, Depth = 15.6f, Low = true, Edge = 0f, X = 14f, Z = -7.8f };
        Check(!CornerRules.Fits(glass, CornerRules.Pose(narrow)) && !CornerRules.Fits(glass, CornerRules.Pose(shallow)), "corner fit: the glass tower is not offered two spaces wide nor two rows deep");
        // The uses in Construction's BuildUse order.
        Check(CornerRules.UseHomes == (int)BuildUse.Homes && CornerRules.UseShops == (int)BuildUse.Shops && CornerRules.UseOffices == (int)BuildUse.Offices
              && CornerRules.UseWarehouses == (int)BuildUse.Warehouses, "corner fit: the uses are Construction's BuildUse order");
        Check(Uses("corner_bodega") == "1" && Uses("corner_bank") == "1,2" && Uses("corner_deco") == "2" && Uses("corner_loft") == "1,3" && Uses("corner_prewar") == "0"
              && Uses("corner_diner") == "1" && Uses("corner_glass") == "2", "corner fit: the owner's uses (bodega shops, bank offices or shops, deco offices, loft warehouses or shops, prewar homes, diner shops, glass offices)");
        // A box shrunk for carving, never under half a metre.
        CornerRules.Carve(new CornerBox { X1 = 0f, Z1 = 0f, X2 = 0.6f, Z2 = 10f, Height = 5f }, out float sx, out float sz);
        Check(Math.Abs(sx - 0.5f) < 0.001f && Math.Abs(sz - 9f) < 0.001f, "corner fit: a carving box is its box less half a metre a side, never under half a metre");
        Check(CornerRules.Models.Where(m => m.Clear > 0f).All(m => { CornerRules.Boxes(m, boxes); return boxes.Count == 2; })
              && CornerRules.Models.Where(m => m.Clear <= 0f).All(m => { CornerRules.Boxes(m, boxes); return boxes.Count == 1; }),
            "corner fit: a cut corner's entrance keeps the corner's square free of the body (two boxes); a door on a front, one box");
    }

    private static string Uses(string id)
    {
        CornerRules.Find(id, out CornerModel m);
        return string.Join(",", Enumerable.Range(0, 4).Where(u => CornerRules.Offers(m, u)));
    }

    // ---------------------------------------------------------------- the signs

    private static void CornerSignChecks()
    {
        // The anchors against the models' export (blender axes: x, y along front B, z up; facing on the ground): the code's model frame is (x, z up, y).
        string path = PropFile(@"models\corner_signs.json");
        Check(File.Exists(path), "corner signs: the models' sign anchors are kept beside them (PropAssets/models/corner_signs.json)");
        if (File.Exists(path))
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            int matched = 0, total = 0;
            foreach (CornerModel m in CornerRules.Models)
            {
                JsonElement[] exported = doc.RootElement.TryGetProperty(m.Id, out JsonElement list) ? list.EnumerateArray().ToArray() : new JsonElement[0];
                total += Math.Max(exported.Length, m.Signs.Length);
                for (int i = 0; i < exported.Length && i < m.Signs.Length; i++)
                {
                    JsonElement e = exported[i];
                    float[] c = e.GetProperty("centre").EnumerateArray().Select(v => (float)v.GetDouble()).ToArray();
                    float[] f = e.GetProperty("facing").EnumerateArray().Select(v => (float)v.GetDouble()).ToArray();
                    CornerSign s = m.Signs[i];
                    bool same = string.Equals(e.GetProperty("kind").GetString(), s.Kind.ToString(), StringComparison.OrdinalIgnoreCase)
                                && Math.Abs(c[0] - s.X) < 0.002f && Math.Abs(c[2] - s.Y) < 0.002f && Math.Abs(c[1] - s.Z) < 0.002f
                                && Math.Abs(f[0] - s.OutX) < 0.001f && Math.Abs(f[1] - s.OutZ) < 0.001f
                                && Math.Abs(e.GetProperty("width").GetDouble() - s.Width) < 0.002 && Math.Abs(e.GetProperty("height").GetDouble() - s.Height) < 0.002;
                    matched += same ? 1 : 0;
                }
            }
            Check(matched == total && total == 11, "corner signs: every anchor of the export is the code's, its blender axes mapped to the model's frame (" + matched + " of " + total + ")");
        }
        Check(CornerRules.Models.All(m => m.Signs.All(s => Math.Abs(s.OutX * s.OutX + s.OutZ * s.OutZ - 1f) < 0.001f && s.Width > 0.5f && s.Height > 0.2f)),
            "corner signs: each anchor faces out one way and has a board's size");
        CornerRules.Find("corner_diner", out CornerModel diner);
        Check(diner.Signs != null && diner.Signs.Count(s => s.Kind == SignKind.Blade) == 2 && diner.Signs.Where(s => s.Kind == SignKind.Blade).Sum(s => s.OutX) == 0f, "corner signs: the blade sign has both its sides");
        // What a sign says.
        Check(CornerRules.Shows(SignKind.Fascia, true, true) == CornerRules.ShowsName && CornerRules.Shows(SignKind.Neon, false, true) == CornerRules.ShowsLease
              && CornerRules.Shows(SignKind.Blade, false, false) == CornerRules.ShowsNothing && CornerRules.Shows(SignKind.Address, false, false) == CornerRules.ShowsNumber
              && CornerRules.Shows(SignKind.Address, true, true) == CornerRules.ShowsNumber,
            "corner signs: a business's name while one trades, For lease while to let, blank when kept empty, the number on the address plate always");
        Check(CornerRules.Words(SignKind.Frieze, CornerRules.ShowsName, "First Bank", "For lease", 12) == "FIRST BANK" && CornerRules.Words(SignKind.Fascia, CornerRules.ShowsName, " Rosa's ", "x", 1) == "Rosa's"
              && CornerRules.Words(SignKind.Fascia, CornerRules.ShowsLease, "Rosa's", "For lease", 1) == "For lease" && CornerRules.Words(SignKind.Address, CornerRules.ShowsNumber, null, null, 77) == "77"
              && CornerRules.Words(SignKind.Address, CornerRules.ShowsNumber, null, null, 0) == null && CornerRules.Words(SignKind.Fascia, CornerRules.ShowsNothing, "Rosa's", "x", 1) == null
              && CornerRules.Words(SignKind.Neon, CornerRules.ShowsName, "", "x", 1) == null,
            "corner signs: the words: a frieze in capitals, a name trimmed, the lease words, the number, a blank board");
        // Its colour reads on its board.
        CornerRules.Colour(SignKind.Fascia, 0f, 0f, 0f, 1f, 1f, 1f, out float r1, out float g1, out float b1);
        CornerRules.Colour(SignKind.Fascia, 0.9f, 0.2f, 0.2f, 1f, 1f, 1f, out float r2, out _, out _);
        CornerRules.Colour(SignKind.Frieze, 1f, 1f, 1f, 1f, 1f, 1f, out float r3, out _, out _);
        CornerRules.Colour(SignKind.Fascia, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f, out float r4, out _, out _);
        CornerRules.Colour(SignKind.Address, 0f, 0f, 0f, 0f, 0f, 0f, out float r5, out _, out _);
        Check(r1 == 1f && g1 == 1f && b1 == 1f && Math.Abs(r2 - 0.9f) < 0.001f && r3 < 0.5f && r4 > 0.9f && Math.Abs(r5 - 0.86f) < 0.001f,
            "corner signs: the business's colour where it reads on the board, else the game's sign colour, else cream or bronze; brass numbers");
        // The game's own door sign covers an anchor on its face, never one on another face or far off.
        CornerSign chamfer = diner.Signs[0];
        Check(CornerRules.Covered(chamfer, chamfer.X, chamfer.Y, chamfer.Z, chamfer.OutX, chamfer.OutZ, 2f, 1f)
              && !CornerRules.Covered(chamfer, chamfer.X, chamfer.Y, chamfer.Z, 0f, -1f, 2f, 1f)
              && !CornerRules.Covered(chamfer, chamfer.X, chamfer.Y - 5f, chamfer.Z, chamfer.OutX, chamfer.OutZ, 2f, 1f)
              && !CornerRules.Covered(new CornerSign { Kind = SignKind.Address, OutZ = -1f, Width = 1f, Height = 1f }, 0f, 0f, 0f, 0f, -1f, 2f, 2f),
            "corner signs: the business's own door sign leaves blank only an anchor it stands over on the same face, never the address");
        CornerRules.Find("corner_bodega", out CornerModel bodega);
        Check(CornerRules.SignScale(bodega, 1f) == 1f && Math.Abs(CornerRules.SignScale(bodega, bodega.DoorWidth * 2f) - 0.5f) < 0.001f, "corner signs: the door sign is scaled down only where it is wider than the entrance");
    }

    // ---------------------------------------------------------------- the models in the second bundle

    private static void CornerBundleChecks()
    {
        string contentsPath = PropFile(@"contents\reallife_props_2.json");
        string setsPath = PropFile(@"sets.json");
        Check(File.Exists(contentsPath) && File.Exists(setsPath), "corner bundle: the second bundle's list and the sets are there");
        if (!File.Exists(contentsPath) || !File.Exists(setsPath))
        {
            return;
        }
        using JsonDocument contents = JsonDocument.Parse(File.ReadAllText(contentsPath));
        using JsonDocument sets = JsonDocument.Parse(File.ReadAllText(setsPath));
        JsonElement second = sets.RootElement.GetProperty("sets").EnumerateArray().First(s => s.GetProperty("name").GetString() == "second");
        Dictionary<string, JsonElement> props = contents.RootElement.GetProperty("props").EnumerateArray().ToDictionary(p => p.GetProperty("id").GetString(), p => p.Clone());
        int total = 0;
        foreach (CornerModel m in CornerRules.Models)
        {
            Check(props.TryGetValue(m.Id, out JsonElement prop), "corner bundle: reallife_props_2 holds " + m.Id);
            if (!props.ContainsKey(m.Id))
            {
                continue;
            }
            float[] size = prop.GetProperty("size").EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
            int triangles = prop.GetProperty("triangles").GetInt32();
            total += triangles;
            JsonElement[] parts = prop.GetProperty("parts").EnumerateArray().ToArray();
            using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(PropFile(@"models\" + m.Id + @"\" + m.Id + ".json")));
            int budget = settings.RootElement.GetProperty("budget").GetInt32();
            Check(size[0] >= m.Wide && size[0] <= m.Wide + 3.5f && size[2] >= m.Deep && size[2] <= m.Deep + 4f && Math.Abs(size[1] - m.Top) < 0.06f,
                "corner bundle: " + m.Id + " is " + string.Join(" x ", size) + " m, the facts " + m.Wide + " by " + m.Deep + ", " + m.Top + " m high");
            Check(triangles > 1000 && triangles <= budget, "corner bundle: " + m.Id + " has " + triangles + " triangles (its budget " + budget + ")");
            Check(parts.Length >= 1 && parts.Length <= CornerRules.MostDraws && parts[0].GetProperty("game").ValueKind == JsonValueKind.Null
                  && parts.Skip(1).All(p => CornerRules.Kinds.Contains(p.GetProperty("game").GetString())) && parts.Sum(p => p.GetProperty("triangles").GetInt32()) == triangles,
                "corner bundle: " + m.Id + " is " + parts.Length + " draw call(s), one a material: the atlas, then the game's brick and glass, adding up to its triangles");
            Check(settings.RootElement.GetProperty("game").EnumerateObject().All(slot => CornerRules.Kinds.Contains(slot.Value.GetProperty("kind").GetString())),
                "corner bundle: " + m.Id + "'s game slots are the kinds the code finds (brick, glass)");
        }
        Check(total > 50000 && contents.RootElement.GetProperty("triangles").GetInt32() <= second.GetProperty("triangles").GetInt32()
              && contents.RootElement.GetProperty("triangle_limit").GetInt32() == second.GetProperty("triangles").GetInt32(),
            "corner bundle: the seven's " + total + " triangles within the second bundle's " + second.GetProperty("triangles").GetInt32());
        Check(contents.RootElement.GetProperty("bytes").GetInt64() <= second.GetProperty("disk_kb").GetInt64() * 1024, "corner bundle: the second bundle is within its size on disk");
        Check(CornerRules.GameNames("brick")[0] == "M_Bricks_01" && CornerRules.GameNames("glass")[0] == "M_Glass" && CornerRules.GameNames("glass")[1] == "M_GlassBlueHue",
            "corner bundle: brick is the game's M_Bricks_01, glass its M_Glass first and M_GlassBlueHue after");
    }

    // ---------------------------------------------------------------- the records' new fields

    private static void CornerStoreChecks()
    {
        var state = new LifeState();
        state.Finished.Add(new FinishedBuilding { Lot = "uptown_2_05_0001", Size = "ba:buildingsize_c", Version = 1, Type = "ba:buildingtype_retail", Design = "Corner", Look = "corner_diner" });
        state.Finished.Add(new FinishedBuilding { Lot = "uptown_2_05_0002", Size = "ba:buildingsize_c", Version = 1, Type = "ba:buildingtype_retail", Design = "Plain" });
        state.Finished.Add(new FinishedBuilding
        {
            Lot = "uptown_2_05_0003", Size = "ba:buildingsize_c", Version = 1, Type = "ba:buildingtype_retail", Design = "Span", Over = new[] { "uptown_2_05_0004" }, Look = "corner_bank"
        });
        string line = LifeStore.Write(state);
        LifeState back = LifeStore.Load(new Dictionary<string, string> { { LifeStore.Key, line } }, out _);
        Check(back != null && back.Finished.Count == 3 && back.Finished[0].Look == "corner_diner" && back.Finished[0].Over == null && back.Finished[1].Look == null
              && back.Finished[2].Look == "corner_bank" && back.Finished[2].Over != null && back.Finished[2].Over[0] == "uptown_2_05_0004",
            "corner store: a finished building's look reads back after the lots it stands over; one without has none");
        string older = line.Replace("|corner_diner", string.Empty).Replace("|uptown_2_05_0004|corner_bank", "|uptown_2_05_0004");
        LifeState old = LifeStore.Load(new Dictionary<string, string> { { LifeStore.Key, older } }, out _);
        Check(old != null && old.Finished.Count == 3 && old.Finished.All(f => f.Look == null), "corner store: an older district line reads with no look");
        // An older reader sees the same record up to the lots it stands over: its fields are where they were.
        string[] fields = line.Substring(line.IndexOf("^f:", StringComparison.Ordinal) + 3).Split('^')[0].Split(';')[0].Split('|');
        Check(fields.Length == 12 && fields[10] == string.Empty && fields[11] == "corner_diner", "corner store: the look is the record's twelfth field, after an empty field of lots it stands over");
    }

    // ---------------------------------------------------------------- the sources

    private static void CornerSourceChecks()
    {
        string look = ModSource(@"Corners\CornerLook.cs");
        string signs = ModSource(@"Corners\CornerSigns.cs");
        string rules = ModSource(@"Corners\CornerRules.cs");
        string copy = ModSource(@"NewBuildings\CopyGame.cs");
        string corners = ModSource(@"Construction\ConstructionDirector.Corners.cs");
        string mode = ModSource(@"Construction\ConstructionDirector.BuildMode.cs");
        string market = ModSource(@"Construction\ConstructionDirector.Market.cs");
        string redev = ModSource(@"Construction\ConstructionDirector.CityRedevelop.cs");
        string stand = ModSource(@"Uptown\LifeDirector.Stand.cs");
        string load = ModSource(@"Uptown\LifeDirector.cs");
        string links = ModSource(@"Wiring\Wiring.CornerLinks.cs");
        string start = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Corners"));
        Check(Directory.Exists(folder) && Directory.GetFiles(folder, "*.cs").Length >= 5, "corner sources: the corner buildings' folder is there");
        foreach (string file in Directory.Exists(folder) ? Directory.GetFiles(folder, "*.cs") : new string[0])
        {
            string text = File.ReadAllText(file);
            Check(!Regex.IsMatch(text, @"void (Update|LateUpdate|FixedUpdate)\(") && !text.Contains("MonoBehaviour"), "corner sources: " + System.IO.Path.GetFileName(file) + " has nothing a frame");
        }
        Check(!Regex.IsMatch(rules, @"using UnityEngine|using RealLifeOverhaul\.(Uptown|Construction|NewBuildings)"), "corner sources: CornerRules is game-free");
        Check(MethodText(look, "private static string Build(").IndexOf("CopyGame.WearOver(c", StringComparison.Ordinal) > 0
              && MethodText(look, "private static string Build(").IndexOf("CopyGame.WearOver(c", StringComparison.Ordinal) < MethodText(look, "private static string Build(").IndexOf("new GameObject(RootName", StringComparison.Ordinal),
            "corner sources: the building's own look is worn over before the model is made under it (its renderers are never the model's)");
        Check(MethodText(copy, "public static bool HideLook(CityBuildingController c)").Contains("Worn.TryGetValue(c, out Wearer w)")
              && MethodText(copy, "public static bool ShowLook(CityBuildingController c)").Contains("Worn.TryGetValue(c, out Wearer w)")
              && MethodText(copy, "public static int ShowAllLooks()").Contains("Worn.Clear();"),
            "corner sources: hiding or showing a building wearing a look hides or shows the look, never its own (the one choke point)");
        Check(MethodText(look, "public static void Loading()").Contains("FindMaterials();") && look.Contains("SceneFinds.InMemory<Material>(") && MethodText(look, "public static string Stand(MadeBuilding made, string look)").Contains("SceneFinds.Black"),
            "corner sources: the game's brick and glass found by name behind the loading screen, or the black screen, never in play");
        Check(MethodText(look, "public static string Stand(MadeBuilding made, string look)").Contains("CornerRules.StoodLine(") && !Regex.IsMatch(look, @"Logger\?\.Info\([^;]*Update"),
            "corner sources: each corner building's triangles and draw calls in one line as it stands");
        Check(MethodText(look, "private static void MoveDoor(CornerHandle h)").Contains("made.DoorX = at.x;") && MethodText(look, "private static void MoveDoor(CornerHandle h)").Contains("c.poiOffsetOnStreet")
              && MethodText(look, "private static void CarrySigns(CornerHandle h, Transform door, Vector3 face, Quaternion facing)").Contains("FarDistricts.LeaveAlone(SignParts[k], false)"),
            "corner sources: the doors moved to the entrance with the record's door and the street marker, the business's own door sign carried and drawn");
        Check(signs.Contains("EventHold.Keep(ref GlobalEvents.onBuildingRegistrationChange, _onChange, ref _changeSeen);") && signs.Contains("_onChange ??= Guard.Wrap<Address>(OnChange);")
              && signs.Contains("GameFont.Asset"), "corner sources: the signs in the game's font, written again on the game's registration change event, one handler made once");
        Check(MethodText(look, "public static void Register(IModLogger logger)").Contains("DistrictBuildings.LookStand = Stand;") && start.Contains("Corners.CornerLook.Register(context.Logger)")
              && start.IndexOf("Corners.CornerLook.Register", StringComparison.Ordinal) < start.IndexOf("Guard.Add<Uptown.LifeDirector>", StringComparison.Ordinal),
            "corner sources: the district asks for its looks from game start, before its buildings are declared");
        Check(stand.Contains("LookGround(spec, f, ground);") && stand.IndexOf("WearLook(made);", StringComparison.Ordinal) < stand.IndexOf("LifeGame.Enter(made);", StringComparison.Ordinal)
              && load.IndexOf("WearLook(made);", StringComparison.Ordinal) < load.IndexOf("LifeGame.Enter(made);", load.IndexOf("WearLook(made);", StringComparison.Ordinal), StringComparison.Ordinal)
              && load.Contains("LooksLoading();") && ModSource(@"Uptown\LifeDirector.Cleared.cs").Contains("LookGone(made);"),
            "corner sources: the look stood at every load and every stand before the building joins the game's lists, and taken away when it is knocked down");
        Check(!corners.Contains("using RealLifeOverhaul.Corners") && corners.Contains("ConstructionBridges.CornerOffers(ground, into);") && ModSource(@"Wiring\Wiring.ConstructionLinks.cs").Contains("ConstructionBridges.CornerOffers = CornerLinks.Offers;") && links.Contains("internal static void Offers(CellCorner corner"),
            "corner sources: the construction game reaches the corner buildings through its bridges, wired in Wiring");
        Check(mode.Contains("CellRules.Corner(Cells, firstColumn, count, out bool low, out float edge)") && mode.Contains("ready = CornerDesigns(site, low, edge, use, into);")
              && MethodText(mode, "public int PlaceProject(").Contains("Wear(p, design);"),
            "corner sources: a corner site's designs list the corner buildings first, and placing one wears it");
        Check(MethodText(corners, "private bool CityCorners(").Contains("GoingPublic.InSession") && market.Contains("CityCorners(lot, use, _choices)") && redev.Contains("CityCorners(site, use, _choices)")
              && market.Contains("design.Look, out string why") && redev.Contains("design.Look, out why"),
            "corner sources: the city's developers and its redevelopments choose them by the same rules, never in a Going Public session");
        Check(ModSource(@"Construction\ConstructionDirector.Projects.cs").Contains("p.Look = d.Look ?? string.Empty;") && ModSource(@"Construction\ConstructionDirector.Projects.cs").Contains("p.Look, out why"),
            "corner sources: a project's design carries its corner look to the handover; any other design takes it off");
    }

    // ---------------------------------------------------------------- the texts

    private static void CornerTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string name in new[] { @"Corners\CornersText.cs", @"Construction\ConstructionDirector.CornerTests.cs", @"Construction\ConstructionGuide.cs" })
        {
            foreach (Match m in Regex.Matches(ModSource(name), "\"(reallife_corner_[a-z0-9_]+[a-z0-9])\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        foreach (CornerModel m in CornerRules.Models)
        {
            keys.Add(m.NameKey);
        }
        Check(keys.Count == 13, "corner texts: their keys are found: " + keys.Count);
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("corner texts", words, codes))
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
            }
            Check(missing == null, "corner texts: every key is in " + codes[l] + ".json, in the style (" + (missing ?? "all") + ")");
        }
        foreach (string key in keys)
        {
            Check((words[0].TryGetValue(key, out string t) ? t : string.Empty).Length <= 240, "corner texts: " + key + " at most 240 characters");
        }
        var starts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Corner buildings in build mode" }, { "pt", "Prédios de esquina no modo de construção" }, { "zh-cn", "建造模式中的街角建筑" }, { "de", "Eckgebäude im Baumodus" },
            { "fr", "Bâtiments d'angle en mode construction" }, { "es", "Edificios de esquina en el modo construcción" }, { "ru", "Угловые здания в режиме строительства" },
            { "nl", "Hoekgebouwen in de bouwmodus" }, { "tr", "İnşa modunda köşe binaları" }
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
            Check(WhatsNewHas(words[l], start), "corner texts: What's new has the corner buildings in some row in " + codes[l]);
            if (words[l].TryGetValue("reallife_changes_next", out string next) && next.Contains(start))
            {
                int at = next.IndexOf(start, StringComparison.Ordinal), park = next.IndexOf(parks[codes[l]], StringComparison.Ordinal);
                bool fixBetween = marks.Any(mark => { int f = next.IndexOf(mark, at, StringComparison.Ordinal); return f >= 0 && f < park; });
                Check(next.IndexOf(start, at + 1, StringComparison.Ordinal) < 0 && park > at && !fixBetween && !next.Contains("  "),
                    "corner texts: its line once in the next row, among the feature lines before the parks' line, no fix between, no double space, in " + codes[l]);
            }
        }
        var bullets = new Dictionary<string, string>(StringComparer.Ordinal) { { "en", "\n- Corner buildings: " }, { "pt", "\n- Prédios de esquina: " }, { "zh-cn", "\n- 街角建筑：" } };
        var heads = new Dictionary<string, string>(StringComparer.Ordinal) { { "en", "\n# Build mode\n" }, { "pt", "\n# Modo de construção\n" }, { "zh-cn", "\n# 建造模式\n" } };
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            string page = words[l].TryGetValue("reallife_guide_construction", out string g) ? g : string.Empty;
            int head = page.IndexOf(heads[codes[l]], StringComparison.Ordinal), bullet = page.IndexOf(bullets[codes[l]], StringComparison.Ordinal);
            Check(head >= 0 && bullet > head, "corner texts: the Guide's construction page has the corner buildings in its build mode section in " + codes[l]);
        }
    }
}
