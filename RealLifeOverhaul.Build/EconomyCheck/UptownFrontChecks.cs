using System;
using System.Collections.Generic;
using RealLifeOverhaul.Landmarks;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.Uptown;

// Uptown built up as Manhattan's busiest (the owner, 25 Oct 2026: "There is so much free space between buildings in uptown
// ... Please fill the lots. The uptown looks very empty"; "keep 30" of the open lots; three parks kept), on the real plan and
// the game's own designs (Census/design-fronts-3682.json): the open lots' rule, each lot's design (FrontageRules.Choose), its
// street's buildings side by side with fillers in what their fronts leave (FrontageRules.Plan), none over another, every
// gap along a run a sliver, the frontage and the blocks' edges built against Manhattan's own, the blocks' interiors, and
// what all of it costs at a load.
internal static partial class Program
{
    private const float ManhattanEdges = 0.71f;   // of a block's edge with a building's face within 3 m: Manhattan's own (16 blocks south of 1st Avenue, the game's buildings' boxes, Build 3682)
    private const float SliverMost = FrontageRules.SliverMost;

    private static void UptownFrontChecks()
    {
        UptownFrontOpen();
        List<FrontDesign> fixture = UptownPlaceFixture();
        if (fixture == null)
        {
            Check(false, "uptown front: the game's designs (" + DesignFronts + ") could not be read");
            return;
        }
        var designs = new List<DesignEntry>(fixture.Count);
        foreach (FrontDesign f in fixture)
        {
            designs.Add(f.Now);
        }
        UptownFrontRules(designs);
        UptownFrontPlan(designs);
    }

    // ---------------------------------------------------------------- the open runs

    private static void UptownFrontOpen()
    {
        List<BuildingsLot> plan = UptownRealPlan();
        var built = UptownIds(plan, BuildingsRules.Built(plan, LandmarkRules.OwnBlock, 0f, 1f, new List<int>()));
        int open = 0, landmarks = 0, runSpaces = 0, wrong = 0;
        foreach (BuildingsLot l in plan)
        {
            runSpaces += l.Open ? 1 : 0;
            if (built.Contains(l.Id) || !BuildingsRules.Clear(l.Id))
            {
                wrong += l.Open && built.Contains(l.Id) ? 1 : 0;
                continue;
            }
            if (LandmarkRules.OwnBlock(l.Block))
            {
                landmarks++;
            }
            else
            {
                open++;
                wrong += l.Open ? 0 : 1;
            }
        }
        Check(open == runSpaces && wrong == 0 && open == LayoutRules.OpenRuns * LayoutRules.OpenRunSpaces && landmarks > 0,
            "uptown front: on the real plan the open runs' " + open + " spaces are open for the construction game (the frontage of the 30 open lots kept before) and " + landmarks
            + " lots kept for the landmarks' sites; " + built.Count + " built from the first day");
        bool kept = true;
        foreach (int b in LayoutRules.KeptForLandmarks)
        {
            kept &= LandmarkRules.OwnBlock(b);
        }
        for (int row = 1; row <= LayoutRules.Rows; row++)
        {
            for (int column = 1; column <= LayoutRules.Columns; column++)
            {
                kept &= !LandmarkRules.OwnBlock(row * 100 + column) || System.Linq.Enumerable.Contains(LayoutRules.KeptForLandmarks, row * 100 + column);
            }
        }
        Check(kept, "uptown front: the blocks the open runs keep off are the landmarks' own (LandmarkRules), every one of them");
    }

    // ---------------------------------------------------------------- a lot's look, on made-up lots

    private static void UptownFrontRules(List<DesignEntry> designs)
    {
        // Of the real designs that fit an 11 by 35 m lot of size A, the one picked is among the widest.
        int a = FrontageRules.Choose(designs, "ba:buildingsize_a", "ba:buildingtype_retail", 11f, 35f, "lot");
        float widest = 0f;
        foreach (DesignEntry d in designs)
        {
            widest = d.Size == "ba:buildingsize_a" && NewBuildingsRules.Copyable(d) && !d.Whole && NewBuildingsRules.Fits(d, 11f, 35f) ? Math.Max(widest, FrontageRules.Across(d)) : widest;
        }
        Check(a >= 0 && FrontageRules.Across(designs[a]) >= widest * FrontageRules.FrontShare && FrontageRules.Choose(designs, "ba:buildingsize_a", "x", 2f, 2f, "lot") == -1
              && FrontageRules.Choose(designs, "ba:buildingsize_a", "ba:buildingtype_retail", 11f, 35f, "lot") == a,
            "uptown front: where no look fills a lot, its building is among the widest of its size that fit (" + (a >= 0 ? FrontageRules.Across(designs[a]).ToString("F1") : "none") + " of "
            + widest.ToString("F1") + " m), the same for the same lot, none where none fits");
        // One space of 7 m: an A1's own look fills it, within the sliver; nothing fills 2 m.
        int one = FrontageRules.Look(designs, "ba:buildingsize_a", 1, "ba:buildingtype_retail", 7f, 35f, "space");
        Check(one >= 0 && designs[one].Size == "ba:buildingsize_a" && designs[one].Version == 1 && SpacesRules.Fills(FrontageRules.Across(designs[one]), 7f)
              && FrontageRules.Look(designs, "ba:buildingsize_a", 1, "ba:buildingtype_retail", 7f, 35f, "space") == one
              && FrontageRules.Look(designs, "ba:buildingsize_a", 1, "ba:buildingtype_retail", 2f, 35f, "space") == -1,
            "uptown front: a building on one space wears a look of its own size and version that fills it (" + (one >= 0 ? designs[one].Name + ", " + FrontageRules.Across(designs[one]).ToString("F1")
            + " m" : "none") + "), the same for the same lot, none where nothing fills");
        // A look of another size only with as many ways in: an M's two.
        bool doors = true;
        foreach (string id in new[] { "m1", "m2", "m3", "m4" })
        {
            int m = FrontageRules.Look(designs, "ba:buildingsize_m", 1, "ba:buildingtype_retail", 35f, 39f, id);
            doors &= m < 0 || SpacesRules.Doors(designs[m].Size) == 2;
        }
        int b1 = FrontageRules.Look(designs, "ba:buildingsize_b", 1, "ba:buildingtype_residential", 14f, 35f, "b");
        Check(doors && b1 >= 0 && SpacesRules.Doors(designs[b1].Size) == 1 && NewBuildingsRules.Pick(designs, "ba:buildingsize_b", "ba:buildingtype_residential", designs[b1].Name, 14.01f, 35f, "b") == b1,
            "uptown front: a look of another size only with as many ways in as the building's own (an M's two), and always one the engine takes by its name for that size");
    }

    // ---------------------------------------------------------------- the real plan

    private static void UptownFrontPlan(List<DesignEntry> designs)
    {
        List<BuildingsLot> plan = UptownRealPlan();
        List<int> built = BuildingsRules.Built(plan, LandmarkRules.OwnBlock, 0f, 1f, new List<int>());
        HashSet<string> leased = UptownIds(plan, BuildingsRules.Leased(plan, new List<int>()));
        var specs = new Dictionary<string, MadeSpec>(StringComparer.Ordinal);
        var into = new List<BuildingsStructure>();
        foreach (int i in built)
        {
            BuildingsLot lot = plan[i];
            List<BuildingsStructure> options = UptownOptions(lot);
            bool lease = leased.Contains(lot.Id);
            int pick = lease ? BuildingsRules.PickOffice(lot.Id, options) : BuildingsRules.PickStructure(lot.Id, options);
            if (pick < 0)
            {
                continue;
            }
            BuildingsRules.Others(options, pick, lease, into);
            var others = new MadeStructure[into.Count];
            for (int k = 0; k < into.Count; k++)
            {
                others[k] = new MadeStructure { Size = into[k].Size, Version = into[k].Version, Type = into[k].Type };
            }
            specs[lot.Id] = new MadeSpec
            {
                Id = lot.Id, Street = lot.Street, Number = lot.Number, Size = options[pick].Size, Version = options[pick].Version, Type = options[pick].Type, Life = true,
                Lots = new[] { new MadeLot(lot.X, lot.Z, lot.Facing, lot.Width, lot.Depth) }, Others = others, Design = BuildingsRules.LookOf(lot.Class)
            };
        }
        var fills = new List<FrontFill>();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int placed = FrontageRules.Plan(plan, id => specs.TryGetValue(id, out MadeSpec s) ? s : null, designs, fills, out int filled);
        double ms = watch.Elapsed.TotalMilliseconds;
        int back = fills.FindAll(f => f.Back).Count, front = fills.Count - back;

        // Every piece's footprint, by block: the buildings' on their new ground, the fillers' on theirs.
        var byBlock = new Dictionary<int, List<float[]>>();
        var lotOf = new Dictionary<string, BuildingsLot>(StringComparer.Ordinal);
        foreach (BuildingsLot l in plan)
        {
            lotOf[l.Id] = l;
        }
        void Add(int block, DesignEntry d, MadeLot ground, bool isFront)
        {
            NewBuildingsRules.Footprint(d, ground, out float cx, out float cz, out float hw, out float hd);
            bool alongX = Math.Abs(Math.Cos(ground.Facing * Math.PI / 180.0)) > 0.5;
            float[] box = alongX ? new[] { cx - hw, cz - hd, cx + hw, cz + hd, ground.Facing, isFront ? 1f : 0f } : new[] { cx - hd, cz - hw, cx + hd, cz + hw, ground.Facing, isFront ? 1f : 0f };
            if (!byBlock.TryGetValue(block, out List<float[]> list))
            {
                list = new List<float[]>();
                byBlock.Add(block, list);
            }
            list.Add(box);
        }
        int named = 0;
        foreach (MadeSpec s in specs.Values)
        {
            int d = NewBuildingsRules.Pick(designs, s.Size, s.Type, s.Design, s.Lots[0].Width, s.Lots[0].Depth, s.Id);
            named += d >= 0 ? 1 : 0;
            if (d >= 0)
            {
                Add(lotOf[s.Id].Block, designs[d], s.Lots[0], true);
            }
        }
        foreach (FrontFill f in fills)
        {
            Add(BlockAt(f.Lot.X, f.Lot.Z), designs[f.Design], f.Lot, !f.Back);
        }
        Check(placed == specs.Count && named == specs.Count && placed > 400,
            "uptown front: all " + placed + " of the band's built lots are laid out along their streets, each with a design by name that the made buildings' engine takes ("
            + named + ")");

        // No ground left inside a building's spaces: its look's frontage and any filler beside it in its lot cover its width
        // but for the sliver; the few the game's designs cannot fill are counted (an M's looks with its two ways in are few, and
        // the theatres' and cinemas' one front, 32.7 m, stands on five spaces of 35).
        int leftOver = 0;
        float worstLeft = 0f;
        string worstLot = null;
        var lookCounts = new int[4];
        foreach (MadeSpec s in specs.Values)
        {
            BuildingsLot l = lotOf[s.Id];
            int d = NewBuildingsRules.Pick(designs, s.Size, s.Type, s.Design, s.Lots[0].Width, s.Lots[0].Depth, s.Id);
            if (d < 0)
            {
                continue;
            }
            lookCounts[designs[d].Size == s.Size ? (designs[d].Version == s.Version ? 0 : 1) : SpacesRules.Fills(FrontageRules.Across(designs[d]), l.Width) ? 2 : 3]++;
            float spanned = FrontageRules.Across(designs[d]);
            bool alongX = Math.Abs(Math.Cos(l.Facing * Math.PI / 180.0)) > 0.5;
            float start = (alongX ? l.X : l.Z) - l.Width * 0.5f;
            foreach (FrontFill f in fills)
            {
                float at = alongX ? f.Lot.X : f.Lot.Z, across = alongX ? f.Lot.Z : f.Lot.X, mine = alongX ? l.Z : l.X;
                if (!f.Back && at > start && at < start + l.Width && Math.Abs(across - mine) < 0.5f && Math.Abs(f.Lot.Facing - l.Facing) < 1f)
                {
                    spanned += FrontageRules.Across(designs[f.Design]);
                }
            }
            float left = l.Width - spanned;
            leftOver += left > SpacesRules.FillSliver + 0.01f ? 1 : 0;
            if (left > worstLeft)
            {
                worstLeft = left;
                worstLot = s.Id + " " + SpacesRules.Of(s.Size, s.Version)?.Name + " " + l.Width + " m";
            }
        }
        Check(filled >= placed * 9 / 10 && leftOver <= placed / 15,
            "uptown spaces: " + filled + " of the " + placed + " buildings wear a look that fills their spaces (" + lookCounts[0] + " their own size and version's, " + lookCounts[1]
            + " their size's, " + lookCounts[2] + " another size's); " + leftOver + " leave more than " + SpacesRules.FillSliver + " m of their spaces, the most "
            + worstLeft.ToString("F1") + " m (" + worstLot + ")");

        // None over another, every piece inside its block.
        int overlaps = 0, outside = 0;
        foreach (KeyValuePair<int, List<float[]>> b in byBlock)
        {
            BuiltGround(b.Key / 100, b.Key % 100, out float x0, out float z0, out float x1, out float z1);
            List<float[]> boxes = b.Value;
            for (int i = 0; i < boxes.Count; i++)
            {
                outside += boxes[i][0] < x0 - 0.05f || boxes[i][2] > x1 + 0.05f || boxes[i][1] < z0 - 0.05f || boxes[i][3] > z1 + 0.05f ? 1 : 0;
                for (int j = i + 1; j < boxes.Count; j++)
                {
                    float wx = Math.Min(boxes[i][2], boxes[j][2]) - Math.Max(boxes[i][0], boxes[j][0]), wz = Math.Min(boxes[i][3], boxes[j][3]) - Math.Max(boxes[i][1], boxes[j][1]);
                    overlaps += wx > NewBuildingsRules.Touch && wz > NewBuildingsRules.Touch ? 1 : 0;
                }
            }
        }
        Check(overlaps == 0 && outside == 0, "uptown front: no building or filler stands over another (" + overlaps + "), none outside its block (" + outside + ")");

        // Along every strip: the pieces' fronts against the built lots' frontage, and every gap inside a run of built lots a sliver.
        float frontage = 0f, covered = 0f, widestGap = 0f;
        int wideGaps = 0;
        var builtIds = new HashSet<string>(specs.Keys, StringComparer.Ordinal);
        foreach (KeyValuePair<int, List<float[]>> b in byBlock)
        {
            foreach (float facing in new[] { 0f, 90f, 180f, 270f })
            {
                bool alongX = facing == 0f || facing == 180f;
                var pieces = b.Value.FindAll(x => x[5] > 0.5f && Math.Abs(x[4] - facing) < 1f);
                pieces.Sort((p, q) => (alongX ? p[0] : p[1]).CompareTo(alongX ? q[0] : q[1]));
                var strip = plan.FindAll(l => l.Block == b.Key && Math.Abs(l.Facing - facing) < 1f && builtIds.Contains(l.Id));
                foreach (BuildingsLot l in strip)
                {
                    frontage += l.Width;
                }
                for (int i = 0; i < pieces.Count; i++)
                {
                    covered += alongX ? pieces[i][2] - pieces[i][0] : pieces[i][3] - pieces[i][1];
                    if (i == 0)
                    {
                        continue;
                    }
                    float a0 = alongX ? pieces[i - 1][2] : pieces[i - 1][3], a1 = alongX ? pieces[i][0] : pieces[i][1];
                    if (a1 - a0 <= SliverMost || !WithinBuilt(strip, a0, a1, alongX))
                    {
                        continue;
                    }
                    wideGaps++;
                    widestGap = Math.Max(widestGap, a1 - a0);
                }
            }
        }
        Check(widestGap < SpacesRules.SpaceWidth && wideGaps <= placed / 10 && covered / frontage >= 0.95f,
            "uptown front: the built lots' frontage is " + (100f * covered / frontage).ToString("F0") + "% buildings and fillers side by side; along the streets " + wideGaps
            + " gaps are wider than " + SliverMost + " m (beside a building whose look leaves part of its spaces), the widest " + widestGap.ToString("F1") + " m, under a space");

        // Each block's edge with a face within 3 m of it, measured as Manhattan's were; the blocks' interiors under buildings.
        var shares = new List<float>();
        float ground = 0f, under = 0f;
        for (int row = 1; row <= LayoutRules.Rows; row++)
        {
            for (int column = 1; column <= LayoutRules.Columns; column++)
            {
                if (LayoutRules.IsSquare(row, column) || LayoutRules.IsFirst(row, column))
                {
                    continue;
                }
                LayoutRules.Block(row, column, out float x0, out float z0, out float x1, out float z1);
                List<float[]> boxes = byBlock.TryGetValue(row * 100 + column, out List<float[]> got) ? got : new List<float[]>();
                float edge = Covered(boxes, x0, x1, f => f[3] > z1 - 3f, 0, 2) + Covered(boxes, x0, x1, f => f[1] < z0 + 3f, 0, 2)
                             + Covered(boxes, z0, z1, f => f[0] < x0 + 3f, 1, 3) + Covered(boxes, z0, z1, f => f[2] > x1 - 3f, 1, 3);
                shares.Add(edge / (2f * (x1 - x0) + 2f * (z1 - z0)));
                ground += (x1 - x0) * (z1 - z0);
                foreach (float[] f in boxes)
                {
                    under += (f[2] - f[0]) * (f[3] - f[1]);
                }
            }
        }
        shares.Sort();
        float median = shares[shares.Count / 2];
        Check(median >= ManhattanEdges, "uptown front: the band's blocks are built along " + (100f * median).ToString("F0") + "% of their edges (the median of " + shares.Count
              + " blocks beyond the first, its open lots among them), Manhattan's own " + (100f * ManhattanEdges).ToString("F0") + "%, the first dense plan's 62%");
        Check(under / ground >= 0.55f, "uptown front: buildings and fillers stand on " + (100f * under / ground).ToString("F0") + "% of those blocks' ground, their interiors with them ("
              + back + " second rows behind buildings)");

        // What it costs at a load: the layout here, and the copies, at the 0.6 ms a made building took on the owner's load of 24 Oct.
        double load = (placed + fills.Count) * 0.6;
        Check(ms < 200.0 && load < placed * NewBuildingsRules.MostMakeMs,
            "uptown front: the layout takes " + ms.ToString("F0") + " ms; " + placed + " buildings and " + front + " + " + back + " fillers are about " + (load / 1000.0).ToString("F1")
            + " s of copies behind the loading screen (wanted under " + (placed * NewBuildingsRules.MostMakeMs / 1000.0).ToString("F1") + " s)");
    }

    // The block holding a point, as row * 100 + column, the band's or its ring's built strip; 0 for none.
    private static int BlockAt(float x, float z)
    {
        for (int row = 1; row <= LayoutRules.Rows + 1; row++)
        {
            for (int column = 0; column <= LayoutRules.Columns + 1; column++)
            {
                if (!BuiltGround(row, column, out float x0, out float z0, out float x1, out float z1))
                {
                    continue;
                }
                if (x >= x0 && x <= x1 && z >= z0 && z <= z1)
                {
                    return row * 100 + column;
                }
            }
        }
        return 0;
    }

    // A block's ground buildings stand on: a band block's whole, a ring block's strip facing the band; false for neither.
    private static bool BuiltGround(int row, int column, out float x0, out float z0, out float x1, out float z1)
    {
        if (LayoutRules.IsRing(row, column))
        {
            LayoutRules.RingStrip(row, column, out x0, out z0, out x1, out z1, out _);
            return true;
        }
        LayoutRules.Block(row, column, out x0, out z0, out x1, out z1);
        return row >= 1 && row <= LayoutRules.Rows && column >= 1 && column <= LayoutRules.Columns;
    }

    // True when every metre of the stretch from a to b along a strip lies on its built lots.
    private static bool WithinBuilt(List<BuildingsLot> strip, float a, float b, bool alongX)
    {
        float on = 0f;
        foreach (BuildingsLot l in strip)
        {
            float s = (alongX ? l.X : l.Z) - l.Width * 0.5f, e = s + l.Width;
            on += Math.Max(0f, Math.Min(b, e) - Math.Max(a, s));
        }
        return on >= b - a - 0.05f;
    }
}
