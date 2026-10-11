using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Nav
{
    // The parts of the minimap and the GPS that know nothing of the game or the engine: where on a route somebody
    // is, what the next turn is, when they have left the route or arrived, what part of the map picture a view
    // shows, and the map picture itself as numbers. RealLifeOverhaul.Build/NavCheck compiles this file (and
    // Core/RouteSearch.cs) as they are and checks them on made-up roads.

    /// <summary>What a route asks of whoever follows it at one place.</summary>
    public enum NavTurnKind { None, KeepLeft, KeepRight, Left, Right, UTurn, Arrive }

    public struct NavTurn
    {
        public float At;            // metres along the route where the junction begins
        public float Angle;         // degrees, left positive
        public float InX, InZ;      // the heading before it
        public NavTurnKind Kind;
        public int Road;            // the road turned onto (the graph's own number), -1 unknown
    }

    public enum NavEvent { None, Reroute, Arrived }

    public static class NavMath
    {
        public const float StraightBelow = 28f;   // a change of heading under this is "carry on": nothing is said
        public const float TurnFrom = 55f;        // from here it is a turn; under it, "keep left/right"
        public const float UTurnFrom = 150f;

        /// <summary>Degrees from one flat direction to another, left (anticlockwise seen from above) positive, -180..180.</summary>
        public static float SignedAngle(float ax, float az, float bx, float bz)
        {
            float cross = ax * bz - az * bx;
            float dot = ax * bx + az * bz;
            if (cross == 0f && dot == 0f)
            {
                return 0f;
            }
            return (float)(Math.Atan2(cross, dot) * 180.0 / Math.PI);
        }

        public static NavTurnKind Classify(float degrees)
        {
            float size = Math.Abs(degrees);
            if (size < StraightBelow)
            {
                return NavTurnKind.None;
            }
            if (size >= UTurnFrom)
            {
                return NavTurnKind.UTurn;
            }
            if (size < TurnFrom)
            {
                return degrees > 0f ? NavTurnKind.KeepLeft : NavTurnKind.KeepRight;
            }
            return degrees > 0f ? NavTurnKind.Left : NavTurnKind.Right;
        }

        /// <summary>A distance as a satnav says it: to 10 m under 200 m, to 50 m under a kilometre, to 100 m above; never 0.</summary>
        public static int RoundMetres(float metres)
        {
            int step = metres < 200f ? 10 : metres < 1000f ? 50 : 100;
            int rounded = (int)Math.Round(metres / step) * step;
            return rounded < 10 ? 10 : rounded;
        }

        /// <summary>The road number in a lane point's name ("Road_25-Lane_3-Waypoint_7" is 25); -1 if the name is not of that form.</summary>
        public static int RoadOf(string name)
        {
            if (name == null || name.Length < 6 || name[0] != 'R' || name[1] != 'o' || name[2] != 'a' || name[3] != 'd' || name[4] != '_')
            {
                return -1;
            }
            int number = 0;
            int i = 5;
            for (; i < name.Length && name[i] >= '0' && name[i] <= '9' && i < 14; i++)
            {
                number = number * 10 + (name[i] - '0');
            }
            return i > 5 ? number : -1;
        }

        /// <summary>True for a lane point that joins two roads at a junction (its name says "Connect").</summary>
        public static bool IsConnector(string name)
        {
            return name != null && name.IndexOf("Connect", StringComparison.Ordinal) >= 0;
        }
    }

    /// <summary>
    /// The lane points in a grid of square cells, for "which are near this place". Built once from the graph's
    /// places (a counting sort: two arrays, no lists); a question looks at the few cells its circle touches.
    /// </summary>
    public sealed class NavIndex
    {
        private readonly float[] _x;
        private readonly float[] _z;
        private readonly float _cell;
        private readonly float _minX;
        private readonly float _minZ;
        private readonly int _columns;
        private readonly int _rows;
        private readonly int[] _cellStart;
        private readonly int[] _points;

        public float MinX => _minX;
        public float MinZ => _minZ;
        public float MaxX { get; }
        public float MaxZ { get; }
        public int Indexed => _points.Length;

        public NavIndex(float[] x, float[] z, bool[] usable, int count, float cell = 32f)
        {
            _x = x;
            _z = z;
            _cell = cell;
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            int used = 0;
            for (int i = 0; i < count; i++)
            {
                if (!usable[i])
                {
                    continue;
                }
                used++;
                minX = Math.Min(minX, x[i]);
                maxX = Math.Max(maxX, x[i]);
                minZ = Math.Min(minZ, z[i]);
                maxZ = Math.Max(maxZ, z[i]);
            }
            if (used == 0)
            {
                minX = minZ = maxX = maxZ = 0f;
            }
            _minX = minX;
            _minZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
            _columns = Math.Max(1, (int)((maxX - minX) / cell) + 1);
            _rows = Math.Max(1, (int)((maxZ - minZ) / cell) + 1);
            _cellStart = new int[_columns * _rows + 1];
            _points = new int[used];
            for (int i = 0; i < count; i++)
            {
                if (usable[i])
                {
                    _cellStart[CellOf(x[i], z[i]) + 1]++;
                }
            }
            for (int c = 0; c < _columns * _rows; c++)
            {
                _cellStart[c + 1] += _cellStart[c];
            }
            var next = new int[_columns * _rows];
            for (int i = 0; i < count; i++)
            {
                if (usable[i])
                {
                    int c = CellOf(x[i], z[i]);
                    _points[_cellStart[c] + next[c]++] = i;
                }
            }
        }

        private int CellOf(float x, float z)
        {
            int column = Clamp((int)((x - _minX) / _cell), 0, _columns - 1);
            int row = Clamp((int)((z - _minZ) / _cell), 0, _rows - 1);
            return row * _columns + column;
        }

        private static int Clamp(int value, int low, int high)
        {
            return value < low ? low : value > high ? high : value;
        }

        /// <summary>The point nearest a place (flat distance) within a radius, or -1.</summary>
        public int Nearest(float x, float z, float within)
        {
            int best = -1;
            float bestSqr = within * within;
            int c0 = Clamp((int)((x - within - _minX) / _cell), 0, _columns - 1);
            int c1 = Clamp((int)((x + within - _minX) / _cell), 0, _columns - 1);
            int r0 = Clamp((int)((z - within - _minZ) / _cell), 0, _rows - 1);
            int r1 = Clamp((int)((z + within - _minZ) / _cell), 0, _rows - 1);
            for (int row = r0; row <= r1; row++)
            {
                for (int column = c0; column <= c1; column++)
                {
                    int cell = row * _columns + column;
                    for (int k = _cellStart[cell]; k < _cellStart[cell + 1]; k++)
                    {
                        int point = _points[k];
                        float dx = _x[point] - x;
                        float dz = _z[point] - z;
                        float sqr = dx * dx + dz * dz;
                        if (sqr < bestSqr)
                        {
                            bestSqr = sqr;
                            best = point;
                        }
                    }
                }
            }
            return best;
        }

        /// <summary>Adds every point within a radius of a place (flat distance) to a list.</summary>
        public void Within(float x, float z, float radius, List<int> into)
        {
            float limit = radius * radius;
            int c0 = Clamp((int)((x - radius - _minX) / _cell), 0, _columns - 1);
            int c1 = Clamp((int)((x + radius - _minX) / _cell), 0, _columns - 1);
            int r0 = Clamp((int)((z - radius - _minZ) / _cell), 0, _rows - 1);
            int r1 = Clamp((int)((z + radius - _minZ) / _cell), 0, _rows - 1);
            for (int row = r0; row <= r1; row++)
            {
                for (int column = c0; column <= c1; column++)
                {
                    int cell = row * _columns + column;
                    for (int k = _cellStart[cell]; k < _cellStart[cell + 1]; k++)
                    {
                        int point = _points[k];
                        float dx = _x[point] - x;
                        float dz = _z[point] - z;
                        if (dx * dx + dz * dz <= limit)
                        {
                            into.Add(point);
                        }
                    }
                }
            }
        }
    }

    public static class NavEdges
    {
        /// <summary>
        /// The same roads for somebody on foot: every edge both ways, and no charge for changing lane. Lanes are
        /// one-way for cars only; a person walks along a street in either direction.
        /// </summary>
        public static void TwoWay(int count, int[] start, int[] to, out int[] start2, out int[] to2, out float[] extra2)
        {
            start2 = new int[count + 1];
            for (int i = 0; i < count; i++)
            {
                for (int e = start[i]; e < start[i + 1]; e++)
                {
                    start2[i + 1]++;
                    start2[to[e] + 1]++;
                }
            }
            for (int i = 0; i < count; i++)
            {
                start2[i + 1] += start2[i];
            }
            to2 = new int[start2[count]];
            extra2 = new float[start2[count]];
            var next = new int[count];
            for (int i = 0; i < count; i++)
            {
                for (int e = start[i]; e < start[i + 1]; e++)
                {
                    int other = to[e];
                    to2[start2[i] + next[i]++] = other;
                    to2[start2[other] + next[other]++] = i;
                }
            }
        }
    }

    /// <summary>
    /// A found route as a line: its points, how far along each is, what road each is on, and the turns. The arrays
    /// are kept and only grown, so finding a new route makes no garbage once the longest has been seen.
    /// </summary>
    public sealed class NavRoute
    {
        private const float Sample = 14f;       // metres of road before and after a junction that give the two headings
        private const float SameJunction = 18f; // two changes of road nearer than this are one turn
        private const float ShortGap = 12f;     // plain points between two curves of one junction span less than this
        private const float PastTurn = 15f;     // a turn is still "the next one" this far past where its junction begins

        public float[] X = new float[0];
        public float[] Y = new float[0];
        public float[] Z = new float[0];
        public float[] Along = new float[0];
        private float[] _time = new float[0];   // seconds from the start at each lane's own speed
        private int[] _road = new int[0];
        private bool[] _junction = new bool[0];

        public readonly List<NavTurn> Turns = new List<NavTurn>(32);

        public int Count { get; private set; }
        public float Length { get; private set; }

        public void Clear()
        {
            Count = 0;
            Length = 0f;
            Turns.Clear();
        }

        /// <summary>
        /// Takes a path of the search. <paramref name="road"/>, <paramref name="junction"/> and <paramref name="speed"/>
        /// (metres a second) are per point of the graph.
        /// </summary>
        public void Build(List<int> path, RouteSearch graph, int[] road, bool[] junction, float[] speed)
        {
            Clear();
            int count = path.Count;
            if (X.Length < count)
            {
                int room = Math.Max(count, X.Length * 2);
                X = new float[room];
                Y = new float[room];
                Z = new float[room];
                Along = new float[room];
                _time = new float[room];
                _road = new int[room];
                _junction = new bool[room];
            }
            for (int i = 0; i < count; i++)
            {
                int node = path[i];
                X[i] = graph.X[node];
                Y[i] = graph.Y[node];
                Z[i] = graph.Z[node];
                _road[i] = road != null ? road[node] : -1;
                _junction[i] = junction != null && junction[node];
                if (i == 0)
                {
                    Along[0] = 0f;
                    _time[0] = 0f;
                    continue;
                }
                float dx = X[i] - X[i - 1];
                float dy = Y[i] - Y[i - 1];
                float dz = Z[i] - Z[i - 1];
                float step = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                float pace = speed != null && speed[node] > 1f ? speed[node] : 13.9f;
                Along[i] = Along[i - 1] + step;
                _time[i] = _time[i - 1] + step / pace;
            }
            Count = count;
            Length = count > 0 ? Along[count - 1] : 0f;
            FindTurns();
        }

        // A turn is where the route's heading changes as it passes a junction: the heading over the last plain stretch
        // before the junction's own points (the curve; they are in neither) against the heading over the first plain
        // stretch after them. A change of road with no junction point between counts the same way. How the game names
        // a junction's points (after the road left, the road entered, or another) does not matter.
        private void FindTurns()
        {
            int i = 1;
            while (i < Count)
            {
                if (_junction[i])
                {
                    int end = i;
                    while (end + 1 < Count)
                    {
                        if (_junction[end + 1])
                        {
                            end++;
                            continue;
                        }
                        // A plain point or two inside a junction (where one of its curves ends and the next begins) do not end it.
                        int gap = end + 1;
                        while (gap < Count && !_junction[gap] && Along[gap] - Along[end + 1] < ShortGap)
                        {
                            gap++;
                        }
                        if (gap >= Count || !_junction[gap])
                        {
                            break;
                        }
                        end = gap;
                    }
                    if (!_junction[i - 1] && end + 1 < Count)
                    {
                        Turn(i - 1, end + 1);
                    }
                    i = end + 1;
                    continue;
                }
                if (!_junction[i - 1] && _road[i] != _road[i - 1] && _road[i] >= 0 && _road[i - 1] >= 0)
                {
                    Turn(i - 1, i);
                }
                i++;
            }
            for (int t = Turns.Count - 1; t >= 0; t--)
            {
                if (Turns[t].Kind == NavTurnKind.None)
                {
                    Turns.RemoveAt(t);
                }
            }
            Turns.Add(new NavTurn { At = Length, Angle = 0f, Kind = NavTurnKind.Arrive, Road = -1 });
        }

        // The turn between the last plain point before a junction and the first after it.
        private void Turn(int last, int first)
        {
            int back = last;
            while (back > 0 && !_junction[back - 1] && Along[last] - Along[back - 1] <= Sample)
            {
                back--;
            }
            if (back == last && last > 0)
            {
                back = last - 1;
            }
            int ahead = first;
            while (ahead + 1 < Count && !_junction[ahead + 1] && Along[ahead + 1] - Along[first] <= Sample)
            {
                ahead++;
            }
            if (ahead == first && first + 1 < Count)
            {
                ahead = first + 1;
            }
            // With nothing before the junction or nothing after it, the way through the junction itself stands in.
            float inX = back < last ? X[last] - X[back] : X[first] - X[last];
            float inZ = back < last ? Z[last] - Z[back] : Z[first] - Z[last];
            float outX = ahead > first ? X[ahead] - X[first] : X[first] - X[last];
            float outZ = ahead > first ? Z[ahead] - Z[first] : Z[first] - Z[last];
            float angle = NavMath.SignedAngle(inX, inZ, outX, outZ);
            float at = Along[last];
            if (Turns.Count > 0 && at - Turns[Turns.Count - 1].At < SameJunction)
            {
                // Two junctions in a few metres are one turn: from the heading before the first to the heading after the second.
                NavTurn before = Turns[Turns.Count - 1];
                before.Angle = NavMath.SignedAngle(before.InX, before.InZ, outX, outZ);
                before.Kind = NavMath.Classify(before.Angle);
                before.Road = _road[first];
                Turns[Turns.Count - 1] = before;
                return;
            }
            Turns.Add(new NavTurn { At = at, Angle = angle, InX = inX, InZ = inZ, Kind = NavMath.Classify(angle), Road = _road[first] });
        }

        /// <summary>The first turn still ahead of somebody this far along (the arrival is the last of them); -1 with no route.</summary>
        public int NextTurn(float along)
        {
            for (int t = 0; t < Turns.Count; t++)
            {
                if (Turns[t].At > along - PastTurn || t == Turns.Count - 1)
                {
                    return t;
                }
            }
            return -1;
        }

        /// <summary>The place this far along the route.</summary>
        public void PointAt(float along, out float x, out float z)
        {
            if (Count == 0)
            {
                x = 0f;
                z = 0f;
                return;
            }
            int s = SegmentAt(along);
            float span = Along[s + 1 < Count ? s + 1 : s] - Along[s];
            float t = span > 0.0001f ? (along - Along[s]) / span : 0f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            int e = s + 1 < Count ? s + 1 : s;
            x = X[s] + (X[e] - X[s]) * t;
            z = Z[s] + (Z[e] - Z[s]) * t;
        }

        /// <summary>The segment (its first point) that holds this distance along.</summary>
        public int SegmentAt(float along)
        {
            int low = 0;
            int high = Count - 1;
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (Along[middle] <= along)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }
            return low;
        }

        /// <summary>
        /// The route from a distance along it to its end as a line of fewer points, for drawing: a point is left out
        /// while the line has not turned four degrees since the last one kept and is not 30 m on. The three arrays
        /// (x, height, z) are grown if they are too short; the answer is how many points were written.
        /// </summary>
        public int Thin(float from, ref float[] x, ref float[] y, ref float[] z)
        {
            if (Count == 0)
            {
                return 0;
            }
            if (x == null || x.Length < Count + 1)
            {
                int room = Math.Max(Count + 1, 64);
                x = new float[room];
                y = new float[room];
                z = new float[room];
            }
            int first = Count > 1 ? SegmentAt(from) : 0;
            PointAt(from, out float keptX, out float keptZ);
            x[0] = keptX;
            y[0] = Y[first];
            z[0] = keptZ;
            int written = 1;
            for (int i = first + 1; i < Count; i++)
            {
                float dx = X[i] - keptX;
                float dz = Z[i] - keptZ;
                float sqr = dx * dx + dz * dz;
                if (i < Count - 1)
                {
                    if (sqr < 1f)
                    {
                        continue;
                    }
                    float turned = NavMath.SignedAngle(dx, dz, X[i + 1] - X[i], Z[i + 1] - Z[i]);
                    if (turned < 4f && turned > -4f && sqr < 30f * 30f)
                    {
                        continue;
                    }
                }
                else if (sqr < 0.01f)
                {
                    continue;
                }
                keptX = X[i];
                keptZ = Z[i];
                x[written] = keptX;
                y[written] = Y[i];
                z[written] = keptZ;
                written++;
            }
            return written;
        }

        /// <summary>Seconds of driving at each lane's own speed from this far along to the end.</summary>
        public float SecondsFrom(float along)
        {
            if (Count < 2)
            {
                return 0f;
            }
            int s = SegmentAt(along);
            float span = Along[s + 1] - Along[s];
            float t = span > 0.0001f ? (along - Along[s]) / span : 0f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            float here = _time[s] + (_time[s + 1] - _time[s]) * t;
            return _time[Count - 1] - here;
        }

        /// <summary>
        /// Where somebody is on the route: the nearest place on it, looked for around the segment they were on last
        /// (<paramref name="segment"/>, updated) and over the whole route only when that finds nothing near.
        /// A stretch on another level (a bridge overhead) does not count.
        /// </summary>
        public bool Locate(float px, float py, float pz, ref int segment, out float along, out float lateral)
        {
            along = 0f;
            lateral = float.MaxValue;
            if (Count == 0)
            {
                return false;
            }
            if (Count == 1)
            {
                float dx = X[0] - px;
                float dz = Z[0] - pz;
                lateral = (float)Math.Sqrt(dx * dx + dz * dz);
                segment = 0;
                return true;
            }
            int best = -1;
            float bestSqr = float.MaxValue;
            float bestT = 0f;
            int from = Math.Max(0, segment - 8);
            int to = Math.Min(Count - 2, segment + 60);
            Scan(px, py, pz, from, to, ref best, ref bestSqr, ref bestT);
            if (best < 0 || bestSqr > 60f * 60f)
            {
                Scan(px, py, pz, 0, Count - 2, ref best, ref bestSqr, ref bestT);
            }
            if (best < 0)
            {
                return false;
            }
            segment = best;
            along = Along[best] + (Along[best + 1] - Along[best]) * bestT;
            lateral = (float)Math.Sqrt(bestSqr);
            return true;
        }

        private void Scan(float px, float py, float pz, int from, int to, ref int best, ref float bestSqr, ref float bestT)
        {
            for (int s = from; s <= to; s++)
            {
                float ax = X[s];
                float az = Z[s];
                float bx = X[s + 1] - ax;
                float bz = Z[s + 1] - az;
                float lengthSqr = bx * bx + bz * bz;
                float t = lengthSqr > 0.0001f ? ((px - ax) * bx + (pz - az) * bz) / lengthSqr : 0f;
                t = t < 0f ? 0f : t > 1f ? 1f : t;
                float dx = ax + bx * t - px;
                float dz = az + bz * t - pz;
                float sqr = dx * dx + dz * dz;
                if (sqr >= bestSqr)
                {
                    continue;
                }
                float height = Y[s] + (Y[s + 1] - Y[s]) * t - py;
                if (height > 12f || height < -12f)
                {
                    continue;
                }
                bestSqr = sqr;
                best = s;
                bestT = t;
            }
        }
    }

    /// <summary>
    /// Decides, a tick at a time, whether whoever follows a route has left it or has arrived. Leaving is said only
    /// after being off the line for a while, and never twice within a few seconds, so a wide junction or a lay-by
    /// does not start a new search.
    /// </summary>
    public sealed class NavTracker
    {
        public const float OffOnFoot = 22f;       // metres from the route's line before it counts as off it
        public const float OffDriving = 32f;
        public const float OffFor = 2f;           // seconds off the line before a new route is looked for
        public const float RerouteGap = 4f;       // seconds between two searches at least
        public const float ArriveOnFoot = 14f;    // metres from the place
        public const float ArriveDriving = 28f;
        public const float RouteEnd = 4f;         // metres of route left that count as its end

        private float _off;
        private float _sinceRoute;

        /// <summary>Seconds off the line so far.</summary>
        public float Off => _off;

        public void Reset()
        {
            _off = 0f;
            _sinceRoute = 0f;
        }

        /// <param name="lateral">Metres from the route's line.</param>
        /// <param name="left">Metres of route still ahead.</param>
        /// <param name="toPlace">Metres in a straight line to the place itself.</param>
        public NavEvent Tick(float dt, bool onFoot, float lateral, float left, float toPlace)
        {
            _sinceRoute += dt;
            if (toPlace <= (onFoot ? ArriveOnFoot : ArriveDriving))
            {
                return NavEvent.Arrived;
            }
            bool atEnd = left <= RouteEnd;
            if (atEnd && !onFoot && lateral <= OffDriving)
            {
                return NavEvent.Arrived;   // the end of the road: as near as a car gets to a place that is not on one
            }
            if (atEnd && onFoot)
            {
                _off = 0f;                 // the last steps from the road to the place are not on the line, and need not be
                return NavEvent.None;
            }
            _off = lateral > (onFoot ? OffOnFoot : OffDriving) ? _off + dt : 0f;
            if (_off >= OffFor && _sinceRoute >= RerouteGap)
            {
                _off = 0f;
                _sinceRoute = 0f;
                return NavEvent.Reroute;
            }
            return NavEvent.None;
        }
    }

    /// <summary>The minimap's view: how much of the city it shows, and which part of the map picture that is.</summary>
    public static class NavView
    {
        public const float BaseMetres = 260f;     // metres across the view at 100% zoom, standing or walking
        public const float WidenFrom = 8f;        // metres a second: faster than this the view begins to widen
        public const float WidenOver = 20f;       // ...and is at its widest this much faster
        public const float WidestBy = 0.9f;       // at its widest it shows this much more again

        /// <summary>Metres across the view for an option value (per cent: more is closer) and a speed.</summary>
        public static float MetresAcross(int zoomPercent, float speed)
        {
            float zoom = zoomPercent < 25 ? 25f : zoomPercent > 400 ? 400f : zoomPercent;
            float fast = (speed - WidenFrom) / WidenOver;
            fast = fast < 0f ? 0f : fast > 1f ? 1f : fast;
            return BaseMetres * 100f / zoom * (1f + WidestBy * fast * fast * (3f - 2f * fast));
        }

        /// <summary>Moves a value towards another by a share of the gap per second; lands on it when within half a per cent.</summary>
        public static float Ease(float current, float target, float dt, float rate)
        {
            float gap = target - current;
            if (Math.Abs(gap) <= Math.Abs(target) * 0.005f)
            {
                return target;
            }
            float share = 1f - (float)Math.Exp(-rate * dt);
            return current + gap * share;
        }

        /// <summary>
        /// The part of the map picture a square of <paramref name="metres"/> a side around a place is, as texture
        /// coordinates (corner and size). The picture covers the world from its origin, <paramref name="worldWidth"/>
        /// by <paramref name="worldHeight"/> metres.
        /// </summary>
        public static void Window(float x, float z, float originX, float originZ, float worldWidth, float worldHeight, float metres,
                                  out float u, out float v, out float width, out float height)
        {
            width = metres / worldWidth;
            height = metres / worldHeight;
            u = (x - originX) / worldWidth - width * 0.5f;
            v = (z - originZ) / worldHeight - height * 0.5f;
        }

        /// <summary>
        /// Where a place in the world is in the view, in the view's own units from its middle (right and up positive),
        /// for a view centred on (px, pz) and turned by <paramref name="turn"/> degrees (the player's heading, clockwise
        /// from north; 0 for north up).
        /// </summary>
        public static void ToView(float wx, float wz, float px, float pz, float turn, float unitsPerMetre, out float vx, out float vy)
        {
            float dx = (wx - px) * unitsPerMetre;
            float dz = (wz - pz) * unitsPerMetre;
            double radians = turn * Math.PI / 180.0;
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            vx = dx * cos - dz * sin;
            vy = dx * sin + dz * cos;
        }
    }

    /// <summary>
    /// The map picture as numbers: every lane of the road graph drawn as a soft-edged band into one byte per pixel
    /// (how much road there is at that pixel). It touches only the arrays it is given, so it can be run away from the
    /// main thread.
    /// </summary>
    public static class NavRaster
    {
        /// <summary>The size of the picture for a piece of world, and how many pixels a metre gets: as many as fit in <paramref name="longest"/> pixels, at most <paramref name="mostPerMetre"/>.</summary>
        public static void Size(float worldWidth, float worldHeight, int longest, float mostPerMetre, out int width, out int height, out float perMetre)
        {
            float span = Math.Max(1f, Math.Max(worldWidth, worldHeight));
            perMetre = Math.Min(mostPerMetre, longest / span);
            width = Math.Max(4, (int)Math.Ceiling(worldWidth * perMetre));
            height = Math.Max(4, (int)Math.Ceiling(worldHeight * perMetre));
        }

        /// <summary>Draws every edge that costs nothing extra (a lane, not a change of lane) between two usable points.</summary>
        public static int Draw(byte[] pixels, int width, int height, float originX, float originZ, float perMetre, float halfWidthMetres,
                               float[] x, float[] z, int[] edgeStart, int[] edgeTo, float[] edgeExtra, bool[] usable, int count)
        {
            float radius = Math.Max(0.75f, halfWidthMetres * perMetre);
            int drawn = 0;
            for (int i = 0; i < count; i++)
            {
                if (!usable[i])
                {
                    continue;
                }
                float ax = (x[i] - originX) * perMetre;
                float az = (z[i] - originZ) * perMetre;
                for (int e = edgeStart[i]; e < edgeStart[i + 1]; e++)
                {
                    int other = edgeTo[e];
                    if (edgeExtra[e] > 0f || !usable[other])
                    {
                        continue;
                    }
                    Band(pixels, width, height, ax, az, (x[other] - originX) * perMetre, (z[other] - originZ) * perMetre, radius);
                    drawn++;
                }
            }
            return drawn;
        }

        // One band: every pixel within the radius of the segment is road; the last pixel of the edge fades out.
        private static void Band(byte[] pixels, int width, int height, float ax, float az, float bx, float bz, float radius)
        {
            float reach = radius + 1f;
            int x0 = (int)Math.Floor(Math.Min(ax, bx) - reach);
            int x1 = (int)Math.Ceiling(Math.Max(ax, bx) + reach);
            int y0 = (int)Math.Floor(Math.Min(az, bz) - reach);
            int y1 = (int)Math.Ceiling(Math.Max(az, bz) + reach);
            // A pixel's width of the picture's edge stays empty, so that looking past the edge shows nothing.
            x0 = x0 < 1 ? 1 : x0;
            y0 = y0 < 1 ? 1 : y0;
            x1 = x1 > width - 2 ? width - 2 : x1;
            y1 = y1 > height - 2 ? height - 2 : y1;
            float dx = bx - ax;
            float dz = bz - az;
            float lengthSqr = dx * dx + dz * dz;
            for (int py = y0; py <= y1; py++)
            {
                int row = py * width;
                float cz = py + 0.5f - az;
                for (int px = x0; px <= x1; px++)
                {
                    float cx = px + 0.5f - ax;
                    float t = lengthSqr > 0.0001f ? (cx * dx + cz * dz) / lengthSqr : 0f;
                    t = t < 0f ? 0f : t > 1f ? 1f : t;
                    float ox = cx - dx * t;
                    float oz = cz - dz * t;
                    float cover = radius + 0.5f - (float)Math.Sqrt(ox * ox + oz * oz);
                    if (cover <= 0f)
                    {
                        continue;
                    }
                    byte value = cover >= 1f ? (byte)255 : (byte)(cover * 255f);
                    if (value > pixels[row + px])
                    {
                        pixels[row + px] = value;
                    }
                }
            }
        }
    }
}
