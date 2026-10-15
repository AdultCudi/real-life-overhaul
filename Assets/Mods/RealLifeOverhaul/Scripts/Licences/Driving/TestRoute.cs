using System.Collections.Generic;
using GleyTrafficSystem;
using GleyUrbanAssets;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The route of a driving lesson or test: five to seven checkpoints along real lanes of the
    /// traffic system's road graph, starting on the lane the school's car stands in and turning
    /// at junctions as the graph allows, and a marked bay at the kerb after the last of them.
    /// Planned over the lanes that are open now (<see cref="TestRoutePlanner"/>): a lane an accident's
    /// cordon or a street event has closed is driven round, and a planning that finds no route says
    /// whether a closure is why. Planned once for each place the school could put its car when a lesson
    /// or test is booked or starts (the planner's bound, a handful of overlap tests for the bay); while
    /// one is driven, one look a step at whether the next checkpoint's lane has been closed since.
    /// Nothing per frame.
    /// </summary>
    internal sealed class TestRoute
    {
        private static readonly float[] KerbOffsets = { 2.9f, 3.2f, 3.5f };

        public readonly List<Vector3> Checkpoints = new List<Vector3>(8);
        public readonly List<Vector3> Runs = new List<Vector3>(8);      // the way the lane runs at each checkpoint
        private readonly List<int> _lanePoints = new List<int>(8);     // the lane point of each checkpoint (the bay's: the one beside it)

        // The middle of the lane the route follows, point by point, and whether each point is inside a junction:
        // what "keeping to the lane" is measured against (the road data's own points may lie metres apart).
        private readonly List<Vector3> _line = new List<Vector3>(256);
        private readonly List<bool> _junction = new List<bool>(256);

        private readonly TestRoutePlanner _planner = new TestRoutePlanner();
        private readonly Lanes _lanes = new Lanes();
        private readonly List<int> _laid = new List<int>(8);
        private Waypoint[] _plannedOn;
        private int _wanted;

        /// <summary>A marked box at the kerb to park in. Without one the test ends with a stop at <see cref="StopAt"/>.</summary>
        public bool HasBay { get; private set; }
        public Vector3 BayCentre { get; private set; }
        public Quaternion BayFacing { get; private set; }
        public float BayHalfWidth { get; private set; }
        public float BayHalfLength { get; private set; }
        public Vector3 StopAt { get; private set; }
        public float Length { get; private set; }

        /// <summary>Lane points the last planning went into (for the log).</summary>
        public int Expanded => _planner.Expanded;

        /// <summary>
        /// Plans a route from a lane point, kept for <see cref="Build"/>. Planned: there is one over the lanes open now.
        /// Closed: there is none now, but there would be with no lane closed (asked only with <paramref name="askWhy"/>).
        /// NoRoad: there is none.
        /// </summary>
        public RouteOutcome Plan(Waypoint start, int wanted, int seed, bool askWhy)
        {
            _plannedOn = null;
            Waypoint[] all = RoadData.Points;   // kept from the city's one search: the traffic system's own accessor searches the whole scene on every call
            if (all == null || start == null || start.listIndex < 0 || start.listIndex >= all.Length || !ReferenceEquals(all[start.listIndex], start))
            {
                return RouteOutcome.NoRoad;
            }
            _lanes.All = all;
            RouteOutcome outcome = _planner.Plan(_lanes, start.listIndex, wanted, seed, askWhy);
            _lanes.All = null;
            if (outcome == RouteOutcome.Planned)
            {
                _plannedOn = all;
                _wanted = wanted;
            }
            return outcome;
        }

        /// <summary>
        /// Lays the route last planned out on the road: its checkpoints, the lane it follows and the bay. False if nothing
        /// is planned (or the city's lane points have changed since).
        /// </summary>
        public bool Build(float carHalfWidth, float carHalfLength)
        {
            Checkpoints.Clear();
            Runs.Clear();
            _lanePoints.Clear();
            _line.Clear();
            _junction.Clear();
            HasBay = false;
            Length = 0f;
            Waypoint[] all = RoadData.Points;
            List<int> path = _planner.Path;
            if (_plannedOn == null || !ReferenceEquals(all, _plannedOn) || path.Count < 2)
            {
                return false;
            }
            _lanes.All = all;
            TestRoutePlanner.Layout(_lanes, path, _wanted, _laid);
            _lanes.All = null;
            if (_laid.Count < TestRoutePlanner.Least)
            {
                return false;
            }
            int laid = 0;
            float past = 0f;
            Vector3 lastRun = Vector3.forward;
            Waypoint at = all[path[0]];
            _line.Add(at.position);
            _junction.Add(!Plain(at));
            for (int i = 1; i < path.Count; i++)
            {
                Waypoint next = all[path[i]];
                Vector3 leg = next.position - at.position;
                float length = leg.magnitude;
                leg.y = 0f;
                if (leg.sqrMagnitude > 0.04f)
                {
                    lastRun = leg.normalized;
                }
                Length += length;
                bool plain = Plain(next);
                _line.Add(next.position);
                _junction.Add(!plain);
                if (laid < _laid.Count)
                {
                    if (_laid[laid] == i)
                    {
                        Checkpoints.Add(next.position);
                        Runs.Add(lastRun);
                        _lanePoints.Add(path[i]);
                        laid++;
                    }
                }
                else
                {
                    // Past the last checkpoint: the first stretch of kerb with room for the bay.
                    past += length;
                    if (plain && past > 18f && FindBay(next, lastRun, carHalfWidth, carHalfLength))
                    {
                        _lanePoints.Add(path[i]);
                        break;
                    }
                    if (past >= TestRoutePlanner.Tail)
                    {
                        break;
                    }
                }
                at = next;
            }
            StopAt = Checkpoints[_laid.Count - 1];
            if (!HasBay)
            {
                // No kerb with room: the last checkpoint becomes the place to stop at (its lane point stays, for the stop).
                Checkpoints.RemoveAt(Checkpoints.Count - 1);
                Runs.RemoveAt(Runs.Count - 1);
            }
            return true;
        }

        /// <summary>
        /// Whether the lane at a checkpoint (or, past the last, at the bay or the stop) has been closed since the route was
        /// planned: an accident's cordon or a street event that came up while the drive was on. One look at one lane point.
        /// </summary>
        public bool ClosedAt(int checkpoint)
        {
            Waypoint[] all = RoadData.Points;
            if (all == null || !ReferenceEquals(all, _plannedOn) || checkpoint < 0 || checkpoint >= _lanePoints.Count)
            {
                return false;
            }
            int point = _lanePoints[checkpoint];
            return point >= 0 && point < all.Length && all[point] != null && all[point].temporaryDisabled;
        }

        /// <summary>
        /// How far to one side of the route's lane a point is, in metres, and whether the nearest stretch of it is in
        /// or beside a junction (where there is no lane to keep to). A few hundred subtractions; called ten times a
        /// second while a test is driven.
        /// </summary>
        public float Lateral(Vector3 point, out bool junction)
        {
            junction = true;
            float best = float.MaxValue;
            int nearest = -1;
            for (int i = 0; i + 1 < _line.Count; i++)
            {
                Vector3 a = _line[i];
                Vector3 b = _line[i + 1];
                float abx = b.x - a.x;
                float abz = b.z - a.z;
                float length = abx * abx + abz * abz;
                float t = length > 0.0001f ? Mathf.Clamp01(((point.x - a.x) * abx + (point.z - a.z) * abz) / length) : 0f;
                float dx = point.x - (a.x + abx * t);
                float dz = point.z - (a.z + abz * t);
                float sqr = dx * dx + dz * dz;
                if (sqr < best && Mathf.Abs(point.y - a.y) < 6f)
                {
                    best = sqr;
                    nearest = i;
                }
            }
            if (nearest < 0)
            {
                return 0f;
            }
            junction = _junction[nearest] || _junction[nearest + 1];
            return Mathf.Sqrt(best);
        }

        // Plain road as the planner counts it (Lanes.Plain with closures): the same test, so what is planned is what is laid.
        private static bool Plain(Waypoint point)
        {
            return Lanes.IsPlain(point, true);
        }

        // Beside the lane on its kerb side: on the carriageway, no corner on a pavement, in nobody's lane, touching nothing.
        private bool FindBay(Waypoint point, Vector3 along, float carHalfWidth, float carHalfLength)
        {
            Vector3 right = Vector3.Cross(Vector3.up, along);
            Quaternion facing = Quaternion.LookRotation(along, Vector3.up);
            float halfWidth = carHalfWidth + 0.45f;
            float halfLength = carHalfLength + 1.2f;
            for (int i = 0; i < KerbOffsets.Length; i++)
            {
                Vector3 place = point.position + right * Mathf.Max(KerbOffsets[i], 1.9f + halfWidth);
                if (!StreetSpots.Ground(place, out float ground) || Mathf.Abs(ground - point.position.y) > 0.5f)
                {
                    continue;
                }
                place.y = ground;
                if (!StreetSpots.OnRoad(place, 0.5f, out _)
                    || OnPavement(place) || OnPavement(place + along * halfLength + right * halfWidth) || OnPavement(place + along * halfLength - right * halfWidth)
                    || OnPavement(place - along * halfLength + right * halfWidth) || OnPavement(place - along * halfLength - right * halfWidth)
                    || !SceneParking.IsFree(place, facing, halfWidth, halfLength, 0.25f)
                    || TrafficTools.ClosestWaypoint(place, 2.1f) != null)
                {
                    continue;
                }
                HasBay = true;
                BayCentre = place;
                BayFacing = facing;
                BayHalfWidth = halfWidth;
                BayHalfLength = halfLength;
                Checkpoints.Add(place);
                Runs.Add(along);
                return true;
            }
            return false;
        }

        private static bool OnPavement(Vector3 point)
        {
            return NavMesh.SamplePosition(point, out _, 0.3f, StreetSpots.Pavement);
        }

        /// <summary>The traffic system's lane points as the planner asks about them. Set to the city's points for one planning, then let go.</summary>
        private sealed class Lanes : RouteRoads
        {
            public Waypoint[] All;

            public override int Count => All.Length;

            public override int Ways(int point)
            {
                List<int> ways = All[point]?.neighbors;
                return ways != null ? ways.Count : 0;
            }

            public override int Way(int point, int k)
            {
                return All[point].neighbors[k];
            }

            // A lane a car may use, and (with closures) not closed for now.
            public override bool Open(int point, bool closures)
            {
                Waypoint lane = All[point];
                return lane != null && RoadGraph.Drivable(lane) && (!closures || !lane.temporaryDisabled);
            }

            public override bool Plain(int point, bool closures)
            {
                return IsPlain(All[point], closures);
            }

            public override float Metres(int from, int to)
            {
                return Vector3.Distance(All[from].position, All[to].position);
            }

            // Road proper, one lane in and one out, no junction: where a checkpoint may stand.
            public static bool IsPlain(Waypoint point, bool closures)
            {
                return point != null && !point.enter && !point.exit && (!closures || !point.temporaryDisabled) && !point.IsInIntersection()
                       && point.neighbors != null && point.neighbors.Count == 1 && point.prev != null && point.prev.Count == 1;
            }
        }
    }
}
