using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The city's drivers' rules, the road part: what binds a car (the most pressing thing on its road ahead), the road
    /// itself as a line through lane points, and the things on it (vehicles, the player, the hold lines). Nothing allocated
    /// after the constructors.
    /// </summary>
    public static partial class DriverRules
    {
        /// <summary>The one thing a car slows for: the most pressing of everything on its road ahead.</summary>
        public struct Binding
        {
            public Why Why;
            public int Obstacle;        // its place among the obstacles, or -1 for a point of the road
            public float Distance;      // metres from the nose to it (to its near face, or to the point)
            public float Room;          // metres from the nose to where the car is to be at PointSpeed
            public float PointSpeed;    // m/s
            public float Ahead;         // m/s it moves on along the car's road
            public float Need;          // m/s2
            public float Keep;          // metres kept behind it beyond the game's sensor (the player's gap)
            public bool Hold;           // the car is kept standing once there: nothing of the game's would hold it short of the thing

            /// <summary>Nothing ahead to slow for.</summary>
            public static Binding None => new Binding { Why = Why.None, Obstacle = -1, Distance = float.MaxValue, Room = float.MaxValue };
        }

        /// <summary>Keeps the more pressing of what is kept and a new thing (with the same need, the one the car comes to first); true if the new one is kept.</summary>
        public static bool Consider(ref Binding best, Why why, int obstacle, float distance, float room, float pointSpeed, float ahead, float speed, float keep = 0f)
        {
            float need = Need(speed, room, pointSpeed, ahead, keep);
            if (need < best.Need || (need == best.Need && room >= best.Room))
            {
                return false;
            }
            best.Why = why;
            best.Obstacle = obstacle;
            best.Distance = distance;
            best.Room = room;
            best.PointSpeed = pointSpeed;
            best.Ahead = ahead;
            best.Need = need;
            best.Keep = keep;
            best.Hold = false;
            return true;
        }

        /// <summary>
        /// A car's road ahead as a line through points, from its nose: built a point at a time, nothing allocated after the
        /// constructor. Asks where something is along it and how far to the side.
        /// </summary>
        public sealed class Road
        {
            private readonly float[] _x;
            private readonly float[] _z;
            private readonly float[] _s;     // metres along the road at each point
            private readonly float[] _dx;    // the way each leg runs, from its point to the next (unit)
            private readonly float[] _dz;
            private float _minX, _maxX, _minZ, _maxZ;
            private bool _straight;          // every leg within about 30 degrees of the first: where a place falls can be guessed
            private float _chordX, _chordZ;  // from the first point to the last...
            private float _chord;            // ...its length squared...
            private float _chordLength;      // ...and its length, worked out at the first question after the road is built
            private bool _chordKnown;

            public Road(int capacity)
            {
                _x = new float[capacity];
                _z = new float[capacity];
                _s = new float[capacity];
                _dx = new float[capacity];
                _dz = new float[capacity];
            }

            /// <summary>Points on it.</summary>
            public int Count { get; private set; }

            /// <summary>Metres from its start to its last point.</summary>
            public float Length => Count > 0 ? _s[Count - 1] : 0f;

            public float X(int point) => _x[point];
            public float Z(int point) => _z[point];

            /// <summary>Metres along the road at that point.</summary>
            public float S(int point) => _s[point];

            /// <summary>Starts again at a nose that faces this way (a unit vector): the first leg runs that way until a second point.</summary>
            public void Begin(float x, float z, float forwardX, float forwardZ)
            {
                Count = 1;
                _x[0] = x;
                _z[0] = z;
                _s[0] = 0f;
                _dx[0] = forwardX;
                _dz[0] = forwardZ;
                _minX = _maxX = x;
                _minZ = _maxZ = z;
                _straight = true;
                _chordKnown = false;
            }

            /// <summary>Adds the next point; one closer than 0.3 m to the last says nothing and is skipped. False when full.</summary>
            public bool Add(float x, float z)
            {
                int count = Count;
                if (count == 0 || count >= _x.Length)
                {
                    return false;
                }
                int last = count - 1;
                float dx = x - _x[last];
                float dz = z - _z[last];
                float squared = dx * dx + dz * dz;
                if (squared < 0.09f)
                {
                    return true;
                }
                float length = (float)Math.Sqrt(squared);
                float inverse = 1f / length;
                float wayX = dx * inverse;
                float wayZ = dz * inverse;
                _dx[last] = wayX;
                _dz[last] = wayZ;
                _straight &= wayX * _dx[0] + wayZ * _dz[0] >= StraightFrom;
                _x[count] = x;
                _z[count] = z;
                _s[count] = _s[last] + length;
                _dx[count] = wayX;
                _dz[count] = wayZ;
                Count = count + 1;
                _chordKnown = false;
                if (x < _minX)
                {
                    _minX = x;
                }
                else if (x > _maxX)
                {
                    _maxX = x;
                }
                if (z < _minZ)
                {
                    _minZ = z;
                }
                else if (z > _maxZ)
                {
                    _maxZ = z;
                }
                return true;
            }

            /// <summary>True if a place is within reach of the box round the road: four comparisons, before asking where on it the place is.</summary>
            public bool Near(float x, float z, float reach)
            {
                return Count >= 2 && x >= _minX - reach && x <= _maxX + reach && z >= _minZ - reach && z <= _maxZ + reach;
            }

            /// <summary>Degrees the road turns at a point between its two legs, right positive; 0 at either end.</summary>
            public float Turn(int point)
            {
                if (point <= 0 || point >= Count - 1)
                {
                    return 0f;
                }
                float dot = _dx[point - 1] * _dx[point] + _dz[point - 1] * _dz[point];
                dot = dot > 1f ? 1f : dot < -1f ? -1f : dot;
                float cross = _dx[point - 1] * _dz[point] - _dz[point - 1] * _dx[point];
                float angle = (float)(Math.Acos(dot) * 180.0 / Math.PI);
                return cross > 0f ? -angle : angle;   // (x east, z north: turning from east towards north is a left turn)
            }

            /// <summary>
            /// How much the road turns from a point on over the next <paramref name="reach"/> metres (degrees, the turns added
            /// with their sign, so a lane's S does not count as a corner), and over how many metres it does.
            /// </summary>
            public float Bend(int from, float reach, out float over)
            {
                over = 0f;
                float turned = 0f;
                float most = 0f;
                for (int i = from; i < Count - 1 && over <= reach; i++)
                {
                    turned += Turn(i);
                    over += Leg(i);
                    float size = turned < 0f ? -turned : turned;
                    if (size > most)
                    {
                        most = size;
                    }
                }
                return most;
            }

            /// <summary>Metres from a point to the next one (the leg it starts); 0 for the last.</summary>
            public float Leg(int point) => point >= 0 && point < Count - 1 ? _s[point + 1] - _s[point] : 0f;

            /// <summary>
            /// Where a place is along the road: metres from its start, metres to the right of it (negative: the left) and the
            /// way the road runs there. False if it is nowhere near: beyond the ends by more than <paramref name="reach"/>,
            /// or further than that to the side. A road of one point has nowhere on it.
            /// </summary>
            public bool Project(float px, float pz, float reach, out float along, out float lateral, out float wayX, out float wayZ)
            {
                along = 0f;
                lateral = float.MaxValue;
                wayX = _dx[0];
                wayZ = _dz[0];
                if (Count < 2 || px < _minX - reach || px > _maxX + reach || pz < _minZ - reach || pz > _maxZ + reach)
                {
                    return false;
                }
                int legs = Count - 1;
                float best = float.MaxValue;
                if (!_straight)
                {
                    return Nearest(px, pz, reach, 0, legs - 1, ref best, ref along, ref lateral, ref wayX, ref wayZ) && best <= reach;
                }
                // On a straight road every point lies on along its chord, so a place further than reach behind its start or
                // beyond its end is on none of it; otherwise the legs about where the place falls along the chord are asked
                // first (lane points are about evenly spaced), and only if none of them has it are the rest.
                if (!_chordKnown)
                {
                    _chordX = _x[legs] - _x[0];
                    _chordZ = _z[legs] - _z[0];
                    _chord = _chordX * _chordX + _chordZ * _chordZ;
                    _chordLength = (float)Math.Sqrt(_chord);
                    _chordKnown = true;
                }
                float on = (px - _x[0]) * _chordX + (pz - _z[0]) * _chordZ;   // metres along the chord, times its length
                if (on < -reach * _chordLength || on > _chord + reach * _chordLength)
                {
                    return false;
                }
                int guess = _chord > 1f ? (int)(on / _chord * legs) : 0;
                guess = guess < 0 ? 0 : guess > legs - 1 ? legs - 1 : guess;
                int first = guess - 2 < 0 ? 0 : guess - 2;
                int end = guess + 2 > legs - 1 ? legs - 1 : guess + 2;
                bool found = Nearest(px, pz, reach, first, end, ref best, ref along, ref lateral, ref wayX, ref wayZ);
                if (!found || best > reach)
                {
                    found = Nearest(px, pz, reach, 0, first - 1, ref best, ref along, ref lateral, ref wayX, ref wayZ) | found;
                    found = Nearest(px, pz, reach, end + 1, legs - 1, ref best, ref along, ref lateral, ref wayX, ref wayZ) | found;
                }
                return found && best <= reach;
            }

            // The leg among from to to (inclusive) the place is nearest the side of, where its foot falls on the leg.
            private bool Nearest(float px, float pz, float reach, int from, int to, ref float best, ref float along, ref float lateral, ref float wayX, ref float wayZ)
            {
                bool found = false;
                int legs = Count - 1;
                for (int i = from; i <= to; i++)
                {
                    float ox = px - _x[i];
                    float oz = pz - _z[i];
                    float t = ox * _dx[i] + oz * _dz[i];
                    float span = _s[i + 1] - _s[i];
                    // (The first leg reaches back a little and the last one on: a thing at the nose or just past the last point is on the road.)
                    float least = i == 0 ? -reach : 0f;
                    float most = i == legs - 1 ? span + reach : span;
                    if (t < least || t > most)
                    {
                        continue;
                    }
                    float side = ox * _dz[i] - oz * _dx[i];
                    float off = side < 0f ? -side : side;
                    if (off < best)
                    {
                        best = off;
                        along = _s[i] + t;
                        lateral = side;
                        wayX = _dx[i];
                        wayZ = _dz[i];
                        found = true;
                    }
                }
                return found;
            }
        }

        /// <summary>
        /// Everything a car may come up behind on its road, as plain numbers: traffic cars, the player's car and its parking
        /// room, the player on foot, the mod's bus and tow truck, and the hold lines. Filled once a look.
        /// </summary>
        public sealed class Obstacles
        {
            public readonly float[] X, Z;            // its middle
            public readonly float[] Fx, Fz;          // the way it faces (unit)
            public readonly float[] Vx, Vz;          // its velocity, m/s
            public readonly float[] HalfLength, HalfWidth;
            public readonly float[] Keep;            // metres kept behind it beyond the game's sensor (the player's time gap and room)
            public readonly bool[] Stop;             // the game's sensor cannot hold a car for it: the car is brought to rest StopGap short
            public readonly float[] Slowing;         // m/s2 it is losing speed with (0 if it is not): a car behind plans to stop behind where it will rest
            public readonly float[] KeepAtRest;      // ...and the gap kept behind it once it is at rest
            public readonly Why[] Kind;
            public readonly int[] Id;                // the traffic car's place in the pool, or -1

            public Obstacles(int capacity)
            {
                X = new float[capacity];
                Z = new float[capacity];
                Fx = new float[capacity];
                Fz = new float[capacity];
                Vx = new float[capacity];
                Vz = new float[capacity];
                HalfLength = new float[capacity];
                HalfWidth = new float[capacity];
                Keep = new float[capacity];
                Stop = new bool[capacity];
                Slowing = new float[capacity];
                KeepAtRest = new float[capacity];
                Kind = new Why[capacity];
                Id = new int[capacity];
            }

            public int Count { get; private set; }

            public void Clear() => Count = 0;

            /// <summary>Adds one; its number, or -1 if full.</summary>
            public int Add(Why kind, int id, float x, float z, float fx, float fz, float vx, float vz, float halfLength, float halfWidth, float keep)
            {
                if (Count >= X.Length)
                {
                    return -1;
                }
                int i = Count++;
                Kind[i] = kind;
                Id[i] = id;
                X[i] = x;
                Z[i] = z;
                Fx[i] = fx;
                Fz[i] = fz;
                Vx[i] = vx;
                Vz[i] = vz;
                HalfLength[i] = halfLength;
                HalfWidth[i] = halfWidth;
                Keep[i] = keep;
                Stop[i] = false;
                Slowing[i] = 0f;
                KeepAtRest[i] = 0f;
                return i;
            }
        }

        /// <summary>
        /// Where an obstacle is on a car's road: metres from the nose to its near face, and its speed on along the road.
        /// False if it is not in the car's way (beside the road, behind the nose, or the car itself).
        /// </summary>
        public static bool OnRoad(Road road, Obstacles obstacles, int i, int self, float carHalfWidth, out float distance, out float ahead)
        {
            distance = float.MaxValue;
            ahead = 0f;
            if (obstacles.Id[i] >= 0 && obstacles.Id[i] == self)
            {
                return false;
            }
            float halfLength = obstacles.HalfLength[i];
            float halfWidth = obstacles.HalfWidth[i];
            float reach = halfLength + halfWidth + carHalfWidth + PathMargin;
            if (!road.Project(obstacles.X[i], obstacles.Z[i], reach, out float along, out float lateral, out float wayX, out float wayZ))
            {
                return false;
            }
            float cosine = obstacles.Fx[i] * wayX + obstacles.Fz[i] * wayZ;
            float sine = obstacles.Fx[i] * wayZ - obstacles.Fz[i] * wayX;
            if (!InWay(lateral, carHalfWidth, Across(halfLength, halfWidth, cosine, sine)))
            {
                return false;
            }
            distance = along - Along(halfLength, halfWidth, cosine, sine);
            if (distance < -BehindNose)
            {
                return false;   // beside the car or behind its nose: what it overlaps now is the game's to sort out
            }
            ahead = obstacles.Vx[i] * wayX + obstacles.Vz[i] * wayZ;
            return true;
        }

        /// <summary>
        /// What on the road binds a car the most among the obstacles: kept in best with what the road's own points gave.
        /// The car keeps the game's sensor behind each thing, arriving at the speed it moves on at or at walking pace, so the
        /// sensor takes over gently. Short of the sensor (the player's own gap kept beyond it, or StopGap short of something
        /// it cannot hold the car for) the car comes to the thing's own speed, and is held there if the thing stands. Behind
        /// something slowing, it plans for where that will come to rest.
        /// </summary>
        public static void Worst(ref Binding best, Road road, Obstacles obstacles, int self, float carHalfWidth, float speed, float sensor)
        {
            float sight = Sight(speed, sensor);
            if (road.Count < 2)
            {
                return;
            }
            // Nothing further from the nose than the sight, a thing's reach and the gap kept behind it can be in sight on
            // the road: a few sums each.
            float noseX = road.X(0);
            float noseZ = road.Z(0);
            for (int i = 0; i < obstacles.Count; i++)
            {
                float ox = obstacles.X[i] - noseX;
                float oz = obstacles.Z[i] - noseZ;
                float within = sight + NearReach + obstacles.Keep[i];
                if (ox * ox + oz * oz > within * within || !road.Near(obstacles.X[i], obstacles.Z[i], NearReach)
                    || !OnRoad(road, obstacles, i, self, carHalfWidth, out float distance, out float ahead) || distance - obstacles.Keep[i] > sight)
                {
                    continue;
                }
                float moving = ahead > MovingFrom ? ahead : 0f;
                bool stop = obstacles.Stop[i];
                float shortOf = stop ? StopGap : sensor;
                float keep = obstacles.Keep[i];
                // Short of the sensor the drivers bring the car to rest themselves, aiming RoomLeast before the place: the
                // last half metre is not worked to the centimetre, and it must not take the car past.
                bool ownStop = stop || keep > 0f;
                float pointSpeed = ownStop || moving > Handover ? moving : Handover;
                float room = distance - shortOf - keep - (ownStop ? RoomLeast : 0f);
                if (Consider(ref best, obstacles.Kind[i], i, distance, room, pointSpeed, moving, speed, keep))
                {
                    best.Hold = ownStop && moving <= 0f;
                }
                float slowing = obstacles.Slowing[i];
                if (moving > 0f && slowing >= SlowingFrom)
                {
                    // It is slowing: the car behind plans for where it will come to rest, not where it is now (once it
                    // stands, the car is held for the thing itself).
                    float rests = StoppingDistance(moving, slowing);
                    float atRest = obstacles.KeepAtRest[i];
                    bool restOwn = stop || atRest > 0f;
                    float restRoom = distance + rests - shortOf - atRest - (restOwn ? RoomLeast : 0f);
                    Consider(ref best, obstacles.Kind[i], -1, distance + rests, restRoom, restOwn ? 0f : Handover, 0f, speed, atRest);
                }
            }
        }
    }
}
