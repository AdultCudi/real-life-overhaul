using System;
using System.Collections.Generic;
using BAModAPI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The city's marked pedestrian crossings, arranged so that "is this person on a crossing, and
    /// whose turn is it there?" costs a few subtractions and no physics query. Each crossing is a
    /// trigger box of the game's with a link to one lamp (CrosswalkToTrafficLightLink.redTrafficLight):
    /// the RED lamp of the cars that drive over it. The game lets its pedestrians walk exactly while
    /// that lamp is lit (PedestrianMovementHandler.CanCrossCrosswalk), so lit means the cars have red
    /// and the people have the crossing.
    /// It is listed once, the first time somebody asks after a city has loaded: one search of the
    /// scene for that one component (a few hundred objects), put in a coarse grid. After that a
    /// question looks at the handful of boxes in one grid square. If the game has no such component
    /// any more, or none is found, it says so once and every question answers "not on a crossing".
    /// </summary>
    internal static class Crossings
    {
        private struct Crossing
        {
            public Vector3 Centre;
            public Vector3 Right;        // flat unit axes of the box
            public Vector3 Forward;
            public float HalfRight;
            public float HalfForward;
            public float Low;
            public float High;
            public GameObject Red;       // the cars' red lamp: lit, people may walk
        }

        private const float CellSize = 40f;
        private const float Margin = 0.3f;        // metres a foot may be outside the box and still be on the crossing
        private const float RetryAfter = 60f;     // seconds before an empty list is looked for again (the street may not have been there yet)

        private static readonly Dictionary<long, List<int>> Cells = new Dictionary<long, List<int>>();
        private static readonly List<Crossing> All = new List<Crossing>(256);
        private static Object _probe;             // one of the links found: gone means the city was unloaded
        private static bool _built;
        private static bool _failed;
        private static float _builtAt;

        public static IModLogger Logger;

        public static int Count => All.Count;

        public static void Reset()
        {
            Cells.Clear();
            All.Clear();
            _probe = null;
            _built = false;
        }

        /// <summary>Lists the crossings if that has not been done for this city. False if there is nothing to ask.</summary>
        public static bool Ensure()
        {
            if (_failed)
            {
                return false;
            }
            if (_built)
            {
                bool gone = (object)_probe != null && _probe == null;
                bool empty = All.Count == 0 && Time.time - _builtAt > RetryAfter;
                if (!gone && !empty)
                {
                    return All.Count > 0;
                }
                Reset();
            }
            _built = true;
            _builtAt = Time.time;
            try
            {
                SessionWatch.Mark("streets: the city's crossings being listed");
                Build();
                Logger?.Info("Streets: " + All.Count + " marked crossings listed, in " + Cells.Count + " grid squares.");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Logger?.Warn("Streets: the game's crossings could not be read; nobody counts as being on one for this session.");
                Reset();
                _built = true;
                _failed = true;
            }
            return All.Count > 0;
        }

        private static void Build()
        {
            CrosswalkToTrafficLightLink[] links = Object.FindObjectsByType<CrosswalkToTrafficLightLink>(FindObjectsSortMode.None);
            for (int i = 0; i < links.Length; i++)
            {
                CrosswalkToTrafficLightLink link = links[i];
                Collider collider = link != null ? link.GetComponent<Collider>() : null;
                if (collider == null)
                {
                    continue;
                }
                Bounds bounds = collider.bounds;
                var crossing = new Crossing
                {
                    Centre = bounds.center,
                    Right = Vector3.right,
                    Forward = Vector3.forward,
                    HalfRight = bounds.extents.x,
                    HalfForward = bounds.extents.z,
                    Low = bounds.min.y - 1f,
                    High = bounds.max.y + 2f,
                    Red = link.redTrafficLight
                };
                // A box that lies flat is taken as it lies, turned however the street runs; anything else by its bounds.
                Transform at = collider.transform;
                if (collider is BoxCollider box && Mathf.Abs(at.up.y) > 0.9f)
                {
                    Vector3 right = at.right;
                    Vector3 forward = at.forward;
                    right.y = 0f;
                    forward.y = 0f;
                    if (right.sqrMagnitude > 0.01f && forward.sqrMagnitude > 0.01f)
                    {
                        Vector3 scale = at.lossyScale;
                        crossing.Centre = at.TransformPoint(box.center);
                        crossing.Right = right.normalized;
                        crossing.Forward = forward.normalized;
                        crossing.HalfRight = Mathf.Abs(box.size.x * scale.x) * 0.5f;
                        crossing.HalfForward = Mathf.Abs(box.size.z * scale.z) * 0.5f;
                    }
                }
                All.Add(crossing);
                int index = All.Count - 1;
                if (_probe == null)
                {
                    _probe = link;
                }
                // In every grid square its bounds reach into, so that a question needs only its own square.
                int fromX = Mathf.FloorToInt((bounds.min.x - Margin) / CellSize);
                int toX = Mathf.FloorToInt((bounds.max.x + Margin) / CellSize);
                int fromZ = Mathf.FloorToInt((bounds.min.z - Margin) / CellSize);
                int toZ = Mathf.FloorToInt((bounds.max.z + Margin) / CellSize);
                for (int x = fromX; x <= toX; x++)
                {
                    for (int z = fromZ; z <= toZ; z++)
                    {
                        long key = Key(x, z);
                        if (!Cells.TryGetValue(key, out List<int> cell))
                        {
                            cell = new List<int>(4);
                            Cells[key] = cell;
                        }
                        cell.Add(index);
                    }
                }
            }
        }

        private static long Key(int x, int z)
        {
            return ((long)x << 32) ^ (z & 0xffffffffL);
        }

        /// <summary>
        /// True if the point is on a marked crossing, with whether the people there have the light (the
        /// cars' red lamp is lit; a crossing with no lamp is always the people's). Call Ensure first.
        /// </summary>
        public static bool At(Vector3 point, out bool peopleMayWalk)
        {
            peopleMayWalk = false;
            if (All.Count == 0
                || !Cells.TryGetValue(Key(Mathf.FloorToInt(point.x / CellSize), Mathf.FloorToInt(point.z / CellSize)), out List<int> cell))
            {
                return false;
            }
            for (int i = 0; i < cell.Count; i++)
            {
                Crossing crossing = All[cell[i]];
                if (point.y < crossing.Low || point.y > crossing.High)
                {
                    continue;
                }
                float dx = point.x - crossing.Centre.x;
                float dz = point.z - crossing.Centre.z;
                float across = dx * crossing.Right.x + dz * crossing.Right.z;
                float along = dx * crossing.Forward.x + dz * crossing.Forward.z;
                if (across > -crossing.HalfRight - Margin && across < crossing.HalfRight + Margin
                    && along > -crossing.HalfForward - Margin && along < crossing.HalfForward + Margin)
                {
                    peopleMayWalk = crossing.Red == null || crossing.Red.activeSelf;
                    return true;
                }
            }
            return false;
        }
    }
}
