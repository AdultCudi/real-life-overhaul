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
    /// Built once when a test starts (one walk along the graph, a handful of overlap tests for
    /// the bay); nothing here runs afterwards.
    /// </summary>
    internal sealed class TestRoute
    {
        private const float Spacing = 105f;          // metres of lane between checkpoints
        private const float BaySearch = 150f;        // metres past the last checkpoint in which a bay is looked for
        private static readonly float[] KerbOffsets = { 2.9f, 3.2f, 3.5f };

        public readonly List<Vector3> Checkpoints = new List<Vector3>(8);
        public readonly List<Vector3> Runs = new List<Vector3>(8);      // the way the lane runs at each checkpoint

        // The middle of the lane the route follows, point by point, and whether each point is inside a junction:
        // what "keeping to the lane" is measured against (the road data's own points may lie metres apart).
        private readonly List<Vector3> _line = new List<Vector3>(256);
        private readonly List<bool> _junction = new List<bool>(256);

        /// <summary>A marked box at the kerb to park in. Without one the test ends with a stop at <see cref="StopAt"/>.</summary>
        public bool HasBay { get; private set; }
        public Vector3 BayCentre { get; private set; }
        public Quaternion BayFacing { get; private set; }
        public float BayHalfWidth { get; private set; }
        public float BayHalfLength { get; private set; }
        public Vector3 StopAt { get; private set; }
        public float Length { get; private set; }

        /// <summary>Builds the route. False if the road graph gives fewer than five checkpoints from here.</summary>
        public bool Build(Vector3 from, Vector3 heading, float carHalfWidth, float carHalfLength, int wanted)
        {
            Checkpoints.Clear();
            Runs.Clear();
            _line.Clear();
            _junction.Clear();
            HasBay = false;
            Length = 0f;
            // Kept from the city's one search: the traffic system's own accessor searches the whole scene on every call, a long frame at the start of every lesson.
            Waypoint[] all = RoadData.Points;
            Waypoint at = TrafficTools.ClosestWaypointAlong(from, heading, 30f);
            if (all == null || at == null)
            {
                return false;
            }
            float since = Spacing * 0.45f;   // the first checkpoint comes sooner: something to aim at straight away
            float past = 0f;
            Vector3 lastRun = heading;
            _line.Add(at.position);
            _junction.Add(!Plain(at));
            for (int step = 0; step < 900; step++)
            {
                Waypoint next = Next(all, at);
                if (next == null)
                {
                    break;
                }
                Vector3 leg = next.position - at.position;
                float length = leg.magnitude;
                leg.y = 0f;
                if (leg.sqrMagnitude > 0.04f)
                {
                    lastRun = leg.normalized;
                }
                since += length;
                Length += length;
                bool plain = Plain(next);
                _line.Add(next.position);
                _junction.Add(!plain);
                if (Checkpoints.Count < wanted)
                {
                    if (since >= Spacing && plain)
                    {
                        Checkpoints.Add(next.position);
                        Runs.Add(lastRun);
                        since = 0f;
                    }
                }
                else
                {
                    // Past the last checkpoint: the first stretch of kerb with room for the bay.
                    past += length;
                    if (plain && past > 18f && FindBay(next, lastRun, carHalfWidth, carHalfLength))
                    {
                        break;
                    }
                    if (past >= BaySearch)
                    {
                        break;
                    }
                }
                at = next;
            }
            if (Checkpoints.Count < 5)
            {
                return false;
            }
            StopAt = Checkpoints[Checkpoints.Count - 1];
            if (!HasBay)
            {
                // No kerb with room: the last checkpoint becomes the place to stop at.
                Checkpoints.RemoveAt(Checkpoints.Count - 1);
                Runs.RemoveAt(Runs.Count - 1);
            }
            return true;
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

        // The way on from a lane point: a random open branch, so that routes turn at junctions.
        private static Waypoint Next(Waypoint[] all, Waypoint at)
        {
            List<int> ways = at.neighbors;
            if (ways == null || ways.Count == 0)
            {
                return null;
            }
            int start = Random.Range(0, ways.Count);
            for (int i = 0; i < ways.Count; i++)
            {
                int index = ways[(start + i) % ways.Count];
                Waypoint way = index >= 0 && index < all.Length ? all[index] : null;
                if (way != null && !way.temporaryDisabled)
                {
                    return way;
                }
            }
            return null;
        }

        private static bool Plain(Waypoint point)
        {
            return !point.enter && !point.exit && !point.temporaryDisabled && !point.IsInIntersection()
                   && point.neighbors != null && point.neighbors.Count == 1 && point.prev != null && point.prev.Count == 1;
        }

        // Beside the lane on its kerb side: on the carriageway, no corner on a pavement, in nobody's lane, touching nothing.
        private bool FindBay(Waypoint point, Vector3 along, float carHalfWidth, float carHalfLength)
        {
            TrafficManager traffic = TrafficManager.Instance;
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
                    || traffic.GetClosestWaypoint(place, 2.1f) != null)
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
    }
}
