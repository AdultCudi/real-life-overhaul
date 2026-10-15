using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using RealLifeOverhaul;

// The driving school's route planner (Licences/Driving/TestRouteRules.cs) on a made-up town of two-way streets: a route
// with no lane closed, a cordon's closed lanes driven round, the car's own lane shut (Closed) and the lane the other way
// open, a turning into a closed stretch that the old walk ran into and stopped at, a road that leads nowhere (NoRoad),
// the reason said to the player, the texts, the cost, and the desk asking before the fee is taken.
internal static partial class Program
{
    private const float BlockMetres = 100f;   // the made-up town: streets this long between crossings...
    private const float PointMetres = 10f;    // ...with a lane point this often, as the game's are spaced

    /// <summary>A made-up town for the planner: crossings on a square grid, each street a lane each way of points 10 m apart.</summary>
    private sealed class GridTown : RouteRoads
    {
        public readonly List<float> X = new List<float>();
        public readonly List<float> Z = new List<float>();
        public readonly List<List<int>> Next = new List<List<int>>();
        public readonly List<bool> IsPlain = new List<bool>();
        public readonly bool[] Shut;
        public readonly int Size;
        private readonly Dictionary<long, List<int>> _streets = new Dictionary<long, List<int>>();

        public GridTown(int size)
        {
            Size = size;
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    Add(i * BlockMetres, j * BlockMetres, false);   // the crossing: a junction, never a checkpoint
                }
            }
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    if (i + 1 < size)
                    {
                        Street(Crossing(i, j), Crossing(i + 1, j));
                        Street(Crossing(i + 1, j), Crossing(i, j));
                    }
                    if (j + 1 < size)
                    {
                        Street(Crossing(i, j), Crossing(i, j + 1));
                        Street(Crossing(i, j + 1), Crossing(i, j));
                    }
                }
            }
            Shut = new bool[X.Count];
        }

        public int Crossing(int i, int j) => i * Size + j;

        /// <summary>The lane points of the street from one crossing to the next, in the order they are driven.</summary>
        public List<int> Lane(int from, int to) => _streets[(long)from * 100000 + to];

        private int Add(float x, float z, bool plain)
        {
            X.Add(x);
            Z.Add(z);
            Next.Add(new List<int>(4));
            IsPlain.Add(plain);
            return X.Count - 1;
        }

        private void Street(int from, int to)
        {
            var lane = new List<int>();
            int count = (int)(BlockMetres / PointMetres) - 1;
            int last = from;
            for (int k = 1; k <= count; k++)
            {
                float t = k / (float)(count + 1);
                int point = Add(X[from] + (X[to] - X[from]) * t, Z[from] + (Z[to] - Z[from]) * t, k > 1 && k < count);
                Next[last].Add(point);
                lane.Add(point);
                last = point;
            }
            Next[last].Add(to);
            _streets[(long)from * 100000 + to] = lane;
        }

        public void Open() => Array.Clear(Shut, 0, Shut.Length);

        public override int Count => X.Count;
        public override int Ways(int point) => Next[point].Count;
        public override int Way(int point, int k) => Next[point][k];
        public override bool Open(int point, bool closures) => !closures || !Shut[point];
        public override bool Plain(int point, bool closures) => IsPlain[point] && (!closures || !Shut[point]);

        public override float Metres(int from, int to)
        {
            float dx = X[to] - X[from], dz = Z[to] - Z[from];
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }

    // The route as the planner hands it over: every step one of the ways on, no point twice, none closed, and its checkpoints laid.
    private static bool Sound(GridTown town, List<int> path, out string why)
    {
        var seen = new HashSet<int>();
        for (int i = 0; i < path.Count; i++)
        {
            if (!seen.Add(path[i]))
            {
                why = "point " + path[i] + " twice";
                return false;
            }
            if (town.Shut[path[i]])
            {
                why = "closed point " + path[i] + " on it";
                return false;
            }
            if (i > 0 && !town.Next[path[i - 1]].Contains(path[i]))
            {
                why = "a step that is no way on at " + i;
                return false;
            }
        }
        why = null;
        return true;
    }

    // The walk the school used before (TestRoute.Next until 0.25.0): a random open way on at each point, stopped at the first
    // point with none, five checkpoints or no lesson.
    private static bool OldWalk(GridTown town, int start, int wanted, int seed)
    {
        var random = new Random(seed);
        int at = start, laid = 0;
        float since = TestRoutePlanner.Spacing * (1f - TestRoutePlanner.FirstAt);
        for (int step = 0; step < 900 && laid < wanted; step++)
        {
            List<int> ways = town.Next[at];
            int first = random.Next(ways.Count), next = -1;
            for (int i = 0; i < ways.Count && next < 0; i++)
            {
                int way = ways[(first + i) % ways.Count];
                if (!town.Shut[way])
                {
                    next = way;
                }
            }
            if (next < 0)
            {
                break;
            }
            since += town.Metres(at, next);
            if (since >= TestRoutePlanner.Spacing && town.Plain(next, true))
            {
                laid++;
                since = 0f;
            }
            at = next;
        }
        return laid >= TestRoutePlanner.Least;
    }

    private static void TestRoutes()
    {
        var town = new GridTown(7);
        var planner = new TestRoutePlanner();
        var laid = new List<int>();
        int a = town.Crossing(3, 3), b = town.Crossing(4, 3), c = town.Crossing(5, 3);
        int start = town.Lane(a, b)[2];

        // No lane closed: a route with every checkpoint asked for, laid as the planning counted, and the stretch for the bay.
        RouteOutcome open = planner.Plan(town, start, 7, 12345, true);
        Check(open == RouteOutcome.Planned && planner.Checkpoints == 7, "lesson route: with no lane closed, seven checkpoints are planned (" + open + ", " + planner.Checkpoints + ")");
        Check(Sound(town, planner.Path, out string why), "lesson route: every step is a way on, no point twice, none closed" + (why != null ? " (" + why + ")" : string.Empty));
        Check(TestRoutePlanner.Layout(town, planner.Path, 7, laid) == planner.Checkpoints, "lesson route: the drive lays as many checkpoints as the planning counted (" + laid.Count + ")");
        float tail = 0f;
        for (int i = laid.Count > 0 ? laid[laid.Count - 1] + 1 : 0; i < planner.Path.Count; i++)
        {
            tail += town.Metres(planner.Path[i - 1], planner.Path[i]);
        }
        Check(tail >= TestRoutePlanner.Tail, "lesson route: past the last checkpoint the route runs on for the bay (" + tail.ToString("F0") + " m of " + TestRoutePlanner.Tail + ")");
        bool spaced = laid.Count > 1;
        for (int i = 1; i < laid.Count; i++)
        {
            float between = 0f;
            for (int k = laid[i - 1] + 1; k <= laid[i]; k++)
            {
                between += town.Metres(planner.Path[k - 1], planner.Path[k]);
            }
            spaced &= between >= TestRoutePlanner.Spacing;
        }
        Check(spaced, "lesson route: the checkpoints stand a spacing apart along the road");

        // The same seed, the same route on every machine; other seeds turn other ways.
        var first = new List<int>(planner.Path);
        planner.Plan(town, start, 7, 12345, true);
        bool same = first.Count == planner.Path.Count;
        for (int i = 0; same && i < first.Count; i++)
        {
            same = first[i] == planner.Path[i];
        }
        Check(same, "lesson route: the same seed plans the same route");
        bool differs = false;
        for (int seed = 1; seed < 20 && !differs; seed++)
        {
            planner.Plan(town, start, 7, seed, true);
            differs = planner.Path.Count != first.Count || planner.Path[planner.Path.Count - 1] != first[first.Count - 1];
        }
        Check(differs, "lesson route: other seeds give other routes");
        Check(planner.Plan(town, start, 3, 7, true) == RouteOutcome.Planned && planner.Checkpoints == TestRoutePlanner.Least, "lesson route: fewer than five asked for is five");
        Check(planner.Plan(town, start, 20, 7, true) == RouteOutcome.Planned && planner.Checkpoints == TestRoutePlanner.Most, "lesson route: more than seven asked for is seven");

        // The owner's case: an accident right by the desk, both directions shut (the cordon closes the lanes from 30 m before
        // the wrecks). From the car's lane on the desk's side nothing moves: Closed, said as the road being shut, not NoRoad.
        List<int> ab = town.Lane(a, b), ba = town.Lane(b, a);
        for (int k = 4; k < ab.Count; k++)
        {
            town.Shut[ab[k]] = true;
        }
        for (int k = 0; k < 5; k++)
        {
            town.Shut[ba[k]] = true;
        }
        Check(planner.Plan(town, ab[1], 7, 99, true) == RouteOutcome.Closed && planner.Path.Count == 0,
            "lesson route: from a lane that runs straight into the cordon there is no route, and the closure is why (Closed)");
        Check(planner.Plan(town, ab[1], 7, 99, false) == RouteOutcome.NoRoad, "lesson route: not asked why, it is only no route");
        Check(planner.Plan(town, ab[5], 7, 99, true) != RouteOutcome.Planned, "lesson route: a closed point is never a start");
        RouteOutcome otherSide = planner.Plan(town, ba[6], 7, 99, true);
        Check(otherSide == RouteOutcome.Planned && Sound(town, planner.Path, out why),
            "lesson route: from the lane the other way, past its closed stretch, a route is planned that keeps off every closed point (" + otherSide + (why != null ? ", " + why : string.Empty) + ")");

        // A turning into a closed stretch: the street straight on from the next crossing is shut from its third point on (its
        // way in left open, as a cordon's apron is). The old walk ran into it and stopped for some seeds; the planner goes back
        // and turns another way for every seed.
        town.Open();
        List<int> bc = town.Lane(b, c);
        for (int k = 2; k < bc.Count; k++)
        {
            town.Shut[bc[k]] = true;
        }
        foreach (int crossing in new[] { town.Crossing(4, 2), town.Crossing(4, 4) })
        {
            List<int> side = town.Lane(b, crossing);
            for (int k = 2; k < side.Count; k++)
            {
                town.Shut[side[k]] = true;   // the turnings at that crossing too: only back the way it came is open
            }
        }
        int oldFails = 0, newFails = 0;
        for (int seed = 1; seed <= 200; seed++)
        {
            oldFails += OldWalk(town, ab[0], 7, seed) ? 0 : 1;
            RouteOutcome outcome = planner.Plan(town, ab[0], 7, seed, true);
            newFails += outcome == RouteOutcome.Planned && Sound(town, planner.Path, out _) ? 0 : 1;
        }
        Check(oldFails > 0, "lesson route: the old walk runs into the closed stretch and gives up for some seeds (" + oldFails + " of 200): the fault the owner met");
        Check(newFails == 0, "lesson route: the planner finds a route round it for every seed (" + newFails + " of 200 failed)");

        // Every way out shut: Closed (the road clears by itself); a road that leads nowhere at all: NoRoad.
        town.Open();
        for (int i = 0; i < town.Size; i++)
        {
            for (int j = 0; j < town.Size; j++)
            {
                if (town.Crossing(i, j) != a)
                {
                    town.Shut[town.Crossing(i, j)] = true;
                }
            }
        }
        Check(planner.Plan(town, ab[0], 7, 5, true) == RouteOutcome.Closed, "lesson route: with every crossing round it shut, the closure is said");
        town.Open();
        var spur = new GridTown(1);
        Check(planner.Plan(spur, 0, 7, 5, true) == RouteOutcome.NoRoad, "lesson route: where no road goes on, closures or not, it is NoRoad");
        Check(planner.Plan(town, -1, 7, 5, true) == RouteOutcome.NoRoad && planner.Plan(town, town.Count, 7, 5, true) == RouteOutcome.NoRoad,
            "lesson route: a start that is no lane point plans nothing");

        // What the player is told.
        Check(TestRouteRules.Refusal(true, true, true) == null, "lesson refusal: a place found is no refusal");
        Check(TestRouteRules.Refusal(false, true, false) == TestRouteRules.ClosedKey && TestRouteRules.Refusal(false, true, true) == TestRouteRules.ClosedKey,
            "lesson refusal: a lane closed for now is said first (it clears by itself)");
        Check(TestRouteRules.Refusal(false, false, true) == TestRouteRules.NoRouteKey, "lesson refusal: room for the car but no drive from it is no route");
        Check(TestRouteRules.Refusal(false, false, false) == TestRouteRules.NoSpotKey, "lesson refusal: no room at the roadside is said as that");

        TestRouteWords();
        TestRouteSources();
        TestRouteCost();
    }

    private static readonly string[] TestRouteKeys = { "reallife_lic_no_route_closed", "reallife_lic_test_closed_skip", "reallife_lic_sheet_end_closed", "reallife_lic_no_route" };

    private static void TestRouteWords()
    {
        string folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
        var words = new Dictionary<string, Dictionary<string, string>>();
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            var map = new Dictionary<string, string>();
            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, code + ".json")), new JsonDocumentOptions { AllowTrailingCommas = true }))
            {
                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    map[property.Name] = property.Value.GetString();
                }
            }
            words[code] = map;
        }
        var missing = new List<string>();
        foreach (string key in TestRouteKeys)
        {
            foreach (string code in words.Keys)
            {
                if (!words[code].TryGetValue(key, out string text) || string.IsNullOrWhiteSpace(text))
                {
                    missing.Add(code + ":" + key);
                }
            }
        }
        Check(missing.Count == 0, "lesson words: every text is in en, pt and zh-cn" + (missing.Count > 0 ? " (" + string.Join(", ", missing) + ")" : string.Empty));
        if (missing.Count > 0)
        {
            return;
        }
        Dictionary<string, string> en = words["en"];
        string closed = en[TestRouteRules.ClosedKey];
        Check(closed.Contains("closed by an accident") && closed.Contains("once it has been cleared"),
            "lesson words: a road shut by an accident is said plainly, with when to come back (" + closed + ")");
        Check(!en[TestRouteRules.NoRouteKey].Contains("another desk"), "lesson words: no route no longer sends the player to another desk (the city has one)");
        bool fits = true, plain = true;
        foreach (string key in TestRouteKeys)
        {
            string text = en[key];
            fits &= text.Length <= 240;
            plain &= text.IndexOf('!') < 0 && text.IndexOf('–') < 0 && text.IndexOf('—') < 0 && !text.Contains(" - ");
        }
        Check(fits, "lesson words: each is a notification of 240 characters at most");
        Check(plain, "lesson words: no exclamation mark and no dash");
        Check(words["pt"][TestRouteRules.ClosedKey] != closed && words["zh-cn"][TestRouteRules.ClosedKey] != closed, "lesson words: pt and zh-cn are translated, not English");
        bool told = false;   // (in the next row, or its version's row once the version is made)
        foreach (var row in en)
        {
            if (row.Key.StartsWith("reallife_changes_", System.StringComparison.Ordinal) && row.Value.IndexOf("lessons and tests plan their route round a road an accident has closed", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                told = true;
            }
        }
        Check(told,
            "lesson words: What's new says the fix");
    }

    // The desk asks before the fee is taken; a refusal there leaves the list up, nothing paid.
    private static void TestRouteSources()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string director = Code(File.ReadAllText(Path.Combine(scripts, @"Licences\LicenceDirector.cs")));
        int take = director.IndexOf("internal string Take(SchoolService service)", StringComparison.Ordinal);
        int stage = take < 0 ? -1 : director.IndexOf("_practical.CanStage(", take, StringComparison.Ordinal);
        int pay = take < 0 ? -1 : director.IndexOf("Licences.Pay(fee", take, StringComparison.Ordinal);
        Check(take >= 0 && stage > take && pay > stage, "lesson desk: the route is asked for before the fee is taken");
        string desk = Code(File.ReadAllText(Path.Combine(scripts, @"Licences\SchoolDesk.cs")));
        Check(desk.Contains("bool failed = LicenceDirector.Refused(done);"), "lesson desk: a refusal of the car is taken as one (nothing booked, the list stays up)");
        string refusals = director.Substring(Math.Max(0, director.IndexOf("RefusalKeys =", StringComparison.Ordinal)));
        refusals = refusals.Substring(0, Math.Max(0, refusals.IndexOf("};", StringComparison.Ordinal)));
        bool every = refusals.Length > 0;
        foreach (string key in new[] { "\"reallife_school_no_money\"", "\"reallife_lic_no_busy\"", "\"reallife_lic_no_on_foot\"", "\"reallife_lic_no_roads\"", "TestRouteRules.NoSpotKey", "TestRouteRules.NoRouteKey", "TestRouteRules.ClosedKey" })
        {
            every &= refusals.Contains(key);
        }
        Check(every, "lesson desk: every reason the car cannot go out counts as a refusal (nothing booked, nothing paid)");
        string route = Code(File.ReadAllText(Path.Combine(scripts, @"Licences\Driving\TestRoute.cs")));
        Check(!route.Contains("Random.") && route.Contains("_planner.Plan("), "lesson route: the game's side has no walk of its own; the planner's route is the one laid");
        string car = Code(File.ReadAllText(Path.Combine(scripts, @"Licences\Driving\TestCar.cs")));
        Check(car.Contains("route(point,") && car.Contains("_routeOther"), "lesson car: the car is set out only where a route starts, the lane the other way tried too");
        string test = Code(File.ReadAllText(Path.Combine(scripts, @"Licences\Driving\PracticalTest.cs")));
        Check(test.Contains("_route.ClosedAt(_next)"), "lesson drive: a checkpoint's lane closed during the drive is looked at");
    }

    private static void TestRouteCost()
    {
        var big = new GridTown(30);   // some 35,000 lane points: more than the game's city
        var planner = new TestRoutePlanner();
        int start = big.Lane(big.Crossing(15, 15), big.Crossing(16, 15))[1];
        for (int i = 0; i < big.IsPlain.Count; i++)
        {
            big.IsPlain[i] = false;   // nowhere a checkpoint may stand: every planning runs to its bound, twice (asked why)
        }
        // The worst case: nothing found with the closures, then asked again without them, each to the bound. Warmed once, then timed.
        Plans(big, planner, start, 1, out _, out _);
        long allocated = Plans(big, planner, start, 20, out double ms, out int expanded);
        Check(expanded <= TestRoutePlanner.Budget * 2, "lesson route cost: a planning goes into no more than its bound (" + expanded + ")");
        Check(ms / 20 < 5.0, "lesson route cost: the worst planning, asked why, takes under 5 ms (" + (ms / 20).ToString("F2") + " ms)");
        Check(allocated / 20 < 16 * 1024, "lesson route cost: a planning allocates almost nothing once warmed (" + allocated / 20 + " bytes)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long Plans(GridTown town, TestRoutePlanner planner, int start, int times, out double ms, out int expanded)
    {
        var watch = new Stopwatch();
        expanded = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        watch.Start();
        for (int i = 0; i < times; i++)
        {
            planner.Plan(town, start, 7, i + 1, true);
            expanded = Math.Max(expanded, planner.Expanded);
        }
        watch.Stop();
        ms = watch.Elapsed.TotalMilliseconds;
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
