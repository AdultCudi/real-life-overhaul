using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The roads as the route planner sees them, lane points by number. <c>closures</c> false asks as if no lane were
    /// closed for the moment.
    /// </summary>
    public abstract class RouteRoads
    {
        public abstract int Count { get; }
        public abstract int Ways(int point);
        public abstract int Way(int point, int k);
        public abstract bool Open(int point, bool closures);

        /// <summary>Plain road, where a checkpoint may stand.</summary>
        public abstract bool Plain(int point, bool closures);
        public abstract float Metres(int from, int to);
    }

    /// <summary>What a planning came to.</summary>
    public enum RouteOutcome
    {
        Planned,    // a route with enough checkpoints
        Closed,     // none now, but there would be one with no lane closed
        NoRoad      // none, closed lanes or not
    }

    /// <summary>
    /// Plans a lesson's route: a depth-first search along open lanes that backs out of dead ends, so a cordon is driven
    /// round. A seed gives the same route on every machine. At most <see cref="Budget"/> points a planning.
    /// </summary>
    public sealed class TestRoutePlanner
    {
        public const float Spacing = 105f;        // metres of lane between checkpoints
        public const float FirstAt = 0.55f;       // ...the first comes at this share of it, something to aim at straight away
        public const float Tail = 150f;           // metres past the last checkpoint in which a bay is looked for
        public const int Least = 5;               // fewer checkpoints than this is no drive
        public const int Most = 7;
        public const int MaxSteps = 900;          // lane points in one route at most
        public const int Budget = 6000;           // points gone into in one planning at most: a bound on the cost

        // One entry per point of the route tried so far.
        private readonly List<int> _node = new List<int>(512);
        private readonly List<int> _turn = new List<int>(512);      // ways on tried
        private readonly List<int> _first = new List<int>(512);     // the way on tried first (from the seed)
        private readonly List<float> _since = new List<float>(512); // metres since the last checkpoint
        private readonly List<int> _count = new List<int>(512);     // checkpoints laid
        private readonly List<float> _past = new List<float>(512);  // metres past the last checkpoint wanted
        private int[] _seen = new int[0];
        private int _stamp;
        private uint _random;

        /// <summary>The route planned, as lane points. Empty when none was.</summary>
        public readonly List<int> Path = new List<int>(512);

        public int Checkpoints { get; private set; }

        public int Expanded { get; private set; }

        /// <summary>Plans over the lanes open now; with <paramref name="askWhy"/>, a failure plans again to tell Closed from NoRoad.</summary>
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

        /// <summary>A route's checkpoints as indexes into <paramref name="path"/>, by the rule the planning counts by.</summary>
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

    /// <summary>What the school says when its car cannot go out, as locale keys.</summary>
    public static class TestRouteRules
    {
        public const string ClosedKey = "reallife_lic_no_route_closed";
        public const string NoRouteKey = "reallife_lic_no_route";
        public const string NoSpotKey = "reallife_lic_no_car_spot";

        /// <summary>Why no car could be set out, null if one could. A closure is said first, as it clears by itself.</summary>
        public static string Refusal(bool found, bool closedSeen, bool roomSeen)
        {
            return found ? null : closedSeen ? ClosedKey : roomSeen ? NoRouteKey : NoSpotKey;
        }
    }
}
