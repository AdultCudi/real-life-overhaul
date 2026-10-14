using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;

// Checks for the lane grid (Core/PointGrid.cs) that Traffic/TrafficTools.ClosestWaypoint asks instead of the traffic system's
// GetClosestWaypoint, which goes through every lane point of the city (some 19,000) each time: a police station's frontage asked
// it some ninety times in one frame. The answer must be the traffic system's to the point, and a filter must be asked of the
// same points in the same order (some of the mod's filters collect what they are shown). So the traffic system's own loop is
// run here over every point and over the grid's points only, and both are compared: the point found, and every call of the
// filter, for thousands of places and distances, with ties, heights, places off the map and filters of every kind the mod hands.
internal static partial class Program
{
    private static void PointGrids()
    {
        var rng = new Random(20261014);
        // A made-up city: lanes along a grid of streets, a lane point every 4 m, at heights that rise and fall; a second
        // district far off (as the Hamptons lie apart), scattered points, and points stacked on the same spot (ties).
        var xs = new List<float>();
        var ys = new List<float>();
        var zs = new List<float>();
        for (int street = 0; street < 24; street++)
        {
            for (int along = 0; along < 240; along++)
            {
                for (int lane = 0; lane < 2; lane++)
                {
                    float offset = lane * 3.3f - 1.65f;
                    Add(xs, ys, zs, street * 90f + offset, (float)Math.Sin(along * 0.05) * 2f, along * 4f);
                    Add(xs, ys, zs, along * 4f, (float)Math.Cos(along * 0.07) * 2f, street * 90f + offset);
                }
            }
        }
        for (int i = 0; i < 1500; i++)
        {
            Add(xs, ys, zs, 4000f + (float)rng.NextDouble() * 900f, (float)rng.NextDouble() * 6f, -2500f + (float)rng.NextDouble() * 700f);
        }
        for (int i = 0; i < 400; i++)
        {
            float x = (float)rng.NextDouble() * 2100f, z = (float)rng.NextDouble() * 2100f;
            Add(xs, ys, zs, x, 0f, z);
            Add(xs, ys, zs, x, 0f, z);   // the same spot twice: the first in the list is the answer
        }
        float[] px = xs.ToArray(), py = ys.ToArray(), pz = zs.ToArray();
        Check(px.Length > 18000, "point grid: the made-up city has as many lane points as the game's (" + px.Length + ")");

        var grid = new PointGrid(px, pz, 20f);
        var candidates = new List<int>(512);
        var callsAll = new List<int>(4096);
        var callsGrid = new List<int>(4096);
        float[] reaches = { 0f, 0.5f, 1.7f, 2.1f, 9f, 12f, 14f, 22f, 25f, 30f, 40f, 45f, 60f, 120f, 250f, -40f };
        int asked = 0, same = 0, sameCalls = 0, found = 0, ordered = 0, holds = 0;
        for (int q = 0; q < 6000; q++)
        {
            // Places on lanes, between them, off the map, and exactly on a lane point.
            float x, y, z;
            int kind = q % 4;
            if (kind == 3)
            {
                int on = rng.Next(px.Length);
                x = px[on];
                y = py[on];
                z = pz[on];
            }
            else
            {
                x = -300f + (float)rng.NextDouble() * (kind == 2 ? 6000f : 2600f);
                z = -2900f + (float)rng.NextDouble() * (kind == 2 ? 6000f : 5200f);
                y = (float)rng.NextDouble() * 4f - 1f;
            }
            float reach = reaches[q % reaches.Length];
            int filter = (q / reaches.Length) % 5;
            callsAll.Clear();
            callsGrid.Clear();
            int a = All(px, py, pz, x, y, z, reach, filter, callsAll);
            grid.Candidates(x, z, Math.Abs(reach), candidates);
            int b = Over(candidates, px, py, pz, x, y, z, reach, filter, callsGrid);
            asked++;
            same += a == b ? 1 : 0;
            found += a >= 0 ? 1 : 0;
            sameCalls += SameList(callsAll, callsGrid) ? 1 : 0;
            ordered += Ascending(candidates) ? 1 : 0;
            holds += HoldsEveryNear(candidates, px, pz, x, z, Math.Abs(reach)) ? 1 : 0;
        }
        Check(same == asked, "point grid: the nearest lane point is the traffic system's own in every one of " + asked + " questions (" + same + " the same; " + found + " found one)");
        Check(sameCalls == asked, "point grid: the filter is asked of the same points in the same order as the traffic system asks it (" + sameCalls + " of " + asked + ")");
        Check(ordered == asked && holds == asked, "point grid: what a question goes through is in the points' own order and holds every point within reach on the ground (" + ordered + ", " + holds + " of " + asked + ")");
        Check(found > asked / 10 && found < asked - asked / 10, "point grid: the questions found a point and found none in turn, so both were tried (" + found + " of " + asked + ")");

        // The edges: no points, one point, every point on one spot.
        var empty = new PointGrid(new float[0], new float[0], 20f);
        empty.Candidates(0f, 0f, 50f, candidates);
        Check(empty.Count == 0 && candidates.Count == 0, "point grid: no points, nothing to go through");
        var one = new PointGrid(new[] { 5f }, new[] { 7f }, 20f);
        one.Candidates(5f, 7f, 0.1f, candidates);
        bool oneNear = candidates.Count == 1 && candidates[0] == 0;
        one.Candidates(500f, 700f, 10f, candidates);
        Check(oneNear && candidates.Count == 0, "point grid: one point, found from beside it and not from far off");
        var stack = new PointGrid(new[] { 1f, 1f, 1f }, new[] { 2f, 2f, 2f }, 20f);
        stack.Candidates(1f, 2f, 5f, candidates);
        callsGrid.Clear();
        int first = Over(candidates, new[] { 1f, 1f, 1f }, new[] { 0f, 0f, 0f }, new[] { 2f, 2f, 2f }, 1f, 0f, 2f, 5f, 0, callsGrid);
        Check(first == 0, "point grid: of three points on one spot, the first in the list is the answer, as the traffic system's");
        bool threw = false;
        try
        {
            _ = new PointGrid(new[] { 1f }, new[] { 1f, 2f }, 20f);
        }
        catch (ArgumentException)
        {
            threw = true;
        }
        Check(threw, "point grid: an x without its z is refused");

        GridCost(grid, px, py, pz, candidates);
        NoWholeRoadQuestions();
    }

    // Over the mod's sources as they ship: nothing asks the traffic system for the nearest lane point itself (a pass over every
    // lane point of the city) but TrafficTools.ClosestWaypoint, where the grid cannot answer.
    private static void NoWholeRoadQuestions()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        if (!System.IO.Directory.Exists(scripts))
        {
            Check(false, "point grid: the mod's sources are at " + scripts);
            return;
        }
        int files = 0;
        var asking = new List<string>();
        foreach (string file in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            files++;
            string code = Code(System.IO.File.ReadAllText(file));
            if (code.Contains(".GetClosestWaypoint(") && !file.EndsWith(@"Traffic\TrafficTools.cs", StringComparison.OrdinalIgnoreCase))
            {
                asking.Add(file.Substring(scripts.Length + 1));
            }
        }
        Check(files > 1000 && asking.Count == 0, "point grid: the nearest lane point is asked of the lane grid everywhere (TrafficTools.ClosestWaypoint), never of the traffic system's pass over every point"
                                                  + (asking.Count > 0 ? ": " + string.Join(", ", asking) : " (" + files + " files)"));
    }

    private static void Add(List<float> xs, List<float> ys, List<float> zs, float x, float y, float z)
    {
        xs.Add(x);
        ys.Add(y);
        zs.Add(z);
    }

    // The filters the mod hands: none, one that takes all, one that takes some, one that collects and takes none, one that
    // turns down the near ones. Each call is written down.
    private static bool Filter(int kind, int point, List<int> calls)
    {
        calls.Add(point);
        switch (kind)
        {
            case 1: return true;
            case 2: return point % 3 == 0;
            case 3: return false;
            default: return point % 7 != 0;
        }
    }

    // The traffic system's loop (GleyTrafficSystem.DensityManager.GetClosestWaypoint), over every point, with Unity's
    // SqrMagnitude of the difference written out in the same order.
    private static int All(float[] px, float[] py, float[] pz, float x, float y, float z, float reach, int filter, List<int> calls)
    {
        float nearest = reach * reach;
        int result = -1;
        for (int i = 0; i < px.Length; i++)
        {
            float dx = x - px[i], dy = y - py[i], dz = z - pz[i];
            float distance = dx * dx + dy * dy + dz * dz;
            if (distance >= nearest || (filter != 0 && !Filter(filter, i, calls)))
            {
                continue;
            }
            result = i;
            nearest = distance;
        }
        return result;
    }

    // The same loop over the grid's points only (Traffic/TrafficTools.ClosestWaypoint).
    private static int Over(List<int> candidates, float[] px, float[] py, float[] pz, float x, float y, float z, float reach, int filter, List<int> calls)
    {
        float nearest = reach * reach;
        int result = -1;
        for (int k = 0; k < candidates.Count; k++)
        {
            int i = candidates[k];
            float dx = x - px[i], dy = y - py[i], dz = z - pz[i];
            float distance = dx * dx + dy * dy + dz * dz;
            if (distance >= nearest || (filter != 0 && !Filter(filter, i, calls)))
            {
                continue;
            }
            result = i;
            nearest = distance;
        }
        return result;
    }

    private static bool SameList(List<int> a, List<int> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    private static bool Ascending(List<int> list)
    {
        for (int i = 1; i < list.Count; i++)
        {
            if (list[i] <= list[i - 1])
            {
                return false;   // (strictly: no point twice)
            }
        }
        return true;
    }

    private static bool HoldsEveryNear(List<int> list, float[] px, float[] pz, float x, float z, float reach)
    {
        var held = new HashSet<int>(list);
        for (int i = 0; i < px.Length; i++)
        {
            float dx = x - px[i], dz = z - pz[i];
            if (dx * dx + dz * dz < reach * reach && !held.Contains(i))
            {
                return false;
            }
        }
        return true;
    }

    // What a question costs: the traffic system's pass over every point against the grid's, at the distances the mod asks
    // (40 m), and what the grid allocates once its list has grown (nothing).
    private static void GridCost(PointGrid grid, float[] px, float[] py, float[] pz, List<int> candidates)
    {
        const int Questions = 4000;
        double allMs = AllQuestions(px, py, pz, Questions);
        long allocated = GridQuestions(grid, px, py, pz, candidates, Questions, out double gridMs);
        Check(gridMs * 10.0 < allMs, "point grid: " + Questions + " questions within 40 m take " + gridMs.ToString("F1") + " ms through the grid against " + allMs.ToString("F1") + " ms through every point (wanted ten times faster at least)");
        Check(allocated == 0, "point grid: questions through the grid allocate nothing once its list has grown (" + allocated + " bytes)");
        Console.WriteLine("point grid: a question within 40 m: " + (allMs * 1000.0 / Questions).ToString("F1") + " microseconds through every one of " + px.Length + " points, "
                          + (gridMs * 1000.0 / Questions).ToString("F1") + " through the grid");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double AllQuestions(float[] px, float[] py, float[] pz, int questions)
    {
        var calls = new List<int>();
        var watch = Stopwatch.StartNew();
        int sum = 0;
        for (int q = 0; q < questions; q++)
        {
            sum += All(px, py, pz, (q * 37) % 2100, 0.5f, (q * 53) % 2100, 40f, 0, calls);
        }
        watch.Stop();
        _gridSink = sum;
        return watch.Elapsed.TotalMilliseconds;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long GridQuestions(PointGrid grid, float[] px, float[] py, float[] pz, List<int> candidates, int questions, out double ms)
    {
        var calls = new List<int>();
        for (int q = 0; q < questions; q++)
        {
            grid.Candidates((q * 37) % 2100, (q * 53) % 2100, 40f, candidates);   // (the list grows to the most a question holds)
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        int sum = 0;
        for (int q = 0; q < questions; q++)
        {
            float x = (q * 37) % 2100, z = (q * 53) % 2100;
            grid.Candidates(x, z, 40f, candidates);
            sum += Over(candidates, px, py, pz, x, 0.5f, z, 40f, 0, calls);
        }
        watch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        _gridSink = sum;
        ms = watch.Elapsed.TotalMilliseconds;
        return allocated;
    }

    private static int _gridSink;
}
