using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using RealLifeOverhaul;
using RealLifeOverhaul.Nav;

// Checks for the game-free parts of the minimap and the GPS. A check that fails prints one line; the last line is
// ALL CHECKS PASSED or the count that failed. Given the path of a file of lane points it then times the search and
// the map picture on that city (timings are printed, never judged). The bus bays are checked here too (BayCheck.cs); with
// --bays as an argument each run of the model bus is printed with what it measured. And the rules of the taxi called from
// the phone (TaxiCheck.cs), the rules of the horn and the reckless drivers (StreetCheck.cs),
// the minimap's buildings and water (BaseCheck.cs), and the small rules of the panels, the bars and the
// camera's stand-down (PolishCheck.cs), the ambulance crew's way as geometry (CrewCheck.cs; with --crew it prints what a
// planning's worth of ways costs), the game-free parts of the work against long frames (HitchCheck.cs), and getting out of the driving school's car (CarExitCheck.cs), and the furnished police station's walkways and cast (StationCheck.cs), and the tow truck's bed, pull and stand (TowCheck.cs), and the tech sector's buildings (TechSiteCheck.cs), and the fit-out of the mod's buildings (FitCheck.cs; with --fit it draws the plans), and whose a car is in a Going Public session and which car of the traffic pool may be put on the road (CarCheck.cs), and the build check and the guard against an older game (BuildCheck.cs).
internal static partial class Program
{
    private static int _failed, _run;

    private static void Check(bool ok, string what)
    {
        _run++;
        if (!ok)
        {
            _failed++;
            Console.WriteLine("FAIL  " + what);
        }
    }

    private static bool Near(float a, float b, float within = 0.01f)
    {
        return Math.Abs(a - b) <= within;
    }

    private static int Main(string[] args)
    {
        Numbers();
        SearchWholeAndSliced();
        Index();
        GridRoutes();
        OnFoot();
        Following();
        View();
        Picture();
        Base();
        Bays(Array.IndexOf(args, "--bays") >= 0);
        Taxi();
        Streets();
        Polish();
        Hitch();
        Hair();
        Solid();
        Crew(Array.IndexOf(args, "--crew") >= 0);
        CarExit();
        Station();
        Tow();
        TechSites();
        Fits();
        Cars();
        Builds();
        if (Array.IndexOf(args, "--sites") >= 0)
        {
            DrawSites();
        }
        if (Array.IndexOf(args, "--fit") >= 0)
        {
            DrawFits();
        }
        Console.WriteLine(_failed == 0 ? "ALL CHECKS PASSED (" + _run + ")" : _failed + " of " + _run + " CHECKS FAILED");
        if (_failed == 0 && args.Length > 0 && File.Exists(args[0]))
        {
            City(args[0]);
        }
        return _failed == 0 ? 0 : 1;
    }

    // ---------------------------------------------------------------- a made-up town

    // A square grid of two-way streets, built the way the game names its lane points: every block is a road with a
    // lane each way ("Road_7-Lane_0-Waypoint_3"), and at every crossing each lane that arrives is joined to each
    // street that leaves by two points named after the road they come from ("Road_7-Lane_0-Connector2").
    private sealed class Town
    {
        public const float Block = 100f;
        public const float Inset = 15f;       // a lane's own points stop this far from the middle of a crossing
        public const float Step = 5f;
        public const float Lane = 2f;         // a lane's middle is this far to the right of the street's

        public readonly List<float> X = new List<float>();
        public readonly List<float> Z = new List<float>();
        public readonly List<string> Name = new List<string>();
        public readonly List<List<int>> Next = new List<List<int>>();
        private readonly Dictionary<long, int[]> _lanes = new Dictionary<long, int[]>();   // (from crossing, to crossing) -> first and last point
        public readonly int Size;
        public int Roads;

        public RouteSearch Drive, Walk;
        public int[] Road;
        public bool[] Junction, Usable;
        public float[] Speed;
        public NavIndex Places;

        private readonly int _naming;   // a junction's points are named after: 0 the road left, 1 the road entered, 2 a road that is neither

        public Town(int size, Func<int, int, int, int, bool> oneWayOnly = null, int naming = 0)
        {
            Size = size;
            _naming = naming;
            for (int cx = 0; cx < size; cx++)
            {
                for (int cz = 0; cz < size; cz++)
                {
                    if (cx + 1 < size)
                    {
                        Street(cx, cz, cx + 1, cz, oneWayOnly);
                    }
                    if (cz + 1 < size)
                    {
                        Street(cx, cz, cx, cz + 1, oneWayOnly);
                    }
                }
            }
            for (int cx = 0; cx < size; cx++)
            {
                for (int cz = 0; cz < size; cz++)
                {
                    Crossing(cx, cz);
                }
            }
            int count = X.Count;
            Drive = new RouteSearch(count);
            Road = new int[count];
            Junction = new bool[count];
            Usable = new bool[count];
            Speed = new float[count];
            var start = new int[count + 1];
            var to = new List<int>();
            for (int i = 0; i < count; i++)
            {
                Drive.X[i] = X[i];
                Drive.Z[i] = Z[i];
                Road[i] = NavMath.RoadOf(Name[i]);
                Junction[i] = NavMath.IsConnector(Name[i]);
                Usable[i] = true;
                Speed[i] = 10f;
                start[i] = to.Count;
                to.AddRange(Next[i]);
            }
            start[count] = to.Count;
            Drive.SetEdges(start, to.ToArray(), new float[to.Count]);
            NavEdges.TwoWay(count, start, to.ToArray(), out int[] start2, out int[] to2, out float[] extra2);
            Walk = new RouteSearch(Drive);
            Walk.SetEdges(start2, to2, extra2);
            Places = new NavIndex(Drive.X, Drive.Z, Usable, count);
        }

        private static long Key(int ax, int az, int bx, int bz)
        {
            return ((long)ax << 48) | ((long)az << 32) | ((long)bx << 16) | (uint)bz;
        }

        private int Add(float x, float z, string name)
        {
            X.Add(x);
            Z.Add(z);
            Name.Add(name);
            Next.Add(new List<int>(2));
            return X.Count - 1;
        }

        private void Street(int ax, int az, int bx, int bz, Func<int, int, int, int, bool> oneWayOnly)
        {
            int road = Roads++;
            LaneOf(road, 0, ax, az, bx, bz);
            if (oneWayOnly == null || !oneWayOnly(ax, az, bx, bz))
            {
                LaneOf(road, 1, bx, bz, ax, az);
            }
        }

        private void LaneOf(int road, int lane, int ax, int az, int bx, int bz)
        {
            float dx = bx - ax, dz = bz - az;
            float rx = dz, rz = -dx;   // to the right of the way it runs
            int first = -1, before = -1, n = 0;
            for (float along = Inset; along <= Block - Inset + 0.01f; along += Step)
            {
                int point = Add(ax * Block + dx * along + rx * Lane, az * Block + dz * along + rz * Lane, "Road_" + road + "-Lane_" + lane + "-Waypoint_" + n++);
                if (before >= 0)
                {
                    Next[before].Add(point);
                }
                else
                {
                    first = point;
                }
                before = point;
            }
            _lanes[Key(ax, az, bx, bz)] = new[] { first, before };
        }

        private void Crossing(int cx, int cz)
        {
            int[][] around = { new[] { cx + 1, cz }, new[] { cx - 1, cz }, new[] { cx, cz + 1 }, new[] { cx, cz - 1 } };
            foreach (int[] from in around)
            {
                if (!_lanes.TryGetValue(Key(from[0], from[1], cx, cz), out int[] arriving))
                {
                    continue;
                }
                int k = 0;
                foreach (int[] to in around)
                {
                    if ((to[0] == from[0] && to[1] == from[1]) || !_lanes.TryGetValue(Key(cx, cz, to[0], to[1]), out int[] leaving))
                    {
                        continue;
                    }
                    int a = arriving[1], b = leaving[0];
                    string like = _naming == 1 ? Name[b] : Name[a];
                    string name = (_naming == 2 ? "Road_9999-Lane_0-" : like.Substring(0, like.LastIndexOf('-') + 1)) + (k % 2 == 0 ? "Connector" : "CConnect") + k++;
                    // two points on a curve through the crossing's middle
                    float mx = cx * Block, mz = cz * Block;
                    int p1 = Add(Curve(X[a], mx, X[b], 0.35f), Curve(Z[a], mz, Z[b], 0.35f), name);
                    int p2 = Add(Curve(X[a], mx, X[b], 0.65f), Curve(Z[a], mz, Z[b], 0.65f), name);
                    Next[a].Add(p1);
                    Next[p1].Add(p2);
                    Next[p2].Add(b);
                }
            }
        }

        private static float Curve(float a, float middle, float b, float t)
        {
            return (1 - t) * (1 - t) * a + 2 * (1 - t) * t * middle + t * t * b;
        }

        /// <summary>The middle point of the lane that runs from one crossing to the next.</summary>
        public int Mid(int ax, int az, int bx, int bz)
        {
            int[] lane = _lanes[Key(ax, az, bx, bz)];
            return (lane[0] + lane[1]) / 2;
        }

        public bool Route(RouteSearch graph, int from, int to, NavRoute route, List<int> path = null)
        {
            path = path ?? new List<int>();
            if (!graph.Find(new[] { from }, new[] { to }, graph.X[to], 0f, graph.Z[to], 0f, 1000000, path))
            {
                return false;
            }
            route.Build(path, graph, Road, Junction, Speed);
            return true;
        }
    }

    // ---------------------------------------------------------------- the checks

    private static void Numbers()
    {
        Check(NavMath.RoadOf("Road_25-Lane_3-Waypoint_7") == 25 && NavMath.RoadOf("Road_0-Lane_0-In") == 0 && NavMath.RoadOf("Road_237-Lane_1-CConnect11") == 237, "the road number in a lane point's name");
        Check(NavMath.RoadOf(null) == -1 && NavMath.RoadOf("") == -1 && NavMath.RoadOf("Road_") == -1 && NavMath.RoadOf("Lane_3") == -1 && NavMath.RoadOf("Road_x") == -1, "a name of another form has no road number");
        Check(NavMath.IsConnector("Road_3-Lane_2-CConnect6") && NavMath.IsConnector("Road_75-Lane_0-Connector4") && !NavMath.IsConnector("Road_75-Lane_0-Waypoint_4") && !NavMath.IsConnector(null), "which lane points are a junction's");
        Check(Near(NavMath.SignedAngle(0, 1, -1, 0), 90f) && Near(NavMath.SignedAngle(0, 1, 1, 0), -90f) && Near(NavMath.SignedAngle(1, 0, 1, 0), 0f), "left is positive: north to west is +90, north to east is -90");
        Check(Near(Math.Abs(NavMath.SignedAngle(0, 1, 0, -1)), 180f) && NavMath.SignedAngle(0, 0, 0, 0) == 0f, "about-face is 180; no direction is no angle");
        Check(NavMath.Classify(10f) == NavTurnKind.None && NavMath.Classify(-27f) == NavTurnKind.None, "a slight bend is not a turn");
        Check(NavMath.Classify(40f) == NavTurnKind.KeepLeft && NavMath.Classify(-40f) == NavTurnKind.KeepRight, "keep left and right");
        Check(NavMath.Classify(90f) == NavTurnKind.Left && NavMath.Classify(-90f) == NavTurnKind.Right && NavMath.Classify(120f) == NavTurnKind.Left, "turn left and right");
        Check(NavMath.Classify(170f) == NavTurnKind.UTurn && NavMath.Classify(-155f) == NavTurnKind.UTurn, "a U-turn either way");
        Check(NavMath.RoundMetres(3f) == 10 && NavMath.RoundMetres(44f) == 40 && NavMath.RoundMetres(196f) == 200 && NavMath.RoundMetres(430f) == 450 && NavMath.RoundMetres(1930f) == 1900, "distances as a satnav says them");
    }

    private static void SearchWholeAndSliced()
    {
        var town = new Town(6);
        int from = town.Mid(0, 0, 1, 0), to = town.Mid(4, 5, 5, 5);
        var whole = new List<int>();
        bool found = town.Drive.Find(new[] { from }, new[] { to }, town.Drive.X[to], 0f, town.Drive.Z[to], 0f, 1000000, whole);
        int expanded = town.Drive.Expanded;
        Check(found && whole.Count > 10 && whole[0] == from && whole[whole.Count - 1] == to, "a route across the made-up town is found");

        var sliced = new List<int>();
        Check(town.Drive.Begin(new[] { from }, new[] { to }, town.Drive.X[to], 0f, town.Drive.Z[to], 0f), "a search can be begun");
        int slices = 0;
        RouteSearch.Outcome outcome;
        do
        {
            outcome = town.Drive.Advance(town.Drive.Expanded + 7, sliced);
            slices++;
            Check(outcome != RouteSearch.Outcome.Searching || sliced.Count == 0, "nothing is in the path while the search is under way");
        }
        while (outcome == RouteSearch.Outcome.Searching && slices < 100000);
        bool same = outcome == RouteSearch.Outcome.Found && sliced.Count == whole.Count;
        for (int i = 0; same && i < whole.Count; i++)
        {
            same = sliced[i] == whole[i];
        }
        Check(same, "seven steps at a time gives the same route as all at once");
        Check(town.Drive.Expanded == expanded && slices >= expanded / 7, "and takes the same number of steps, over many slices (" + slices + ")");
        Check(town.Drive.Advance(int.MaxValue, sliced) == RouteSearch.Outcome.NoWay, "a finished search does not go on");

        var limited = new List<int>();
        Check(!town.Drive.Find(new[] { from }, new[] { to }, town.Drive.X[to], 0f, town.Drive.Z[to], 0f, 5, limited) && town.Drive.HitLimit && limited.Count == 0, "a search held to five steps says it hit its limit");
        Check(!town.Drive.Begin(new int[0], new[] { to }, 0f, 0f, 0f, 0f) && !town.Drive.Begin(new[] { from }, null, 0f, 0f, 0f, 0f), "no start or no end is no search");
        Check(town.Drive.Find(new[] { from }, new[] { from }, town.Drive.X[from], 0f, town.Drive.Z[from], 0f, 10, limited) && limited.Count == 1, "from a place to itself is a route of one point");

        // several ends: the nearer one is taken
        int near = town.Mid(1, 0, 2, 0);
        var either = new List<int>();
        Check(town.Drive.Find(new[] { from }, new[] { to, near }, town.Drive.X[near], 0f, town.Drive.Z[near], 800f, 1000000, either) && either[either.Count - 1] == near, "of two ends the nearer is reached");
    }

    private static void Index()
    {
        var town = new Town(4);
        var random = new Random(7);
        bool agree = true;
        var found = new List<int>();
        int count = town.Drive.Count;
        for (int n = 0; n < 300 && agree; n++)
        {
            float x = (float)(random.NextDouble() * 400 - 50), z = (float)(random.NextDouble() * 400 - 50), within = 5f + (float)random.NextDouble() * 60f;
            int best = -1;
            float bestSqr = within * within;
            int inside = 0;
            for (int i = 0; i < count; i++)
            {
                float sqr = (town.Drive.X[i] - x) * (town.Drive.X[i] - x) + (town.Drive.Z[i] - z) * (town.Drive.Z[i] - z);
                inside += sqr <= within * within ? 1 : 0;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }
            int got = town.Places.Nearest(x, z, within);
            agree = got == best || (got >= 0 && best >= 0 && Near((town.Drive.X[got] - x) * (town.Drive.X[got] - x) + (town.Drive.Z[got] - z) * (town.Drive.Z[got] - z), bestSqr, 0.001f));
            found.Clear();
            town.Places.Within(x, z, within, found);
            agree &= found.Count == inside;
        }
        Check(agree, "the grid of cells finds what a look at every point finds");
        Check(town.Places.Nearest(-5000f, -5000f, 30f) == -1 && town.Places.Nearest(5000f, 9000f, 30f) == -1, "far outside the town nothing is near");
        Check(town.Places.Indexed == count && town.Places.MinX < town.Places.MaxX, "every usable point is in the index");
        var none = new NavIndex(new float[3], new float[3], new bool[3], 3);
        Check(none.Nearest(0f, 0f, 100f) == -1 && none.Indexed == 0, "an index of nothing answers nothing");
    }

    private static void GridRoutes()
    {
        var town = new Town(5);
        var route = new NavRoute();

        // east along the bottom street, then north: one left turn
        Check(town.Route(town.Drive, town.Mid(0, 0, 1, 0), town.Mid(1, 0, 1, 1), route), "a route round one corner");
        Check(route.Turns.Count == 2 && route.Turns[0].Kind == NavTurnKind.Left && route.Turns[1].Kind == NavTurnKind.Arrive && Near(route.Turns[0].Angle, 90f, 3f), "east then north is one left turn of 90 degrees, then the arrival" + Said(route));
        Check(route.Turns.Count > 0 && Near(route.Turns[0].At, 35f, 6f), "the turn is counted down to where the junction begins");
        Check(route.Turns.Count > 0 && route.Turns[0].Road == NavMath.RoadOf(town.Name[town.Mid(1, 0, 1, 1)]), "and names the road turned onto");
        Check(Near(route.Turns[route.Turns.Count - 1].At, route.Length), "the arrival is at the route's end");

        // however the junction's points are named, it is the same one turn
        for (int naming = 1; naming <= 2; naming++)
        {
            var other = new Town(5, null, naming);
            var named = new NavRoute();
            Check(other.Route(other.Drive, other.Mid(0, 0, 1, 0), other.Mid(1, 0, 1, 1), named) && named.Turns.Count == 2 && named.Turns[0].Kind == NavTurnKind.Left
                  && Near(named.Turns[0].Angle, 90f, 3f) && named.Turns[0].Road == NavMath.RoadOf(other.Name[other.Mid(1, 0, 1, 1)]),
                  "junction points named after " + (naming == 1 ? "the road entered" : "another road") + ": still one left turn of 90 degrees" + Said(named));
            Check(other.Route(other.Drive, other.Mid(0, 2, 1, 2), other.Mid(3, 2, 4, 2), named) && named.Turns.Count == 1, "...and straight on is still not a turn" + Said(named));
        }

        // a junction walked through by two of its curves, with a plain point where one ends and the next begins:
        // south down a street, back up into the junction and out to the east. One turn (left), not a U-turn and a right.
        {
            float[] xs = { 0, 0, 0, 0, 0, 0, 0, 1, 4, 9, 15, 20, 25, 30, 35 };
            float[] zs = { 60, 55, 50, 45, 40, 33, 26, 31, 37, 40, 40, 40, 40, 40, 40 };
            bool[] curve = { false, false, false, false, false, true, true, false, true, true, false, false, false, false, false };
            int[] roads = { 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 3, 3, 3, 3, 3 };
            var hook = new RouteSearch(xs.Length);
            var all = new List<int>();
            for (int i = 0; i < xs.Length; i++)
            {
                hook.X[i] = xs[i];
                hook.Z[i] = zs[i];
                all.Add(i);
            }
            var hooked = new NavRoute();
            hooked.Build(all, hook, roads, curve, null);
            Check(hooked.Turns.Count == 2 && hooked.Turns[0].Kind == NavTurnKind.Left && hooked.Turns[0].Road == 3 && Near(hooked.Turns[0].At, 20f, 0.1f),
                  "two curves of one junction with a plain point between them are one turn" + Said(hooked));
        }

        // north along the left street, then east: one right turn
        Check(town.Route(town.Drive, town.Mid(0, 0, 0, 1), town.Mid(0, 1, 1, 1), route) && route.Turns.Count == 2 && route.Turns[0].Kind == NavTurnKind.Right, "north then east is one right turn" + Said(route));

        // straight through three crossings: nothing but the arrival
        Check(town.Route(town.Drive, town.Mid(0, 2, 1, 2), town.Mid(3, 2, 4, 2), route) && route.Turns.Count == 1 && route.Turns[0].Kind == NavTurnKind.Arrive, "straight on through three crossings is not a turn" + Said(route));
        Check(Near(route.Length, 300f, 2f), "three blocks straight is 300 m (" + route.Length.ToString("F1") + ")");

        // a longer way: every turn is a left or a right, in an order that gets there
        Check(town.Route(town.Drive, town.Mid(0, 0, 1, 0), town.Mid(3, 4, 4, 4), route), "a route across the town");
        bool sane = route.Turns.Count >= 2 && route.Turns.Count <= 9;
        float before = -1f;
        for (int t = 0; t < route.Turns.Count; t++)
        {
            sane &= route.Turns[t].At > before && route.Turns[t].Kind != NavTurnKind.None;
            before = route.Turns[t].At;
        }
        Check(sane, "its turns are in order along it, and none is 'no turn'" + Said(route));
        Check(Near(route.SecondsFrom(0f), route.Length / 10f, 0.5f) && Near(route.SecondsFrom(route.Length), 0f) && route.SecondsFrom(route.Length / 2f) < route.SecondsFrom(0f), "time left is distance left over the lanes' speed");

        // the next turn for somebody on the way
        town.Route(town.Drive, town.Mid(0, 0, 1, 0), town.Mid(1, 0, 1, 1), route);
        Check(route.NextTurn(0f) == 0 && route.NextTurn(route.Turns[0].At + 5f) == 0 && route.NextTurn(route.Turns[0].At + 30f) == 1 && route.NextTurn(route.Length) == 1, "the next turn stays the next until its junction is behind");
        route.PointAt(0f, out float x0, out float z0);
        route.PointAt(route.Length, out float x1, out float z1);
        Check(Near(x0, route.X[0]) && Near(z0, route.Z[0]) && Near(x1, route.X[route.Count - 1]) && Near(z1, route.Z[route.Count - 1]), "the place at each end of the route");
        route.PointAt(-50f, out x0, out z0);
        route.PointAt(route.Length + 50f, out x1, out z1);
        Check(Near(x0, route.X[0]) && Near(x1, route.X[route.Count - 1]), "a distance off either end is the end");

        // the line that is drawn: fewer points, the same way
        town.Route(town.Drive, town.Mid(0, 0, 1, 0), town.Mid(3, 4, 4, 4), route);
        float[] lineX = null, lineY = null, lineZ = null;
        int thin = route.Thin(0f, ref lineX, ref lineY, ref lineZ);
        Check(thin >= 2 && thin < route.Count / 2 && Near(lineX[0], route.X[0]) && Near(lineZ[0], route.Z[0]) && Near(lineX[thin - 1], route.X[route.Count - 1]) && Near(lineZ[thin - 1], route.Z[route.Count - 1]),
              "the drawn line has under half the points (" + thin + " of " + route.Count + ") and the same two ends");
        float furthest = 0f;
        for (int i = 0; i < route.Count; i++)
        {
            float best = float.MaxValue;
            for (int k = 0; k + 1 < thin; k++)
            {
                float bx = lineX[k + 1] - lineX[k], bz = lineZ[k + 1] - lineZ[k];
                float t = Math.Clamp(((route.X[i] - lineX[k]) * bx + (route.Z[i] - lineZ[k]) * bz) / Math.Max(0.0001f, bx * bx + bz * bz), 0f, 1f);
                float ox = lineX[k] + bx * t - route.X[i], oz = lineZ[k] + bz * t - route.Z[i];
                best = Math.Min(best, (float)Math.Sqrt(ox * ox + oz * oz));
            }
            furthest = Math.Max(furthest, best);
        }
        Check(furthest < 1.5f, "and no point of the route is more than 1.5 m from it (" + furthest.ToString("F2") + " m)");
        float[] sameX = lineX;
        int later = route.Thin(route.Length * 0.6f, ref lineX, ref lineY, ref lineZ);
        route.PointAt(route.Length * 0.6f, out float fromX, out float fromZ);
        Check(later >= 2 && later < thin && ReferenceEquals(sameX, lineX) && Near(lineX[0], fromX) && Near(lineZ[0], fromZ), "cut back to where the player is, it begins there and reuses its arrays");
        Check(route.Thin(route.Length, ref lineX, ref lineY, ref lineZ) >= 1, "at the very end there is still a point to draw from");

        // a route of one point, and an empty one
        var one = new NavRoute();
        one.Build(new List<int> { 5 }, town.Drive, town.Road, town.Junction, town.Speed);
        int segment = 0;
        Check(one.Count == 1 && one.Length == 0f && one.Turns.Count == 1 && one.Locate(one.X[0] + 3f, 0f, one.Z[0], ref segment, out float a1, out float l1) && Near(l1, 3f) && one.SecondsFrom(0f) == 0f, "a route of one point is a route");
        one.Clear();
        Check(one.Count == 0 && !one.Locate(0f, 0f, 0f, ref segment, out a1, out l1) && one.NextTurn(0f) == -1, "no route answers nothing");

        // a second, shorter route in the same object leaves nothing of the first
        town.Route(town.Drive, town.Mid(0, 0, 1, 0), town.Mid(3, 4, 4, 4), route);
        float[] kept = route.X;
        town.Route(town.Drive, town.Mid(0, 0, 0, 1), town.Mid(0, 1, 1, 1), route);
        Check(ReferenceEquals(kept, route.X) && route.Turns.Count == 2, "a shorter route reuses the longer one's arrays");
    }

    private static string Said(NavRoute route)
    {
        string said = " [";
        for (int t = 0; t < route.Turns.Count; t++)
        {
            said += (t > 0 ? ", " : string.Empty) + route.Turns[t].Kind + "@" + route.Turns[t].At.ToString("F0") + "(" + route.Turns[t].Angle.ToString("F0") + ")";
        }
        return said + "]";
    }

    private static void OnFoot()
    {
        // The bottom street runs east only between the first two crossings.
        var town = new Town(4, (ax, az, bx, bz) => az == 0 && bz == 0 && ax == 0);
        var route = new NavRoute();
        int east = town.Mid(0, 0, 1, 0);
        int first = east - 3, last = east + 3;   // west of the middle, and east of it, on the one lane
        Check(town.Route(town.Drive, first, last, route) && Near(route.Length, 30f, 1f), "with the traffic along a one-way street");
        Check(town.Route(town.Drive, last, first, route) && route.Length > 250f, "against it a car goes round the block (" + route.Length.ToString("F0") + " m)");
        Check(town.Route(town.Walk, last, first, route) && Near(route.Length, 30f, 1f), "on foot the same street is walked either way");
        Check(town.Walk.X == town.Drive.X && town.Walk.EdgeTo.Length == town.Drive.EdgeTo.Length * 2, "the walking graph shares the places and has every edge both ways");
    }

    private static void Following()
    {
        var town = new Town(4);
        var route = new NavRoute();
        town.Route(town.Drive, town.Mid(0, 0, 1, 0), town.Mid(2, 0, 3, 0), route);
        int segment = 0;
        Check(route.Locate(route.X[0], 0f, route.Z[0], ref segment, out float along, out float lateral) && Near(along, 0f, 0.1f) && Near(lateral, 0f, 0.1f), "at the start of the route");
        route.PointAt(120f, out float x, out float z);
        Check(route.Locate(x, 0f, z + 6f, ref segment, out along, out lateral) && Near(along, 120f, 1f) && Near(lateral, 6f, 0.2f), "120 m along and 6 m to one side");
        Check(route.Locate(x, 30f, z, ref segment, out along, out lateral) == false || lateral > 1f, "a road 30 m overhead is not the one being driven");
        segment = 0;
        route.PointAt(route.Length - 10f, out x, out z);
        Check(route.Locate(x, 0f, z, ref segment, out along, out lateral) && Near(along, route.Length - 10f, 1f), "found again from anywhere when the last place known is far behind");

        var tracker = new NavTracker();
        NavEvent said = NavEvent.None;
        for (int tick = 0; tick < 7 && said == NavEvent.None; tick++)
        {
            said = tracker.Tick(0.25f, false, 50f, 500f, 600f);
        }
        Check(said == NavEvent.None, "off the route for under two seconds is not yet off it");
        for (int tick = 0; tick < 12 && said == NavEvent.None; tick++)
        {
            said = tracker.Tick(0.25f, false, 50f, 500f, 600f);
        }
        Check(said == NavEvent.Reroute, "off it for long enough (and four seconds after the route was made) a new one is asked for");
        int again = 0;
        for (int tick = 0; tick < 14; tick++)
        {
            again += tracker.Tick(0.25f, false, 50f, 500f, 600f) == NavEvent.Reroute ? 1 : 0;
        }
        Check(again == 0, "and not again within four seconds");
        tracker.Reset();
        bool steady = true;
        for (int tick = 0; tick < 200; tick++)
        {
            // a wide junction: off the line for a second and a half, back on it, again and again
            steady &= tracker.Tick(0.25f, false, tick % 8 < 6 ? 40f : 5f, 500f, 600f) == NavEvent.None;
        }
        Check(steady, "leaving the line for a moment at a time never asks for a new route");
        tracker.Reset();
        Check(tracker.Tick(0.25f, false, 3f, 200f, 27f) == NavEvent.Arrived && tracker.Tick(0.25f, true, 3f, 200f, 27f) == NavEvent.None && tracker.Tick(0.25f, true, 3f, 200f, 13f) == NavEvent.Arrived, "arriving: 28 m by car, 14 m on foot");
        Check(tracker.Tick(0.25f, false, 5f, 2f, 80f) == NavEvent.Arrived, "a car at the end of the road has arrived, though the place is off the road");
        tracker.Reset();
        bool walking = true;
        for (int tick = 0; tick < 60; tick++)
        {
            walking &= tracker.Tick(0.25f, true, 40f, 1f, 60f) == NavEvent.None;
        }
        Check(walking, "on foot the last steps from the road to the place are not 'off the route'");
        tracker.Reset();
        said = NavEvent.None;
        for (int tick = 0; tick < 40 && said == NavEvent.None; tick++)
        {
            said = tracker.Tick(0.25f, true, 25f, 300f, 400f);
        }
        Check(said == NavEvent.Reroute, "on foot 22 m from the line is off it");
    }

    private static void View()
    {
        Check(Near(NavView.MetresAcross(100, 0f), NavView.BaseMetres) && Near(NavView.MetresAcross(200, 0f), NavView.BaseMetres / 2f) && Near(NavView.MetresAcross(50, 3f), NavView.BaseMetres * 2f), "the zoom option: more per cent is closer");
        Check(Near(NavView.MetresAcross(100, NavView.WidenFrom), NavView.BaseMetres) && Near(NavView.MetresAcross(100, 60f), NavView.BaseMetres * (1f + NavView.WidestBy), 0.1f), "fast driving shows more of the city, up to a limit");
        bool rising = true;
        for (float speed = 0f; speed < 40f; speed += 1f)
        {
            rising &= NavView.MetresAcross(100, speed + 1f) >= NavView.MetresAcross(100, speed);
        }
        Check(rising, "and never less for going faster");
        Check(NavView.MetresAcross(0, 0f) > 0f && !float.IsInfinity(NavView.MetresAcross(-5, 0f)), "a zoom option of nothing does not divide by nothing");

        float value = 260f;
        for (int frame = 0; frame < 600; frame++)
        {
            value = NavView.Ease(value, 494f, 1f / 60f, 1.6f);
        }
        Check(value == 494f, "the view eases to its new width and lands on it");
        Check(NavView.Ease(100f, 100f, 0.016f, 1.6f) == 100f && NavView.Ease(100f, 200f, 0f, 1.6f) == 100f, "no time, no change");

        NavView.Window(500f, 250f, 0f, 0f, 1000f, 500f, 100f, out float u, out float v, out float w, out float h);
        Check(Near(w, 0.1f) && Near(h, 0.2f) && Near(u + w / 2f, 0.5f) && Near(v + h / 2f, 0.5f), "the window on the picture is centred on the player");
        NavView.Window(-100f, -50f, -100f, -50f, 1000f, 500f, 100f, out u, out v, out w, out h);
        Check(Near(u, -0.05f) && Near(v, -0.1f), "at the picture's corner the window hangs over the edge");

        NavView.ToView(0f, 100f, 0f, 0f, 0f, 2f, out float vx, out float vy);
        Check(Near(vx, 0f) && Near(vy, 200f), "north up: a place to the north is up");
        NavView.ToView(100f, 0f, 0f, 0f, 0f, 2f, out vx, out vy);
        Check(Near(vx, 200f) && Near(vy, 0f), "north up: a place to the east is right");
        NavView.ToView(100f, 0f, 0f, 0f, 90f, 1f, out vx, out vy);
        Check(Near(vx, 0f, 0.01f) && Near(vy, 100f, 0.01f), "facing east: what is to the east is up");
        NavView.ToView(0f, 100f, 0f, 0f, 90f, 1f, out vx, out vy);
        Check(Near(vx, -100f, 0.01f) && Near(vy, 0f, 0.01f), "facing east: what is to the north is on the left");
        NavView.ToView(30f, 40f, 30f, 40f, 123f, 3f, out vx, out vy);
        Check(Near(vx, 0f) && Near(vy, 0f), "the player is the middle, whichever way they face");
    }

    private static void Picture()
    {
        NavRaster.Size(3780f, 2190f, 4096, 1.25f, out int width, out int height, out float perMetre);
        Check(width <= 4096 && height <= 4096 && Near(perMetre, 4096f / 3780f, 0.001f) && width == 4096, "a large city's picture is held to 4096 pixels");
        NavRaster.Size(800f, 600f, 4096, 1.25f, out width, out height, out perMetre);
        Check(Near(perMetre, 1.25f) && width == 1000 && height == 750, "a small one gets its pixels per metre and no more");

        var town = new Town(3);
        int w = 280, h = 280;
        var pixels = new byte[w * h];
        int drawn = NavRaster.Draw(pixels, w, h, -40f, -40f, 1f, 2.3f, town.Drive.X, town.Drive.Z, town.Drive.EdgeStart, town.Drive.EdgeTo, town.Drive.EdgeExtra, town.Usable, town.Drive.Count);
        Check(drawn == town.Drive.EdgeTo.Length, "every lane is drawn");
        Check(pixels[(40 - 2) * w + 40 + 50] == 255 && pixels[(40 + 2) * w + 40 + 50] == 255, "both lanes of a street are road");
        Check(pixels[(40 + 50) * w + 40 + 50] == 0, "the middle of a block is not");
        bool edge = true;
        for (int i = 0; i < w; i++)
        {
            edge &= pixels[i] == 0 && pixels[(h - 1) * w + i] == 0 && pixels[i * w] == 0 && pixels[i * w + w - 1] == 0;
        }
        Check(edge, "the picture's outermost pixels stay empty");
        int soft = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            soft += pixels[i] > 0 && pixels[i] < 255 ? 1 : 0;
        }
        Check(soft > 0, "the bands have soft edges");

        // off the picture altogether: nothing is written, nothing is thrown
        var small = new byte[16 * 16];
        NavRaster.Draw(small, 16, 16, 5000f, 5000f, 1f, 2.3f, town.Drive.X, town.Drive.Z, town.Drive.EdgeStart, town.Drive.EdgeTo, town.Drive.EdgeExtra, town.Usable, town.Drive.Count);
        bool empty = true;
        for (int i = 0; i < small.Length; i++)
        {
            empty &= small[i] == 0;
        }
        Check(empty, "roads outside the picture leave it empty");
    }

    // ---------------------------------------------------------------- a real city, timed

    private static void City(string file)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(file));
        JsonElement points = document.RootElement.GetProperty("points");
        int count = points.GetArrayLength();
        var graph = new RouteSearch(count);
        var road = new int[count];
        var junction = new bool[count];
        var usable = new bool[count];
        var speed = new float[count];
        var start = new int[count + 1];
        var to = new List<int>(count + 1024);
        var drivable = new bool[count];
        int i = 0;
        foreach (JsonElement point in points.EnumerateArray())
        {
            foreach (JsonElement car in point.GetProperty("c").EnumerateArray())
            {
                drivable[i] |= car.GetInt32() == 0;
            }
            i++;
        }
        i = 0;
        foreach (JsonElement point in points.EnumerateArray())
        {
            JsonElement p = point.GetProperty("p");
            graph.X[i] = p[0].GetSingle();
            graph.Y[i] = p[1].GetSingle();
            graph.Z[i] = p[2].GetSingle();
            string name = point.GetProperty("name").GetString();
            road[i] = NavMath.RoadOf(name);
            junction[i] = NavMath.IsConnector(name);
            speed[i] = point.GetProperty("s").GetInt32() / 3.6f;
            start[i] = to.Count;
            foreach (JsonElement next in point.GetProperty("n").EnumerateArray())
            {
                int n = next.GetInt32();
                if (n >= 0 && n < count && drivable[n])
                {
                    to.Add(n);
                    usable[i] = true;
                    usable[n] = true;
                }
            }
            i++;
        }
        start[count] = to.Count;
        graph.SetEdges(start, to.ToArray(), new float[to.Count]);
        Stopwatch watch = Stopwatch.StartNew();
        NavEdges.TwoWay(count, start, graph.EdgeTo, out int[] start2, out int[] to2, out float[] extra2);
        var walk = new RouteSearch(graph);
        walk.SetEdges(start2, to2, extra2);
        double twoWay = watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        var index = new NavIndex(graph.X, graph.Z, usable, count);
        double indexed = watch.Elapsed.TotalMilliseconds;
        Console.WriteLine("City: " + count + " lane points, " + to.Count + " edges (lane changes left out), " + index.Indexed + " usable; walking graph " + twoWay.ToString("F2") + " ms, index " + indexed.ToString("F2") + " ms.");

        float worldW = index.MaxX - index.MinX + 48f, worldH = index.MaxZ - index.MinZ + 48f;
        NavRaster.Size(worldW, worldH, 4096, 1.25f, out int width, out int height, out float perMetre);
        var pixels = new byte[width * height];
        watch.Restart();
        NavRaster.Draw(pixels, width, height, index.MinX - 24f, index.MinZ - 24f, perMetre, 2.3f, graph.X, graph.Z, graph.EdgeStart, graph.EdgeTo, graph.EdgeExtra, usable, count);
        Console.WriteLine("City: picture " + width + " x " + height + " (" + perMetre.ToString("F2") + " px per metre, " + (pixels.Length / 1048576f).ToString("F1") + " MB) drawn in " + watch.Elapsed.TotalMilliseconds.ToString("F1") + " ms.");

        // Routes between random usable points: how many steps, how long, and the longest slice of 400 steps.
        var random = new Random(11);
        var usableList = new List<int>();
        for (int n = 0; n < count; n++)
        {
            if (usable[n])
            {
                usableList.Add(n);
            }
        }
        var path = new List<int>(4096);
        var route = new NavRoute();
        int passes = 0;
        var starts = new List<int>();
        var goals = new List<int>();
        var kinds = new int[7];
        foreach (RouteSearch search in new[] { graph, walk, graph, walk })   // (each once unmeasured first, so that what is timed is the code and not its first compiling)
        {
            int found = 0, most = 0, steps = 0, turns = 0;
            double total = 0, worst = 0, worstSlice = 0, longest = 0, build = 0;
            const int Tries = 400;
            bool measured = random.Next() >= 0 && ++passes > 2;
            if (passes == 3)
            {
                random = new Random(11);
            }
            for (int n = 0; n < Tries; n++)
            {
                int a = usableList[random.Next(usableList.Count)], b = usableList[random.Next(usableList.Count)];
                starts.Clear();
                goals.Clear();
                index.Within(graph.X[a], graph.Z[a], 30f, starts);
                index.Within(graph.X[b], graph.Z[b], 30f, goals);
                watch.Restart();
                search.Begin(starts, goals, graph.X[b], graph.Y[b], graph.Z[b], 30f);
                RouteSearch.Outcome outcome;
                double began = 0;
                do
                {
                    outcome = search.Advance(search.Expanded + 64, path);
                    double now = watch.Elapsed.TotalMilliseconds;
                    worstSlice = Math.Max(worstSlice, now - began);
                    began = now;
                }
                while (outcome == RouteSearch.Outcome.Searching);
                double took = watch.Elapsed.TotalMilliseconds;
                total += took;
                worst = Math.Max(worst, took);
                most = Math.Max(most, search.Expanded);
                steps += search.Expanded;
                if (outcome == RouteSearch.Outcome.Found)
                {
                    found++;
                    watch.Restart();
                    route.Build(path, graph, road, junction, speed);
                    build = Math.Max(build, watch.Elapsed.TotalMilliseconds);
                    longest = Math.Max(longest, route.Length);
                    turns += route.Turns.Count - 1;
                    for (int t = 0; measured && t < route.Turns.Count; t++)
                    {
                        kinds[(int)route.Turns[t].Kind]++;
                    }
                }
            }
            if (!measured)
            {
                continue;
            }
            Console.WriteLine("City (" + (search == graph ? "driving" : "on foot") + "): " + found + " of " + Tries + " random routes found; steps " + steps / Tries + " on average, " + most + " at most; "
                              + (total / Tries).ToString("F3") + " ms on average, " + worst.ToString("F2") + " ms at most for a whole search; longest run of 64 steps " + worstSlice.ToString("F3")
                              + " ms; longest route " + (longest / 1000).ToString("F1") + " km; " + (found > 0 ? (turns / (float)found).ToString("F1") : "0") + " turns a route; route made in at most " + build.ToString("F3") + " ms.");
        }
        Console.WriteLine("City: turns said over those routes: keep left " + kinds[(int)NavTurnKind.KeepLeft] + ", keep right " + kinds[(int)NavTurnKind.KeepRight] + ", left " + kinds[(int)NavTurnKind.Left]
                          + ", right " + kinds[(int)NavTurnKind.Right] + ", U-turn " + kinds[(int)NavTurnKind.UTurn] + ".");
    }
}
