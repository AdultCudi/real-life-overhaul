using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's road graph (the traffic system's lane points) as <see cref="RouteSearch"/> wants it:
    /// a lane's own next points, and the changes of lane the graph offers between two lanes that run
    /// the same way side by side. One pass over the lane points, twice (count, then fill); whoever
    /// calls it names the work for the session record and does it once per city.
    ///
    /// Used by the bus network (<see cref="TransitMap"/>) and by the GPS (Scripts/Navigation). Each
    /// builds a search of its own, because a search keeps its state in the object and the two may
    /// be searching in the same frames.
    /// </summary>
    public static class RoadGraph
    {
        /// <summary>A lane point cars may use.</summary>
        public static bool Drivable(Waypoint point)
        {
            return point.allowedCars != null && point.allowedCars.Contains(VehicleTypes.Car);
        }

        /// <summary>A lane point of a road proper: not part of a junction, not where traffic gives way.</summary>
        public static bool Plain(Waypoint point)
        {
            return point != null && !point.enter && !point.exit && !point.giveWay && !point.IsInIntersection() && TrafficManager.IsNotIntersection(point);
        }

        /// <summary>The graph over these lane points. A change of lane costs <paramref name="laneChangeCost"/> metres over its length.</summary>
        public static RouteSearch Build(Waypoint[] points, float laneChangeCost, out int laneChanges)
        {
            int count = points.Length;
            var graph = new RouteSearch(count);
            var start = new int[count + 1];
            int edges = 0;
            int[] to = null;
            float[] extra = null;
            laneChanges = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1)
                {
                    to = new int[edges];
                    extra = new float[edges];
                }
                edges = 0;
                for (int i = 0; i < count; i++)
                {
                    Waypoint point = points[i];
                    if (pass == 1)
                    {
                        start[i] = edges;
                    }
                    if (point == null)
                    {
                        continue;
                    }
                    if (pass == 0)
                    {
                        graph.X[i] = point.position.x;
                        graph.Y[i] = point.position.y;
                        graph.Z[i] = point.position.z;
                    }
                    for (int k = 0; point.neighbors != null && k < point.neighbors.Count; k++)
                    {
                        int next = point.neighbors[k];
                        if (next < 0 || next >= count || points[next] == null || !Drivable(points[next]))
                        {
                            continue;
                        }
                        if (pass == 1)
                        {
                            to[edges] = next;
                            extra[edges] = 0f;
                        }
                        edges++;
                    }
                    for (int k = 0; point.otherLanes != null && k < point.otherLanes.Count; k++)
                    {
                        int beside = point.otherLanes[k];
                        if (!LaneChange(points, point, beside))
                        {
                            continue;
                        }
                        if (pass == 1)
                        {
                            to[edges] = beside;
                            extra[edges] = laneChangeCost;
                            laneChanges++;
                        }
                        edges++;
                    }
                }
            }
            start[count] = edges;
            graph.SetEdges(start, to, extra);
            return graph;
        }

        // A change of lane a vehicle may make: between two plain lane points a few metres apart whose lanes run the same way.
        private static bool LaneChange(Waypoint[] points, Waypoint point, int beside)
        {
            if (beside < 0 || beside >= points.Length)
            {
                return false;
            }
            Waypoint other = points[beside];
            if (other == null || !Drivable(other) || !Drivable(point) || !Plain(point) || !Plain(other)
                || point.neighbors == null || point.neighbors.Count == 0 || other.neighbors == null || other.neighbors.Count == 0)
            {
                return false;
            }
            Waypoint ahead = At(points, point.neighbors[0]);
            Waypoint otherAhead = At(points, other.neighbors[0]);
            if (ahead == null || otherAhead == null)
            {
                return false;
            }
            Vector3 across = other.position - point.position;
            if (Mathf.Abs(across.y) > 2f)
            {
                return false;
            }
            across.y = 0f;
            Vector3 lane = Flat(ahead.position - point.position);
            Vector3 otherLane = Flat(otherAhead.position - other.position);
            if (across.sqrMagnitude < 1f || across.sqrMagnitude > 14f * 14f || lane.sqrMagnitude < 0.01f || otherLane.sqrMagnitude < 0.01f)
            {
                return false;
            }
            lane.Normalize();
            return Vector3.Dot(lane, otherLane.normalized) > 0.7f && Vector3.Dot(lane, across) > -1f;
        }

        private static Waypoint At(Waypoint[] points, int index)
        {
            return index >= 0 && index < points.Length ? points[index] : null;
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }
    }
}
