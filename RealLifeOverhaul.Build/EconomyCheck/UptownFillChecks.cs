using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Landmarks;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.Uptown;

// Uptown's band filled (the owner, 23 Oct 2026: "All 54 blocks", "Mostly built"), on the real plan of its 422 lots: the lots
// built from the first day and the open ones (the first block's old quarter kept, the landmarks' own blocks open, a share
// open on every other block, the same in any order and on every machine), the eight offices leased to the city's rival
// builders (office lots outside the first block, one a block, as many as the rivals), the landmarks' sites and the
// developers' lots on the filled plan, the lanes' squares for the made buildings' ground check against a whole pass, the
// other structures a building takes where no design of its own size fits its lot, and the sources: the load's declarations,
// the generator for new buildings alone, the leases by the game's own start of a business, the map's row placed, the look
// check, the tests in the run, the wiring; and the texts.
internal static partial class Program
{
    private static void UptownFillChecks()
    {
        List<BuildingsLot> plan = UptownRealPlan();
        UptownFillRules(plan);
        UptownFillLeases(plan);
        UptownFillLandmarks(plan);
        UptownFillLanes();
        UptownFillOthers(plan);
        UptownFillSources();
        UptownFillTexts();
    }

    // The band's lots as LifeGame.Lots reads them from the ground's plan (UptownLots.All), their blocks with them.
    private static List<BuildingsLot> UptownRealPlan()
    {
        var plan = new List<BuildingsLot>(512);
        foreach (Lot l in UptownLots.All)
        {
            plan.Add(new BuildingsLot
            {
                Id = l.Id, Street = l.Street, Number = l.Number, Block = l.Block, First = l.First, Avenue = BuildingsRules.FrontsAvenue(l.Facing), X = l.X, Z = l.Z,
                Facing = l.Facing, Width = l.Width, Depth = l.Depth, Class = (BuildingsClass)(int)l.Class, Sizes = l.Sizes
            });
        }
        return plan;
    }

    private static HashSet<string> UptownIds(List<BuildingsLot> plan, List<int> indices)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (int i in indices)
        {
            ids.Add(plan[i].Id);
        }
        return ids;
    }

    // ---------------------------------------------------------------- built from the first day, and open

    private static void UptownFillRules(List<BuildingsLot> plan)
    {
        Check(BuildingsRules.OpenCount(0, true) == 0 && BuildingsRules.OpenCount(11, true) == 3 && BuildingsRules.OpenCount(9, true) == 2 && BuildingsRules.OpenCount(10, true) == 3
              && BuildingsRules.OpenCount(11, false) == 1 && BuildingsRules.OpenCount(15, false) == 2 && BuildingsRules.OpenCount(1, false) == 1 && BuildingsRules.OpenCount(-2, false) == 0,
            "uptown fill: a block leaves its share of lots open, a quarter in the first block and an eighth elsewhere, halves up, at least one, never more than it has");
        var built = BuildingsRules.Built(plan, LandmarkRules.OwnBlock, 0f, 1f, new List<int>());
        HashSet<string> builtIds = UptownIds(plan, built);
        HashSet<string> legacy = UptownIds(plan, BuildingsRules.DayOne(plan, 0f, 1f, new List<int>()));
        HashSet<string> leased = UptownIds(plan, BuildingsRules.Leased(plan, new List<int>()));
        int first = 0, firstBuilt = 0;
        foreach (BuildingsLot lot in plan)
        {
            first += lot.First ? 1 : 0;
            firstBuilt += lot.First && builtIds.Contains(lot.Id) ? 1 : 0;
        }
        Check(plan.Count == 422 && built.Count == 352 && legacy.Count == 21 && first == 83 && firstBuilt == 44,
            "uptown fill: of the band's 422 lots, 352 are built from the first day and 70 left open; 44 of the first block's 83 (" + plan.Count + ", " + built.Count + ", "
            + legacy.Count + ", " + firstBuilt + " of " + first + ")");
        bool keepsOld = legacy.IsSubsetOf(builtIds) && leased.IsSubsetOf(builtIds);
        Check(keepsOld, "uptown fill: the first block's quarter built before the band was filled stays built (saves hold their records), and every leased office is built");

        var blocks = new SortedDictionary<int, List<BuildingsLot>>();
        foreach (BuildingsLot lot in plan)
        {
            if (!blocks.TryGetValue(lot.Block, out List<BuildingsLot> list))
            {
                list = new List<BuildingsLot>();
                blocks.Add(lot.Block, list);
            }
            list.Add(lot);
        }
        bool shares = true, someOpen = true, landmarksOpen = true;
        string wrong = null;
        foreach (KeyValuePair<int, List<BuildingsLot>> block in blocks)
        {
            int open = 0, fixedOnes = 0;
            bool firstBlock = false;
            foreach (BuildingsLot lot in block.Value)
            {
                open += builtIds.Contains(lot.Id) ? 0 : 1;
                fixedOnes += legacy.Contains(lot.Id) || leased.Contains(lot.Id) ? 1 : 0;
                firstBlock |= lot.First;
            }
            if (LandmarkRules.OwnBlock(block.Key))
            {
                bool all = open == block.Value.Count - fixedOnes;
                landmarksOpen &= all;
                wrong = all ? wrong : wrong ?? "block " + block.Key + " (a landmark's) has " + open + " open of " + block.Value.Count;
                continue;
            }
            int wanted = Math.Min(block.Value.Count - fixedOnes, BuildingsRules.OpenCount(block.Value.Count, firstBlock));
            shares &= open == wanted;
            someOpen &= open >= 1;
            wrong = open == wanted ? wrong : wrong ?? "block " + block.Key + " has " + open + " open, wanted " + wanted;
        }
        Check(blocks.Count == 37 && landmarksOpen && shares && someOpen,
            "uptown fill: each landmark's own block open but for the old quarter's lots, every other block its share open and at least one, in each of the 37 blocks with lots"
            + (wrong != null ? " (" + wrong + ")" : string.Empty));
        Check(LandmarkRules.OwnBlock(111) && LandmarkRules.OwnBlock(113) && LandmarkRules.OwnBlock(110) && !LandmarkRules.OwnBlock(106) && !LandmarkRules.OwnBlock(112)
              && !LandmarkRules.OwnBlock(201),
            "uptown fill: the blocks kept open are the tower's, the mall's and the stadium's own (111, 113, 110)");

        bool sorted = true;
        for (int i = 1; i < built.Count; i++)
        {
            sorted &= string.CompareOrdinal(plan[built[i - 1]].Id, plan[built[i]].Id) < 0;
        }
        bool same = true;
        var order = new int[plan.Count];
        for (int i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }
        for (int turn = 0; turn < 12; turn++)
        {
            Shuffle(order, turn + 40);
            var shuffled = new List<BuildingsLot>(plan.Count);
            foreach (int i in order)
            {
                shuffled.Add(plan[i]);
            }
            same &= UptownIds(shuffled, BuildingsRules.Built(shuffled, LandmarkRules.OwnBlock, 0f, 1f, new List<int>())).SetEquals(builtIds);
            var leasedAgain = BuildingsRules.Leased(shuffled, new List<int>());
            var lease0 = BuildingsRules.Leased(plan, new List<int>());
            for (int k = 0; k < lease0.Count && k < leasedAgain.Count; k++)
            {
                same &= shuffled[leasedAgain[k]].Id == plan[lease0[k]].Id;
            }
        }
        Check(sorted && same, "uptown fill: the built lots in the order of their ids, and the same lots and leases from the plan in any order");

        var none = BuildingsRules.Built(plan, null, 0f, 1f, new List<int>());
        int noneFirst = 0;
        foreach (int i in none)
        {
            noneFirst += plan[i].First ? 1 : 0;
        }
        Check(none.Count > built.Count && noneFirst > firstBuilt && BuildingsRules.Built(new List<BuildingsLot>(), LandmarkRules.OwnBlock, 0f, 1f, new List<int>()).Count == 0
              && BuildingsRules.Built(null, null, 0f, 1f, new List<int>()).Count == 0,
            "uptown fill: with no block kept open the first block is built to its shares too; no plan builds nothing");

        int developers = 0, openFirst = 0;
        foreach (BuildingsLot lot in plan)
        {
            if (!builtIds.Contains(lot.Id))
            {
                developers += DevelopmentRules.Theirs(lot.Id) ? 1 : 0;
                openFirst += lot.First ? 1 : 0;
            }
        }
        Check(developers >= 25 && openFirst >= 30, "uptown fill: the city's developers have " + developers + " open lots to build on, " + openFirst
                                                  + " of the open lots in the first block, where they build first and the landmarks stand");

        var options = new List<BuildingsStructure>
        {
            new BuildingsStructure { Size = "ba:buildingsize_d", Version = 2, Type = "ba:buildingtype_office" },
            new BuildingsStructure { Size = "ba:buildingsize_d", Version = 2, Type = "ba:buildingtype_retail" },
            new BuildingsStructure { Size = "ba:buildingsize_j", Version = 1, Type = "ba:buildingtype_office" },
            new BuildingsStructure { Size = "ba:buildingsize_k", Version = 1, Type = "ba:buildingtype_office" }
        };
        options.Sort(BuildingsRules.CompareStructures);
        bool offices = true;
        var used = new HashSet<int>();
        foreach (BuildingsLot lot in plan)
        {
            int pick = BuildingsRules.PickOffice(lot.Id, options);
            offices &= pick >= 0 && options[pick].Type == BuildingsRules.OfficeType && pick == BuildingsRules.PickOffice(lot.Id, options);
            used.Add(pick);
        }
        var retail = new List<BuildingsStructure> { new BuildingsStructure { Size = "ba:buildingsize_d", Version = 2, Type = "ba:buildingtype_retail" } };
        Check(offices && used.Count == 3 && BuildingsRules.PickOffice("x", retail) == -1 && BuildingsRules.PickOffice(null, options) == -1,
            "uptown fill: a leased lot is built with one of the game's office structures for its sizes, picked by its id, every one used; none where the game has none");
    }

    // ---------------------------------------------------------------- the offices leased to the city's builders

    private static void UptownFillLeases(List<BuildingsLot> plan)
    {
        var leased = BuildingsRules.Leased(plan, new List<int>());
        bool offices = true, outside = true, oneABlock = true;
        var blocks = new HashSet<int>();
        foreach (int i in leased)
        {
            offices &= plan[i].Class == BuildingsClass.Office;
            outside &= !plan[i].First;
            oneABlock &= blocks.Add(plan[i].Block);
        }
        Check(leased.Count == BuildingsRules.LeasedOffices && BuildingsRules.LeasedOffices == BuildCatalogue.RivalCount && offices && outside && oneABlock,
            "uptown fill: " + leased.Count + " office lots leased, one for each of the construction game's " + BuildCatalogue.RivalCount
            + " rival builders: office lots outside the first block (the landmarks' blocks), one a block");
        int rows = 0;
        var rowSeen = new HashSet<int>();
        foreach (int i in leased)
        {
            rows += rowSeen.Add(plan[i].Block / 100) ? 1 : 0;
        }
        Check(rows >= 2, "uptown fill: the builders' offices are spread over the band's rows (" + rows + " rows)");
        Check(BuildCatalogue.OfficeDayPerCrew > 0 && BuildCatalogue.OfficeAboutKey == "reallife_bld_office_about",
            "uptown fill: a builder's office is valued by its crews' days, and BizMan describes it in the texts");
        Check(BuildingsRules.Leased(null, new List<int>()).Count == 0 && BuildingsRules.Leased(new List<BuildingsLot>(), new List<int>()).Count == 0,
            "uptown fill: no plan leases nothing");
    }

    // ---------------------------------------------------------------- the landmarks on the filled plan

    private static void UptownFillLandmarks(List<BuildingsLot> plan)
    {
        HashSet<string> builtIds = UptownIds(plan, BuildingsRules.Built(plan, LandmarkRules.OwnBlock, 0f, 1f, new List<int>()));
        var lots = new List<SiteLot>();
        foreach (Lot l in LayoutRules.FirstLots)
        {
            lots.Add(new SiteLot
            {
                Id = l.Id, Block = l.Block, First = l.First, FirstDay = builtIds.Contains(l.Id), Avenue = l.Facing < 1f || l.Facing > 359f, X = l.X, Z = l.Z, Yaw = l.Yaw,
                Width = l.Width, Depth = l.Depth, Class = (int)l.Class
            });
        }
        var held = new List<int>();
        LandmarkSite tower = SiteOf(LandmarkKind.Tower, lots, null, held);
        held.Add(tower != null ? tower.Block : 0);
        LandmarkSite mall = SiteOf(LandmarkKind.Mall, lots, null, held);
        held.Add(mall != null ? mall.Block : 0);
        LandmarkSite stadium = SiteOf(LandmarkKind.Stadium, lots, null, held);
        bool own = tower != null && tower.Block == 111 && mall != null && mall.Block == 113 && stadium != null && stadium.Block == 110;
        bool noneBuilt = true;
        foreach (LandmarkSite site in new[] { tower, mall, stadium })
        {
            foreach (string id in site != null ? site.Lots : new List<string>())
            {
                noneBuilt &= !builtIds.Contains(id);
            }
        }
        Check(own && noneBuilt, "uptown fill: on the filled band each landmark still takes its own block, and none of its site's lots is built from the first day");
    }

    // ---------------------------------------------------------------- the lanes' squares

    private static void UptownFillLanes()
    {
        const int Points = 20176;
        var xs = new float[Points];
        var zs = new float[Points];
        var present = new bool[Points];
        uint seed = 2166136261u;
        for (int i = 0; i < Points; i++)
        {
            seed = unchecked(seed * 1664525u + 1013904223u);
            xs[i] = -3000f + (seed % 600000u) / 100f;
            seed = unchecked(seed * 1664525u + 1013904223u);
            zs[i] = -1500f + (seed % 250000u) / 100f;
            present[i] = i % 97 != 0;   // a few missing, as a lane point the game has dropped
        }
        var grid = new LaneGrid();
        grid.Fill(xs, zs, present, Points);
        bool same = true;
        int inside = 0, near = 0;
        for (int q = 0; q < 600; q++)
        {
            seed = unchecked(seed * 1664525u + 1013904223u);
            float cx = -3000f + (seed % 600000u) / 100f;
            seed = unchecked(seed * 1664525u + 1013904223u);
            float cz = -1500f + (seed % 250000u) / 100f;
            float facing = (seed >> 8) % 4u * 90f + (q % 3 == 0 ? 17f : 0f);
            float hw = 5f + q % 12, hd = 10f + q % 9;
            int brute = -1, bruteNear = -1;
            float best = float.MaxValue;
            for (int i = 0; i < Points; i++)
            {
                if (!present[i])
                {
                    continue;
                }
                if (brute < 0 && NewBuildingsRules.Inside(xs[i], zs[i], cx, cz, facing, hw, hd, NewBuildingsRules.RoadClear))
                {
                    brute = i;
                }
                float dx = xs[i] - cx, dz = zs[i] - cz, sqr = dx * dx + dz * dz;
                if (sqr < best)
                {
                    best = sqr;
                    bruteNear = i;
                }
            }
            float reach = NewBuildingsRules.StreetReach + Math.Max(hw, hd);
            int gridInside = grid.Inside(cx, cz, facing, hw, hd, NewBuildingsRules.RoadClear);
            int gridNear = grid.Nearest(cx, cz, reach, out float squared);
            same &= gridInside == brute && (best <= reach * reach ? gridNear == bruteNear && squared == best : gridNear == -1);
            inside += brute >= 0 ? 1 : 0;
            near += gridNear >= 0 ? 1 : 0;
        }
        Check(same && inside > 0 && inside < 600 && near > 0 && grid.Count == Points - (Points + 96) / 97,
            "uptown fill: the lanes' squares give a whole pass's answers, the lowest point inside a footprint and the nearest within reach, on 600 footprints over "
            + Points + " points (" + inside + " with a lane inside, " + near + " fronting one)");
        long began = Stopwatch.GetTimestamp();
        int hits = 0;
        for (int q = 0; q < 360; q++)
        {
            hits += grid.Inside(xs[q * 37] + 30f, zs[q * 37] + 30f, 90f, 6f, 12f, NewBuildingsRules.RoadClear) >= 0 ? 1 : 0;
            hits += grid.Nearest(xs[q * 37] + 30f, zs[q * 37] + 30f, 72f, out _) >= 0 ? 1 : 0;
        }
        double ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
        Check(ms < 20.0 && hits > 0, "uptown fill: 360 buildings' lane checks in " + ms.ToString("F2") + " ms with the squares (wanted under 20 ms; a whole pass each is some 7 million point tests)");
    }

    // ---------------------------------------------------------------- another structure where no design of its own fits

    // The game's structures for the band's sizes as its BuildingSizeData has them (the game's files, 23 Oct 2026; LifeGame.Structures
    // reads them at each load): size, version, type, never the special ones. No design of P or Q that can be copied fits a 29 by
    // 39 m warehouse lot (the smallest are 34.9 by 36.6 and 48.9 by 69.5 m), so a warehouse lot whose hash picks one of them
    // must take another of its sizes.
    private static readonly string[][] GameStructures =
    {
        new[] { "a", "1", "retail" }, new[] { "a", "2", "retail" }, new[] { "a", "3", "office" }, new[] { "b", "1", "residential" },
        new[] { "c", "1", "retail" }, new[] { "c", "1", "office" }, new[] { "c", "2", "retail" }, new[] { "f", "1", "residential" },
        new[] { "d", "2", "retail" }, new[] { "d", "2", "office" }, new[] { "j", "1", "office" }, new[] { "k", "1", "office" },
        new[] { "l", "1", "residential" }, new[] { "m", "1", "retail" }, new[] { "n", "1", "residential" },
        new[] { "h", "1", "warehouse" }, new[] { "h", "2", "warehouse" }, new[] { "h", "3", "warehouse" }, new[] { "i", "1", "warehouse" },
        new[] { "i", "2", "warehouse" }, new[] { "i", "3", "warehouse" }, new[] { "p", "1", "warehouse" }, new[] { "p", "2", "warehouse" },
        new[] { "p", "3", "warehouse" }, new[] { "q", "1", "warehouse" }, new[] { "q", "2", "warehouse" }, new[] { "q", "3", "warehouse" }
    };

    // A lot's options as LifeGame.Structures lists them from the table above: its class's sizes, in the rules' order.
    private static List<BuildingsStructure> UptownOptions(BuildingsLot lot)
    {
        var options = new List<BuildingsStructure>();
        foreach (string[] s in GameStructures)
        {
            string size = "ba:buildingsize_" + s[0];
            for (int i = 0; lot.Sizes != null && i < lot.Sizes.Count; i++)
            {
                if (lot.Sizes[i] == size)
                {
                    options.Add(new BuildingsStructure { Size = size, Version = int.Parse(s[1]), Type = "ba:buildingtype_" + s[2] });
                }
            }
        }
        options.Sort(BuildingsRules.CompareStructures);
        return options;
    }

    // A list of structures as "Q1w H1w": the game's short name of each and its type's first letter.
    private static string UptownNames(List<BuildingsStructure> list)
    {
        var b = new StringBuilder();
        foreach (BuildingsStructure s in list)
        {
            b.Append(b.Length > 0 ? " " : string.Empty).Append(NewBuildingsRules.StructureName(s.Size, s.Version)).Append(s.Type.Substring(s.Type.LastIndexOf('_') + 1, 1));
        }
        return b.ToString();
    }

    private static BuildingsStructure UptownStructure(string size, int version, string type)
    {
        return new BuildingsStructure { Size = "ba:buildingsize_" + size, Version = version, Type = "ba:buildingtype_" + type };
    }

    private static void UptownFillOthers(List<BuildingsLot> plan)
    {
        var warehouses = new List<BuildingsStructure>();
        foreach (string size in new[] { "h", "i", "p", "q" })
        {
            for (int v = 1; v <= 3; v++)
            {
                warehouses.Add(UptownStructure(size, v, "warehouse"));
            }
        }
        warehouses.Sort(BuildingsRules.CompareStructures);
        var into = new List<BuildingsStructure>();
        string fromP2 = UptownNames(BuildingsRules.Others(warehouses, 7, false, into));
        string fromQ3 = UptownNames(BuildingsRules.Others(warehouses, 11, false, into));
        string fromH1 = UptownNames(BuildingsRules.Others(warehouses, 0, false, into));
        Check(fromP2 == "Q1w H1w I1w" && fromQ3 == "H1w I1w P1w" && fromH1 == "I1w P1w Q1w",
            "uptown fill: a warehouse whose size has no design for its lot takes one of each other size, from its own on and round again (" + fromP2 + "; " + fromQ3 + "; " + fromH1 + ")");

        var small = new List<BuildingsStructure>
        {
            UptownStructure("a", 1, "retail"), UptownStructure("a", 2, "retail"), UptownStructure("a", 3, "office"), UptownStructure("b", 1, "residential"),
            UptownStructure("c", 1, "retail"), UptownStructure("c", 1, "office"), UptownStructure("c", 2, "retail"), UptownStructure("f", 1, "residential")
        };
        small.Sort(BuildingsRules.CompareStructures);
        int a1 = small.FindIndex(s => s.Size == "ba:buildingsize_a" && s.Version == 1), a3 = small.FindIndex(s => s.Size == "ba:buildingsize_a" && s.Version == 3);
        string fromA1 = UptownNames(BuildingsRules.Others(small, a1, false, into));
        string fromA3 = UptownNames(BuildingsRules.Others(small, a3, false, into));
        string officesA3 = UptownNames(BuildingsRules.Others(small, a3, true, into));
        Check(fromA1 == "B1r C1r F1r" && fromA3 == "B1r C1o F1r" && officesA3 == "C1o",
            "uptown fill: the other sizes keep the building's type where they take it, and a leased office takes offices alone (" + fromA1 + "; " + fromA3 + "; " + officesA3 + ")");
        Check(BuildingsRules.Others(null, 0, false, into).Count == 0 && BuildingsRules.Others(small, -1, false, into).Count == 0
              && BuildingsRules.Others(small, small.Count, false, into).Count == 0
              && BuildingsRules.Others(new List<BuildingsStructure> { UptownStructure("a", 1, "retail") }, 0, false, into).Count == 0,
            "uptown fill: no other structures where the pick is none of the options or its size is the only one");

        // On the real plan: every built lot's own structure and its others reach every size of its class (every office size
        // for a leased one), each size once; so a building stands wherever any size of its class has a design that fits.
        List<int> built = BuildingsRules.Built(plan, LandmarkRules.OwnBlock, 0f, 1f, new List<int>());
        HashSet<string> leased = UptownIds(plan, BuildingsRules.Leased(plan, new List<int>()));
        int reached = 0, onPq = 0, leasedOk = 0;
        string wrong = null;
        foreach (int i in built)
        {
            BuildingsLot lot = plan[i];
            List<BuildingsStructure> options = UptownOptions(lot);
            bool lease = leased.Contains(lot.Id);
            int pick = lease ? BuildingsRules.PickOffice(lot.Id, options) : BuildingsRules.PickStructure(lot.Id, options);
            if (pick < 0)
            {
                wrong = wrong ?? lot.Id + " has no structure";
                continue;
            }
            BuildingsRules.Others(options, pick, lease, into);
            var sizes = new HashSet<string> { options[pick].Size };
            bool once = true, offices = !lease || options[pick].Type == BuildingsRules.OfficeType;
            foreach (BuildingsStructure o in into)
            {
                once &= sizes.Add(o.Size);
                offices &= !lease || o.Type == BuildingsRules.OfficeType;
            }
            bool all = true;
            foreach (BuildingsStructure o in options)
            {
                all &= sizes.Contains(o.Size) || (lease && o.Type != BuildingsRules.OfficeType);
            }
            if (once && offices && all)
            {
                reached++;
                leasedOk += lease ? 1 : 0;
            }
            else
            {
                wrong = wrong ?? lot.Id + " (" + UptownNames(into) + ")";
            }
            onPq += lot.Class == BuildingsClass.Warehouse && (options[pick].Size == "ba:buildingsize_p" || options[pick].Size == "ba:buildingsize_q") ? 1 : 0;
        }
        Check(reached == built.Count && leasedOk == BuildingsRules.LeasedOffices && onPq > 0,
            "uptown fill: each of the " + built.Count + " buildings may take every size of its lot's class (" + reached + "; the eight offices offices alone: " + leasedOk + "), "
            + onPq + " warehouse lots picking P or Q, which no design fits" + (wrong != null ? "; not so for " + wrong : string.Empty));

        var spec = new MadeSpec { Id = "x", Life = false };
        var life = new MadeSpec { Id = "y", Life = true };
        Func<string, int, string, bool> any = (size, version, type) => true;
        Func<string, int, string, bool> none = (size, version, type) => false;
        var home = new MadeStructure { Size = "ba:buildingsize_b", Version = 1, Type = NewBuildingsRules.HomeType };
        var office = new MadeStructure { Size = "ba:buildingsize_d", Version = 2, Type = "ba:buildingtype_office" };
        var special = new MadeStructure { Size = "ba:buildingsize_d", Version = 3, Type = NewBuildingsRules.SpecialType };
        Check(NewBuildingsRules.OtherTakes(spec, office, any, false) && !NewBuildingsRules.OtherTakes(spec, office, none, true) && !NewBuildingsRules.OtherTakes(spec, special, any, true)
              && !NewBuildingsRules.OtherTakes(spec, home, any, false) && NewBuildingsRules.OtherTakes(spec, home, any, true) && NewBuildingsRules.OtherTakes(life, home, any, false)
              && !NewBuildingsRules.OtherTakes(null, office, any, true) && !NewBuildingsRules.OtherTakes(spec, new MadeStructure(), any, true)
              && !NewBuildingsRules.OtherTakes(spec, office, null, true),
            "new buildings: another structure is taken by the declaration's own limits: one the game has, never the special type, a home only where the deliveries leave it alone");
    }

    // ---------------------------------------------------------------- the sources

    private static void UptownFillSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string Read(string file)
        {
            string path = System.IO.Path.Combine(scripts, file);
            return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : string.Empty;
        }
        string director = Read("Uptown/LifeDirector.cs"), lots = Read("Uptown/LifeDirector.Lots.cs"), leases = Read("Uptown/LifeDirector.Leases.cs");
        string checks = Read("Uptown/LifeDirector.Checks.cs"), gameBuildings = Read("Uptown/LifeGame.Buildings.cs"), gameLeases = Read("Uptown/LifeGame.Leases.cs");
        string map = Read("Uptown/LifeGame.Map.cs"), guide = Read("Uptown/LifeGuide.cs"), districtBuildings = Read("Uptown/DistrictBuildings.cs");
        string landmarks = Read("Landmarks/LandmarksDirector.cs"), wiring = Read("Wiring/Wiring.ConstructionLinks.cs"), rivals = Read("Construction/ConstructionApp.Rivals.cs");
        string copyGame = Read("NewBuildings/CopyGame.cs"), ground = Read("NewBuildings/CopyGame.Ground.cs"), newDirector = Read("NewBuildings/NewBuildingsDirector.cs");

        string declare = MemberText(director, "private static void Declare()");
        Check(declare.Contains("BuildingsRules.Built(Plan, DistrictBuildings.KeptOpen") && declare.Contains("LifeGame.Kept(lot)") && declare.Contains("BuildingsRules.PickOffice(")
              && !declare.Contains("BuildingsRules.DayOne("),
            "uptown fill: the load declares the band's built lots, every lot whose records the save holds, and a leased lot as an office");
        string firstDay = MemberText(lots, "public static bool FirstDay(string lotId)");
        Check(firstDay.Contains("BuildingsRules.Built(Plan, kept") && firstDay.Contains("ReferenceEquals(kept, _firstDayFor)"),
            "uptown fill: the lots built from the first day the landmarks read are the same rule's, worked out again if the kept blocks change");
        Check(MemberText(lots, "public static bool StandsOn(string lotId)").Contains("StandingLots.Contains(lotId)") && director.Contains("MarkStanding();"),
            "uptown fill: whether a building stands on a lot is a set's look, made once the buildings stand (the construction game asks it of every lot on a page)");
        string firstLife = MemberText(director, "private static bool FirstLife()");
        Check(firstLife.Contains("LifeGame.PopulateOnly(save, NewOnes, Standing)") && firstLife.Contains("Leases(save)") && !firstLife.Contains("FinishOwner.City, 0, save"),
            "uptown fill: buildings new to a save the district lived in are set up by the game's own generator, not left empty to let, and the leases move in after");
        string only = MemberText(gameBuildings, "public static int PopulateOnly(GameInstance save, List<MadeBuilding> fresh, List<MadeBuilding> standing)");
        Check(only.Contains("all.Remove(record)") && only.Contains("finally") && only.Contains("all.AddRange(AllWas)") && only.Contains("UnityEngine.Random.state = was")
              && only.Contains("CityGenerator.InitializeCity(DistrictValues.Id)") && only.Contains("CityGenerator.DistributeBuildingsToRivals(DistrictValues.Id)"),
            "uptown fill: the generator sees the new buildings alone, and the game's list of all buildings and its chance are put back as they were, after a fault too");
        string lease = MemberText(gameLeases, "public static string Lease(MadeBuilding made, DistrictLease lease, int index, GameInstance save, out bool movedIn)");
        string moveIn = MemberText(gameLeases, "private static void MoveIn(BuildingRegistration reg, DistrictLease lease, AiBusinessDefault from, GameInstance save)");
        Check(lease.Contains("GoingPublic.Foreign(reg)") && lease.Contains("reg.RentedByPlayer || reg.BuildingOwnedByPlayer") && lease.Contains("made.NewRegistration")
              && moveIn.Contains("CompetitionHelper.StartNewCompetitorBusiness(lease.BusinessType, reg, impactMarket: false, from, string.Empty, setUpFirstDailyIncome: false)")
              && moveIn.Contains("UnityEngine.Random.InitState(") && moveIn.Contains("UnityEngine.Random.state = was") && moveIn.Contains("reg.dailyIncomes = new List<float>(IncomeDays)"),
            "uptown fill: a builder moves into its office by the game's own start of a business, seeded, never into the player's or another player's, with days of income to value it by");
        string borrow = MemberText(gameLeases, "private static string Borrow(string from, string to)");
        Check(borrow.Contains("BusinessLayoutSetHelper.GetAllBusinessLayoutSets()") && borrow.Contains("warnIfNotFound: false") && borrow.Contains("!= lent")
              && borrow.Contains("all.Remove(key)") && borrow.Contains("BusinessLayoutSetHelper.loadingLayouts"),
            "uptown fill: the studio's layouts are filed under the builders' type in the game's own cache, and taken out again if the game does not find them there");
        Check(Regex.Matches(gameLeases + gameBuildings, @"\bUpdate\(\)|LateUpdate\(|FixedUpdate\(").Count == 0 && Regex.Matches(leases + checks, @"void (Late|Fixed)?Update\(").Count == 0,
            "uptown fill: nothing of the fill, the leases or the checks runs a frame");
        Check(MemberText(map, "private static bool Row(float x1, float z1, float x2, float z2, out string why)").Contains("Place(_row, under);")
              && MemberText(map, "private static void Place(CityMapFilter row, CityMapFilterCategory under)").Contains("SetSiblingIndex("),
            "uptown fill: Uptown's row is stood among the game's district rows, where the game stands its own, not at the end of the list where its CreateFilter puts a late one");
        Check(landmarks.Contains("DistrictBuildings.KeptOpen = LandmarkRules.OwnBlock;") && MemberText(landmarks, "public static void Register(IModLogger logger)").Contains("KeptOpen"),
            "uptown fill: the landmarks keep their own blocks open at game start, whatever their option says");
        Check(wiring.Contains("DistrictBuildings.Lessee = RivalLease;") && MemberText(wiring, "internal static void Disconnect()").Contains("DistrictBuildings.Lessee = null;")
              && wiring.Contains("ConstructionBridges.RivalOffice = RivalOffice;") && rivals.Contains("ConstructionBridges.RivalOffice(r)") && rivals.Contains("reallife_bld_rival_office_go"),
            "uptown fill: the construction game leases its rivals' offices through the wiring, and its Rivals tab shows each office and makes it the destination");
        Check(districtBuildings.Contains("public static Func<int, bool> KeptOpen;") && districtBuildings.Contains("public static Func<int, DistrictLease> Lessee;"),
            "uptown fill: the district's bridges for kept blocks and leases are fields set once, never searched for");
        Check(guide.Contains(".Check(\"reallife_uptown_test_offices\", LifeDirector.TestOffices, \"Uptown check (builders' offices)\")")
              && guide.Contains(".Check(\"reallife_uptown_test_row\", LifeDirector.TestRow, \"Uptown check (map row)\")")
              && guide.Contains(".Check(\"reallife_uptown_test_cost\", LifeDirector.TestCost, \"Uptown check (frame cost)\", RunPlace.UptownLot)")
              && checks.Contains("\"Uptown check (builders' offices): ") && checks.Contains("\"Uptown check (map row): ") && checks.Contains("\"Uptown check (frame cost): "),
            "uptown fill: the offices, the map row and the frame cost are in the test run, each writing the line it is read by");
        string looks = MemberText(copyGame, "public static bool LooksAsGame(CityBuildingController c, out string why)");
        Check(looks.Contains("ShadowCastingMode.ShadowsOnly") && looks.Contains("IsInCameraBlockMode") && director.Length > 0
              && Read("Uptown/LifeDirector.Tests.cs").Contains("MadeBuildings.LooksAsGame(Standing[i], out string why)"),
            "uptown fill: the census judges every building's look against the game's (its box drawn only as a shadow while the map is shut)");
        Check(ground.Contains("Lanes.Inside(cx, cz, facing, hw, hd, NewBuildingsRules.RoadClear)") && ground.Contains("Lanes.Nearest(cx, cz, reach, out _)")
              && ground.Contains("ReferenceEquals(points, _lanesOf)"),
            "uptown fill: the made buildings' ground check reads the lanes' squares, made again when the lanes change");
        Check(newDirector.Contains("Made buildings check (load time): ") && newDirector.Contains("NewBuildingsRules.MostMakeMs") && newDirector.Contains("if (!spec.Life || Meta.Dev.Verbose)"),
            "uptown fill: the load's line says what measuring and making cost, a district's buildings are one line, each with Developer diagnostics");
        string make = MemberText(newDirector, "private bool Make(MadeSpec spec)");
        int tries = make.IndexOf("for (int s = -1; lot < 0 && noDesign && s < (spec.Others != null ? spec.Others.Length : 0); s++)", StringComparison.Ordinal);
        int takes = make.IndexOf("spec.Size = other.Size;", StringComparison.Ordinal), record = make.IndexOf("MadeGame.MakeRecord(spec", StringComparison.Ordinal);
        Check(tries > 0 && make.Contains("noDesign &= pick < 0;") && make.Contains("NewBuildingsRules.OtherTakes(spec, spec.Others[s], MadeGame.StructureTakes, MadeGame.HomesLeftOut())")
              && takes > tries && record > takes && make.Contains("spec.Version = other.Version;") && make.Contains("spec.Type = other.Type;"),
            "new buildings: where no design of its size fits any lot, a building tries its other structures in order (never for a lot its ground refused), its record made with the one taken");
        Check(declare.Contains("BuildingsRules.Others(Options, pick, lease, Others);") && declare.Contains("LifeGame.Spec(GroundOf(lot, f), structure, Others,")
              && declare.IndexOf("Others.Clear();", StringComparison.Ordinal) >= 0
              && declare.IndexOf("Others.Clear();", StringComparison.Ordinal) < declare.IndexOf("if (f != null)", StringComparison.Ordinal)
              && MemberText(gameBuildings, "public static MadeSpec Spec(BuildingsLot lot, BuildingsStructure structure, IList<BuildingsStructure> others,").Contains("Others = instead"),
            "uptown fill: each day-one building is declared with its other structures (offices alone for a leased office), a finished one with none");
    }

    // ---------------------------------------------------------------- the texts

    private static void UptownFillTexts()
    {
        string folder = LocalesFolder();
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json")), pt = LoadWords(System.IO.Path.Combine(folder, "pt.json"));
        Dictionary<string, string> zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        string[] keys =
        {
            "reallife_uptown_test_offices", "reallife_uptown_test_offices_done", "reallife_uptown_test_offices_none", "reallife_uptown_test_row", "reallife_uptown_test_row_done",
            "reallife_uptown_test_row_wrong", "reallife_uptown_test_cost", "reallife_uptown_test_cost_start", "reallife_uptown_test_cost_done", "reallife_uptown_test_cost_none",
            "reallife_bld_row_office", "reallife_bld_rival_office_none", "reallife_bld_rival_office_go", "reallife_bld_rival_office_set", "reallife_bld_office_about"
        };
        bool all = en != null && pt != null && zh != null, short240 = true;
        string missing = null;
        foreach (string key in keys)
        {
            bool there = all && en.ContainsKey(key) && pt.ContainsKey(key) && zh.ContainsKey(key) && en[key].Length > 0 && SameHoles(en[key], pt[key], zh[key]);
            missing = there ? missing : missing ?? key;
            all &= there;
            short240 &= en == null || !en.ContainsKey(key) || en[key].Length <= 240;
        }
        Check(all && short240, "uptown fill: every new text in en, pt and zh-cn with the same {holes}, every notice at most 240 characters (" + (missing ?? keys.Length + " texts") + ")");
        var langs = new[] { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" };
        bool lines = true;
        string lineMissing = null;
        for (int i = 0; i < langs.Length; i++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, langs[i] + ".json"));
            bool found = false;
            if (words != null)
            {
                foreach (KeyValuePair<string, string> kv in words)
                {
                    if (kv.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && kv.Value.Contains(UptownFillMark[i]))
                    {
                        found = true;
                        break;
                    }
                }
            }
            lines &= found;
            lineMissing = found ? lineMissing : lineMissing ?? langs[i];
        }
        Check(lines && UptownFillMark.Length == langs.Length, "uptown fill: What's new names the rival builders' offices in all nine languages"
                                                             + (lineMissing != null ? " (not in " + lineMissing + ")" : string.Empty));
        bool built = true;
        string builtMissing = null;
        for (int i = 0; i < langs.Length; i++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, langs[i] + ".json"));
            bool found = false;
            if (words != null)
            {
                foreach (KeyValuePair<string, string> kv in words)
                {
                    found |= kv.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && kv.Value.Contains(UptownBuiltMark[i]);
                }
            }
            built &= found;
            builtMissing = found ? builtMissing : builtMissing ?? langs[i];
        }
        Check(built && UptownBuiltMark.Length == langs.Length, "uptown fill: What's new says Uptown is built up from the first day in all nine languages"
                                                              + (builtMissing != null ? " (not in " + builtMissing + ")" : string.Empty));
    }

    // A few words of the rival builders' offices' line in each language's What's new, in the order en, pt, zh-cn, de, fr, es, ru, nl, tr.
    private static readonly string[] UptownFillMark = { "offices in Uptown", "escritórios em Uptown", "在上城有了办公室", "Büros in Uptown", "bureaux à Uptown", "oficinas en Uptown",
                                                        "офисы в Uptown", "kantoren in Uptown", "Uptown'da ofisleri" };

    // A few words of Uptown's line saying it is built up from the first day, in the same order.
    private static readonly string[] UptownBuiltMark = { "built up like the city's own districts", "construído como os bairros da própria cidade", "像城市原有的街区一样建满了楼",
                                                         "bebaut wie die Viertel der Stadt", "bâti comme les quartiers de la ville", "construido como los barrios de la ciudad",
                                                         "застроен как районы самого города", "volgebouwd zoals de wijken van de stad", "şehrin kendi semtleri gibi yapılaşmış" };
}
