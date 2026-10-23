using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul.NewGround;
using RealLifeOverhaul.Uptown;

// Uptown's ground (Uptown/LayoutRules.cs, LayoutPlans.cs, LayoutStreets.cs, UptownLots.cs and the folder's sources): the band's
// streets on the game's own grid, the lots round its blocks in Manhattan's mix with their classes' footprints, clear of
// the streets and of one another, their ids, streets and numbers fixed; the first block's 80 to 100 lots on the ground
// it lays; its streets built as lanes and joined through 1st Avenue's own junctions (a model of each, from the game's
// baked lanes), never off the road grid, the same twice; the walking ground under every lot and its door; the walls,
// the colliders, the hiding, the zones and the terminus where they belong. Over the sources: the game touched only
// through the folder's Game files, nothing saved or sent, no LINQ, nothing left to chance, the texts in three languages.
internal static partial class Program
{
    private static void Uptowns(bool print)
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        UptownStreetChecks();
        UptownLotChecks(print);
        UptownGroundChecks();
        UptownRoadChecks(print);
        UptownWalkChecks();
        UptownPlaceChecks();
        UptownCountChecks();
        UptownLifeChecks();
        UptownTimingChecks();
        UptownSourceChecks(scripts);
        UptownGameSourceChecks(scripts);
        UptownTextChecks(scripts);
    }

    // ---------------------------------------------------------------- the streets

    private static void UptownStreetChecks()
    {
        LayoutStreet[] all = LayoutStreets.All;
        Check(all.Length == 22, "uptown streets: nineteen streets and three avenues (" + all.Length + ")");
        Check(all.All(s => s.Id.StartsWith("reallife:street_uptown_", StringComparison.Ordinal) && s.Id.StartsWith("reallife:street_", StringComparison.Ordinal)),
            "uptown streets: every id under the mod's own street prefix, never one of the game's");
        Check(all.Select(s => s.Id).Distinct().Count() == all.Length && all.Select(s => s.Abbreviation).Distinct().Count() == all.Length
              && all.All(s => s.Abbreviation.Length == 3 && s.Abbreviation != "PRL"), "uptown streets: an id and a three-letter short name of their own each");
        Check(all.All(s => s.NameKey.StartsWith("reallife_newbuildings_street_", StringComparison.Ordinal)), "uptown streets: their names under the mod's street names' prefix");
        for (int k = LayoutStreets.WestMost; k <= LayoutStreets.EastMost; k++)
        {
            LayoutStreet s = LayoutStreets.Street(k);
            Check(s != null && !s.Avenue && s.Index == k && LayoutStreets.Find(s.Id) == s, "uptown streets: street " + k + " found by its place and its id");
        }
        Check(LayoutStreets.Street(-6) == null && LayoutStreets.Street(14) == null && LayoutStreets.Avenue(0) == null && LayoutStreets.Avenue(4) == null
              && LayoutStreets.Find("ba:street_secondstreet") == null && LayoutStreets.Find(null) == null, "uptown streets: nothing outside the band, and none of the game's");
        string[] game = { "ba:street_ninthstreet", "ba:street_eighthstreet", "ba:street_seventhstreet", "ba:street_sixthstreet", "ba:street_fifthstreet",
            "ba:street_fourthstreet", "ba:street_thirdstreet", "ba:street_secondstreet", "ba:street_firststreet" };
        for (int k = 0; k <= 8; k++)
        {
            LayoutStreet s = LayoutStreets.Street(k);
            Check(s.Continues == game[k] && s.FirstNumber == 1001, "uptown streets: street " + k + " continues the game's " + game[k] + " with numbers from 1001");
        }
        Check(all.Where(s => s.Continues == null).All(s => s.FirstNumber == 1), "uptown streets: new streets and the avenues number from 1");
        Check(LayoutStreets.ContinuedFirst > 82 * 10, "uptown streets: a continued street's numbers never meet the game's (its highest is 82)");
        for (int r = 1; r <= 3; r++)
        {
            Check(LayoutStreets.Avenue(r) != null && LayoutStreets.Avenue(r).Avenue && LayoutStreets.Avenue(r).Index == r, "uptown streets: avenue " + r + " found by its row");
        }
        // The game's own grid: Manhattan's street modules' middles at its north edge, and 1st Avenue's.
        float[] modules = { -301.67f, -214.37f, -127.07f, -39.77f, 47.53f, 134.83f, 222.13f, 309.43f, 396.73f };
        for (int k = 0; k <= 8; k++)
        {
            Check(Near(LayoutRules.StreetX(k), modules[k], 0.05f), "uptown streets: street " + k + " stands on the game's own module at x " + modules[k]);
        }
        Check(Near(LayoutRules.AvenueZ(0), 464.58f, 0.01f) && Near(LayoutRules.AvenueZ(1), 611.78f, 0.01f), "uptown streets: 1st Avenue as the game has it, Uptown Avenue a street module and a crossing north");
        Check(Near(LayoutRules.StreetModule + 2f * LayoutRules.CrossingHalfZ, LayoutRules.AvenuePitch, 0.01f)
              && Near(LayoutRules.AvenueModule + 2f * LayoutRules.CrossingHalfX, LayoutRules.StreetPitch, 0.01f),
            "uptown streets: the game's modules tile the grid exactly, crossings and streets, crossings and avenues");
    }

    // ---------------------------------------------------------------- the lots

    private static void UptownLotChecks(bool print)
    {
        List<Lot> lots = LayoutRules.Lots;
        List<Lot> first = LayoutRules.FirstLots;
        int[] all = new int[4], firstMix = new int[4];
        foreach (Lot l in lots)
        {
            all[(int)l.Class]++;
        }
        foreach (Lot l in first)
        {
            firstMix[(int)l.Class]++;
        }
        if (print)
        {
            Console.WriteLine("uptown lots: " + lots.Count + " in all (" + string.Join(", ", all) + "), the first block " + first.Count + " (" + string.Join(", ", firstMix) + ")");
        }
        Check(lots.Count >= 400 && lots.Count <= 460, "uptown lots: about 430 in the band (" + lots.Count + ")");
        Check(first.Count >= 80 && first.Count <= 100, "uptown lots: a first block of 80 to 100 (" + first.Count + ")");
        for (int c = 0; c < 4; c++)
        {
            float share = 100f * all[c] / lots.Count, want = 100f * LayoutRules.Share((LotClass)c) / 98f;
            Check(Math.Abs(share - want) <= 1.5f, "uptown lots: " + (LotClass)c + " " + share.ToString("0.0") + "% of the band, Manhattan's " + want.ToString("0.0") + "%");
        }
        Check(firstMix[0] < first.Count && firstMix[1] + firstMix[2] + firstMix[3] >= first.Count / 8, "uptown lots: the first block holds offices, big stores and a warehouse as well as small lots");
        var id = new Regex(@"^uptown_[1-3]_(0[1-9]|1[0-8])_\d\d$");
        Check(lots.All(l => id.IsMatch(l.Id)) && lots.Select(l => l.Id).Distinct().Count() == lots.Count, "uptown lots: every id of the form uptown_<row>_<column>_<n>, letters, digits and underscores, none twice");
        Check(lots.All(l => UptownLots.Get(l.Id) == l) && UptownLots.Get("uptown_9_99_99") == null && UptownLots.Get(null) == null, "uptown lots: each found by its id, nothing else");
        Check(lots.SequenceEqual(lots.OrderBy(l => l.Row).ThenBy(l => l.Column)) && lots.All(l => l.Block == l.Row * 100 + l.Column), "uptown lots: in id order, row by row, the block its row and column");
        Check(first.All(l => l.First && l.Row == 1 && l.Column >= 6 && l.Column <= 13) && lots.Count(l => l.First) == first.Count, "uptown lots: the first block is row 1 between 9th and 1st Street");
        foreach (Lot l in lots)
        {
            LayoutRules.Footprint(l.Class, out int w, out int d);
            bool ok = l.Width == w && l.Depth == d && l.Yaw % 90 == 0 && Near(l.Facing, (l.Yaw + 180) % 360);
            LayoutRules.Block(l.Row, l.Column, out float x0, out float z0, out float x1, out float z1);
            Corners(l, out float lx0, out float lz0, out float lx1, out float lz1);
            ok &= lx0 >= x0 - 0.01f && lx1 <= x1 + 0.01f && lz0 >= z0 - 0.01f && lz1 <= z1 + 0.01f;
            // Its front on its street's line, facing it.
            float front = l.Facing == 0f ? lz1 : l.Facing == 180f ? lz0 : l.Facing == 90f ? lx1 : lx0;
            float edge = l.Facing == 0f ? z1 : l.Facing == 180f ? z0 : l.Facing == 90f ? x1 : x0;
            ok &= Near(front, edge, 0.02f);
            LayoutStreet street = LayoutStreets.Find(l.Street);
            ok &= street != null && (l.Facing == 0f || l.Facing == 180f ? street.Avenue && street.Index == (l.Facing == 0f ? l.Row : l.Row - 1)
                : !street.Avenue && street.Index == LayoutRules.WestStreet(l.Column) + (l.Facing == 90f ? 1 : 0));
            Check(ok, "uptown lots: " + l.Id + " (" + l.Class + ") has its class's footprint, inside its block, its front on the street it names");
        }
        // Clear of one another: two lots never closer than the gap a made building keeps.
        var byBlock = lots.GroupBy(l => l.Block).ToList();
        int clashes = 0;
        foreach (var block in byBlock)
        {
            Lot[] b = block.ToArray();
            for (int i = 0; i < b.Length; i++)
            {
                for (int j = i + 1; j < b.Length; j++)
                {
                    clashes += Gap(b[i], b[j]) < LayoutRules.LotGap - 0.01f ? 1 : 0;
                }
            }
        }
        Check(clashes == 0, "uptown lots: no two lots of a block closer than " + LayoutRules.LotGap + " m (" + clashes + ")");
        // Clear of the streets: inside a block, so off every street, avenue and crossing module.
        Check(lots.All(l => OffStreets(l)), "uptown lots: none on a street, an avenue or a crossing");
        // Numbers: odd on one side, even on the other, from the street's first, along it; every address once.
        Check(lots.Select(l => l.Street + "#" + l.Number).Distinct().Count() == lots.Count, "uptown lots: every address once");
        foreach (var side in lots.GroupBy(l => l.Street + (l.Number % 2)))
        {
            Lot[] s = side.OrderBy(l => l.Order).ToArray();
            LayoutStreet street = LayoutStreets.Find(s[0].Street);
            bool ok = s.Select((l, i) => l.Order == i && l.Number == s[0].Number + 2 * i).All(x => x);
            ok &= s[0].Number == street.FirstNumber || s[0].Number == street.FirstNumber + 1;
            Check(ok, "uptown lots: the numbers along " + s[0].Street + " (" + (s[0].Number % 2 == 1 ? "odd" : "even") + " side) run from its first, two apart, in order");
        }
        Check(lots.Where(l => LayoutStreets.Find(l.Street).Continues != null).All(l => l.Number > 1000), "uptown lots: on a continued street every number is over 1000");
        Check(first.All(l => UptownLots.Open(l) == UptownLots.Ready) && lots.Where(l => !l.First).All(l => !UptownLots.Open(l)), "uptown lots: only the first block's open, and only while the ground stands");
        Check(UptownLots.SizesOf(LotClass.Small).SequenceEqual(new[] { "ba:buildingsize_a", "ba:buildingsize_b", "ba:buildingsize_c", "ba:buildingsize_f" })
              && UptownLots.SizesOf(LotClass.Office).SequenceEqual(new[] { "ba:buildingsize_d", "ba:buildingsize_j", "ba:buildingsize_k" })
              && UptownLots.SizesOf(LotClass.Big).SequenceEqual(new[] { "ba:buildingsize_m", "ba:buildingsize_l", "ba:buildingsize_n" })
              && UptownLots.SizesOf(LotClass.Warehouse).SequenceEqual(new[] { "ba:buildingsize_h", "ba:buildingsize_i", "ba:buildingsize_p", "ba:buildingsize_q" }),
            "uptown lots: each class holds the game's sizes places.md counts in it");
        if (print)
        {
            var rows = lots.Select(l => new { l.Id, l.Row, l.Column, l.First, l.Street, l.Number, l.X, l.Z, l.Yaw, l.Width, l.Depth, Class = l.Class.ToString() });
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "uptown_lots.json"), JsonSerializer.Serialize(rows));
            Console.WriteLine("uptown lots written to " + Path.Combine(AppContext.BaseDirectory, "uptown_lots.json"));
        }
    }

    // A lot's corners on the ground (its footprint is square to the grid).
    private static void Corners(Lot l, out float x0, out float z0, out float x1, out float z1)
    {
        bool along = l.Facing == 0f || l.Facing == 180f;
        float hx = (along ? l.Width : l.Depth) * 0.5f, hz = (along ? l.Depth : l.Width) * 0.5f;
        x0 = l.X - hx;
        x1 = l.X + hx;
        z0 = l.Z - hz;
        z1 = l.Z + hz;
    }

    private static float Gap(Lot a, Lot b)
    {
        Corners(a, out float ax0, out float az0, out float ax1, out float az1);
        Corners(b, out float bx0, out float bz0, out float bx1, out float bz1);
        float dx = Math.Max(0f, Math.Max(bx0 - ax1, ax0 - bx1)), dz = Math.Max(0f, Math.Max(bz0 - az1, az0 - bz1));
        return (float)Math.Sqrt(dx * dx + dz * dz);
    }

    private static bool OffStreets(Lot l)
    {
        Corners(l, out float x0, out float z0, out float x1, out float z1);
        for (int k = LayoutStreets.WestMost; k <= LayoutStreets.EastMost; k++)
        {
            float c = LayoutRules.StreetX(k);
            if (x1 > c - LayoutRules.StreetHalf + 0.01f && x0 < c + LayoutRules.StreetHalf - 0.01f)
            {
                return false;
            }
        }
        for (int r = 0; r <= 3; r++)
        {
            float a = LayoutRules.AvenueZ(r);
            if (z1 > a - LayoutRules.AvenueHalf + 0.01f && z0 < a + LayoutRules.AvenueHalf - 0.01f)
            {
                return false;
            }
        }
        return true;
    }

    // ---------------------------------------------------------------- the first block's own ground and the way out

    private static void UptownGroundChecks()
    {
        List<GroundRect> ground = LayoutRules.FirstGround;
        Check(ground.Count == 18 && ReferenceEquals(UptownLots.Ground, ground), "uptown ground: eight blocks, nine streets and the avenue's band (" + ground.Count + ")");
        Check(LayoutRules.FirstLots.All(l => LayoutRules.OnFirstGround(l.X, l.Z)), "uptown ground: every first lot's middle on it");
        // The game's own buildings north of 1st Avenue and its doors there are never the district's ground (Build 3682).
        float[][] doors = { new[] { 373.1f, 478.9f }, new[] { 266.8f, 479.5f }, new[] { 92.0f, 479f }, new[] { 2f, 478f }, new[] { -94f, 478f }, new[] { -175f, 478f }, new[] { -272f, 478f } };
        Check(doors.All(d => !LayoutRules.OnFirstGround(d[0], d[1])), "uptown ground: 1st Avenue's north side and its doors stay the game's");
        Check(ground.All(r => r.Z1 >= LayoutRules.OldWallZ - 0.01f && r.X1 >= -330f && r.X2 <= 430f && r.Z2 <= 640f), "uptown ground: north of the old wall, inside the band's first row");
        UptownExit exit = UptownLots.Exit;
        Check(!LayoutRules.OnFirstGround(exit.X, exit.Z) && exit.Z > 472.1f && exit.Z < 474.5f && exit.X > 66.2f && exit.X < 116.2f && exit.Heading == 270f
              && exit.District == "ba:neighborhood_midtown", "uptown ground: the way out is a parking place on 1st Avenue's north side, heading west, in Midtown");
    }

    // ---------------------------------------------------------------- the streets as lanes, through 1st Avenue's junctions

    // A model of 1st Avenue's junction where street k meets it, from the game's baked lanes (Build 3682): its stop points
    // (the avenue's four in way 0, the street's northbound in way 1) and its exits, the corners as the game has them.
    private static void GameJunction(PlanThrough through, int k, int firstIndex)
    {
        float c = LayoutRules.StreetX(k);
        through.Stops.Clear();
        through.Exits.Clear();
        through.Existing = 20 + k;
        int index = firstIndex;
        GamePoint P(float x, float z, float dx, float dz, int way, string lane) => new GamePoint
        {
            Index = index++, X = x, Y = 0f, Z = z, Dx = dx, Dz = dz, Way = way, RoadId = 40 + k, Lane = lane, Speed = 50, Cars = true, Trucks = true
        };
        if (k == 0)
        {
            // The north-west corner: 1st Avenue's westbound turns south into 9th Street, 9th Street's northbound east.
            through.Stops.Add(P(c + 14.9f, 466.7f, -1f, 0f, 0, "Road_59-Lane_1"));
            through.Stops.Add(P(c + 14.9f, 470.7f, -1f, 0f, 0, "Road_59-Lane_3"));
            through.Stops.Add(P(c + 3.2f, 444.9f, 0f, 1f, 1, "Road_66-Lane_0"));
            through.Exits.Add(P(c - 0.8f, 446.4f, 0f, -1f, -1, "Road_66-Lane_1"));
            through.Exits.Add(P(c + 13.5f, 462.7f, 1f, 0f, -1, "Road_59-Lane_0"));
            through.Exits.Add(P(c + 13.5f, 458.7f, 1f, 0f, -1, "Road_59-Lane_2"));
            return;
        }
        if (k == 8)
        {
            // The north-east corner: 1st Street's northbound turns west into 1st Avenue; the eastbound turns south before it.
            through.Stops.Add(P(c + 3.1f, 445.1f, 0f, 1f, 0, "Road_42-Lane_1"));
            through.Exits.Add(P(c - 0.7f, 446.5f, 0f, -1f, -1, "Road_42-Lane_0"));
            through.Exits.Add(P(c - 12.9f, 466.3f, -1f, 0f, -1, "Road_23-Lane_0"));
            through.Exits.Add(P(c - 12.9f, 470.3f, -1f, 0f, -1, "Road_23-Lane_1"));
            return;
        }
        through.Stops.Add(P(c - 14.85f, 462.6f, 1f, 0f, 0, "Road_35-Lane_0"));
        through.Stops.Add(P(c - 14.85f, 458.6f, 1f, 0f, 0, "Road_35-Lane_2"));
        through.Stops.Add(P(c + 14.85f, 470.6f, -1f, 0f, 0, "Road_29-Lane_3"));
        through.Stops.Add(P(c + 14.85f, 466.6f, -1f, 0f, 0, "Road_29-Lane_1"));
        through.Stops.Add(P(c + 3.15f, 444.9f, 0f, 1f, 1, "Road_69-Lane_0"));
        through.Exits.Add(P(c - 0.85f, 446.4f, 0f, -1f, -1, "Road_69-Lane_1"));
        through.Exits.Add(P(c + 13.45f, 462.6f, 1f, 0f, -1, "Road_29-Lane_0"));
        through.Exits.Add(P(c + 13.45f, 458.6f, 1f, 0f, -1, "Road_29-Lane_2"));
        through.Exits.Add(P(c - 13.35f, 470.6f, -1f, 0f, -1, "Road_35-Lane_3"));
        through.Exits.Add(P(c - 13.35f, 466.6f, -1f, 0f, -1, "Road_35-Lane_1"));
    }

    private const int GamePoints = 18791;   // the game's lane points in Build 3682; the models' points stand for some of them

    private static RoadBuild UptownBuild(out RoadPlan plan)
    {
        plan = LayoutPlans.Streets();
        int index = 100;
        foreach (PlanThrough through in plan.Throughs)
        {
            int k = (int)Math.Round((through.X - LayoutRules.StreetFirstX) / LayoutRules.StreetPitch);
            GameJunction(through, k, index);
            index += 20;
        }
        return RoadRules.Build(plan, GameGrid, GamePoints, 300);
    }

    private static void UptownRoadChecks(bool print)
    {
        RoadBuild build = UptownBuild(out RoadPlan plan);
        Check(build.Problem == null, "uptown streets: the first block's plan builds (" + build.Problem + ")");
        string check = RoadRules.Check(build, GameGrid, GamePoints, 299);
        Check(check == null, "uptown streets: the build is sound (" + check + ")");
        if (build.Problem != null)
        {
            return;
        }
        if (print)
        {
            Console.WriteLine("uptown streets: " + RoadRules.Words(build));
            var modules = new List<ModulePlace>();
            LayoutPlans.Modules(modules);
            var walls = new List<PlanBox>();
            LayoutPlans.NewWalls(walls);
            var hides = new List<GroundRect>();
            LayoutPlans.Hides(hides);
            var zones = new List<PlanBox>();
            LayoutPlans.Zones(zones);
            object Boxes(GroundPlan p) => p.Boxes.Select(b => new[] { b.X - b.HalfX, b.Z - b.HalfZ, b.X + b.HalfX, b.Z + b.HalfZ }).ToArray();
            var dump = new
            {
                Points = build.Points.Select(p => new[] { p.X, p.Z, p.Through ? 1f : 0f }).ToArray(),
                Player = Boxes(LayoutPlans.PlayerGround()),
                Npc = Boxes(LayoutPlans.NpcGround()),
                Walls = walls.Select(w => new[] { w.X - w.HalfX, w.Z - w.HalfZ, w.X + w.HalfX, w.Z + w.HalfZ }).ToArray(),
                Hides = hides.Select(h => new[] { h.X1, h.Z1, h.X2, h.Z2 }).ToArray(),
                Zones = zones.Select(w => new[] { w.X - w.HalfX, w.Z - w.HalfZ, w.X + w.HalfX, w.Z + w.HalfZ }).ToArray(),
                Modules = modules.Select(m => new[] { (float)m.Kind, m.X, m.Z }).ToArray(),
                Ground = LayoutRules.FirstGround.Select(h => new[] { h.X1, h.Z1, h.X2, h.Z2 }).ToArray()
            };
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "uptown_plans.json"), JsonSerializer.Serialize(dump));
        }
        Check(plan.Roads.Count == 15 && plan.Junctions.Count == 8 && plan.Throughs.Count == 8, "uptown streets: eight continued streets, seven stretches of Uptown Avenue, eight junctions");
        Check(!LayoutPlans.HasLanes(4) && LayoutPlans.JunctionAt(4) == -1 && Enumerable.Range(0, 9).Where(k => k != 4).All(LayoutPlans.HasLanes),
            "uptown streets: every street but 5th Street (no junction of 1st Avenue's there) has lanes");
        // Through 1st Avenue's junctions: each street's lane north an exit of it, its lane south a stop point in the street's way.
        Check(build.Through.Count == 16 && build.Through.Count(t => t.Way >= 0) == 8, "uptown streets: eight exits and eight stop points given to 1st Avenue's junctions");
        foreach (ThroughEdit t in build.Through)
        {
            LanePoint p = build.At(t.Point);
            Check(p != null && p.Name.EndsWith(t.Way >= 0 ? "-Out" : "-In") && Near(p.Z, LayoutPlans.ThroughZ, 0.1f),
                "uptown streets: " + (t.Way >= 0 ? "a stop point" : "an exit") + " given to junction " + t.Junction + " is the street's own end there");
        }
        // Its way is the one of the street's own northbound lane (way 1 at a T, way 1 at the north-west corner, way 0 at the north-east).
        foreach (PlanThrough through in plan.Throughs)
        {
            int street = through.Existing - 20;
            int way = build.Through.First(t => t.Junction == through.Existing && t.Way >= 0).Way;
            Check(way == (street == 8 ? 0 : 1), "uptown streets: the street south stops on the green of the street north at junction " + through.Existing + " (way " + way + ")");
        }
        // The turns through each junction: from the game's stop points, named after the game's lanes and keeping their road ids.
        List<LanePoint> turns = build.Points.Where(p => p.Keeps >= 0).ToList();
        Check(turns.Count > 0 && turns.All(p => p.Through && p.RoadId == p.Keeps && Regex.IsMatch(p.Name, @"^Road_\d+-Lane_\d+-(CConnect|Connector)\d+$")),
            "uptown streets: turns from the game's stop points keep its lane's name and road id, as its own turns do");
        foreach (PlanThrough through in plan.Throughs)
        {
            int street = through.Existing - 20;
            foreach (GamePoint stop in through.Stops)
            {
                bool turnsOut = build.Edits.Any(e => e.Existing == stop.Index && e.AsNext);
                // Straight on from the street; right from the avenue's kerb lane, left from its middle lane, as the game's own turns.
                bool expected = street == 0 ? stop.Lane != "Road_59-Lane_1" : street == 8 || (stop.Lane != "Road_35-Lane_2" && stop.Lane != "Road_29-Lane_1");
                Check(turnsOut == expected, "uptown streets: at junction " + through.Existing + " the game's " + stop.Lane + (expected ? " turns into the new street" : " goes on as it did"));
            }
            foreach (GamePoint exit in through.Exits)
            {
                bool reached = build.Edits.Any(e => e.Existing == exit.Index && !e.AsNext);
                bool expected = street == 0 ? exit.Lane != "Road_59-Lane_2" : street == 8 ? exit.Lane != "Road_23-Lane_0" : exit.Lane != "Road_29-Lane_2" && exit.Lane != "Road_35-Lane_1";
                Check(reached == expected, "uptown streets: at junction " + through.Existing + " the street south " + (expected ? "turns into" : "leaves alone") + " the game's " + exit.Lane);
            }
        }
        // Uptown Avenue's junctions: the avenue's two ways on one green, the street's on the other.
        Check(build.Junctions.Count == 8 && build.Junctions.All(j => j.Lights && j.Ways.Count == 2), "uptown streets: each junction on Uptown Avenue has lights and two greens");
        Check(build.Points.All(p => GameGrid.Inside(p.X, p.Z, RoadRules.EdgeMargin)), "uptown streets: never a lane point off the road grid");
        Check(build.Points.Where(p => !p.Through).All(p => p.Z >= LayoutPlans.ThroughZ - 0.01f && p.Z <= LayoutRules.AvenueZ(1) + 8f),
            "uptown streets: the new lanes stay on the first block's ground, north of 1st Avenue's junctions");
        // The game's lanes round 1st Avenue never clash with the new streets but at the junctions.
        var gx = new List<float>();
        var gz = new List<float>();
        for (float x = -320f; x <= 420f; x += 4.8f)
        {
            foreach (float z in new[] { 458.6f, 462.6f, 466.6f, 470.6f })
            {
                gx.Add(x);
                gz.Add(z);
            }
        }
        Check(RoadRules.Clear(build, Enumerable.Range(0, gx.Count).ToArray(), gx.ToArray(), gz.ToArray()) == null,
            "uptown streets: 1st Avenue's own lanes are beside the new streets only where they go through its junctions");
        string clash = RoadRules.Clear(build, new[] { 9999 }, new[] { LayoutRules.StreetX(2) + 3.1f }, new[] { 540f });
        Check(clash != null && clash.Contains("9999"), "uptown streets: a road of the game's where a new street goes is found (a free update built there): " + clash);
        // The same plan twice gives the same build.
        RoadBuild again = UptownBuild(out _);
        Check(again.Count == build.Count && again.Points.Select((p, i) => p.Name == build.Points[i].Name && p.X == build.Points[i].X && p.Z == build.Points[i].Z
                  && p.Next.SequenceEqual(build.Points[i].Next)).All(s => s) && again.Edits.Count == build.Edits.Count,
            "uptown streets: the same build every time, on every machine");
        // A junction of 1st Avenue's not found at this load: the streets stand down, nothing built.
        RoadPlan missing = LayoutPlans.Streets();
        foreach (PlanThrough through in missing.Throughs)
        {
            through.Existing = -1;
        }
        Check((RoadRules.Build(missing, GameGrid, GamePoints, 300).Problem ?? string.Empty).Contains("found no junction"), "uptown streets: no junction of the game's found, the streets stand down");
    }

    // ---------------------------------------------------------------- the walking ground

    private static void UptownWalkChecks()
    {
        GroundPlan player = LayoutPlans.PlayerGround(), npc = LayoutPlans.NpcGround();
        Check(player.Player && !player.Npc && !npc.Player && npc.Npc && player.ExpectColliders == 0 && npc.ExpectColliders == 0,
            "uptown walking: one plan for the player's mesh and one for the pedestrians', nothing of the game's on either");
        bool Covered(GroundPlan plan, float x, float z) => plan.Boxes.Any(b => b.Covers(x, z, 0.01f));
        // The made buildings' ground check: the player's mesh under a lot's middle, its corners a metre in, and its door.
        foreach (Lot l in LayoutRules.FirstLots)
        {
            Corners(l, out float x0, out float z0, out float x1, out float z1);
            bool under = Covered(player, l.X, l.Z) && Covered(player, x0 + 1f, z0 + 1f) && Covered(player, x1 - 1f, z0 + 1f) && Covered(player, x0 + 1f, z1 - 1f)
                         && Covered(player, x1 - 1f, z1 - 1f);
            float dx = l.Facing == 90f ? 1f : l.Facing == 270f ? -1f : 0f, dz = l.Facing == 0f ? 1f : l.Facing == 180f ? -1f : 0f;
            float doorX = l.X + dx * l.Depth * 0.5f, doorZ = l.Z + dz * l.Depth * 0.5f;
            bool door = Covered(player, doorX + dx * 1f, doorZ + dz * 1f) && Covered(npc, doorX + dx * 1f, doorZ + dz * 1f);
            Check(under && door, "uptown walking: " + l.Id + " stands on the player's ground, and both meshes reach its door");
        }
        // Joined to Manhattan: every street's carriageway (the player's) and east strip (the pedestrians') begins over the old dead end.
        for (int k = 0; k <= 8; k++)
        {
            float c = LayoutRules.StreetX(k);
            Check(Covered(player, c, 477f) && Covered(player, c, 560f) && Covered(npc, c + 7.5f, 480f) && Covered(npc, c + 7.5f, 530f) && Covered(npc, c + 7.5f, 597f),
                "uptown walking: street " + k + " joins Manhattan's ground for both meshes, up to Uptown Avenue");
        }
        Check(!Covered(npc, LayoutRules.StreetX(2) + 1f, 540f) && !Covered(npc, LayoutRules.FirstLots[0].X, LayoutRules.FirstLots[0].Z),
            "uptown walking: the pedestrians keep to the pavements and crosswalks, off the carriageways and the lots");
        Check(player.Boxes.Concat(npc.Boxes).All(b => { b.Extent(out _, out float bz0, out _, out float bz1); return bz0 >= LayoutPlans.ArmFrom - 0.01f && bz1 <= 626f; }),
            "uptown walking: every box north of 1st Avenue's road, none past Uptown Avenue's north pavement");
        Check(player.Boxes.Concat(npc.Boxes).All(b => Near(b.Top, LayoutPlans.GroundTop) && b.HalfX > 0.2f && b.HalfZ > 0.2f), "uptown walking: every box flat at the ground's height, none a sliver");
        // The game's own colliders measured on the north arms' pavements (Build 3682) are never under a box, nor the
        // backdrop's buildings east of 1st Street, which stay (their walls stand 0.3 m onto its east pavement).
        float[][] game =
        {
            new[] { -309.0f, -307.8f, 478.9f, 479.9f }, new[] { -47.3f, -46.7f, 480.9f, 503.7f }, new[] { 127.4f, 128.8f, 479.0f, 480.2f }, new[] { 140.0f, 141.8f, 482.1f, 483.4f },
            new[] { 127.3f, 127.8f, 485.2f, 508.9f }, new[] { 214.3f, 214.5f, 480.0f, 506.8f }, new[] { 215.2f, 217.0f, 482.2f, 483.5f }, new[] { 227.3f, 229.1f, 484.5f, 485.8f },
            new[] { 302.5f, 304.3f, 484.6f, 485.9f }, new[] { 314.6f, 316.4f, 482.1f, 483.4f }, new[] { 386.7f, 389.6f, 478.2f, 508.9f }, new[] { 401.9f, 403.7f, 482.1f, 483.4f },
            new[] { -205.8f, -155.8f, 478.5f, 508.5f }, new[] { 405.2f, 431.4f, 478.0f, 500.2f }, new[] { 405.1f, 434.0f, 500.0f, 523.9f }
        };
        foreach (float[] g in game)
        {
            bool touched = player.Boxes.Concat(npc.Boxes).Any(b =>
            {
                b.Extent(out float x0, out float z0, out float x1, out float z1);
                return x1 > g[0] && x0 < g[1] && z1 > g[2] && z0 < g[3];
            });
            Check(!touched, "uptown walking: the game's own collider at x " + g[0] + " to " + g[1] + ", z " + g[2] + " to " + g[3] + " is under no box");
        }
    }

    // ---------------------------------------------------------------- the walls, the colliders, the hiding, the people and the buses

    private static void UptownPlaceChecks()
    {
        var modules = new List<ModulePlace>();
        LayoutPlans.Modules(modules);
        Check(modules.Count(m => m.Kind == ModuleKind.Street) == 9 && modules.Count(m => m.Kind == ModuleKind.Crossing) == 9 && modules.Count(m => m.Kind == ModuleKind.Avenue) == 8,
            "uptown art: nine streets, nine crossings and eight lengths of avenue of the game's own modules");
        Check(modules.Where(m => m.Kind == ModuleKind.Street).All(m => Near(m.Z - LayoutRules.StreetModule * 0.5f, LayoutRules.FirstAvenueZ + LayoutRules.CrossingHalfZ, 0.01f)
                                                                       && Near(m.Z + LayoutRules.StreetModule * 0.5f, LayoutRules.AvenueZ(1) - LayoutRules.CrossingHalfZ, 0.01f)),
            "uptown art: each street module fills its street from 1st Avenue's crossing to Uptown Avenue's");
        Check(modules.Where(m => m.Kind == ModuleKind.Crossing).All(m => m.Junction == LayoutPlans.JunctionAt(m.Street)) && modules.Count(m => m.Junction >= 0) == 8,
            "uptown art: every crossing but 5th Street's is one of the plan's junctions, its lights theirs");
        var walls = new List<PlanBox>();
        LayoutPlans.NewWalls(walls);
        GroundPlan player = LayoutPlans.PlayerGround();
        Check(walls.Count == 3 && walls.All(w => Near(w.Top, LayoutPlans.WallHigh)), "uptown edge: three new walls of the game's height round the first block");
        Check(walls.All(w => player.Boxes.All(b =>
        {
            b.Extent(out float x0, out float z0, out float x1, out float z1);
            return !(x1 > w.X - w.HalfX + 0.01f && x0 < w.X + w.HalfX - 0.01f && z1 > w.Z - w.HalfZ + 0.01f && z0 < w.Z + w.HalfZ - 0.01f);
        })), "uptown edge: the walls stand off the walking ground, round its edge");
        var old = new List<float>();
        LayoutPlans.OldWalls(old, out float oldZ);
        float[] gameWalls = { -299f, -212f, -123f, -37f, 50f, 137f, 224f, 310f, 397f };   // the game's walls across the street ends (Build 3682)
        Check(old.Count == 9 && gameWalls.All(w => old.Any(x => Math.Abs(x - w) <= LayoutPlans.OldWallReach)) && Math.Abs(oldZ - 483f) < 1f,
            "uptown edge: the nine old walls across the street ends are the ones opened");
        LayoutPlans.Colliders(out PlanBox ground, new List<PlanBox>());
        var roads = new List<PlanBox>();
        LayoutPlans.Colliders(out _, roads);
        Check(LayoutRules.FirstLots.All(l => ground.Holds(l.X, l.Z, 0f)) && roads.Count == 10 && Near(ground.Top, UptownLots.GroundY),
            "uptown ground: a ground collider under the whole first block at the city's height, and one under each of ten carriageways");
        var hides = new List<GroundRect>();
        LayoutPlans.Hides(hides);
        Check(hides.Count == 10 && hides.All(h => h.Z1 >= LayoutPlans.HideFrom - 0.01f) && !hides.Any(h => h.Holds(LayoutRules.StreetX(0), 700f)),
            "uptown hiding: the dead ends and the first block's main area, never 1st Avenue's road nor the rows beyond");
        Check(!hides.Any(h => h.Holds(-345f, 611.4f)) && !hides.Any(h => h.Holds(LayoutRules.StreetX(3), 685f)) && hides.Any(h => h.Holds(LayoutRules.StreetX(3), 538f))
              && hides.Any(h => h.Holds(LayoutRules.StreetX(3) + 43.65f, 611.4f)), "uptown hiding: the backdrop's own street north of Uptown Avenue and its avenue west of 9th Street stay; those on the first block go");
        var zones = new List<PlanBox>();
        LayoutPlans.Zones(zones);
        GroundPlan npc = LayoutPlans.NpcGround();
        Check(zones.Count == 17 && zones.All(z => npc.Boxes.Any(b => b.Covers(z.X, z.Z, 0.01f)) && npc.Boxes.Any(b => b.Covers(z.X - z.HalfX + 0.1f, z.Z - z.HalfZ + 0.1f, 0.01f))
                                                    && npc.Boxes.Any(b => b.Covers(z.X + z.HalfX - 0.1f, z.Z + z.HalfZ - 0.1f, 0.01f))),
            "uptown people: seventeen zones, each on the pedestrians' pavements");
        LayoutPlans.Terminus(out float tx, out float tz);
        Check(npc.Boxes.Any(b => b.Covers(tx, tz, 0.01f)) && Math.Abs(tz - (LayoutRules.AvenueZ(1) - 6f)) < 10f, "uptown buses: the terminus on Uptown Avenue's south pavement, beside its lanes");
        Check(LayoutRules.FirstLots.All(l => l.Z + 40f <= LayoutPlans.MapNorth), "uptown map: the city map's bounds reach 40 m past every first lot, as a made building needs");
    }

    // ---------------------------------------------------------------- what the census counts, and the lots' life at a load

    private static void UptownCountChecks()
    {
        var modules = new List<ModulePlace>();
        LayoutPlans.Modules(modules);
        var old = new List<float>();
        LayoutPlans.OldWalls(old, out _);
        var zones = new List<PlanBox>();
        LayoutPlans.Zones(zones);
        Check(LayoutPlans.StreetCount == 9 && LayoutPlans.ModuleCount == modules.Count && LayoutPlans.OldWallCount == old.Count && LayoutPlans.ZoneCount == zones.Count,
            "uptown census: it expects the modules, walls and zones the plans lay (" + LayoutPlans.ModuleCount + ", " + LayoutPlans.OldWallCount + ", " + LayoutPlans.ZoneCount + ")");
        int streets = modules.Count(m => m.Kind == ModuleKind.Street), avenues = modules.Count(m => m.Kind == ModuleKind.Avenue);
        Check(LayoutPlans.ParkingCount == streets * LayoutPlans.StreetParking + avenues * LayoutPlans.AvenueParking && LayoutPlans.ParkingCount == 25,
            "uptown census: a parking lane along each copied street and two along each length of avenue (" + LayoutPlans.ParkingCount + ")");
        bool nearest = true;
        for (float x = -400f; x <= 500f; x += 2.5f)
        {
            int k = LayoutPlans.NearestLanes(x);
            float best = Enumerable.Range(0, 9).Where(LayoutPlans.HasLanes).Min(s => Math.Abs(LayoutRules.StreetX(s) - x));
            nearest &= LayoutPlans.HasLanes(k) && Near(Math.Abs(LayoutRules.StreetX(k) - x), best, 0.001f);
        }
        Check(nearest && LayoutPlans.NearestLanes(LayoutRules.StreetX(4)) != 4, "uptown census: a walk from Manhattan starts at the nearest street with lanes, never 5th Street");
    }

    private static void UptownLifeChecks()
    {
        int stamp = UptownLots.Stamp;
        UptownLots.Begin("the city is loading");
        Check(!UptownLots.Ready && !UptownLots.Closed && UptownLots.Why == "the city is loading" && !UptownLots.Open(LayoutRules.FirstLots[0]),
            "uptown lots: a load begins with nothing open and why");
        UptownLots.Laid(true, null);
        Check(UptownLots.Ready && UptownLots.Why == null && UptownLots.Stamp == stamp + 1 && UptownLots.Open(LayoutRules.FirstLots[0]) && !UptownLots.Open(LayoutRules.Lots.First(l => !l.First)),
            "uptown lots: laid, the first block opens and the stamp moves once");
        var heard = new List<string>();
        Action<string> first = why => heard.Add("first " + UptownLots.Ready);
        Action<string> faulty = why => throw new InvalidOperationException("a handler's fault");
        Action<string> last = why => heard.Add("last " + why);
        UptownLots.Closing += first;
        UptownLots.Closing += faulty;
        UptownLots.Closing += last;
        int faults = 0;
        UptownLots.Close("the test's clean-up", e => faults++);
        UptownLots.Close("again", e => faults++);
        UptownLots.Closing -= first;
        UptownLots.Closing -= faulty;
        UptownLots.Closing -= last;
        Check(heard.Count == 2 && heard[0] == "first True" && heard[1] == "last the test's clean-up" && faults == 1,
            "uptown lots: closing tells every handler first, while the lots are still open, a faulty one apart, and only once");
        Check(UptownLots.Closed && !UptownLots.Ready && UptownLots.Why.Contains("the test's clean-up") && UptownLots.Stamp == stamp + 2 && !UptownLots.Open(LayoutRules.FirstLots[0]),
            "uptown lots: closed until the next load, with why, the stamp moved again");
        UptownLots.Begin("the city is loading");
        Check(!UptownLots.Closed && !UptownLots.Ready, "uptown lots: the next load opens the way again");
        UptownLots.Laid(false, null);
        Check(!UptownLots.Ready && UptownLots.Why != null, "uptown lots: a step that stood down without a reason still says something");
    }

    // ---------------------------------------------------------------- the cost

    private static void UptownTimingChecks()
    {
        var watch = Stopwatch.StartNew();
        RoadPlan plan = LayoutPlans.Streets();
        GroundPlan player = LayoutPlans.PlayerGround(), npc = LayoutPlans.NpcGround();
        RoadBuild build = UptownBuild(out _);
        watch.Stop();
        Check(watch.Elapsed.TotalMilliseconds < 100.0 && plan.Roads.Count > 0 && player.Boxes.Count > 0 && npc.Boxes.Count > 0 && build.Problem == null,
            "uptown cost: the first block's plans and its streets' build in under 100 ms (" + watch.Elapsed.TotalMilliseconds.ToString("0.0") + " ms), once a load");
    }

    // ---------------------------------------------------------------- the sources

    // The folder holds the district's life too (Life*, DistrictValues, DistrictBuildings, BuildingsRules), which keeps rules
    // of its own and is checked by EconomyCheck's UptownLifeChecks: it seeds Unity's Random and gives it back, keeps the
    // district's own save line and touches the scene in its LifeGame files. These rules are the ground's.
    private static bool GroundsFile(string name)
    {
        return name.StartsWith("Layout", StringComparison.Ordinal) || name.StartsWith("Ground", StringComparison.Ordinal) || name == "UptownLots.cs";
    }

    private static void UptownSourceChecks(string scripts)
    {
        string folder = Path.Combine(scripts, "Uptown");
        string[] files = Directory.GetFiles(folder, "*.cs").Where(f => GroundsFile(Path.GetFileName(f))).ToArray();
        var code = files.ToDictionary(Path.GetFileName, f => Code(File.ReadAllText(f)));
        foreach (string name in new[] { "UptownLots.cs", "LayoutRules.cs", "LayoutPlans.cs", "LayoutStreets.cs" })
        {
            Check(code.ContainsKey(name), "uptown sources: " + name + " is there");
            if (code.TryGetValue(name, out string text))
            {
                Check(!Regex.IsMatch(text, @"\busing UnityEngine|\busing Gley|\bVector3\b|\bMathf\b|\bGameObject\b"), "uptown sources: " + name + " is game-free");
            }
        }
        foreach (var file in code)
        {
            Check(!Regex.IsMatch(file.Value, @"\bSystem\.Linq\b|using System\.Linq"), "uptown sources: no LINQ (" + file.Key + ")");
            Check(!Regex.IsMatch(file.Value, @"new System\.Random\(\)|\bUnityEngine\.Random\b|\bRandom\.Range\b|DateTime\.Now"), "uptown sources: nothing left to chance or the clock (" + file.Key + ")");
            Check(!Regex.IsMatch(file.Value, @"\bmodData\b|\bSaveHook\b|\bSend\("), "uptown sources: nothing saved or sent (" + file.Key + ")");
        }
    }

    // The game's side: the scene touched in the Game files alone, the load's order, what stands down gives back, the
    // session asked, the contract's line, the director's place in the city's load, the shared lines.
    private static void UptownGameSourceChecks(string scripts)
    {
        string folder = Path.Combine(scripts, "Uptown");
        var code = Directory.GetFiles(folder, "*.cs").Where(f => GroundsFile(Path.GetFileName(f))).ToDictionary(Path.GetFileName, f => Code(File.ReadAllText(f)));
        string[] mine = { "GroundGame.cs", "GroundGame.Art.cs", "GroundDirector.cs", "GroundDirector.Tests.cs", "GroundSession.cs", "GroundGuide.cs", "GroundText.cs" };
        foreach (string name in mine)
        {
            Check(code.ContainsKey(name), "uptown sources: " + name + " is there");
        }
        if (!mine.All(code.ContainsKey))
        {
            return;
        }
        var scene = new Regex(@"\b(?:Object\.Instantiate|Instantiate\(|SetActive\(|AddComponent<|GetComponentsInChildren<|SceneManager\.|DestroyImmediate|\.enabled = |\.bounds = |redTrafficLight)");
        foreach (var file in code)
        {
            bool gameFile = file.Key.StartsWith("GroundGame", StringComparison.Ordinal);
            Check(gameFile || !scene.IsMatch(file.Value), "uptown sources: " + file.Key + (gameFile ? " is a Game file" : " leaves the scene to the Game files"));
            Check(!Regex.IsMatch(file.Value, @"\bGetField\(|\bGetProperty\(|\bGetMethod\(|BindingFlags|MP(?:Server|Client|Config)"),
                "uptown sources: " + file.Key + " reads nothing by name; the game through the contract, Going Public through GoingPublic");
            Check(!Regex.IsMatch(file.Value, @"\bvoid\s+(?:Update|LateUpdate|FixedUpdate)\s*\(") || file.Key == "GroundDirector.cs",
                "uptown sources: only the director has a frame method (" + file.Key + ")");
        }
        string director = code["GroundDirector.cs"], tests = code["GroundDirector.Tests.cs"], art = code["GroundGame.Art.cs"], game = code["GroundGame.cs"];
        string session = code["GroundSession.cs"];
        int Order(string text, string what) => text.IndexOf(what, StringComparison.Ordinal);
        string[] steps =
        {
            "GroundSession.Agrees(", "GroundGame.FindBackdrop()", "GroundGame.FindTemplates()", "GroundGame.HideBackdrop(", "GroundGame.OpenWalls(",
            "GroundWork.Streets(", "GroundWork.Ground(LayoutPlans.PlayerGround())", "GroundWork.TakeBack(", "GroundWork.Ground(LayoutPlans.NpcGround())",
            "GroundGame.Build(", "GroundGame.CopyArt(", "GroundGame.WidenMap(", "TransitMap.AddTerminus(", "ScenePrewarm.AddLate(", "UptownLots.Laid(_walkable"
        };
        bool ordered = true;
        for (int i = 0; i < steps.Length; i++)
        {
            ordered &= Order(director, steps[i]) >= 0 && (i == 0 || Order(director, steps[i]) > Order(director, steps[i - 1]));
        }
        Check(ordered, "uptown load: asked, found, the backdrop and the old walls put away (the ground's check counts what is left), the streets, the player's ground "
                       + "(or the streets taken back), the pedestrians', the new walls and colliders, the art, the map, the terminus, the late parking and people, "
                       + "then the lots open; in that order");
        Check(Regex.Matches(director, @"StandDown\(""its streets: "" \+ why, true\)|StandDown\(""its ground for the player: "" \+ why, true\)").Count == 2
              && Regex.IsMatch(director, @"StandDown\(why, false\)") && Regex.IsMatch(director, @"if \(laid\)\s*\{\s*GroundGame\.GiveBack\(\);"),
            "uptown load: every step that stands down after the backdrop was put away gives it all back the same load, with one line");
        Check(Regex.IsMatch(director, @"private void Awake\(\)\s*\{[^}]*UptownLots\.Begin\([^}]*Lay\(\);") && director.Contains("UptownLots.Laid(false, stays ?"),
            "uptown lots: closed as a load begins, opened by the ground's step alone, closed again when the city goes");
        Check(Regex.IsMatch(session, @"if \(!Settings\.Uptown\)") && session.Contains("GoingPublic.RefusesOtherMods") && session.Contains("GoingPublic.ModsAsHost")
              && Order(session, "Settings.Uptown") < Order(session, "GoingPublic.IsHost"),
            "uptown session: the option first, then a session's host that refuses other mods and a guest whose mods are the host's");
        Check(code.Where(f => f.Key != "GroundSession.cs" && f.Key != "GroundGuide.cs").All(f => !f.Value.Contains("Settings.Uptown")),
            "uptown session: the option is read through the session's one question");
        Check(art.Contains("GameContract.RoadStreetName.Ok") && art.Contains("GameContract.RoadStreetName.Field.SetValue(") && code.Where(f => f.Key != "GroundGame.Art.cs").All(f => !f.Value.Contains("RoadStreetName")),
            "uptown sources: the street's label written through the contract's line, in the Game file");
        Check(Regex.IsMatch(art, @"_art = new GameObject\(ArtName\);\s*_art\.SetActive\(false\);") && Order(art, "_art.SetActive(true);") > Order(art, "Strip(copy);")
              && Order(art, "_art.SetActive(true);") > Order(art, "Wire(copy, build"),
            "uptown art: the copies made asleep and woken together once stripped and wired");
        Check(Regex.Matches(art, @"SceneFinds\.All<").Count == 2 && !Regex.IsMatch(game + art, @"FindObjects|GameObject\.Find\("), "uptown art: two searches of the scene, behind the loading screen, through SceneFinds");
        Check(game.Contains("ShowBackdrop();") && game.Contains("CloseWalls();") && Regex.IsMatch(game, @"public static void Forget\(\)\s*\{\s*DropArt\(\);"),
            "uptown sources: the backdrop and the walls given back on a stand-down, the mod's own pieces when the city goes");
        Check(tests.Contains("\"Uptown check (path, \"") && tests.Contains("\"Uptown check (traffic): ") && tests.Contains("\"Uptown check (census): ")
              && Regex.Matches(tests, @"TestRefusals\.(?:Tell|TellKey)\(").Count >= 6,
            "uptown tests: each says why it did not start and writes one check line");
        string mod = Code(File.ReadAllText(Path.Combine(scripts, "Core", "RealLifeOverhaulMod.cs")));
        int uptown = Order(mod, "Guard.Add<Uptown.GroundDirector>(_root)"), buildings = Order(mod, "Guard.Add<NewBuildings.NewBuildingsDirector>(_root)"),
            prewarm = Order(mod, "ScenePrewarm.Begin()"), ground = Order(mod, "Guard.Add<NewGround.GroundDirector>(_root)");
        Check(uptown > ground && uptown < buildings && buildings < prewarm, "uptown load: the director added after new ground's, before the new buildings' and the scene's prewarm");
        string cities = Code(File.ReadAllText(Path.Combine(scripts, "Meta", "FeatureCities.cs")));
        string tree = Code(File.ReadAllText(Path.Combine(scripts, "Meta", "OptionsTree.cs")));
        string streets = Code(File.ReadAllText(Path.Combine(scripts, "NewBuildings", "NewBuildingsRules.cs")));
        string contract = Code(File.ReadAllText(Path.Combine(scripts, "Core", "GameContract.cs")));
        Check(cities.Contains("new Line(\"uptown\", CityScope.NewYork,") && tree.Contains("S(\"uptown\").For(\"uptown\").Main(\"uptown\")") && streets.Contains("Uptown.LayoutStreets.All")
              && Regex.IsMatch(contract, @"RoadStreetName = Optional\(Field\(UptownLabels, Game, ""Road"", ""streetName"", ""String""\)\)"),
            "uptown sources: New York's alone, its section on the options page, its streets among the mod's own, the label's field an optional line of the contract");
    }

    // The texts: every key the folder names in the three languages, short, without dashes or exclamation marks; the street
    // names fixed words in every language; and What's new's line in all nine, before the first fix.
    private static void UptownTextChecks(string scripts)
    {
        string folder = Path.Combine(scripts, "Uptown");
        string locales = Path.GetFullPath(Path.Combine(scripts, @"..\Locales"));
        var named = Directory.GetFiles(folder, "Ground*.cs").SelectMany(f => Regex.Matches(File.ReadAllText(f), "\"(reallife_[a-z_]+)\"").Select(m => m.Groups[1].Value))
                             .Concat(new[] { "reallife_opt_sec_uptown", "reallife_opt_tip_uptown" }).Concat(LayoutStreets.All.Select(s => s.NameKey)).Distinct().ToList();
        Check(named.Count >= 35, "uptown texts: its texts are found: " + named.Count);
        foreach (string language in new[] { "en", "pt", "zh-cn" })
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(locales, language + ".json")));
            foreach (string key in named)
            {
                Check(doc.RootElement.TryGetProperty(key, out JsonElement value) && value.GetString().Length > 0, "uptown texts: " + key + " is in " + language + ".json");
                if (language == "en" && doc.RootElement.TryGetProperty(key, out JsonElement en) && key != "reallife_opt_tip_uptown")
                {
                    Check(en.GetString().Length <= 240, "uptown texts: " + key + " is at most 240 characters");
                    Check(!Regex.IsMatch(en.GetString(), "[–—!]| - "), "uptown texts: " + key + " has no dash and no exclamation mark");
                }
            }
            foreach (LayoutStreet s in LayoutStreets.All)
            {
                Check(doc.RootElement.TryGetProperty(s.NameKey, out JsonElement name) && Regex.IsMatch(name.GetString(), @"^(?:\d+(?:st|nd|rd|th)|[A-Z][a-z]+) (?:Street|Avenue)$"),
                    "uptown texts: " + s.Id + " is named as the game names its own streets, in " + language);
            }
        }
        var fixMark = new Dictionary<string, string>
        {
            { "en", "Fixed: " }, { "pt", "Corrigido: " }, { "zh-cn", "已修复：" }, { "de", "Behoben: " }, { "fr", "Corrigé : " }, { "es", "Corregido: " },
            { "ru", "Исправлено: " }, { "nl", "Opgelost: " }, { "tr", "Düzeltildi: " }
        };
        var piece = new Dictionary<string, string>
        {
            { "en", "Manhattan now goes on north past 1st Avenue into Uptown" }, { "pt", "Manhattan agora segue para o norte depois da 1st Avenue" },
            { "zh-cn", "曼哈顿现在越过 1st Avenue 继续向北延伸" }, { "de", "Manhattan geht jetzt hinter der 1st Avenue nach Norden weiter" },
            { "fr", "Manhattan se prolonge désormais vers le nord après la 1st Avenue" }, { "es", "Manhattan ahora sigue hacia el norte más allá de la 1st Avenue" },
            { "ru", "Манхэттен теперь продолжается на север за 1st Avenue" }, { "nl", "Manhattan loopt nu door naar het noorden, voorbij 1st Avenue" },
            { "tr", "Manhattan artık 1st Avenue'nun ötesinde kuzeye" }
        };
        foreach (var language in piece)
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(locales, language.Key + ".json")));
            string next = doc.RootElement.TryGetProperty("reallife_changes_next", out JsonElement row) ? row.GetString() : string.Empty;
            int at = next.IndexOf(language.Value, StringComparison.Ordinal), fix = next.IndexOf(fixMark[language.Key], StringComparison.Ordinal);
            Check(at >= 0 && (fix < 0 || at < fix), "uptown texts: What's new's next row tells of Uptown in " + language.Key + ", before its fixes");
        }
    }
}
