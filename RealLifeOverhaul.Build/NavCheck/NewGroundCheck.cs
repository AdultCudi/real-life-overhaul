using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul.NewGround;

// New ground (NewGround/RoadRules.cs, RoadPlan.cs, GroundRules.cs and the folder's sources). The road generator lays
// streets as the game's traffic system lays its own (read from its baked lanes): right-hand lanes named as the game
// names them, turns through junctions that keep the road they come from, lane changes a step ahead on the lane beside,
// junctions with lights or by priority, every point filed in its cell, the game's own points never renumbered, and never
// a point off the road grid: a point north or east of it would throw in the traffic's every frame. The walking rules:
// the tile grid, the boxes the tests lay, the samples and the lines. Over the sources: the game touched only through the
// folder's Game files, every member read by name a line of the contract, the road users reading the lane points through
// Core/RoadData (so appended roads reach them all), the texts in three languages.
internal static partial class Program
{
    // The game's own road grid in Build 3682, as test data: the mod reads it from the game at every load.
    private static readonly RoadGrid GameGrid = new RoadGrid(-4294.78f, -2511.64f, 80f, 46, 74);

    private static void NewGrounds()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        GroundGridChecks();
        GroundTownChecks();
        GroundBlockChecks();
        GroundOffGridChecks();
        GroundFaultChecks();
        GroundCopyChecks();
        GroundClearChecks();
        GroundTimingChecks();
        GroundWalkChecks();
        GroundLineChecks();
        GroundSourceChecks(scripts);
    }

    // ---------------------------------------------------------------- the road grid

    private static void GroundGridChecks()
    {
        RoadGrid g = GameGrid;
        Check(Near(g.EastEdge, 1625.22f, 0.05f) && Near(g.NorthEdge, 1168.36f, 0.05f), "new ground grid: the game's grid reaches x 1625.2 and z 1168.4");
        Check(g.Row(-2511.64f) == 0 && g.Column(-4294.78f) == 0 && g.Row(-2511.64f + 79.9f) == 0 && g.Row(-2511.64f + 80.1f) == 1,
            "new ground grid: rows of 80 m from the corner");
        // The game's own lookup: FloorToInt(Abs((corner - z) / cell)); the same for every point on the grid.
        foreach (float z in new[] { -2500f, -1000f, 0f, 480f, 1160f })
        {
            int game = (int)Math.Floor(Math.Abs((g.CornerZ - z) / g.Cell));
            Check(g.Row(z) == game, "new ground grid: row of z " + z + " as the game looks it up (" + game + ")");
        }
        Check(g.Inside(0f, 0f, 1f) && !g.Inside(0f, 1168f, 1f) && !g.Inside(1625f, 0f, 1f) && !g.Inside(-4294.5f, 0f, 1f) && !g.Inside(0f, -2511f, 1f),
            "new ground grid: inside a metre from each edge, never on or past one");
        Check(!new RoadGrid(0f, 0f, 0f, 46, 74).Valid && !new RoadGrid(0f, 0f, 80f, 0, 74).Valid && g.Valid, "new ground grid: a grid with no cells is not valid");
        Check(Near(g.Room(0f, 0f), Math.Min(Math.Min(4294.78f, 1625.22f), Math.Min(2511.64f, 1168.36f)), 0.05f), "new ground grid: room to the nearest edge");
    }

    // ---------------------------------------------------------------- a town: a lights junction with four arms joined to the game's lanes

    // Four arms from a junction at (500, 500), the east and north arms two lanes out and one in, joined at their open
    // ends to 12 lane points standing for the game's own (indices 0 to 11).
    private static RoadPlan TownPlan(out float[] gameX, out float[] gameZ)
    {
        var plan = new RoadPlan { Name = "town" };
        plan.Junctions.Add(new PlanJunction { X = 500f, Y = 0f, Z = 500f, Lights = true });
        float[][] ends = { new[] { 560f, 500f }, new[] { 500f, 560f }, new[] { 440f, 500f }, new[] { 500f, 440f } };
        var gx = new List<float>();
        var gz = new List<float>();
        for (int arm = 0; arm < 4; arm++)
        {
            var road = new PlanRoad { Ahead = arm < 2 ? 2 : 1, Back = 1, Speed = 50, Start = 0 };
            road.Point(500f, 0f, 500f).Point(ends[arm][0], 0f, ends[arm][1]);
            plan.Roads.Add(road);
            for (int lane = 0; lane < road.Ahead + road.Back; lane++)
            {
                bool forward = lane < road.Ahead;
                plan.Joins.Add(new PlanJoin { Road = arm, Lane = lane, Fork = !forward, X = ends[arm][0], Z = ends[arm][1], Existing = gx.Count });
                gx.Add(ends[arm][0]);
                gz.Add(ends[arm][1]);
            }
        }
        gameX = gx.ToArray();
        gameZ = gz.ToArray();
        return plan;
    }

    private static void GroundTownChecks()
    {
        RoadPlan plan = TownPlan(out float[] gx, out _);
        int existing = gx.Length;
        RoadBuild build = RoadRules.Build(plan, GameGrid, existing, 300);
        Check(build.Problem == null, "new ground town: the plan builds (" + build.Problem + ")");
        string check = RoadRules.Check(build, GameGrid, existing, 299);
        Check(check == null, "new ground town: the build is sound (" + check + ")");
        if (build.Problem != null)
        {
            return;
        }
        var name = new Regex(@"^Road_\d+-Lane_\d+-(Waypoint_\d+|In|Out|CConnect\d+|Connector\d+)$");
        Check(build.Points.All(p => name.IsMatch(p.Name)), "new ground town: every lane point is named as the game names its own");
        Check(build.First == existing && build.Points.All(p => p.Next.All(n => n >= 0 && n < existing + build.Count)), "new ground town: new points come after the game's, every link in range");
        Check(build.Points.All(p => p.RoadId >= 300 && p.RoadId < 304), "new ground town: four roads, ids from the first new one");
        Check(build.Junctions.Count == 1 && build.Junctions[0].Lights && build.Junctions[0].Ways.Count == 4, "new ground town: one junction with lights, a way in for each of four roads");
        Check(build.Junctions[0].Exits.Count == 6, "new ground town: six lanes leave the junction (two on two arms, one on two)");
        int ins = build.Points.Count(p => p.Name.EndsWith("-In")), outs = build.Points.Count(p => p.Name.EndsWith("-Out"));
        Check(ins == 6 && outs == 4, "new ground town: an In point for each lane leaving the junction (" + ins + ") and an Out for each coming in (" + outs + ")");
        Check(build.Edits.Count == 10, "new ground town: ten joins to the game's lanes, a fork or a merge for each open end (" + build.Edits.Count + ")");
        Check(build.Edits.All(e => e.Existing >= 0 && e.Existing < existing), "new ground town: the joins are to the game's own points");

        // Right-hand traffic: the east arm's lanes out (heading east) lie south of its middle line, its lane in north.
        List<LanePoint> eastOut = build.Points.Where(p => p.RoadId == 300 && p.Name.Contains("-Lane_0-Waypoint_")).ToList();
        List<LanePoint> eastIn = build.Points.Where(p => p.RoadId == 300 && p.Name.Contains("-Lane_2-Waypoint_")).ToList();
        Check(eastOut.Count > 0 && eastOut.All(p => p.Z < 500f) && eastIn.Count > 0 && eastIn.All(p => p.Z > 500f), "new ground town: traffic keeps right, as the game's does");
        List<LanePoint> kerb = build.Points.Where(p => p.RoadId == 300 && p.Name.Contains("-Lane_1-Waypoint_")).ToList();
        Check(kerb.Count > 0 && kerb.All(p => p.Z < eastOut[0].Z), "new ground town: the second lane out is the kerb lane, further right");

        // Lane changes: only between the two lanes out, to a point a step ahead on the other.
        foreach (LanePoint p in build.Points)
        {
            foreach (int b in p.Beside)
            {
                LanePoint other = build.At(b);
                Check(other != null && other.RoadId == p.RoadId && other.Name.Contains("Waypoint_") && Math.Abs(other.X - p.X) > 1f && Math.Abs(other.Z - p.Z) > 2f,
                    "new ground town: the lane change from " + p.Name + " is to the lane beside, a step ahead");
            }
        }
        Check(build.Points.Count(p => p.Beside.Count > 0) > 4, "new ground town: the two-lane arms have lane changes");
        Check(build.Points.Where(p => p.RoadId >= 302).All(p => p.Beside.Count == 0), "new ground town: the one-lane arms have none");

        // Turns keep the road they come from; the point after a turn is on the road it leads to.
        int turns = 0;
        foreach (LanePoint p in build.Points.Where(p => p.Name.EndsWith("-Out")))
        {
            foreach (int first in p.Next)
            {
                int at = first;
                LanePoint q = build.At(at);
                while (q != null && q.Name.Contains("Connect"))
                {
                    Check(q.RoadId == p.RoadId, "new ground town: the turn point " + q.Name + " keeps the road it comes from");
                    q = build.At(q.Next[0]);
                }
                Check(q != null && q.Name.EndsWith("-In") && q.RoadId != p.RoadId, "new ground town: a turn from " + p.Name + " ends on the In point of another road");
                turns++;
            }
        }
        Check(turns >= 8, "new ground town: every lane in has its turns (" + turns + ")");
        // A left turn only from the middle lane: from the kerb lane of the east arm's lanes in... the arms' lanes in have one lane each, so the
        // two-lane check is the lanes out of other arms into it: no turn ends on the kerb lane of the east arm but a right turn or straight on.
        Check(build.Points.Where(p => p.Name.Contains("Connect")).All(p => !p.Spawn), "new ground town: no car is put on the road inside the junction");
        Check(build.Points.Where(p => p.Name.EndsWith("-In") || p.Name.EndsWith("-Out")).All(p => !p.Spawn), "new ground town: nor at its stop and exit points");
        Check(build.Points.Where(p => p.Name.Contains("Waypoint_")).All(p => p.Spawn), "new ground town: but on every plain point");
        Check(build.Points.All(p => p.Row == GameGrid.Row(p.Z) && p.Column == GameGrid.Column(p.X)), "new ground town: every point filed in its own cell");
        Check(build.Junctions[0].Cells.Count >= 1 && build.Junctions[0].Cells.All(c => c >= 0 && c < GameGrid.Rows * GameGrid.Columns), "new ground town: the junction filed in its cells");
        float longest = 0f;
        foreach (LanePoint p in build.Points)
        {
            foreach (int n in p.Next.Where(n => n >= existing))
            {
                LanePoint q = build.At(n);
                longest = Math.Max(longest, (float)Math.Sqrt((q.X - p.X) * (q.X - p.X) + (q.Z - p.Z) * (q.Z - p.Z)));
            }
        }
        Check(longest <= RoadRules.Step * 1.6f, "new ground town: points stand a step apart, the longest link " + longest.ToString("0.0") + " m");

        // The same plan builds the same lanes on every machine.
        RoadBuild again = RoadRules.Build(TownPlan(out _, out _), GameGrid, existing, 300);
        bool same = again.Count == build.Count;
        for (int i = 0; same && i < build.Count; i++)
        {
            LanePoint a = build.Points[i], b = again.Points[i];
            same = a.Name == b.Name && a.X == b.X && a.Z == b.Z && a.Next.SequenceEqual(b.Next) && a.Prev.SequenceEqual(b.Prev) && a.Beside.SequenceEqual(b.Beside);
        }
        Check(same, "new ground town: the same plan builds the same lanes (deterministic)");
        Check(RoadRules.Words(build).Contains("1 junction(s) (1 with lights)"), "new ground town: the log's words say what was added");
    }

    // ---------------------------------------------------------------- a block: four priority junctions round a square

    private static RoadPlan BlockPlan(bool lights)
    {
        var plan = new RoadPlan { Name = "block" };
        float[][] corners = { new[] { 0f, 0f }, new[] { 200f, 0f }, new[] { 200f, 200f }, new[] { 0f, 200f } };
        foreach (float[] c in corners)
        {
            plan.Junctions.Add(new PlanJunction { X = c[0], Z = c[1], Lights = lights });
        }
        for (int i = 0; i < 4; i++)
        {
            float[] a = corners[i], b = corners[(i + 1) % 4];
            plan.Roads.Add(new PlanRoad { Start = i, End = (i + 1) % 4 }.Point(a[0], 0f, a[1]).Point(b[0], 0f, b[1]));
        }
        return plan;
    }

    private static void GroundBlockChecks()
    {
        RoadBuild build = RoadRules.Build(BlockPlan(false), GameGrid, 0, 1);
        string check = RoadRules.Check(build, GameGrid, 0, 0);
        Check(build.Problem == null && check == null, "new ground block: a closed square of two-way streets builds and is sound (" + (build.Problem ?? check) + ")");
        Check(build.Junctions.Count == 4 && build.Junctions.All(j => !j.Lights && j.Ways.Count == 2), "new ground block: four priority junctions, two roads in at each");
        Check(build.Points.All(p => p.Next.Count >= 1 && p.Prev.Count >= 1), "new ground block: every point leads on and is led into (no dead end)");
        // A car can go round the block both ways: from any lane point, following the first way on, it comes back.
        foreach (int start in new[] { 0, build.Count / 2 })
        {
            int at = start, steps = 0;
            do
            {
                at = build.At(at).Next[0];
                steps++;
            }
            while (at != start && steps < 2000);
            Check(at == start, "new ground block: round the block from point " + start + " and back in " + steps + " steps");
        }
        RoadBuild lit = RoadRules.Build(BlockPlan(true), GameGrid, 0, 1);
        Check(lit.Problem == null && lit.Junctions.All(j => j.Lights && j.Green > 0f && j.Yellow > 0f), "new ground block: the same with lights, the game's green and yellow");
    }

    // ---------------------------------------------------------------- never a point off the road grid

    private static void GroundOffGridChecks()
    {
        RoadGrid g = GameGrid;
        // A street from the junction at the block's corner out past each edge of the grid, its end joined to a lane of the game's.
        var cases = new (string Edge, float X, float Z)[]
        {
            ("north", 0f, g.NorthEdge + 20f), ("east", g.EastEdge + 20f, 0f), ("south", 0f, g.CornerZ - 20f), ("west", g.CornerX - 20f, 0f),
        };
        foreach (var edge in cases)
        {
            var plan = new RoadPlan { Name = "edge" };
            float fromX = edge.X == 0f ? 0f : (edge.X > 0f ? g.EastEdge - 60f : g.CornerX + 60f);
            float fromZ = edge.Z == 0f ? 0f : (edge.Z > 0f ? g.NorthEdge - 60f : g.CornerZ + 60f);
            plan.Roads.Add(new PlanRoad { Ahead = 1, Back = 0 }.Point(fromX, 0f, fromZ).Point(edge.X == 0f ? fromX : edge.X, 0f, edge.Z == 0f ? fromZ : edge.Z));
            plan.Joins.Add(new PlanJoin { Road = 0, Lane = 0, Fork = true, Existing = 0 });
            plan.Joins.Add(new PlanJoin { Road = 0, Lane = 0, Fork = false, Existing = 1 });
            RoadBuild build = RoadRules.Build(plan, g, 2, 1);
            string check = RoadRules.Check(build, g, 2, 0);
            Check(check != null && check.Contains("off the game's road grid"), "new ground off the grid: a street past the " + edge.Edge + " edge is refused (" + check + ")");
        }
        // Just inside each edge is taken.
        var inside = new RoadPlan { Name = "inside" };
        inside.Roads.Add(new PlanRoad { Ahead = 1, Back = 0 }.Point(g.EastEdge - 30f, 0f, g.NorthEdge - 3f).Point(g.EastEdge - 3f, 0f, g.NorthEdge - 3f));
        inside.Joins.Add(new PlanJoin { Road = 0, Lane = 0, Fork = true, Existing = 0 });
        inside.Joins.Add(new PlanJoin { Road = 0, Lane = 0, Fork = false, Existing = 1 });
        RoadBuild near = RoadRules.Build(inside, g, 2, 1);
        string nearCheck = RoadRules.Check(near, g, 2, 0);
        Check(near.Points.All(p => g.Inside(p.X, p.Z, RoadRules.EdgeMargin)) == (nearCheck == null), "new ground off the grid: a street hugging the north-east corner is judged by its points alone (" + nearCheck + ")");
        // Every point the town and the block lay is inside, and their cells exist.
        RoadBuild town = RoadRules.Build(TownPlan(out _, out _), g, 12, 300);
        Check(town.Points.All(p => p.Row >= 0 && p.Row < g.Rows && p.Column >= 0 && p.Column < g.Columns), "new ground off the grid: the town's points are all in the grid's cells");
    }

    // ---------------------------------------------------------------- what is refused

    private static void GroundFaultChecks()
    {
        RoadGrid g = GameGrid;
        Check(RoadRules.Build(new RoadPlan(), g, 0, 1).Problem != null, "new ground faults: a plan with no road is refused");
        Check(RoadRules.Build(TownPlan(out _, out _), new RoadGrid(), 12, 300).Problem != null, "new ground faults: no road grid, nothing built");
        var dead = new RoadPlan { Name = "dead" };
        dead.Roads.Add(new PlanRoad().Point(0f, 0f, 0f).Point(100f, 0f, 0f));
        string deadCheck = RoadRules.Check(RoadRules.Build(dead, g, 0, 1), g, 0, 0);
        Check(deadCheck != null && (deadCheck.Contains("leads nowhere") || deadCheck.Contains("nothing leads into")), "new ground faults: a street that ends nowhere is refused (" + deadCheck + ")");
        var star = new RoadPlan { Name = "star" };
        star.Junctions.Add(new PlanJunction { X = 0f, Z = 0f, Lights = false });
        for (int i = 0; i < 5; i++)
        {
            double a = i * 2 * Math.PI / 5;
            star.Roads.Add(new PlanRoad { Start = 0 }.Point(0f, 0f, 0f).Point((float)Math.Sin(a) * 80f, 0f, (float)Math.Cos(a) * 80f));
        }
        Check((RoadRules.Build(star, g, 0, 1).Problem ?? string.Empty).Contains("priority junction"), "new ground faults: five roads into a priority junction are refused (the game weighs four)");
        RoadBuild town = RoadRules.Build(TownPlan(out _, out _), g, 12, 300);
        Check((RoadRules.Check(town, g, 12, 300) ?? string.Empty).Contains("road id"), "new ground faults: a road id the game's roads may use is refused");
        Check((RoadRules.Check(town, g, 13, 299) ?? string.Empty).Contains("lane points"), "new ground faults: a build made for another count of the game's lane points is refused");
        RoadPlan badJoin = TownPlan(out _, out _);
        badJoin.Joins[0].Existing = -1;
        Check((RoadRules.Build(badJoin, g, 12, 300).Problem ?? string.Empty).Contains("found no lane"), "new ground faults: a join whose lane of the game's was not found at this load is refused");
        RoadPlan shortRoad = new RoadPlan { Name = "short" };
        shortRoad.Junctions.Add(new PlanJunction { X = 0f, Z = 0f });
        shortRoad.Junctions.Add(new PlanJunction { X = 12f, Z = 0f });
        shortRoad.Roads.Add(new PlanRoad { Start = 0, End = 1 }.Point(0f, 0f, 0f).Point(12f, 0f, 0f));
        Check((RoadRules.Build(shortRoad, g, 0, 1).Problem ?? string.Empty).Contains("too short"), "new ground faults: a street too short for its junctions is refused");
        RoadPlan slow = BlockPlan(true);
        slow.Roads[0].Speed = 5;
        Check(RoadRules.Build(slow, g, 0, 1).Problem != null, "new ground faults: a speed outside 10 to 130 km/h is refused");
        RoadPlan wide = BlockPlan(true);
        wide.Roads[1].LaneWidth = 9f;
        Check(RoadRules.Build(wide, g, 0, 1).Problem != null, "new ground faults: a lane 9 m wide is refused");
    }

    // ---------------------------------------------------------------- the test road: a copy of a lane with a fork and a merge

    private static void GroundCopyChecks()
    {
        RoadGrid g = GameGrid;
        int[] chain = { 100, 101, 102, 103, 104, 105, 106, 107 };
        float[] x = chain.Select(i => 10f + (i - 100) * 4.8f).ToArray();
        float[] y = chain.Select(i => 0f).ToArray();
        float[] z = chain.Select(i => -381.9f).ToArray();
        RoadBuild copy = RoadRules.Copy(chain, x, y, z, 50, true, true, g, 18791, 273);
        string check = RoadRules.Check(copy, g, 18791, 272);
        Check(copy.Problem == null && check == null, "new ground test road: a copy of eight lane points is sound (" + (copy.Problem ?? check) + ")");
        Check(copy.Count == 6 && copy.First == 18791, "new ground test road: the six inner points copied after the game's 18791");
        Check(copy.Points.All(p => p.RoadId == 273 && p.Name.StartsWith("Road_273-Lane_0-Waypoint_") && !p.Spawn), "new ground test road: a road of its own, plain points, no car put on it");
        Check(copy.Edits.Count == 2 && copy.Edits[0].Existing == 100 && copy.Edits[0].AsNext && copy.Edits[1].Existing == 107 && !copy.Edits[1].AsNext,
            "new ground test road: a fork from the chain's first point and a merge into its last");
        Check(copy.Points[0].Prev.SequenceEqual(new[] { 100 }) && copy.Points[5].Next.SequenceEqual(new[] { 107 }), "new ground test road: its ends are the fork and the merge");
        Check(Near(copy.Points[0].X, x[1]) && Near(copy.Points[5].X, x[6]), "new ground test road: the copy stands where the lane it copies does");
        Check(RoadRules.Copy(new[] { 1, 2 }, new[] { 0f, 1f }, new[] { 0f, 0f }, new[] { 0f, 0f }, 50, true, true, g, 10, 5).Problem != null, "new ground test road: a chain of two is refused");
        float[] off = chain.Select(i => g.NorthEdge + 5f).ToArray();
        Check(RoadRules.Check(RoadRules.Copy(chain, x, y, off, 50, true, true, g, 18791, 273), g, 18791, 272) != null, "new ground test road: never a copy off the grid");
    }

    // ---------------------------------------------------------------- the game has put a road there since

    private static void GroundClearChecks()
    {
        RoadBuild town = RoadRules.Build(TownPlan(out float[] gx, out float[] gz), GameGrid, 12, 300);
        int[] indices = Enumerable.Range(0, gx.Length).ToArray();
        Check(RoadRules.Clear(town, indices, gx, gz) == null, "new ground clear: the game's lanes the town joins are beside it, not in its way");
        LanePoint middle = town.Points.First(p => p.Name.EndsWith("-Out"));
        string clash = RoadRules.Clear(town, new[] { 999 }, new[] { middle.X + 1f }, new[] { middle.Z });
        Check(clash != null && clash.Contains("999"), "new ground clear: a lane of the game's where the town would be is found (a game update built there): " + clash);
        Check(RoadRules.Clear(town, new[] { 999 }, new[] { 5000f }, new[] { 5000f }) == null, "new ground clear: one far away is not");
        var join = new PlanJoin { X = 100f, Z = 100f };
        int found = RoadRules.FindJoin(join, 1f, 0f, new[] { 7, 8, 9 }, new[] { 104f, 101f, 100f }, new[] { 100f, 100f, 100f }, new[] { 1f, -1f, 1f }, new[] { 0f, 0f, 0f });
        Check(found == 9, "new ground clear: a join takes the nearest lane of the game's that runs its way (" + found + ")");
        Check(RoadRules.FindJoin(join, 1f, 0f, new[] { 8 }, new[] { 101f }, new[] { 100f }, new[] { -1f }, new[] { 0f }) == -1, "new ground clear: never one running the other way");
        Check(RoadRules.FindJoin(join, 1f, 0f, new[] { 7 }, new[] { 120f }, new[] { 100f }, new[] { 1f }, new[] { 0f }) == -1, "new ground clear: nor one beyond reach");
    }

    // ---------------------------------------------------------------- the cost

    private static void GroundTimingChecks()
    {
        // A district's worth: a ten by ten grid of junctions joined by two-way streets, built and checked.
        var plan = new RoadPlan { Name = "district" };
        const int Side = 10;
        const float Block = 120f;
        for (int i = 0; i < Side; i++)
        {
            for (int j = 0; j < Side; j++)
            {
                plan.Junctions.Add(new PlanJunction { X = i * Block, Z = j * Block, Lights = (i + j) % 2 == 0 });
            }
        }
        for (int i = 0; i < Side; i++)
        {
            for (int j = 0; j < Side; j++)
            {
                if (i + 1 < Side)
                {
                    plan.Roads.Add(new PlanRoad { Start = i * Side + j, End = (i + 1) * Side + j }.Point(i * Block, 0f, j * Block).Point((i + 1) * Block, 0f, j * Block));
                }
                if (j + 1 < Side)
                {
                    plan.Roads.Add(new PlanRoad { Start = i * Side + j, End = i * Side + j + 1 }.Point(i * Block, 0f, j * Block).Point(i * Block, 0f, (j + 1) * Block));
                }
            }
        }
        var watch = Stopwatch.StartNew();
        RoadBuild build = RoadRules.Build(plan, GameGrid, 18791, 300);
        string check = RoadRules.Check(build, GameGrid, 18791, 299);
        watch.Stop();
        Check(build.Problem == null && check == null, "new ground cost: a district of 100 junctions and 180 streets builds and is sound (" + (build.Problem ?? check) + ")");
        Check(watch.Elapsed.TotalMilliseconds < 400.0, "new ground cost: " + build.Count + " lane points built and checked in " + watch.Elapsed.TotalMilliseconds.ToString("0") + " ms (under 400 ms, once a load)");
        var mixed = build.Junctions.Where(j => !j.Lights).ToList();
        Check(mixed.All(j => j.Ways.Count <= RoadRules.MostPriorityWays), "new ground cost: its priority junctions take four roads at most");
    }

    // ---------------------------------------------------------------- the walking rules

    private static void GroundWalkChecks()
    {
        var grid = new TileGrid(-28.0077f, 34.1263f, 512 * 0.07f);
        Check(Near(grid.Side, 35.84f) && grid.Valid && !new TileGrid(0f, 0f, 0f).Valid, "new ground walk: the game's tiles are 512 voxels of 0.07 m");
        foreach (int tx in new[] { -96, -1, 0, 1, 37 })
        {
            foreach (int tz in new[] { -56, -1, 0, 20 })
            {
                long key = TileGrid.Key(tx, tz);
                Check(TileGrid.KeyX(key) == tx && TileGrid.KeyZ(key) == tz, "new ground walk: tile (" + tx + ", " + tz + ") kept and read back as one number");
            }
        }
        Check(grid.TileX(-28.0f) == 0 && grid.TileX(-28.02f) == -1 && grid.TileX(-28.0077f + 35.85f) == 1, "new ground walk: tiles laid from the mesh's own position");

        var box = new GroundBox(10f, 20f, 1f, 3f, 1f, 90f, 0.5f);
        box.World(1f, 0f, out float wx, out float wz);
        Check(Near(wx, 10f) && Near(wz, 19f), "new ground walk: a box turned 90 degrees has its own x pointing south, as Unity turns");
        Check(box.Covers(10f, 22.9f, 0f) && !box.Covers(10f, 23.2f, 0f) && box.Covers(11.9f, 20f, 0.1f) == false, "new ground walk: the turned box covers its own ground and no more");
        box.Extent(out float minX, out float minZ, out float maxX, out float maxZ);
        Check(Near(minX, 9f) && Near(maxX, 11f) && Near(minZ, 17f) && Near(maxZ, 23f), "new ground walk: its extent on the world's axes");

        var xs = new float[GroundRules.RingPoints];
        var zs = new float[GroundRules.RingPoints];
        GroundRules.Ring(5f, 5f, xs, zs);
        Check(Near(xs[0], 5f) && Enumerable.Range(1, 8).All(i => Near((float)Math.Sqrt((xs[i] - 5f) * (xs[i] - 5f) + (zs[i] - 5f) * (zs[i] - 5f)), 10f)),
            "new ground walk: under me samples the player's own point and eight 10 m round");
        GroundBox patch = GroundRules.Patch(0f, 0f, 0f, 90f);
        GroundRules.NearSide(patch, out float nx, out float nz);
        Check(Near(patch.X, 12f) && Near(patch.Z, 0f) && Near(patch.HalfX, 10f) && Near(nx, 2.5f) && Near(nz, 0f), "new ground walk: the patch is 20 m square, its near side 2 m off, the link's end just inside");

        foreach (int heading in new[] { 0, 90, 180, 270 })
        {
            Check(GroundRules.Strip(0f, 50f, 1f, heading, grid, out GroundBox strip, out float toBorder), "new ground walk: a strip can be laid heading " + heading);
            float a = heading * (float)Math.PI / 180f;
            float dx = (float)Math.Sin(a), dz = (float)Math.Cos(a);
            float bx = dx * (toBorder + GroundRules.StripInto), bz = 50f + dz * (toBorder + GroundRules.StripInto);
            long beside = GroundRules.Beside(0f, 50f, heading, grid);
            Check(GroundRules.OnTile(bx - dx * 0.1f, bz - dz * 0.1f, beside, grid), "new ground walk: the strip heading " + heading + " ends inside the tile beside");
            Check(strip.Covers(-dx * (GroundRules.JoinOverlap - 0.05f), 50f - dz * (GroundRules.JoinOverlap - 0.05f), 0.01f), "new ground walk: it begins half a metre back on the old ground");
            Check(toBorder > 0f && toBorder <= grid.Side, "new ground walk: the border is ahead within a tile (" + toBorder.ToString("0.0") + " m)");
        }
        Check(!GroundRules.Strip(0f, 50f, 1f, 45, grid, out _, out _), "new ground walk: only the grid's own four headings");
        var far = new TileGrid(0f, 0f, 100f);
        Check(!GroundRules.Strip(1f, 1f, 0f, 90, far, out _, out _), "new ground walk: no strip from further than 40 m from the border");

        var under = new List<long>();
        GroundRules.TilesUnder(new GroundBox(grid.MinX(3), grid.MinZ(3) + 5f, 0f, 4f, 2f, 0f, 0.5f), grid, 0f, under);
        Check(under.Count == 2 && under.Contains(TileGrid.Key(2, 3)) && under.Contains(TileGrid.Key(3, 3)), "new ground walk: a box over a border covers both tiles");
        var round = new List<long>();
        GroundRules.Round(under, round);
        Check(round.Count == 10 && round.All(t => !under.Contains(t)), "new ground walk: ten tiles round two, none of them the two");
        var px = new List<float>();
        var pz = new List<float>();
        GroundRules.Spread(TileGrid.Key(3, 3), grid, 5, px, pz);
        Check(px.Count == 25 && Enumerable.Range(0, 25).All(i => GroundRules.OnTile(px[i], pz[i], TileGrid.Key(3, 3), grid)), "new ground walk: 25 samples, all on their tile");
        GroundRules.TileBounds(TileGrid.Key(3, 3), grid, out float b0, out float c0, out float b1, out float c1);
        Check(grid.TileX(b0) == 3 && grid.TileX(b1) == 3 && grid.TileZ(c0) == 3 && grid.TileZ(c1) == 3 && Near(b1 - b0, grid.Side - 0.02f, 0.001f),
            "new ground walk: a one-tile build's bounds stay just inside its tile, never reaching the next");
        GroundBox shrunk = GroundRules.Shrunk(new GroundBox(0f, 0f, 0f, 2f, 1f, 0f, 0.5f), 0.25f);
        Check(Near(shrunk.HalfX, 1.75f) && Near(shrunk.HalfZ, 0.75f), "new ground walk: new ground in an old tile keeps the agent's margin itself");

        float n = float.NaN;
        var before = new[] { 1f, 2f, 3f, n, n, n, 5f, 5f, 5f };
        Check(GroundRules.Alike(before, (float[])before.Clone()) == 3, "new ground walk: an unchanged print is alike at every sample");
        Check(GroundRules.Alike(before, new[] { 1f, 2f, 3.2f, n, n, n, 5f, 5f, 5f }) == 2, "new ground walk: a sample moved 0.2 m is not");
        Check(GroundRules.Alike(before, new[] { 1f, 2f, 3f, 0f, 0f, 0f, 5f, 5f, 5f }) == 2, "new ground walk: nor one found where there was none");
        Check(GroundRules.Alike(before, new float[3]) == 0, "new ground walk: prints of different sizes are never alike");
        Check(GroundRules.InTriangle(1f, 1f, 0f, 0f, 4f, 0f, 0f, 4f) && GroundRules.InTriangle(2f, 0f, 0f, 0f, 4f, 0f, 0f, 4f) && !GroundRules.InTriangle(3f, 3f, 0f, 0f, 4f, 0f, 0f, 4f),
            "new ground walk: a point in a triangle, on its edge, and outside");
        Check(GroundRules.TilesOf(0f, 0f, 19.9f, 19.9f, new TileGrid(0f, 0f, 35.84f)) == 1 && GroundRules.TilesOf(-1f, 0f, 19f, 19f, new TileGrid(0f, 0f, 35.84f)) == 2,
            "new ground walk: a 20 m patch is one tile or two, as it falls");
    }

    // ---------------------------------------------------------------- the self-check lines

    private static void GroundLineChecks()
    {
        Check(GroundRules.UnderLine("Player", 9, "NPC", 3, 12.34f, -45.6f) == "Walk check (navmesh here): Player 9 of 9, NPC 3 of 9 at (12.3, -45.6)",
            "new ground lines: what is under me, as the owner's test list has it");
        string build = GroundRules.BuildLine("Player", 4, 12.4, "NPC", 4, 9.6, true, "PARTIAL");
        Check(build == "Walk check (runtime build): Player 4 tiles in 12 ms, NPC 4 tiles in 10 ms, sample PASS, path from the player PARTIAL",
            "new ground lines: the runtime build: " + build);
        string grow = GroundRules.GrowLine("NPC", "PARTIAL", "COMPLETE", "the old mesh's own 40 triangles", 90, 90);
        Check(grow.Contains("path pavement to new ground COMPLETE: PASS, polygons elsewhere unchanged") && grow.StartsWith("Walk check (grow one tile, NPC)"),
            "new ground lines: the grow: " + grow);
        Check(GroundRules.GrowLine("NPC", "PARTIAL", "PARTIAL", "x", 89, 90).Contains("FAIL, polygons elsewhere CHANGED (1 of 90"), "new ground lines: a failed grow says what moved");
        Check(GroundRules.LinkLine("Player", "COMPLETE", "NPC", "COMPLETE") == "Walk check (link): path to the patch Player COMPLETE, NPC COMPLETE: PASS", "new ground lines: the link");
        Check(GroundRules.BackLine("Player", 25, 25, 90, 90).EndsWith(": PASS") && GroundRules.BackLine("Player", 24, 25, 90, 90).Contains("FAIL"), "new ground lines: given back, or not");
        Check(GroundRules.DryLine("NPC", null, 0f, 3.2).Contains(": PASS (old tiles rebuilt with a margin of 0.00 m)"), "new ground lines: the dry run");
        Check(GroundRules.PeopleLine(true, 3, 20f).EndsWith(": PASS") && GroundRules.PeopleLine(true, 0, 20f).EndsWith(": FAIL"), "new ground lines: pedestrians on new ground");
        Check(GroundRules.SourcesLine(10, 0, 2, 3, 30f).Contains("0 readable as build sources") && GroundRules.SourcesLine(10, 0, 2, 3, 30f).Contains("the old mesh's own triangles"),
            "new ground lines: the game's meshes counted as build sources");
    }

    // ---------------------------------------------------------------- the sources

    private static void GroundSourceChecks(string scripts)
    {
        string folder = Path.Combine(scripts, "NewGround");
        string[] files = Directory.GetFiles(folder, "*.cs");
        Check(files.Length >= 12, "new ground sources: the folder holds its files: " + files.Length);
        var code = files.ToDictionary(Path.GetFileName, f => Code(File.ReadAllText(f)));
        foreach (var file in code)
        {
            bool rules = file.Key == "RoadRules.cs" || file.Key == "RoadPlan.cs" || file.Key == "GroundRules.cs" || file.Key == "NewGroundText.cs";
            if (rules)
            {
                Check(!Regex.IsMatch(file.Value, @"\busing UnityEngine|\busing Gley|\bVector3\b|\bMathf\b|\bGameObject\b"), "new ground sources: " + file.Key + " is game-free");
            }
            if (file.Key != "WalkGame.cs")
            {
                Check(!Regex.IsMatch(file.Value, @"\bNavMesh\.|\bNavMeshBuilder\.|\.navmeshAgent\b"), "new ground sources: only WalkGame calls Unity's navigation (" + file.Key + ")");
            }
            Check(!Regex.IsMatch(file.Value, @"\.GetField\(|\.GetProperty\(|\.GetMethod\(|BindingFlags|Activator\.|Type\.GetType\("),
                "new ground sources: no member of the game's read by name but through the contract (" + file.Key + ")");
            Check(!Regex.IsMatch(file.Value, @"\bmodData\b|\bSaveHook\b|\bSharedCity\b|\bMPClient\b|\bMPServer\b|\bSend\("), "new ground sources: nothing saved or sent (" + file.Key + ")");
            Check(!Regex.IsMatch(file.Value, @"\bSystem\.Linq\b|using System\.Linq"), "new ground sources: no LINQ (" + file.Key + ")");
            Check(!Regex.IsMatch(file.Value, @"new System\.Random\(\)|\bUnityEngine\.Random\b|\bRandom\.Range\b|DateTime\.Now"), "new ground sources: nothing left to chance or the clock (" + file.Key + ")");
        }
        foreach (string script in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string text = Code(File.ReadAllText(script));
            string name = Path.GetFileName(script);
            if (name != "RoadsGame.cs")
            {
                Check(!Regex.IsMatch(text, @"\.(allWaypoints|roadIds|allIntersections|allLightsIntersections|allPriorityIntersections)\s*=[^=]"),
                    "new ground sources: only NewGround/RoadsGame writes the traffic's road data (" + name + ")");
            }
            if (name != "RoadData.cs")
            {
                Check(!Regex.IsMatch(text, @"FindObjectsByType<CurrentSceneData>|FindObjectsOfType<CurrentSceneData>|GetSceneInstance\(\)|All<CurrentSceneData>"),
                    "new ground sources: the road users find the road data only through Core/RoadData, so appended roads reach them all (" + name + ")");
            }
            if (name.EndsWith("Store.cs", StringComparison.Ordinal) || name.EndsWith("State.cs", StringComparison.Ordinal))
            {
                Check(!Regex.IsMatch(text, @"\blistIndex\b|\ballWaypoints\b|\bGetWaypoint\("), "new ground sources: no save keeps a lane point's index, which an appended road or a game update moves (" + name + ")");
            }
        }
        string graph = Code(File.ReadAllText(Path.Combine(scripts, @"Core\RoadGraph.cs")));
        string tools = Code(File.ReadAllText(Path.Combine(scripts, @"Traffic\TrafficTools.cs")));
        string data = Code(File.ReadAllText(Path.Combine(scripts, @"Core\RoadData.cs")));
        Check(graph.Contains("ReferenceEquals(points, _sharedFor)"), "new ground sources: the shared road graph is built again for a new array of lane points");
        Check(tools.Contains("ReferenceEquals(all, _laneGridOf)") && tools.Contains("SameLanes(TrafficManager.Instance, all)"),
            "new ground sources: the lane grid is keyed to the array and checked against the traffic's own first and last points");
        Check(data.Contains("return scene != null ? scene.allWaypoints : null;"), "new ground sources: RoadData hands out the scene's lane points live, never a copy");

        string roads = code["RoadsGame.cs"];
        Match append = Regex.Match(roads, @"public static string Append\([^)]*\)\s*\{(?<b>(?:[^{}]|\{(?<d>)|\}(?<-d>))*)\}");
        Check(append.Success, "new ground sources: RoadsGame has its Append");
        if (append.Success)
        {
            string b = append.Groups["b"].Value;
            int checkAt = b.IndexOf("RoadRules.Check(", StringComparison.Ordinal);
            int firstWrite = Regex.Match(b, @"data\.(allWaypoints|roadIds|allIntersections)\s*=[^=]").Index;
            Check(b.Contains("if (TrafficManager.IsInitialized)") && b.IndexOf("TrafficManager.IsInitialized", StringComparison.Ordinal) < checkAt,
                "new ground sources: nothing is appended once the traffic has started (it is never started again)");
            Check(checkAt > 0 && firstWrite > checkAt, "new ground sources: a build is checked, off-grid points refused, before the road data is touched");
            Check(b.Contains("Undo("), "new ground sources: a failed append is taken off again");
        }
        string start = Code(File.ReadAllText(Path.Combine(scripts, @"Core\RealLifeOverhaulMod.cs")));
        int director = start.IndexOf("Guard.Add<NewGround.GroundDirector>(_root)", StringComparison.Ordinal);
        Check(director > start.IndexOf("_root = new GameObject(RootName);", StringComparison.Ordinal) && director < start.IndexOf("ScenePrewarm.Begin()", StringComparison.Ordinal)
              && director < start.IndexOf("RoadData.Prewarm", StringComparison.Ordinal), "new ground sources: the test road is appended in the city's load, before the road data is read for anything");
        string contract = Code(File.ReadAllText(Path.Combine(scripts, @"Core\GameContract.cs")));
        foreach (string member in new[] { "SurfacesActive", "SurfaceData", "SurfaceAgent", "SurfaceLayers", "SurfaceArea", "SurfaceSettings" })
        {
            Check(Regex.IsMatch(contract, @"GameMember " + member + @" = .*NewGroundWalk, Navigation, ""Unity\.AI\.Navigation\.NavMeshSurface"""),
                "new ground sources: " + member + " is a line of the game contract");
            Check(code["WalkGame.cs"].Contains("GameContract." + member + "."), "new ground sources: WalkGame reads " + member + " through the contract");
        }
        Check(contract.Contains("private const string Navigation = \"Unity.AI.Navigation\";"), "new ground sources: the navigation package is named in the contract, read at run time");
        string project = File.ReadAllText(Path.Combine(scripts, @"..\..\..\..\RealLifeOverhaul.Build\RealLifeOverhaul.csproj"));
        Check(!project.Contains("Unity.AI.Navigation"), "new ground sources: no new dependency in the build (the navigation package is read through the contract)");
        string director2 = code["GroundDirector.cs"];
        Match update = Regex.Match(director2, @"private void OnUpdate\(\)\s*\{(?<b>(?:[^{}]|\{(?<d>)|\}(?<-d>))*)\}");
        Check(update.Success && Regex.IsMatch(update.Groups["b"].Value, @"^\s*float now = Time\.unscaledTime;\s*if \(now < _nextLook\)\s*\{\s*return;\s*\}"),
            "new ground sources: between looks the director costs one comparison a frame");
        Check(update.Success && !Regex.IsMatch(update.Groups["b"].Value, @"\bnew\b|""\s*\+|\+\s*""|\$"""), "new ground sources: its look allocates nothing");
        Check(Regex.IsMatch(director2, @"private void Awake\(\)\s*\{\s*Instance = this;\s*enabled = false;"), "new ground sources: the director is disabled while nothing of its own runs");
        Check(director2.Contains("trackOnSaveGame: false"), "new ground sources: its notices stay out of the save");
        string walk = code["WalkGame.cs"];
        Check(walk.Contains("settings.preserveTilesOutsideBounds = true;") && Regex.Matches(walk, @"preserveTilesOutsideBounds = true").Count == 2,
            "new ground sources: every update of a walking mesh keeps the tiles beyond its bounds as they are");
        Check(code["GroundBuilder.cs"].Contains("GroundRules.TileBounds(tile"), "new ground sources: every update of the city's mesh is one tile, its bounds just inside it");

        // What a district stands along its new streets: parking that wakes where it is put, crossings tied to their road's red lamp.
        string parking = code["ParkingGame.cs"];
        Match copyLane = Regex.Match(parking, @"public static ParkingLaneGenerator Copy\(ParkingLaneGenerator template, Vector3 at, Quaternion turn, out string why\)\s*\{(?<b>(?:[^{}]|\{(?<d>)|\}(?<-d>))*)\}");
        Check(copyLane.Success && Regex.IsMatch(copyLane.Groups["b"].Value, @"SetPositionAndRotation\(at, turn\);[\s\S]*localScale = template\.transform\.lossyScale;[\s\S]*SetParent\(null, true\);"),
            "new ground sources: a parking copy is placed and sized while asleep, so it wakes and parks where a kerb wants it");
        Check(Regex.IsMatch(roads, @"public static GameObject RedLamp\(BuiltJunction junction, int way\)") && Regex.Matches(roads, @"LampName\(junction, way, ").Count == 2,
            "new ground sources: a crossing finds its road's red lamp by the name the junction's lamps were made with");

        // The front door a district lays through: the game's own roads and colliders looked for first, at every load.
        string work = code["GroundWork.cs"];
        Match streets = Regex.Match(work, @"public static string Streets\([^)]*\)\s*\{(?<b>(?:[^{}]|\{(?<d>)|\}(?<-d>))*)\}");
        Check(streets.Success && streets.Groups["b"].Value.IndexOf("RoadsGame.FindJoins(", StringComparison.Ordinal) >= 0
              && streets.Groups["b"].Value.IndexOf("RoadRules.Clear(", StringComparison.Ordinal) < streets.Groups["b"].Value.IndexOf("RoadsGame.Append(", StringComparison.Ordinal)
              && streets.Groups["b"].Value.IndexOf("RoadRules.Clear(", StringComparison.Ordinal) > 0,
            "new ground sources: a district's streets find their joins again and stand down where the game has a road of its own, before anything is appended");
        Match ground = Regex.Match(work, @"public static string Ground\([^)]*\)\s*\{(?<b>(?:[^{}]|\{(?<d>)|\}(?<-d>))*)\}");
        Check(ground.Success && Regex.IsMatch(ground.Groups["b"].Value, @"WalkGame\.Built\([^;]*;[\s\S]*if \(found > plan\.ExpectColliders\)[\s\S]*Grow\("),
            "new ground sources: a district's ground counts the game's colliders on it against the plan's before it grows anything");
        Match grow = Regex.Match(work, @"private static string Grow\([^)]*\)\s*\{(?<b>(?:[^{}]|\{(?<d>)|\}(?<-d>))*)\}");
        Check(grow.Success && Regex.IsMatch(grow.Groups["b"].Value, @"GroundPass\.Join\);[\s\S]*GroundBuilder\.RoundAlike\(job\)[\s\S]*GroundBuilder\.Now\(job, GroundPass\.Back\);"),
            "new ground sources: a district's ground compares the tiles round about after it grows, and gives its tiles back when anything failed");
        Match now = Regex.Match(code["GroundBuilder.cs"], @"public static void Now\([^)]*\)\s*\{(?<b>(?:[^{}]|\{(?<d>)|\}(?<-d>))*)\}");
        Check(now.Success && Regex.IsMatch(now.Groups["b"].Value, @"if \(pass != GroundPass\.Back\)\s*\{\s*job\.Pending\.Clear\(\);"),
            "new ground sources: a pass laid now builds nothing more once the builder refuses a tile, and giving back carries on");
        Check(!Regex.IsMatch(work, @"\bconst\s+float\s+\w*(Corner|Cell|Origin|Side|Bounds)\w*\s*="), "new ground sources: the grid, the tiles and the map are read from the game, never kept in the code");

        // The buses: lines run between the game's subway stations, then termini of the mod's own (a district has no station).
        string transit = Code(File.ReadAllText(Path.Combine(scripts, @"Transport\TransitMap.cs")));
        Match begin = Regex.Match(transit, @"private static bool Begin\(\)\s*\{(?<b>(?:[^{}]|\{(?<d>)|\}(?<-d>))*)\}");
        Check(begin.Success && Regex.IsMatch(begin.Groups["b"].Value, @"Stations\.Sort\([\s\S]*Ends\.Clear\(\);[\s\S]*Ends\.Add\(new TransitEnd \{ At = Stations\[s\]\.transform\.position[\s\S]*termini\.Sort\([\s\S]*Ends\.AddRange\(termini\);"),
            "new ground sources: the buses' ends are the game's stations in their order, then the mod's termini sorted by name (none: as before)");
        Check(!Regex.IsMatch(transit, @"Stations\[(_pa|_pb|to|from)\]") && transit.Contains("ToKey = Ends[to].NameKey"),
            "new ground sources: every pairing, search and line's name reads the ends, so a terminus is a station to the buses");
        Check(Regex.IsMatch(transit, @"sx\[s\] = Stations\[s\]\.transform\.position\.x;"), "new ground sources: a change to the subway is offered only at the game's own stations");
        Check(code["GroundDirector.cs"].Contains("TransitMap.ClearTermini();"), "new ground sources: the termini go with their city");

        // The texts: every key the folder names is in the three languages, the notices and refusals short.
        string locales = Path.GetFullPath(Path.Combine(scripts, @"..\Locales"));
        var named = files.SelectMany(f => Regex.Matches(File.ReadAllText(f), "\"(reallife_[a-z_]+)\"").Select(m => m.Groups[1].Value))
                         .Concat(new[] { "reallife_opt_sec_newground", "reallife_opt_tip_newground_test_road" }).Distinct().ToList();
        Check(named.Count >= 30, "new ground texts: its texts are found: " + named.Count);
        foreach (string language in new[] { "en", "pt", "zh-cn" })
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(locales, language + ".json")));
            foreach (string key in named)
            {
                Check(doc.RootElement.TryGetProperty(key, out JsonElement value) && value.GetString().Length > 0, "new ground texts: " + key + " is in " + language + ".json");
                if (language == "en" && doc.RootElement.TryGetProperty(key, out JsonElement en))
                {
                    Check(en.GetString().Length <= 240, "new ground texts: " + key + " is at most 240 characters");
                    Check(!Regex.IsMatch(en.GetString(), "[–—!]| - "), "new ground texts: " + key + " has no dash and no exclamation mark");
                }
            }
        }
    }
}
