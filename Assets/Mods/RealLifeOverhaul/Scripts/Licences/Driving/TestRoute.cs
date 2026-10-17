using System.Collections.Generic;
using GleyTrafficSystem;
using GleyUrbanAssets;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>A lesson's route on the traffic system's lanes: the checkpoints planned, and a bay at the kerb after the last.</summary>
    internal sealed class TestRoute
    {
        private static readonly float[] KerbOffsets = { 2.9f, 3.2f, 3.5f };

        public readonly List<Vector3> Checkpoints = new List<Vector3>(8);
        public readonly List<Vector3> Runs = new List<Vector3>(8);      // the way the lane runs at each checkpoint
        private readonly List<int> _lanePoints = new List<int>(8);     // each checkpoint's lane point; the bay's is the one beside it

        // The lane followed, point by point, and whether each point is in a junction.
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

        public int Expanded => _planner.Expanded;

        /// <summary>Plans a route from a lane point, kept for <see cref="Build"/>.</summary>
        public RouteOutcome Plan(Waypoint start, int wanted, int seed, bool askWhy)
        {
            _plannedOn = null;
            Waypoint[] all = RoadData.Points;   // the traffic system's own accessor searches the whole scene on every call
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

        /// <summary>Lays out the route last planned. False if none is, or the city's lane points have changed since.</summary>
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
                    // Past the last checkpoint: the first kerb with room for the bay.
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
                // No kerb with room: the last checkpoint becomes the stop (its lane point stays).
                Checkpoints.RemoveAt(Checkpoints.Count - 1);
                Runs.RemoveAt(Runs.Count - 1);
            }
            return true;
        }

        /// <summary>Whether the lane at a checkpoint, or past the last at the bay or stop, has closed since the planning.</summary>
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

        /// <summary>Metres from a point to the route's lane, and whether that stretch is at a junction. A few hundred subtractions.</summary>
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

        // The planner's own test, so what is planned is what is laid.
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

        /// <summary>The traffic system's lane points for the planner; <see cref="All"/> is set for one planning.</summary>
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

            // One lane in and one out, no junction.
            public static bool IsPlain(Waypoint point, bool closures)
            {
                return point != null && !point.enter && !point.exit && (!closures || !point.temporaryDisabled) && !point.IsInIntersection()
                       && point.neighbors != null && point.neighbors.Count == 1 && point.prev != null && point.prev.Count == 1;
            }
        }
    }
}
