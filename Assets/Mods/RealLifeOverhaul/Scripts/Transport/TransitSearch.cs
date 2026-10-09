using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The shortest way over a directed graph of points from any of several starts to any of
    /// several ends (A*, straight-line distance to the ends' middle less their reach ahead).
    /// Nothing of the game or the engine in it, so it is also run on made-up graphs in a console
    /// test: <see cref="TransitMap"/> fills it from the road graph once per city load and lets
    /// go of it when the network is laid out.
    ///
    /// Why several starts and ends: a bus line's end is "at this subway station", not "on this
    /// one lane point". The single nearest lane point is often on a piece of road that leads
    /// only one way (a one-way street, a slip road, the lane heading away), and from such a
    /// point the other end cannot be reached, or it can be left but never come back to.
    /// An edge may carry an extra cost: a change of lane is allowed, but costs as much as a
    /// stretch of road, so it is only taken where following the lane does not get there.
    /// </summary>
    public sealed class RouteSearch
    {
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

        /// <summary>Points the last search got to at all.</summary>
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

        /// <summary>The edges, as three arrays: where each point's edges begin, where each edge leads, and what it costs over its length.</summary>
        public void SetEdges(int[] edgeStart, int[] edgeTo, float[] edgeExtra)
        {
            _edgeStart = edgeStart;
            _edgeTo = edgeTo;
            _edgeExtra = edgeExtra;
        }

        /// <summary>True if the last search got to this point.</summary>
        public bool WasReached(int node)
        {
            return node >= 0 && node < Count && _seen[node] == _search && _search > 0;
        }

        /// <summary>
        /// Finds the shortest way. <paramref name="path"/> is filled start to end (a start and an end included).
        /// </summary>
        /// <param name="gx">The middle of the ends (the station), for the estimate ahead.</param>
        /// <param name="goalReach">No end is further than this from that middle.</param>
        public bool Find(IList<int> starts, IList<int> goals, float gx, float gy, float gz, float goalReach, int maxExpansions, List<int> path)
        {
            path.Clear();
            Reached = 0;
            Expanded = 0;
            HitLimit = false;
            if (_edgeStart == null || starts == null || goals == null || starts.Count == 0 || goals.Count == 0)
            {
                return false;
            }
            _search++;
            _heapNode.Clear();
            _heapKey.Clear();
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
                Push(start, Ahead(start, gx, gy, gz, goalReach));
            }
            while (_heapNode.Count > 0)
            {
                if (Expanded >= maxExpansions)
                {
                    HitLimit = true;
                    return false;
                }
                int node = Pop(out float key);
                if (key > _cost[node] + Ahead(node, gx, gy, gz, goalReach) + 0.01f)
                {
                    continue;   // an older, worse entry for a point since reached more cheaply
                }
                if (_goal[node] == _search)
                {
                    for (int at = node; at >= 0; at = _from[at])
                    {
                        path.Add(at);
                    }
                    path.Reverse();
                    return true;
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
                    Push(to, cost + Ahead(to, gx, gy, gz, goalReach));
                }
            }
            return false;
        }

        private float Distance(int a, int b)
        {
            float x = X[a] - X[b];
            float y = Y[a] - Y[b];
            float z = Z[a] - Z[b];
            return (float)Math.Sqrt(x * x + y * y + z * z);
        }

        // Never more than the road still to drive: the straight line to the ends' middle, less how far from it an end may be.
        private float Ahead(int node, float gx, float gy, float gz, float reach)
        {
            float x = X[node] - gx;
            float y = Y[node] - gy;
            float z = Z[node] - gz;
            float left = (float)Math.Sqrt(x * x + y * y + z * z) - reach;
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

    /// <summary>
    /// What the bus stop's panel says about the next bus, and the one figure in it. Nothing of
    /// the game or the engine in it (it is run in a console test).
    ///
    /// The figure (<see cref="Seconds"/>) only ever goes down. Before a bus is on the road it
    /// is the timetable's wait; with a bus on the road it is that bus's metres of line still
    /// to drive over the speed it has been making while moving, and never more than was shown
    /// before. When that cannot be kept (the bus stands for long, is much further off than
    /// was promised, was lost, or none could set out) the panel says DELAYED and shows no
    /// figure; once the bus has been moving again for a moment the figure is set afresh from
    /// where it now is, once, and goes down from there.
    /// </summary>
    public sealed class TransitEta
    {
        public enum Mode { Timetable, EnRoute, Delayed, Here }

        public const float StartSpeed = 7f;      // m/s assumed of a bus that has only just set out
        public const float LeastSpeed = 2.5f;
        private const float LateSlack = 12f;     // seconds a bus may be behind the figure before it is called late
        private const float LateFor = 6f;        // ...and for how long
        private const float StillFor = 12f;      // seconds standing (a red light is usually shorter) before DELAYED
        private const float NoBusFor = 6f;       // seconds past due with no bus able to set out before DELAYED
        private const float DelayShownLeast = 4f;
        private const float MovingFor = 2f;      // seconds it must have been moving again before a new figure is given
        private const float Moving = 1.5f;       // m/s

        private float _late;
        private float _still;
        private float _delayed;
        private float _moving;
        private float _lastMetres;

        public Mode State { get; private set; }

        /// <summary>The figure, in seconds. Not shown while <see cref="State"/> is Delayed or Here.</summary>
        public float Seconds { get; private set; }

        /// <summary>Metres a second the bus has been making while it moved.</summary>
        public float Speed { get; private set; } = StartSpeed;

        /// <summary>How often a figure has been set afresh: by the timetable, or coming out of a delay. Whoever shows it may only let it rise when this has changed.</summary>
        public int Epoch { get; private set; }

        public bool HasBus { get; private set; }

        /// <summary>A new wait by the timetable begins (arriving at the stop, or a bus has just left).</summary>
        public void Timetable(float seconds)
        {
            State = Mode.Timetable;
            Seconds = seconds > 0f ? seconds : 0f;
            HasBus = false;
            Epoch++;
            Clear();
        }

        /// <summary>No bus on the road: the timetable's wait runs down. <paramref name="cannotSetOut"/>: it is due and none could be sent.</summary>
        public void Wait(float dt, bool cannotSetOut)
        {
            HasBus = false;
            if (State == Mode.Here || State == Mode.EnRoute)
            {
                State = Mode.Timetable;
            }
            Seconds = Seconds > dt ? Seconds - dt : 0f;
            if (State == Mode.Delayed)
            {
                _delayed += dt;
                return;
            }
            _late = cannotSetOut ? _late + dt : 0f;
            if (_late > NoBusFor)
            {
                Delay();
            }
        }

        /// <summary>A bus has been put on the road, this many metres of line from the stop.</summary>
        public void Assign(float metres)
        {
            HasBus = true;
            Speed = StartSpeed;
            _lastMetres = metres;
            _late = 0f;
            _still = 0f;
            _moving = 0f;
            float raw = metres / StartSpeed;
            if (State == Mode.Delayed)
            {
                return;   // it stays DELAYED until this bus has been moving for a moment (Track)
            }
            if (raw > Seconds + LateSlack)
            {
                Delay();   // further off than was promised: said so, not counted up
                return;
            }
            State = Mode.EnRoute;
            if (raw < Seconds)
            {
                Seconds = raw;
            }
        }

        /// <summary>The bus on the road, each tick: metres of line still to drive, and whether it is being held (a light, a closure, traffic).</summary>
        public void Track(float dt, float metres, bool held)
        {
            if (dt <= 0f)
            {
                return;
            }
            HasBus = true;
            float progress = (_lastMetres - metres) / dt;
            _lastMetres = metres;
            if (progress < 0f)
            {
                progress = 0f;
            }
            if (progress > 30f)
            {
                progress = 30f;   // it was moved up the line out of view: not a speed
            }
            bool moving = progress >= Moving && !held;
            if (moving)
            {
                Speed += (progress - Speed) * (dt < 3f ? dt / 3f : 1f);   // only while moving: a red light is a wait, not a slower bus
                if (Speed < LeastSpeed)
                {
                    Speed = LeastSpeed;
                }
            }
            float raw = metres / Speed;
            if (State == Mode.Delayed)
            {
                _delayed += dt;
                _moving = moving ? _moving + dt : 0f;
                if (_delayed >= DelayShownLeast && _moving >= MovingFor)
                {
                    State = Mode.EnRoute;
                    Seconds = raw;
                    Epoch++;
                    _late = 0f;
                    _still = 0f;
                }
                return;
            }
            State = Mode.EnRoute;
            if (raw < Seconds)
            {
                Seconds = raw;
            }
            _still = progress < 0.3f || held ? _still + dt : 0f;
            _late = raw > Seconds + LateSlack ? _late + dt : 0f;
            if (_still > StillFor || _late > LateFor)
            {
                Delay();
            }
        }

        /// <summary>The bus that was coming is not coming (taken off the road, lost): DELAYED until another is moving.</summary>
        public void Lost()
        {
            HasBus = false;
            Delay();
        }

        public void Arrived()
        {
            State = Mode.Here;
            Seconds = 0f;
            HasBus = true;
            Clear();
        }

        private void Delay()
        {
            if (State != Mode.Delayed)
            {
                State = Mode.Delayed;
                _delayed = 0f;
            }
            _moving = 0f;
            _late = 0f;
            _still = 0f;
        }

        private void Clear()
        {
            _late = 0f;
            _still = 0f;
            _delayed = 0f;
            _moving = 0f;
        }
    }
}
