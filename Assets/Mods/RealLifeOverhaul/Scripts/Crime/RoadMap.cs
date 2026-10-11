using System;
using System.Collections.Generic;
using GleyTrafficSystem;
using GleyUrbanAssets;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The traffic system's road data arranged so that questions about the player's own car are
    /// cheap: the stop line of every lane at every traffic light (with the light's red lamp, so
    /// red can be told from amber), and every lane point, both in a coarse grid. It is built
    /// once, the first time the player drives after a city load: one pass over the road data.
    /// After that a question looks at the handful of entries in the grid squares around the car.
    /// If the road data is not what this expects, it reports itself unusable and the offences
    /// that depend on it are simply never detected.
    /// </summary>
    internal static class RoadMap
    {
        internal struct StopLine
        {
            public Vector3 Position;
            public Vector3 Forward;      // the way the lane runs across the line
            public Waypoint Point;       // its "stop" flag is true while the light is not green
            public GameObject Red;       // the red lamp of that light
        }

        private const float CellSize = 40f;

        private static readonly Dictionary<long, List<int>> LineCells = new Dictionary<long, List<int>>();
        private static readonly Dictionary<long, List<int>> LaneCells = new Dictionary<long, List<int>>();
        private static readonly List<StopLine> Lines = new List<StopLine>();
        private static Waypoint[] _lanes;
        private static bool _built;
        private static bool _usable;

        /// <summary>True once the map is built and has something in it.</summary>
        public static bool Usable => _usable;
        public static int LineCount => Lines.Count;

        public static void Reset()
        {
            LineCells.Clear();
            LaneCells.Clear();
            Lines.Clear();
            _lanes = null;
            _built = false;
            _usable = false;
        }

        /// <summary>Builds the map if it has not been tried yet. Call only while the traffic system is running.</summary>
        public static bool Ensure()
        {
            if (_built)
            {
                return _usable;
            }
            _built = true;
            try
            {
                Build();
            }
            catch (Exception e)
            {
                Crime.Fault(e);
                _usable = false;
            }
            Crime.Logger?.Info(_usable
                ? $"Crime: road map built, {Lines.Count} stop lines at traffic lights, {(_lanes != null ? _lanes.Length : 0)} lane points."
                : "Crime: the traffic system's road data could not be read; red lights and speed limits are not policed.");
            return _usable;
        }

        private static void Build()
        {
            CurrentSceneData data = CurrentSceneData.GetSceneInstance();
            if (data == null || data.allWaypoints == null || data.allWaypoints.Length == 0)
            {
                return;
            }
            _lanes = data.allWaypoints;
            for (int i = 0; i < _lanes.Length; i++)
            {
                Waypoint lane = _lanes[i];
                if (lane != null)
                {
                    Put(LaneCells, lane.position, i);
                }
            }

            TrafficLightsIntersection[] lights = data.allLightsIntersections;
            for (int i = 0; lights != null && i < lights.Length; i++)
            {
                List<IntersectionStopWaypointsIndex> roads = lights[i] != null ? lights[i].stopWaypoints : null;
                for (int r = 0; roads != null && r < roads.Count; r++)
                {
                    IntersectionStopWaypointsIndex road = roads[r];
                    GameObject red = FirstLamp(road != null ? road.redLightObjects : null);
                    if (red == null || road.roadWaypoints == null)
                    {
                        continue;   // without the lamp, red cannot be told from amber: this light is not policed
                    }
                    for (int w = 0; w < road.roadWaypoints.Count; w++)
                    {
                        int index = road.roadWaypoints[w];
                        Waypoint point = index >= 0 && index < _lanes.Length ? _lanes[index] : null;
                        if (point == null || point.prev == null || point.prev.Count == 0)
                        {
                            continue;
                        }
                        int before = point.prev[0];
                        if (before < 0 || before >= _lanes.Length || _lanes[before] == null)
                        {
                            continue;
                        }
                        Vector3 forward = point.position - _lanes[before].position;
                        forward.y = 0f;
                        if (forward.sqrMagnitude < 0.01f)
                        {
                            continue;
                        }
                        Lines.Add(new StopLine { Position = point.position, Forward = forward.normalized, Point = point, Red = red });
                        Put(LineCells, point.position, Lines.Count - 1);
                    }
                }
            }
            _usable = _lanes.Length > 0;
        }

        private static GameObject FirstLamp(List<GameObject> lamps)
        {
            for (int i = 0; lamps != null && i < lamps.Count; i++)
            {
                if (lamps[i] != null)
                {
                    return lamps[i];
                }
            }
            return null;
        }

        private static long Key(float x, float z)
        {
            long cx = Mathf.FloorToInt(x / CellSize);
            long cz = Mathf.FloorToInt(z / CellSize);
            return (cx << 32) ^ (cz & 0xffffffffL);
        }

        private static void Put(Dictionary<long, List<int>> cells, Vector3 position, int index)
        {
            long key = Key(position.x, position.z);
            if (!cells.TryGetValue(key, out List<int> cell))
            {
                cell = new List<int>(4);
                cells[key] = cell;
            }
            cell.Add(index);
        }

        /// <summary>True while the light at a stop line is red (not amber, not green).</summary>
        public static bool IsRed(in StopLine line)
        {
            return line.Point.stop && line.Red != null && line.Red.activeInHierarchy;
        }

        /// <summary>
        /// The stop line the car is about to cross or has just crossed: in its own lane (within
        /// a lane's width sideways), running its way, no more than 30 m ahead or 8 m behind.
        /// Returns its index and how far the car's middle is past it (negative while short of it).
        /// </summary>
        public static bool LineAt(Vector3 position, Vector3 heading, out int index, out float along)
        {
            index = -1;
            along = 0f;
            float best = float.MaxValue;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (!LineCells.TryGetValue(Key(position.x + dx * CellSize, position.z + dz * CellSize), out List<int> cell))
                    {
                        continue;
                    }
                    for (int i = 0; i < cell.Count; i++)
                    {
                        StopLine line = Lines[cell[i]];
                        Vector3 offset = position - line.Position;
                        if (offset.y > 4f || offset.y < -4f || Vector3.Dot(line.Forward, heading) < 0.8f)
                        {
                            continue;
                        }
                        float ahead = offset.x * line.Forward.x + offset.z * line.Forward.z;
                        float side = offset.x * line.Forward.z - offset.z * line.Forward.x;
                        if (ahead < -30f || ahead > 8f || side > 2.4f || side < -2.4f)
                        {
                            continue;
                        }
                        float distance = ahead < 0f ? -ahead : ahead;
                        if (distance < best)
                        {
                            best = distance;
                            index = cell[i];
                            along = ahead;
                        }
                    }
                }
            }
            return index >= 0;
        }

        public static StopLine Line(int index)
        {
            return Lines[index];
        }

        /// <summary>
        /// The lane point nearest a position (not one inside a junction), within 30 m or so.
        /// Gives the distance to it, the way its lane runs and its speed limit in km/h.
        /// </summary>
        public static bool NearestLane(Vector3 position, out float distance, out Vector3 runs, out float limitKmh)
        {
            return NearestLane(position, out distance, out runs, out limitKmh, out _);
        }

        /// <summary>The same, with where that lane point is: which side of somebody on the pavement the road lies.</summary>
        public static bool NearestLane(Vector3 position, out float distance, out Vector3 runs, out float limitKmh, out Vector3 at)
        {
            at = position;
            distance = float.MaxValue;
            runs = Vector3.forward;
            limitKmh = CrimeLaw.SpeedLimitKmh;
            Waypoint nearest = null;
            float best = float.MaxValue;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (!LaneCells.TryGetValue(Key(position.x + dx * CellSize, position.z + dz * CellSize), out List<int> cell))
                    {
                        continue;
                    }
                    for (int i = 0; i < cell.Count; i++)
                    {
                        Waypoint lane = _lanes[cell[i]];
                        Vector3 offset = lane.position - position;
                        if (offset.y > 4f || offset.y < -4f)
                        {
                            continue;
                        }
                        float sqr = offset.x * offset.x + offset.z * offset.z;
                        if (sqr < best && !lane.enter && !lane.exit && lane.neighbors != null && lane.neighbors.Count > 0)
                        {
                            best = sqr;
                            nearest = lane;
                        }
                    }
                }
            }
            if (nearest == null)
            {
                return false;
            }
            distance = Mathf.Sqrt(best);
            at = nearest.position;
            int next = nearest.neighbors[0];
            if (next >= 0 && next < _lanes.Length && _lanes[next] != null)
            {
                Vector3 way = _lanes[next].position - nearest.position;
                way.y = 0f;
                if (way.sqrMagnitude > 0.01f)
                {
                    runs = way.normalized;
                }
            }
            if (nearest.maxSpeed >= 20 && nearest.maxSpeed <= 130)
            {
                limitKmh = Mathf.Max(nearest.maxSpeed, CrimeLaw.SpeedLimitKmh);
            }
            return true;
        }
    }
}
