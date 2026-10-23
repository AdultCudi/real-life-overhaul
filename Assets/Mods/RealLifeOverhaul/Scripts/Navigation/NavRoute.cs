using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// A found route as a line: its points, how far along each is, the road each is on, and the turns. Game-free and
    /// checked by NavCheck. The arrays are only grown, so a new route makes no garbage once the longest has been seen.
    /// </summary>
    public sealed class NavRoute
    {
        private const float Sample = 14f;       // metres of road before and after a junction that give the two headings
        private const float SameJunction = 18f; // metres: two changes of road nearer than this are one turn
        private const float ShortGap = 12f;     // metres: plain points between two curves of one junction span less
        private const float PastTurn = 15f;     // metres past where its junction begins that a turn is still the next

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

        /// <summary>Takes a path of the search; the road, junction and speed (metres a second) arrays are per point of the graph.</summary>
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

        // A turn is the heading over the last plain stretch before a junction's own points against the heading over the
        // first plain stretch after them; a change of road with no junction point between counts the same way. How the
        // game names a junction's points does not matter.
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
                        // A plain point or two where one curve of a junction ends and the next begins do not end it.
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

        /// <summary>The first turn still ahead this far along, the arrival being the last; -1 with no route.</summary>
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

        /// <summary>The rest of the route as fewer points for drawing, dropping points on a straight under 30 m; returns how many were written.</summary>
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

        /// <summary>The nearest place on the route, near the last <paramref name="segment"/> first; a stretch on another level does not count.</summary>
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
    /// Decides a tick at a time whether whoever follows a route has left it or arrived. Leaving is said only after a while
    /// off the line and never twice within a few seconds, so a wide junction or a lay-by starts no new search.
    /// </summary>
    public sealed class NavTracker
    {
        public const float OffOnFoot = 22f;       // metres from the route's line before it counts as off it
        public const float OffDriving = 32f;      // metres
        public const float OffFor = 2f;           // seconds off the line before a new route is looked for
        public const float RerouteGap = 4f;       // seconds between two searches at least
        public const float ArriveOnFoot = 14f;    // metres from the place
        public const float ArriveDriving = 28f;   // metres
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

        /// <summary>
        /// One tick of <paramref name="dt"/> seconds; <paramref name="lateral"/> is metres from the route's line,
        /// <paramref name="left"/> metres of route ahead and <paramref name="toPlace"/> metres straight to the place.
        /// </summary>
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
}
