using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The shortest way over a directed graph of points from any of several starts to any of several ends (A*, straight
    /// line to the ends' middle less their reach). Several starts and ends because the single nearest lane point is
    /// often on a one-way piece of road. An edge may carry an extra cost (a lane change costs a stretch of road). A
    /// search runs in one call (<see cref="Find"/>) or a slice at a time (<see cref="Begin"/>, <see cref="Advance"/>).
    /// Game-free.
    /// </summary>
    public sealed class RouteSearch
    {
        public enum Outcome { Searching, Found, NoWay }

        public readonly int Count;
        public readonly float[] X;
        public readonly float[] Y;
        public readonly float[] Z;

        private int[] _edgeStart;    // Count + 1 entries: a point's edges are [_edgeStart[i], _edgeStart[i + 1])
        private int[] _edgeTo;
        private float[] _edgeExtra;  // metres added to an edge's length (a change of lane), usually 0

        private readonly float[] _cost;
        private readonly int[] _from;
        private readonly int[] _seen;
        private readonly int[] _goal;
        private int _search;
        private readonly List<int> _heapNode = new List<int>(4096);
        private readonly List<float> _heapKey = new List<float>(4096);
        private float _gx, _gy, _gz, _reach;   // the search under way: the ends' middle and how far from it an end may be
        private bool _begun;

        /// <summary>Points the last search reached at all.</summary>
        public int Reached { get; private set; }

        /// <summary>Points the last search went on from.</summary>
        public int Expanded { get; private set; }

        /// <summary>The last search stopped at its limit, not because it ran out of road.</summary>
        public bool HitLimit { get; private set; }

        public RouteSearch(int count)
        {
            Count = count;
            X = new float[count];
            Y = new float[count];
            Z = new float[count];
            _cost = new float[count];
            _from = new int[count];
            _seen = new int[count];
            _goal = new int[count];
        }

        /// <summary>A second search over the same points, which are shared, not copied.</summary>
        public RouteSearch(RouteSearch samePoints)
        {
            Count = samePoints.Count;
            X = samePoints.X;
            Y = samePoints.Y;
            Z = samePoints.Z;
            _cost = new float[Count];
            _from = new int[Count];
            _seen = new int[Count];
            _goal = new int[Count];
        }

        /// <summary>A search of its own over the same roads; <paramref name="ownCosts"/>: a copy of the extra costs to change.</summary>
        public static RouteSearch Over(RouteSearch roads, bool ownCosts)
        {
            var search = new RouteSearch(roads);
            float[] extra = roads._edgeExtra;
            search.SetEdges(roads._edgeStart, roads._edgeTo, ownCosts && extra != null ? (float[])extra.Clone() : extra);
            return search;
        }

        /// <summary>Gives up the search under way as <see cref="Find"/> does at its limit.</summary>
        public void GiveUp()
        {
            HitLimit = true;
            _begun = false;
        }

        /// <summary>The edges: where each point's edges begin, where each leads, and its cost over its length.</summary>
        public void SetEdges(int[] edgeStart, int[] edgeTo, float[] edgeExtra)
        {
            _edgeStart = edgeStart;
            _edgeTo = edgeTo;
            _edgeExtra = edgeExtra;
        }

        /// <summary>The edges as set; read only.</summary>
        public int[] EdgeStart => _edgeStart;
        public int[] EdgeTo => _edgeTo;
        public float[] EdgeExtra => _edgeExtra;

        public bool WasReached(int node)
        {
            return node >= 0 && node < Count && _seen[node] == _search && _search > 0;
        }

        /// <summary>
        /// Finds the shortest way; <paramref name="path"/> is filled start to end, both included. (gx, gy, gz) is the
        /// ends' middle for the estimate ahead, and no end is further than <paramref name="goalReach"/> from it.
        /// </summary>
        public bool Find(IList<int> starts, IList<int> goals, float gx, float gy, float gz, float goalReach, int maxExpansions, List<int> path)
        {
            path.Clear();
            if (!Begin(starts, goals, gx, gy, gz, goalReach))
            {
                return false;
            }
            Outcome outcome = Advance(maxExpansions, path);
            if (outcome == Outcome.Searching)
            {
                GiveUp();
            }
            return outcome == Outcome.Found;
        }

        /// <summary>Sets a search up, giving up one under way; false if there is nothing to search.</summary>
        public bool Begin(IList<int> starts, IList<int> goals, float gx, float gy, float gz, float goalReach)
        {
            Reached = 0;
            Expanded = 0;
            HitLimit = false;
            _begun = false;
            if (_edgeStart == null || starts == null || goals == null || starts.Count == 0 || goals.Count == 0)
            {
                return false;
            }
            _search++;
            _heapNode.Clear();
            _heapKey.Clear();
            _gx = gx;
            _gy = gy;
            _gz = gz;
            _reach = goalReach;
            for (int i = 0; i < goals.Count; i++)
            {
                if (goals[i] >= 0 && goals[i] < Count)
                {
                    _goal[goals[i]] = _search;
                }
            }
            for (int i = 0; i < starts.Count; i++)
            {
                int start = starts[i];
                if (start < 0 || start >= Count || _seen[start] == _search)
                {
                    continue;
                }
                _seen[start] = _search;
                _cost[start] = 0f;
                _from[start] = -1;
                Reached++;
                Push(start, Ahead(start));
            }
            _begun = true;
            return true;
        }

        /// <summary>Goes on until decided or <see cref="Expanded"/> reaches <paramref name="expandedLimit"/>; on Found, fills the path.</summary>
        public Outcome Advance(int expandedLimit, List<int> path)
        {
            if (!_begun)
            {
                return Outcome.NoWay;
            }
            while (_heapNode.Count > 0)
            {
                if (Expanded >= expandedLimit)
                {
                    return Outcome.Searching;
                }
                int node = Pop(out float key);
                if (key > _cost[node] + Ahead(node) + 0.01f)
                {
                    continue;   // an older, worse entry for a point since reached more cheaply
                }
                if (_goal[node] == _search)
                {
                    path.Clear();
                    for (int at = node; at >= 0; at = _from[at])
                    {
                        path.Add(at);
                    }
                    path.Reverse();
                    _begun = false;
                    return Outcome.Found;
                }
                Expanded++;
                for (int e = _edgeStart[node]; e < _edgeStart[node + 1]; e++)
                {
                    int to = _edgeTo[e];
                    float cost = _cost[node] + Distance(node, to) + _edgeExtra[e];
                    if (_seen[to] == _search)
                    {
                        if (cost >= _cost[to])
                        {
                            continue;
                        }
                    }
                    else
                    {
                        _seen[to] = _search;
                        Reached++;
                    }
                    _cost[to] = cost;
                    _from[to] = node;
                    Push(to, cost + Ahead(to));
                }
            }
            _begun = false;
            return Outcome.NoWay;
        }

        private float Distance(int a, int b)
        {
            float x = X[a] - X[b];
            float y = Y[a] - Y[b];
            float z = Z[a] - Z[b];
            return (float)Math.Sqrt(x * x + y * y + z * z);
        }

        // Never more than the road still to drive.
        private float Ahead(int node)
        {
            float x = X[node] - _gx;
            float y = Y[node] - _gy;
            float z = Z[node] - _gz;
            float left = (float)Math.Sqrt(x * x + y * y + z * z) - _reach;
            return left > 0f ? left : 0f;
        }

        private void Push(int node, float key)
        {
            _heapNode.Add(node);
            _heapKey.Add(key);
            int i = _heapNode.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (_heapKey[parent] <= key)
                {
                    break;
                }
                _heapNode[i] = _heapNode[parent];
                _heapKey[i] = _heapKey[parent];
                i = parent;
            }
            _heapNode[i] = node;
            _heapKey[i] = key;
        }

        private int Pop(out float key)
        {
            int top = _heapNode[0];
            key = _heapKey[0];
            int last = _heapNode.Count - 1;
            int node = _heapNode[last];
            float moved = _heapKey[last];
            _heapNode.RemoveAt(last);
            _heapKey.RemoveAt(last);
            if (last == 0)
            {
                return top;
            }
            int i = 0;
            while (true)
            {
                int child = i * 2 + 1;
                if (child >= last)
                {
                    break;
                }
                if (child + 1 < last && _heapKey[child + 1] < _heapKey[child])
                {
                    child++;
                }
                if (_heapKey[child] >= moved)
                {
                    break;
                }
                _heapNode[i] = _heapNode[child];
                _heapKey[i] = _heapKey[child];
                i = child;
            }
            _heapNode[i] = node;
            _heapKey[i] = moved;
            return top;
        }
    }
}
