using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The roads as the driving school's route planner sees them: lane points by number, the ways on from each,
    /// whether a point may be driven now, whether it is plain road (a checkpoint may stand there), and the metres
    /// between two. <paramref name="closures"/> false asks as if no lane were closed for the moment (an accident's
    /// cordon, a street event): what tells a road that is closed for now from one that leads nowhere.
    /// The game's side is Licences/Driving/TestRoute (the traffic system's lane points); a made-up town is the
    /// console test's (RealLifeOverhaul.Build/NavCheck/TestRouteCheck.cs).
    /// </summary>
    public abstract class RouteRoads
    {
        public abstract int Count { get; }
        public abstract int Ways(int point);
        public abstract int Way(int point, int k);
        public abstract bool Open(int point, bool closures);
        public abstract bool Plain(int point, bool closures);
        public abstract float Metres(int from, int to);
    }

    /// <summary>What a planning came to.</summary>
    public enum RouteOutcome
    {
        Planned,    // a route with enough checkpoints
        Closed,     // none now, but there would be one with no lane closed: the road is shut for the moment
        NoRoad      // none, closed lanes or not
    }

    /// <summary>
    /// The route of a driving lesson or test, planned over the road graph: from the lane point the school's car
    /// stands at, along lanes that are open now, turning at junctions as the graph allows, far enough for five to
    /// seven checkpoints a fixed distance apart and a stretch after the last to look for a bay in.
    ///
    /// Why a planner and not a walk: the route used to be a walk that took a random open way on at each point and
    /// stopped at the first point with none. An accident's cordon shuts every lane point of its stretch of road
    /// (Traffic/SceneCordon), so a walk that ran up to it stopped there, short of five checkpoints, and the lesson
    /// was called off with "could not plan a route" (a cordon of 46 closed lane points on the street of a school's
    /// only desk had every lesson refused). This one goes back from a dead end and
    /// tries the next way on (a search depth first, each point gone into once), so a route that turns off before
    /// the closure is found whenever one exists; and when none does, it says whether the closure is why
    /// (<see cref="RouteOutcome.Closed"/>), so the player is told the road is shut for now rather than that the
    /// street is no good.
    ///
    /// The ways on are tried in a random order from a seed (its own small generator, the same route for the same
    /// seed on every machine). Nothing of the game or the engine in it. Cost: once when a lesson or test is booked or
    /// started, at most <see cref="Budget"/> points gone into (under a millisecond); nothing per frame. Its lists are
    /// kept and reused; only its mark of the points seen is made again when the city's count of points changes.
    /// </summary>
    public sealed class TestRoutePlanner
    {
        public const float Spacing = 105f;        // metres of lane between checkpoints
        public const float FirstAt = 0.55f;       // ...the first comes sooner, at this share of it: something to aim at straight away
        public const float Tail = 150f;           // metres past the last checkpoint in which a bay is looked for
        public const int Least = 5;               // fewer checkpoints than this is no drive (the last becomes the stop with no bay)
        public const int Most = 7;                // the most checkpoints asked for
        public const int MaxSteps = 900;          // lane points in one route at most
        public const int Budget = 6000;           // points gone into in one planning at most: well over any route's need, a bound on the cost

        private readonly List<int> _node = new List<int>(512);
        private readonly List<int> _turn = new List<int>(512);      // the ways on from each point of the route tried so far
        private readonly List<int> _first = new List<int>(512);     // ...from which way on they are tried (random)
        private readonly List<float> _since = new List<float>(512); // metres since the last checkpoint, at each point
        private readonly List<int> _count = new List<int>(512);     // checkpoints laid by each point
        private readonly List<float> _past = new List<float>(512);  // metres past the last checkpoint wanted
        private int[] _seen = new int[0];
        private int _stamp;
        private uint _random;

        /// <summary>The route planned: lane points from the start on. Empty when none was.</summary>
        public readonly List<int> Path = new List<int>(512);

        /// <summary>Checkpoints the route carries (<see cref="Layout"/> lays them).</summary>
        public int Checkpoints { get; private set; }

        /// <summary>Points gone into by the last planning (for the log and the cost check).</summary>
        public int Expanded { get; private set; }

        /// <summary>
        /// Plans a route from <paramref name="start"/> for <paramref name="wanted"/> checkpoints (at least <see cref="Least"/>
        /// are needed) and, past the last, <see cref="Tail"/> metres if the road goes on, over the lanes open now. If that
        /// finds nothing and <paramref name="askWhy"/>, asked again as if no lane were closed, only to say whether a closure
        /// is why (Closed); without it, or with no route either way, NoRoad.
        /// </summary>
        public RouteOutcome Plan(RouteRoads roads, int start, int wanted, int seed, bool askWhy)
        {
            if (Search(roads, start, wanted, seed, true))
            {
                return RouteOutcome.Planned;
            }
            if (!askWhy)
            {
                return RouteOutcome.NoRoad;
            }
            int expanded = Expanded;
            bool without = Search(roads, start, wanted, seed, false);
            Expanded += expanded;
            Path.Clear();
            Checkpoints = 0;
            return without ? RouteOutcome.Closed : RouteOutcome.NoRoad;
        }

        /// <summary>
        /// The checkpoints a route carries, as places in it (indexes into <paramref name="path"/>), laid as the drive
        /// lays them: the first at <see cref="FirstAt"/> of <see cref="Spacing"/> along, each next one a spacing on,
        /// each on plain road. The same rule the planning counts by, so what it planned is what is laid.
        /// </summary>
        public static int Layout(RouteRoads roads, List<int> path, int wanted, List<int> at)
        {
            at.Clear();
            float since = Spacing * (1f - FirstAt);
            for (int i = 1; i < path.Count && at.Count < wanted; i++)
            {
                since += roads.Metres(path[i - 1], path[i]);
                if (since >= Spacing && roads.Plain(path[i], true))
                {
                    at.Add(i);
                    since = 0f;
                }
            }
            return at.Count;
        }

        private bool Search(RouteRoads roads, int start, int wanted, int seed, bool closures)
        {
            Path.Clear();
            Checkpoints = 0;
            Expanded = 0;
            wanted = wanted < Least ? Least : wanted > Most ? Most : wanted;
            int count = roads.Count;
            if (start < 0 || start >= count || !roads.Open(start, closures))
            {
                return false;
            }
            if (_seen.Length != count)
            {
                _seen = new int[count];
                _stamp = 0;
            }
            if (++_stamp == int.MaxValue)
            {
                System.Array.Clear(_seen, 0, _seen.Length);
                _stamp = 1;
            }
            _random = (uint)seed * 2654435761u + 0x9E3779B9u;
            if (_random == 0u)
            {
                _random = 1u;
            }
            _node.Clear();
            _turn.Clear();
            _first.Clear();
            _since.Clear();
            _count.Clear();
            _past.Clear();
            _seen[start] = _stamp;
            Push(roads, start, Spacing * (1f - FirstAt), 0, 0f);
            int bestCount = -1;
            float bestPast = -1f;
            while (_node.Count > 0 && Expanded < Budget)
            {
                int depth = _node.Count - 1;
                int at = _node[depth];
                int ways = roads.Ways(at);
                if (_turn[depth] >= ways || depth + 1 >= MaxSteps)
                {
                    Pop();
                    continue;
                }
                int k = (_first[depth] + _turn[depth]) % ways;
                _turn[depth]++;
                int next = roads.Way(at, k);
                if (next < 0 || next >= count || _seen[next] == _stamp || !roads.Open(next, closures))
                {
                    continue;
                }
                _seen[next] = _stamp;
                Expanded++;
                float since = _since[depth] + roads.Metres(at, next);
                int laid = _count[depth];
                float past = _past[depth];
                if (laid < wanted)
                {
                    if (since >= Spacing && roads.Plain(next, closures))
                    {
                        laid++;
                        since = 0f;
                    }
                }
                else
                {
                    past += roads.Metres(at, next);
                }
                Push(roads, next, since, laid, past);
                if (laid > bestCount || (laid == bestCount && past > bestPast))
                {
                    bestCount = laid;
                    bestPast = past;
                    Path.Clear();
                    Path.AddRange(_node);
                    if (laid >= wanted && past >= Tail)
                    {
                        break;
                    }
                }
            }
            if (bestCount < Least)
            {
                Path.Clear();
                return false;
            }
            Checkpoints = bestCount;
            return true;
        }

        private void Push(RouteRoads roads, int point, float since, int laid, float past)
        {
            int ways = roads.Ways(point);
            _node.Add(point);
            _turn.Add(0);
            _first.Add(ways > 1 ? (int)(Next() % (uint)ways) : 0);
            _since.Add(since);
            _count.Add(laid);
            _past.Add(past);
        }

        private void Pop()
        {
            int last = _node.Count - 1;
            _node.RemoveAt(last);
            _turn.RemoveAt(last);
            _first.RemoveAt(last);
            _since.RemoveAt(last);
            _count.RemoveAt(last);
            _past.RemoveAt(last);
        }

        // xorshift32: the same numbers from the same seed everywhere.
        private uint Next()
        {
            uint x = _random;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _random = x;
            return x;
        }
    }

    /// <summary>What the school tells the player when its car cannot be taken out, as a key of the locales.</summary>
    public static class TestRouteRules
    {
        public const string ClosedKey = "reallife_lic_no_route_closed";
        public const string NoRouteKey = "reallife_lic_no_route";
        public const string NoSpotKey = "reallife_lic_no_car_spot";

        /// <summary>
        /// Why no car could be set out: a lane near the desk closed for now (a closed spot passed over, or a route that
        /// only a closure stops) is said first, because it is the one that clears by itself; then a street with room for
        /// the car but no drive long enough from it; then no room at the roadside at all. Null when a place was found.
        /// </summary>
        public static string Refusal(bool found, bool closedSeen, bool roomSeen)
        {
            return found ? null : closedSeen ? ClosedKey : roomSeen ? NoRouteKey : NoSpotKey;
        }
    }
}
