using System;
using System.Collections.Generic;
using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One lane closed for roadworks through the traffic system's own switch on its road points, with the way into it at the
    /// junction behind, and only where traffic keeps another way. Open opens exactly what this closed. Not part of a save.
    /// </summary>
    public sealed class RoadClosure
    {
        private const float EntryWalk = 320f;   // metres back along the lane the junction behind is looked for
        private const float LaneHalf = 1.25f;   // a road point this near the lane's line is in the lane

        private static readonly List<Waypoint> Found = new List<Waypoint>(96);
        private static readonly Predicate<Waypoint> Collect = Keep;
        private static readonly Stack<Waypoint> Walk = new Stack<Waypoint>();
        private static readonly HashSet<int> Visited = new HashSet<int>();
        private static readonly List<Waypoint> Entries = new List<Waypoint>(8);
        private static readonly List<Waypoint> EntryParents = new List<Waypoint>(8);

        private readonly List<Waypoint> _closed = new List<Waypoint>(32);
        private Vector3 _centre, _lane, _across;
        private float _half;

        public bool Closed => _closed.Count > 0;

        /// <summary>The middle of the lane found by <see cref="Lane"/>, level with the place asked about.</summary>
        public Vector3 LaneCentre { get; private set; }

        /// <summary>Which way the lane's traffic runs.</summary>
        public Vector3 LaneDirection { get; private set; }

        /// <summary>Finds the lane nearest a place: its direction, and its centre point level with the place. False without traffic, without a lane in reach, or in a junction.</summary>
        public bool Lane(Vector3 place, float reach)
        {
            if (!TrafficTools.Ready)
            {
                return false;
            }
            TrafficManager traffic = TrafficManager.Instance;
            Waypoint nearest = TrafficTools.ClosestWaypoint(place, reach);
            if (nearest == null || Forked(nearest))
            {
                return false;
            }
            Vector3 direction = Direction(traffic, nearest);
            if (direction == Vector3.zero)
            {
                return false;
            }
            Vector3 offset = place - nearest.position;
            offset.y = 0f;
            LaneDirection = direction;
            LaneCentre = nearest.position + direction * Vector3.Dot(offset, direction);
            return true;
        }

        /// <summary>Closes the lane found by <see cref="Lane"/> for a stretch either side of a point. False, with nothing closed, where it cannot be done safely.</summary>
        public bool Close(Vector3 laneCentre, float halfLength)
        {
            Open();
            if (!TrafficTools.Ready)
            {
                return false;
            }
            try
            {
                TrafficManager traffic = TrafficManager.Instance;
                _centre = laneCentre;
                _lane = LaneDirection;
                _across = Vector3.Cross(Vector3.up, _lane);
                _half = halfLength;
                Found.Clear();
                TrafficTools.ClosestWaypoint(laneCentre, halfLength + 6f, Collect);
                for (int i = Found.Count - 1; i >= 0; i--)
                {
                    Waypoint point = Found[i];
                    Vector3 offset = point.position - laneCentre;
                    float along = Vector3.Dot(offset, _lane);
                    float across = Vector3.Dot(offset, _across);
                    bool inLane = Mathf.Abs(across) < LaneHalf && Mathf.Abs(along) < halfLength && Vector3.Dot(Direction(traffic, point), _lane) > 0.5f;
                    if (!inLane)
                    {
                        Found.RemoveAt(i);
                    }
                    else if (Forked(point) || point.temporaryDisabled)
                    {
                        Found.Clear();   // a junction in the stretch, or somebody else has this lane shut already: not here
                        return false;
                    }
                    else if (TransitMap.Guards(point.listIndex))
                    {
                        Found.Clear();   // a bus line's way into or out of a stop: shut, the stop would be cut off; the works go elsewhere
                        return false;
                    }
                }
                if (Found.Count == 0)
                {
                    return false;
                }

                // The ways into the lane, where traffic last had a choice. Every one of them must be shuttable.
                Entries.Clear();
                EntryParents.Clear();
                Visited.Clear();
                for (int i = 0; i < Found.Count; i++)
                {
                    Visited.Add(Found[i].listIndex);
                }
                for (int i = 0; i < Found.Count; i++)
                {
                    FindEntries(traffic, Found[i]);
                }
                bool safe = Entries.Count > 0;
                for (int i = 0; i < Entries.Count && safe; i++)
                {
                    safe = !Entries[i].temporaryDisabled && HasOtherWay(traffic, EntryParents[i]);
                }
                if (safe)
                {
                    for (int i = 0; i < Entries.Count; i++)
                    {
                        Shut(Entries[i]);
                    }
                    for (int i = 0; i < Found.Count; i++)
                    {
                        Shut(Found[i]);
                    }
                }
                Entries.Clear();
                EntryParents.Clear();
                Found.Clear();
                Visited.Clear();
                return safe && _closed.Count > 0;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                Open();
                return false;
            }
        }

        /// <summary>Opens every road point this closure closed, and no other. Safe at any time, any number of times.</summary>
        public void Open()
        {
            for (int i = 0; i < _closed.Count; i++)
            {
                if (_closed[i] != null)
                {
                    _closed[i].temporaryDisabled = false;
                }
            }
            _closed.Clear();
        }

        /// <summary>Takes traffic cars standing in or just short of the closed stretch off the road, out of view. Every twenty seconds while closed.</summary>
        public void Sweep()
        {
            if (!Closed || !TrafficTools.Ready)
            {
                return;
            }
            try
            {
                List<VehicleComponent> cars = Manager.GetVehicleList();
                TrafficManager traffic = TrafficManager.Instance;
                for (int i = 0; i < cars.Count; i++)
                {
                    VehicleComponent car = cars[i];
                    if (car == null || !car.gameObject.activeInHierarchy || car.presetPath != null || car.GetComponent<TaxiController>() != null
                        || traffic.IsVehicleIgnored(car.GetIndex()) || car.GetCurrentSpeed() > 1f)
                    {
                        continue;
                    }
                    Vector3 offset = car.transform.position - _centre;
                    float along = Vector3.Dot(offset, _lane);
                    float across = Vector3.Dot(offset, _across);
                    if (along > -_half - 25f && along < _half && Mathf.Abs(across) < LaneHalf + 0.4f && !CitySpots.InView(car.transform.position))
                    {
                        TrafficTools.Remove(car, "roadworks: standing in the closed lane, out of view");
                    }
                }
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
            }
        }

        private void Shut(Waypoint point)
        {
            if (!point.temporaryDisabled)
            {
                point.temporaryDisabled = true;
                _closed.Add(point);
            }
        }

        private static bool Keep(Waypoint point)
        {
            Found.Add(point);
            return false;
        }

        private static bool Forked(Waypoint point)
        {
            return point.neighbors == null || point.prev == null || point.neighbors.Count > 1 || point.prev.Count > 1 || point.IsInIntersection();
        }

        private static Vector3 Direction(TrafficManager traffic, Waypoint point)
        {
            Vector3 direction = Vector3.zero;
            if (point.neighbors != null && point.neighbors.Count > 0)
            {
                Waypoint next = traffic.GetWaypoint(point.neighbors[0]);
                direction = next != null ? next.position - point.position : Vector3.zero;
            }
            else if (point.prev != null && point.prev.Count > 0)
            {
                Waypoint before = traffic.GetWaypoint(point.prev[0]);
                direction = before != null ? point.position - before.position : Vector3.zero;
            }
            direction.y = 0f;
            return direction.sqrMagnitude < 0.01f ? Vector3.zero : direction.normalized;
        }

        // Walks back along the lane from one of its points to where traffic turned into it.
        private static void FindEntries(TrafficManager traffic, Waypoint from)
        {
            Walk.Clear();
            Walk.Push(from);
            float budget = EntryWalk;
            while (Walk.Count > 0 && budget > 0f)
            {
                Waypoint point = Walk.Pop();
                if (point.prev == null)
                {
                    continue;
                }
                for (int i = 0; i < point.prev.Count; i++)
                {
                    Waypoint before = traffic.GetWaypoint(point.prev[i]);
                    if (before == null || !Visited.Add(before.listIndex))
                    {
                        continue;
                    }
                    if (before.neighbors != null && before.neighbors.Count > 1)
                    {
                        Entries.Add(point);
                        EntryParents.Add(before);
                        continue;
                    }
                    budget -= Vector3.Distance(point.position, before.position);
                    Walk.Push(before);
                }
            }
            Walk.Clear();
        }

        private static bool HasOtherWay(TrafficManager traffic, Waypoint parent)
        {
            for (int i = 0; i < parent.neighbors.Count; i++)
            {
                Waypoint way = traffic.GetWaypoint(parent.neighbors[i]);
                if (way != null && !way.temporaryDisabled && !Entries.Contains(way))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
