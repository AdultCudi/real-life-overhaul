using System;
using System.Collections.Generic;
using GleyTrafficSystem;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What one Plan knows about the street, asked once. Plan tries up to some 1,800 ways and tests each every half
    /// metre; asked of the engine pose by pose that was tens of thousands of box tests, ground rays, pavement lookups
    /// and searches of every road point in the city, all in the one frame the crew steps out (measured: 3.9 s).
    /// So the street round the scene is read once, when Plan starts, and the ways are tested against that:
    ///   everything solid in a square round the scene, each as the rectangle it covers on the ground, sorted into
    ///   3 m buckets. A pose whose strip touches none of those rectangles is clear, and the engine is not asked (a
    ///   rectangle never covers less than what it stands for). One that reaches well into a vehicle's or a box's
    ///   rectangle, at the height the crew is, is blocked, and the engine is not asked either. Only a strip that
    ///   grazes one, or meets something of another shape, is asked of the engine as before. What the engine said of
    ///   a strip is remembered for the length of the Plan: the 56 ways of one last move all end in the same pose, and
    ///   all the ways along one side of the ambulance begin in the same one, so a lamp post beside the casualty is
    ///   asked about once, not 56 times. Of new strips at most Askings a tier are asked: past that such a strip
    ///   counts as blocked (a way is refused, never let through);
    ///   the ground, and whether it is carriageway, remembered by 0.2 m square: asked once for a square, at its
    ///   middle, however many ways cross it;
    ///   the road points near the scene, so that the nearest one to a place is found among a few dozen.
    /// Nothing is kept past the end of Plan (Forget), and a place outside the square is asked of the engine as before.
    /// The buffers are made once and nothing is allocated by a Plan after the first.
    /// </summary>
    public sealed partial class CrewWay
    {
        private const float LookPad = 12f;        // metres the square reaches beyond the rear doors, the casualty and the tape
        private const float LookMost = 96f;       // half its side at most (the crew walks 150 m at the very most): a scene wider than that is asked pose by pose, as before
        private const float LookTall = 40f;       // half its height: nothing solid at street level anywhere on it is above or below that
        private const float Bucket = 3f;
        private const float Square02 = 0.2f;
        private const float BoxPad = 0.03f;       // what each rectangle is taken larger by: it never holds less than the engine would find
        private const float Well = 0.2f;          // ...and smaller by, for a strip to be well into it: inside a vehicle's corners and mirrors
        private const float BandLow = 0.85f;      // from the ground: what stands there must reach above this...
        private const float BandHigh = 1.35f;     // ...and start below this, to be in the crew's way whatever the camber
        private const int Askings = 160;          // different strips a tier that the engine is asked about at most (and as many again for the way back)
        private const float Upright = 0.996f;     // the least a thing's own "up" may point up (5 degrees off) for its rectangle to be taken as its true shape
        private const int MostSolid = 8192;
        private const int MostEntries = 32768;
        private const int MostBuckets = 64;       // a side: LookMost * 2 / Bucket
        private const int Across02 = 1024;        // squares a side at most: LookMost * 2 / Square02, and room

        private const byte GroundAsked = 1, Grounded = 2, CarriageAsked = 4, Carriage = 8;

        private struct Spot
        {
            public float Height;
            public byte State;
        }

        private static readonly Collider[] Solid = new Collider[MostSolid];
        private static readonly Rigidbody[] SolidBody = new Rigidbody[MostSolid];
        private static readonly int[] SolidLayer = new int[MostSolid];
        private static readonly float[] SolidX = new float[MostSolid];       // the middle of each rectangle...
        private static readonly float[] SolidZ = new float[MostSolid];
        private static readonly float[] SolidAlongX = new float[MostSolid];  // ...the way its long side runs...
        private static readonly float[] SolidAlongZ = new float[MostSolid];
        private static readonly float[] SolidHalfA = new float[MostSolid];   // ...half its size that way, and across
        private static readonly float[] SolidHalfB = new float[MostSolid];
        private static readonly float[] SolidHalfX = new float[MostSolid];   // half of the square-on box round it (for the buckets)
        private static readonly float[] SolidHalfZ = new float[MostSolid];
        private static readonly bool[] SolidSure = new bool[MostSolid];      // a vehicle or a box at the crew's height: well into its rectangle is into it
        private static readonly int[] BucketStart = new int[MostBuckets * MostBuckets + 1];
        private static readonly int[] BucketFill = new int[MostBuckets * MostBuckets];
        private static readonly int[] BucketItems = new int[MostEntries];
        private static readonly Dictionary<int, Spot> Spots = new Dictionary<int, Spot>(4096);
        private static readonly Dictionary<long, Collider> Answers = new Dictionary<long, Collider>(512);   // what the engine said of a strip (null: clear)
        private static readonly List<Waypoint> Near = new List<Waypoint>(256);
        private static readonly Predicate<Waypoint> CollectNear = KeepNear;

        private static bool _looked;          // a Plan is under way and the square has been read
        private static bool _boxes;           // ...its solid things too (false: there were more than the buffer holds)
        private static bool _bucketed;        // ...and sorted into buckets (false: they are gone through one by one)
        private static bool _points;          // ...and its road points
        private static int _solidCount, _side;
        private static float _lookX, _lookZ, _lookSize, _lookY;   // the square's low corner, its side, and the ground at its middle

        /// <summary>What the last Plan cost, for the log: ways tried, poses tested, and how many of them the engine was asked about.</summary>
        public string Cost => _ways + " way(s) tried, " + _poses + " pose(s), " + _asked + " asked of the engine"
                              + (_refused > 0 ? " and " + _refused + " counted as blocked unasked" : string.Empty) + ", " + _solidSeen + " solid thing(s) and "
                              + _spotsSeen + " square(s) of ground read" + (_lookedLast ? string.Empty : "; the scene was too wide to read at once");

        private static int _ways, _poses, _asked, _askedNow, _refused, _solidSeen, _spotsSeen;
        private static bool _lookedLast;

        private static bool KeepNear(Waypoint point)
        {
            Near.Add(point);
            return false;
        }

        // Reads the square round the rear doors, the casualty and the tape. Called once, at the start of Plan.
        private void Look()
        {
            Forget();
            _ways = _poses = _asked = _askedNow = _refused = _solidSeen = _spotsSeen = 0;
            _lookedLast = false;
            float lowX = Mathf.Min(Door.x, Bed.x), highX = Mathf.Max(Door.x, Bed.x);
            float lowZ = Mathf.Min(Door.z, Bed.z), highZ = Mathf.Max(Door.z, Bed.z);
            if (_framed)
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector3 corner = Edge(i);
                    lowX = Mathf.Min(lowX, corner.x);
                    highX = Mathf.Max(highX, corner.x);
                    lowZ = Mathf.Min(lowZ, corner.z);
                    highZ = Mathf.Max(highZ, corner.z);
                }
            }
            float half = Mathf.Max(highX - lowX, highZ - lowZ) * 0.5f + LookPad;
            if (half > LookMost)
            {
                return;
            }
            float midX = (lowX + highX) * 0.5f, midZ = (lowZ + highZ) * 0.5f;
            _lookX = midX - half;
            _lookZ = midZ - half;
            _lookSize = half * 2f;
            _lookY = StartHeight;
            _looked = true;
            _lookedLast = true;

            // Everything solid on it, on either set of layers a way is tested against.
            int count = Physics.OverlapBoxNonAlloc(new Vector3(midX, _lookY, midZ), new Vector3(half, LookTall, half), Solid, Quaternion.identity,
                SceneParking.StreetMask | SceneParking.RoadsideMask, QueryTriggerInteraction.Ignore);
            _boxes = count < MostSolid;   // (a full buffer may have left some out: then every pose is asked of the engine)
            _solidCount = _boxes ? count : 0;
            _solidSeen = count;
            _side = Mathf.Clamp(Mathf.CeilToInt(_lookSize / Bucket), 1, MostBuckets);
            int buckets = _side * _side;
            Array.Clear(BucketStart, 0, buckets + 1);
            int entries = 0;
            for (int i = 0; i < _solidCount; i++)
            {
                Outline(i);
                Buckets(SolidX[i] - SolidHalfX[i], SolidX[i] + SolidHalfX[i], SolidZ[i] - SolidHalfZ[i], SolidZ[i] + SolidHalfZ[i],
                    out int fromX, out int toX, out int fromZ, out int toZ);
                for (int z = fromZ; z <= toZ; z++)
                {
                    for (int x = fromX; x <= toX; x++)
                    {
                        BucketStart[z * _side + x + 1]++;
                        entries++;
                    }
                }
            }
            for (int i = _solidCount; i < count; i++)
            {
                Solid[i] = null;
            }
            _bucketed = _boxes && entries <= MostEntries;
            if (_bucketed)
            {
                for (int i = 0; i < buckets; i++)
                {
                    BucketStart[i + 1] += BucketStart[i];
                    BucketFill[i] = BucketStart[i];
                }
                for (int i = 0; i < _solidCount; i++)
                {
                    Buckets(SolidX[i] - SolidHalfX[i], SolidX[i] + SolidHalfX[i], SolidZ[i] - SolidHalfZ[i], SolidZ[i] + SolidHalfZ[i],
                        out int fromX, out int toX, out int fromZ, out int toZ);
                    for (int z = fromZ; z <= toZ; z++)
                    {
                        for (int x = fromX; x <= toX; x++)
                        {
                            BucketItems[BucketFill[z * _side + x]++] = i;
                        }
                    }
                }
            }

            // The road points within reach of any place on it (InLane looks 6 m from a place).
            _points = TrafficManager.HasInstance;
            if (_points)
            {
                TrafficTools.ClosestWaypoint(new Vector3(midX, _lookY, midZ), half * 1.4143f + LaneReach + 3f, CollectNear);
            }
        }

        // Lets go of everything the square held. Called when Plan ends, and before the next is read.
        private static void Forget()
        {
            if (_solidCount > 0)
            {
                Array.Clear(Solid, 0, _solidCount);
                Array.Clear(SolidBody, 0, _solidCount);
            }
            _solidCount = 0;
            _spotsSeen = Spots.Count;
            Spots.Clear();
            Answers.Clear();
            Near.Clear();
            _looked = false;
            _boxes = false;
            _bucketed = false;
            _points = false;
        }

        private static void Buckets(float lowX, float highX, float lowZ, float highZ, out int fromX, out int toX, out int fromZ, out int toZ)
        {
            fromX = Mathf.Clamp((int)((lowX - _lookX) / Bucket), 0, _side - 1);
            toX = Mathf.Clamp((int)((highX - _lookX) / Bucket), 0, _side - 1);
            fromZ = Mathf.Clamp((int)((lowZ - _lookZ) / Bucket), 0, _side - 1);
            toZ = Mathf.Clamp((int)((highZ - _lookZ) / Bucket), 0, _side - 1);
        }

        // The rectangle one solid thing covers on the ground: a box collider's or a mesh's own box seen from above,
        // turned as it is turned (so a car standing askew is no wider than a car); anything else square-on.
        private static void Outline(int i)
        {
            Collider solid = Solid[i];
            Bounds bounds = solid.bounds;
            SolidBody[i] = solid.attachedRigidbody;
            SolidLayer[i] = 1 << solid.gameObject.layer;
            var box = solid as BoxCollider;
            var mesh = box == null ? solid as MeshCollider : null;
            Mesh shape = mesh != null ? mesh.sharedMesh : null;
            float alongX = 1f, alongZ = 0f, halfA = bounds.extents.x, halfB = bounds.extents.z;
            Vector3 middle = bounds.center;
            bool upright = false;
            if (box != null || shape != null)
            {
                Transform t = solid.transform;
                Bounds own = shape != null ? shape.bounds : default;
                Vector3 centre = box != null ? box.center : own.center;
                Vector3 half = box != null ? box.size * 0.5f : own.extents;
                Vector3 scale = t.lossyScale;
                half.x = Mathf.Abs(half.x * scale.x);
                half.y = Mathf.Abs(half.y * scale.y);
                half.z = Mathf.Abs(half.z * scale.z);
                Vector3 right = t.right, up = t.up, forward = t.forward;
                upright = Mathf.Abs(up.y) > Upright;
                float rightFlat = right.x * right.x + right.z * right.z, forwardFlat = forward.x * forward.x + forward.z * forward.z;
                float flat = Mathf.Sqrt(Mathf.Max(rightFlat, forwardFlat));
                if (flat > 0.01f)
                {
                    alongX = (rightFlat >= forwardFlat ? right.x : forward.x) / flat;
                    alongZ = (rightFlat >= forwardFlat ? right.z : forward.z) / flat;
                }
                // Its three sides laid onto the ground: what they reach along that direction, and across it.
                halfA = half.x * Mathf.Abs(right.x * alongX + right.z * alongZ) + half.y * Mathf.Abs(up.x * alongX + up.z * alongZ)
                        + half.z * Mathf.Abs(forward.x * alongX + forward.z * alongZ);
                halfB = half.x * Mathf.Abs(right.z * alongX - right.x * alongZ) + half.y * Mathf.Abs(up.z * alongX - up.x * alongZ)
                        + half.z * Mathf.Abs(forward.z * alongX - forward.x * alongZ);
                middle = t.TransformPoint(centre);
            }
            SolidX[i] = middle.x;
            SolidZ[i] = middle.z;
            SolidAlongX[i] = alongX;
            SolidAlongZ[i] = alongZ;
            SolidHalfA[i] = halfA;
            SolidHalfB[i] = halfB;
            SolidHalfX[i] = Mathf.Abs(alongX) * halfA + Mathf.Abs(alongZ) * halfB + BoxPad;
            SolidHalfZ[i] = Mathf.Abs(alongZ) * halfA + Mathf.Abs(alongX) * halfB + BoxPad;
            // Sure only of a box, or of a vehicle's own hull, standing upright: a thing lying on its side, leaning on
            // another, or round (a ball, a capsule) is not its rectangle, and the engine is asked.
            SolidSure[i] = upright && (box != null || SolidBody[i] != null) && halfA > Well + 0.05f && halfB > Well + 0.05f
                           && bounds.min.y < _lookY + BandHigh && bounds.max.y > _lookY + BandLow;
        }

        // What stands in a strip of road, as SceneParking.InStrip finds it. The engine is asked only when a rectangle
        // read at the start is grazed (or the strip leaves the square, or the square was never read).
        private static Collider InStrip(Vector3 from, Vector3 to, float halfWidth, Rigidbody except, int mask)
        {
            _poses++;
            if (!_looked)
            {
                _asked++;
                return SceneParking.InStrip(from, to, halfWidth, except, mask);
            }
            int met = Beyond, which = -1;
            if (_boxes)
            {
                met = Meets(from, to, halfWidth, except, mask, out which);
                if (met == Free)
                {
                    return null;
                }
                if (met == Into)
                {
                    return Solid[which];
                }
            }
            long key = Strip(from, to, halfWidth, except, mask);
            if (Answers.TryGetValue(key, out Collider said))
            {
                return said;
            }
            if (met == Grazes && _askedNow >= Askings)
            {
                _refused++;
                return Solid[which];
            }
            _asked++;
            _askedNow++;
            said = SceneParking.InStrip(from, to, halfWidth, except, mask);
            Answers[key] = said;
            return said;
        }

        // One number for a strip, to the millimetre: the same strip met again has the same number. (64 bits mixed
        // from six whole numbers: two different strips of one Plan sharing a number is not a thing that happens.)
        private static long Strip(Vector3 from, Vector3 to, float halfWidth, Rigidbody except, int mask)
        {
            unchecked
            {
                const long Step = unchecked((long)0x9E3779B97F4A7C15UL);
                long key = Mathf.RoundToInt((from.x - _lookX) * 1000f);
                key = key * Step + Mathf.RoundToInt((from.z - _lookZ) * 1000f);
                key = key * Step + Mathf.RoundToInt((to.x - _lookX) * 1000f);
                key = key * Step + Mathf.RoundToInt((to.z - _lookZ) * 1000f);
                key = key * Step + Mathf.RoundToInt(halfWidth * 1000f);
                key = key * Step + (mask ^ (except != null ? 0x40000000 : 0));
                return key ^ (key >> 29);
            }
        }

        private const int Free = 0, Grazes = 1, Into = 2, Beyond = 3;

        // A strip against the rectangles read at the start. Free: it touches none, so nothing solid is in it. Into:
        // it is well into one that is certainly at the crew's height. Grazes: it touches one, and only the engine
        // can say. Beyond: it leaves the square that was read.
        private static int Meets(Vector3 from, Vector3 to, float halfWidth, Rigidbody except, int mask, out int which)
        {
            which = -1;
            float runX = to.x - from.x, runZ = to.z - from.z;
            float length = Mathf.Sqrt(runX * runX + runZ * runZ);
            if (length < 0.05f)
            {
                return Beyond;   // (SceneParking.InStrip has its own answer for a strip of no length)
            }
            float alongX = runX / length, alongZ = runZ / length;   // the strip's own two directions: along it, and across (-alongZ, alongX)
            float halfLength = length * 0.5f;
            float midX = (from.x + to.x) * 0.5f, midZ = (from.z + to.z) * 0.5f;
            float reachX = Mathf.Abs(alongX) * halfLength + Mathf.Abs(alongZ) * halfWidth;
            float reachZ = Mathf.Abs(alongZ) * halfLength + Mathf.Abs(alongX) * halfWidth;
            float lowX = midX - reachX, highX = midX + reachX, lowZ = midZ - reachZ, highZ = midZ + reachZ;
            if (lowX < _lookX || lowZ < _lookZ || highX > _lookX + _lookSize || highZ > _lookZ + _lookSize)
            {
                return Beyond;
            }
            int met = Free;
            if (!_bucketed)
            {
                for (int i = 0; i < _solidCount && met != Into; i++)
                {
                    Touch(i, midX, midZ, alongX, alongZ, halfLength, halfWidth, lowX, highX, lowZ, highZ, except, mask, ref met, ref which);
                }
                return met;
            }
            Buckets(lowX, highX, lowZ, highZ, out int fromX, out int toX, out int fromZ, out int toZ);
            for (int z = fromZ; z <= toZ && met != Into; z++)
            {
                for (int x = fromX; x <= toX && met != Into; x++)
                {
                    int bucket = z * _side + x;
                    for (int e = BucketStart[bucket]; e < BucketStart[bucket + 1] && met != Into; e++)
                    {
                        Touch(BucketItems[e], midX, midZ, alongX, alongZ, halfLength, halfWidth, lowX, highX, lowZ, highZ, except, mask, ref met, ref which);
                    }
                }
            }
            return met;
        }

        // One rectangle against one strip, seen from above.
        private static void Touch(int i, float midX, float midZ, float alongX, float alongZ, float halfLength, float halfWidth,
            float lowX, float highX, float lowZ, float highZ, Rigidbody except, int mask, ref int met, ref int which)
        {
            if ((SolidLayer[i] & mask) == 0)
            {
                return;
            }
            float x = SolidX[i], z = SolidZ[i];
            if (x + SolidHalfX[i] < lowX || x - SolidHalfX[i] > highX || z + SolidHalfZ[i] < lowZ || z - SolidHalfZ[i] > highZ
                || (except != null && SolidBody[i] == except)
                || Apart(i, BoxPad, x - midX, z - midZ, alongX, alongZ, halfLength, halfWidth))
            {
                return;
            }
            if (SolidSure[i] && !Apart(i, -Well, x - midX, z - midZ, alongX, alongZ, halfLength, halfWidth))
            {
                met = Into;
                which = i;
            }
            else if (met == Free)
            {
                met = Grazes;
                which = i;
            }
        }

        // True if a rectangle (taken larger or smaller by a margin) and a strip are apart: along one of the strip's two directions, or one of its own.
        private static bool Apart(int i, float margin, float offX, float offZ, float alongX, float alongZ, float halfLength, float halfWidth)
        {
            float ownX = SolidAlongX[i], ownZ = SolidAlongZ[i], halfA = SolidHalfA[i] + margin, halfB = SolidHalfB[i] + margin;
            float cos = Mathf.Abs(alongX * ownX + alongZ * ownZ), sin = Mathf.Abs(alongZ * ownX - alongX * ownZ);
            return Mathf.Abs(offX * alongX + offZ * alongZ) > halfLength + halfA * cos + halfB * sin
                   || Mathf.Abs(offZ * alongX - offX * alongZ) > halfWidth + halfA * sin + halfB * cos
                   || Mathf.Abs(offX * ownX + offZ * ownZ) > halfA + halfLength * cos + halfWidth * sin
                   || Mathf.Abs(offZ * ownX - offX * ownZ) > halfB + halfLength * sin + halfWidth * cos;
        }

        // True if there is ground under a place.
        private static bool HasGround(Vector3 place)
        {
            if (!SquareOf(place, out int key, out Vector3 middle))
            {
                return StreetSpots.Ground(place, out _);
            }
            return (Read(key, middle, false).State & Grounded) != 0;
        }

        // True if a place is on the carriageway (OnCarriage), by the square of ground it is in.
        private static bool OnRoad(Vector3 place)
        {
            if (!SquareOf(place, out int key, out Vector3 middle))
            {
                return OnCarriage(place);
            }
            return (Read(key, middle, true).State & Carriage) != 0;
        }

        // The 0.2 m square a place is in, and its middle. False outside the square that was read (or with none read).
        private static bool SquareOf(Vector3 place, out int key, out Vector3 middle)
        {
            key = 0;
            middle = place;
            if (!_looked)
            {
                return false;
            }
            float x = (place.x - _lookX) / Square02, z = (place.z - _lookZ) / Square02;
            if (x < 0f || z < 0f || x >= Across02 || z >= Across02 || place.x > _lookX + _lookSize || place.z > _lookZ + _lookSize)
            {
                return false;
            }
            int squareX = (int)x, squareZ = (int)z;
            key = squareZ * Across02 + squareX;
            middle = new Vector3(_lookX + (squareX + 0.5f) * Square02, place.y, _lookZ + (squareZ + 0.5f) * Square02);
            return true;
        }

        private static Spot Read(int key, Vector3 middle, bool carriage)
        {
            Spots.TryGetValue(key, out Spot spot);
            byte before = spot.State;
            if ((spot.State & GroundAsked) == 0)
            {
                spot.State |= GroundAsked;
                if (StreetSpots.Ground(middle, out spot.Height))
                {
                    spot.State |= Grounded;
                }
            }
            if (carriage && (spot.State & CarriageAsked) == 0)
            {
                spot.State |= CarriageAsked;
                if ((spot.State & Grounded) != 0)
                {
                    middle.y = spot.Height;
                    if (!NavMesh.SamplePosition(middle, out _, 0.3f, StreetSpots.Pavement) || InLane(middle, Nearest(middle)))
                    {
                        spot.State |= Carriage;
                    }
                }
            }
            if (spot.State != before)
            {
                Spots[key] = spot;
            }
            return spot;
        }

        // The nearest road point within a lane's reach of a place: among those read at the start, exactly as the
        // traffic system picks it among all of them (it goes through every road point of the city for each asking).
        private static Waypoint Nearest(Vector3 place)
        {
            if (!_points)
            {
                return TrafficManager.HasInstance ? TrafficTools.ClosestWaypoint(place, LaneReach) : null;
            }
            float best = LaneReach * LaneReach;
            Waypoint found = null;
            for (int i = 0; i < Near.Count; i++)
            {
                float sqr = (place - Near[i].position).sqrMagnitude;
                if (sqr < best)
                {
                    best = sqr;
                    found = Near[i];
                }
            }
            return found;
        }
    }
}
