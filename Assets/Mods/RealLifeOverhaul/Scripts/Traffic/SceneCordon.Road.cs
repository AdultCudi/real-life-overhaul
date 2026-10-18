using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using GleyTrafficSystem;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Scene control: the road measured and closed through the traffic system's road points, and where a unit stands.</summary>
    public sealed partial class SceneCordon
    {
        private const float EntryWalk = 320f;     // metres back along a lane the junction behind is looked for
        private const float HoldBack = 24f;       // metres before the scene that traffic let past it is held: behind the ambulance
        private static readonly List<Waypoint> Found = new List<Waypoint>(96);
        private static readonly HashSet<int> Lane = new HashSet<int>();
        private static readonly HashSet<int> Visited = new HashSet<int>();
        private static readonly Stack<Waypoint> Walk = new Stack<Waypoint>();
        private static readonly List<Waypoint> Entries = new List<Waypoint>(8);
        private static readonly List<Waypoint> EntryParents = new List<Waypoint>(8);
        private static readonly Predicate<Waypoint> Collect = Keep;

        /// <summary>
        /// Where a unit should stand: in a blocked lane, short of the wrecks in that lane's direction, so it drives up the
        /// scene's own carriageway and stops in sight of it. False if the scene has no such lane.
        /// </summary>
        /// <param name="farEnd">The far end, reached by the lane running the other way (see HasFarEnd).</param>
        /// <param name="back">Metres from the nearest wreck to the middle of the vehicle.</param>
        /// <param name="anchor">The road point at or just before the place: a route there ends at it.</param>
        public bool Stand(bool farEnd, float back, out Waypoint anchor, out Vector3 at)
        {
            anchor = null;
            at = default;
            if (!Surveyed)
            {
                return false;
            }
            float middle = (_acrossMin + _acrossMax) * 0.5f;
            float best = float.MaxValue;
            float bestGap = 0f;
            for (int i = 0; i < _lanePoints.Count; i++)
            {
                if (_laneWith[i] == farEnd)
                {
                    continue;
                }
                Waypoint point = _lanePoints[i];
                Locate(point, out float along, out float across, out _);
                float gap = farEnd ? along - _alongMax : _alongMin - along;   // metres short of the wrecks
                if (gap < Margin + 2f)
                {
                    continue;
                }
                // The nearest point at or before the place; failing that, the one that falls least short of it.
                // Never in a junction or on its crossing if a point of plain lane will do.
                float cost = (gap >= back ? gap - back : 100f + back - gap) + Mathf.Abs(across - middle) * 2f + (IsForked(point) ? 60f : 0f);
                if (cost < best)
                {
                    best = cost;
                    bestGap = gap;
                    anchor = point;
                }
            }
            if (anchor == null)
            {
                return false;
            }
            // On from the anchor to the exact place, unless that would take it into a junction: then the anchor is the place.
            Waypoint next = anchor.neighbors != null && anchor.neighbors.Count == 1 ? TrafficManager.Instance.GetWaypoint(anchor.neighbors[0]) : null;
            bool plainAhead = next != null && !IsForked(next) && !IsForked(anchor);
            at = plainAhead ? anchor.position + LaneDirection(anchor) * Mathf.Max(0f, bestGap - back) : anchor.position;
            return true;
        }

        private static bool IsForked(Waypoint point)
        {
            return point.neighbors.Count > 1 || point.prev.Count > 1 || point.IsInIntersection();
        }

        // The road's direction at the scene, and how far the wrecks reach along and across it.
        private void Measure(Vector3 scene, Vector3 heading, List<Footprint> footprints)
        {
            _scene = scene;
            Waypoint nearest = TrafficTools.ClosestWaypoint(scene, 40f);
            Vector3 lane = nearest != null ? LaneDirection(nearest) : Vector3.zero;
            if (lane == Vector3.zero)
            {
                lane = heading;
                lane.y = 0f;
            }
            _forward = lane.sqrMagnitude < 0.01f ? Vector3.forward : lane.normalized;
            _right = Vector3.Cross(Vector3.up, _forward);

            _alongMin = _acrossMin = _solidMin = float.MaxValue;
            _alongMax = _acrossMax = _solidMax = float.MinValue;
            _anySolid = false;
            for (int i = 0; i < footprints.Count; i++)
            {
                Footprint print = footprints[i];
                Vector3 forward = print.Forward;
                forward.y = 0f;
                forward = forward.sqrMagnitude < 0.01f ? _forward : forward.normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 point = print.Centre - scene
                                    + forward * (corner < 2 ? print.HalfLength : -print.HalfLength)
                                    + right * (corner % 2 == 0 ? print.HalfWidth : -print.HalfWidth);
                    float along = Vector3.Dot(point, _forward);
                    float across = Vector3.Dot(point, _right);
                    _alongMin = Mathf.Min(_alongMin, along);
                    _alongMax = Mathf.Max(_alongMax, along);
                    _acrossMin = Mathf.Min(_acrossMin, across);
                    _acrossMax = Mathf.Max(_acrossMax, across);
                    if (print.Vehicle)
                    {
                        _anySolid = true;
                        _solidMin = Mathf.Min(_solidMin, across);
                        _solidMax = Mathf.Max(_solidMax, across);
                    }
                }
            }
            _bandMin = _acrossMin - Side;
            _bandMax = _acrossMax + Side;
        }

        private void CloseLanes(bool shut)
        {
            TrafficManager traffic = TrafficManager.Instance;
            float reach = Mathf.Max(-_alongMin, _alongMax) + Apron + Mathf.Max(-_acrossMin, _acrossMax) + RoadWidth;
            Found.Clear();
            TrafficTools.ClosestWaypoint(_scene, reach, Collect);

            // First what the wrecks block: each lane from the start of its apron to just past them.
            _bothWays = false;
            _gateSide = 0f;
            _gateAgainst = false;
            bool junction = false;
            bool crossed = false;
            float near = Margin + 1.5f;
            Lane.Clear();
            for (int i = Found.Count - 1; i >= 0; i--)
            {
                Waypoint point = Found[i];
                Locate(point, out float along, out float across, out bool withTraffic);
                bool alongside = along > _alongMin - near && along < _alongMax + near;
                bool forked = point.neighbors.Count > 1 || point.prev.Count > 1 || point.IsInIntersection();
                // A junction within the apron's length behind the scene: the ambulance may have to stand beyond it.
                crossed |= forked && !alongside && across > _acrossMin - RoadWidth && across < _acrossMax + RoadWidth
                           && along > _alongMin - Apron && along < _alongMin;
                // The apron is only ever plain lane: it ends where a junction begins.
                bool onApron = !alongside && !forked
                               && (withTraffic ? along <= _alongMin - near && along > _alongMin - Apron
                                   : along >= _alongMax + near && along < _alongMax + Apron);
                bool inLane = across > _acrossMin - LaneClear && across < _acrossMax + LaneClear;
                bool blocked = inLane && (alongside || onApron);
                bool beside = alongside && !inLane && across > _acrossMin - RoadWidth && across < _acrossMax + RoadWidth;
                if (!blocked && !beside)
                {
                    Found.RemoveAt(i);
                    continue;
                }
                junction |= alongside && forked;
                if (blocked)
                {
                    if (shut)
                    {
                        Shut(point);
                    }
                    _lanePoints.Add(point);
                    _laneWith.Add(withTraffic);
                    _bandMin = Mathf.Min(_bandMin, across - 1.6f);
                    _bandMax = Mathf.Max(_bandMax, across + 1.6f);
                    _bothWays |= !withTraffic;
                    Found.RemoveAt(i);
                }
                else
                {
                    Lane.Add(point.listIndex);   // a lane beside the wrecks that is clear
                }
            }

            if (!shut)
            {
                Found.Clear();
                return;
            }

            // A junction at or just behind the scene: every way across it near the wrecks, or between them and the ambulance,
            // is shut too, so nothing drives through the crew; traffic takes the junction's other ways, or waits.
            _crossClosed = 0;
            if (junction || crossed)
            {
                CloseCrossings(traffic, junction);
            }

            // A clear lane gets a gate: the first of its points alongside the scene. At a junction there is no
            // such lane to work: the blocked ways are shut and traffic takes the others.
            for (int i = 0; i < Found.Count && !junction; i++)
            {
                Waypoint point = Found[i];
                if (!HasPrevious(point, Lane))
                {
                    // Held well before the scene, behind the ambulance, so what waits does not stand by its rear doors.
                    Waypoint hold = HoldLine(traffic, point);
                    _gates.Add(hold);
                    _touched.Add(hold);
                    Locate(point, out _, out float across, out bool withTraffic);
                    _openAcross.Add(across);
                    _gateAgainst |= !withTraffic;
                    _gateSide = across > (_acrossMin + _acrossMax) * 0.5f ? 1f : -1f;
                }
            }

            // And the way into this stretch is shut where traffic last had a choice.
            Entries.Clear();
            EntryParents.Clear();
            Visited.Clear();
            int closedHere = _closed.Count;
            for (int i = 0; i < closedHere; i++)
            {
                FindEntries(traffic, _closed[i]);
            }
            for (int i = 0; i < _gates.Count; i++)
            {
                FindEntries(traffic, _gates[i]);
            }
            for (int i = 0; i < Entries.Count; i++)
            {
                // Never the only way on from where traffic stands: it would wait there for ever.
                if (HasOtherWay(traffic, EntryParents[i]))
                {
                    Shut(Entries[i]);
                }
            }
            Entries.Clear();
            EntryParents.Clear();
            Found.Clear();
        }

        // Shuts every open road point in the crew's way: within working room of the wrecks across the road, and along it from
        // the ambulance's rear doors to just past the wrecks. Plain lane beside the scene is left to the gate unless the scene
        // is in the junction itself.
        private void CloseCrossings(TrafficManager traffic, bool inJunction)
        {
            float back = Margin + 1.5f;
            if (Stand(false, 9f, out _, out Vector3 stand))
            {
                // To a little behind the ambulance: its middle, half its length, and room at its rear doors.
                back = Mathf.Max(back, _alongMin - Vector3.Dot(stand - _scene, _forward) + 6.5f);
            }
            float reach = Mathf.Max(-_alongMin + back, _alongMax) + Mathf.Max(-_acrossMin, _acrossMax) + RoadWidth;
            Crossing.Clear();
            TrafficTools.ClosestWaypoint(_scene, reach, CollectCrossing);
            for (int i = 0; i < Crossing.Count; i++)
            {
                Waypoint point = Crossing[i];
                if (point.temporaryDisabled)
                {
                    continue;
                }
                Locate(point, out float along, out float across, out _);
                bool forked = IsForked(point);
                float room = forked ? WorkRoom + 1.2f : inJunction ? WorkRoom : LaneClear;
                if (along > _alongMin - back && along < _alongMax + Margin + 1.5f && across > _acrossMin - room && across < _acrossMax + room)
                {
                    Shut(point);
                    _crossClosed++;
                    _bandMin = Mathf.Min(_bandMin, across - 1.6f);
                    _bandMax = Mathf.Max(_bandMax, across + 1.6f);
                }
            }
            Crossing.Clear();
        }

        private static readonly List<Waypoint> Crossing = new List<Waypoint>(96);
        private static readonly Predicate<Waypoint> CollectCrossing = KeepCrossing;

        private static bool KeepCrossing(Waypoint point)
        {
            Crossing.Add(point);
            return false;
        }

        // Walks back along a lane to the point where traffic turned into it.
        private static void FindEntries(TrafficManager traffic, Waypoint from)
        {
            Walk.Clear();
            Walk.Push(from);
            float budget = EntryWalk;
            while (Walk.Count > 0 && budget > 0f)
            {
                Waypoint point = Walk.Pop();
                for (int i = 0; i < point.prev.Count; i++)
                {
                    Waypoint before = traffic.GetWaypoint(point.prev[i]);
                    if (before == null || !Visited.Add(before.listIndex))
                    {
                        continue;
                    }
                    if (before.neighbors.Count > 1)
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

        // The point of an open lane where its traffic is held: back along the lane from where it comes alongside the
        // scene, as far as the hold line, stopping short of any junction (what waits must not stand in one).
        private static Waypoint HoldLine(TrafficManager traffic, Waypoint from)
        {
            Waypoint hold = from;
            float walked = 0f;
            while (walked < HoldBack && hold.prev != null && hold.prev.Count == 1 && !IsForked(hold))
            {
                Waypoint before = traffic.GetWaypoint(hold.prev[0]);
                if (before == null || before.temporaryDisabled || IsForked(before))
                {
                    break;
                }
                walked += Vector3.Distance(hold.position, before.position);
                hold = before;
            }
            return hold;
        }

        private static bool HasPrevious(Waypoint point, HashSet<int> among)
        {
            for (int i = 0; i < point.prev.Count; i++)
            {
                if (among.Contains(point.prev[i]))
                {
                    return true;
                }
            }
            return false;
        }

        private void Locate(Waypoint point, out float along, out float across, out bool withTraffic)
        {
            Vector3 offset = point.position - _scene;
            along = Vector3.Dot(offset, _forward);
            across = Vector3.Dot(offset, _right);
            withTraffic = Vector3.Dot(LaneDirection(point), _forward) >= 0f;
        }

        // Which way a lane runs at one of its points; zero if it cannot be told.
        private static Vector3 LaneDirection(Waypoint point)
        {
            TrafficManager traffic = TrafficManager.Instance;
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

        // A road point is only ever closed by us if it was open, so that Open gives back exactly what was taken.
        private void Shut(Waypoint point)
        {
            if (!point.temporaryDisabled)
            {
                point.temporaryDisabled = true;
                _closed.Add(point);
                _touched.Add(point);
            }
        }

        private void SetGates(bool open)
        {
            _gateOpen = open;
            for (int i = 0; i < _gates.Count; i++)
            {
                _gates[i].temporaryDisabled = !open;
            }
        }

        // Kept as a field so that asking the traffic system for the points near the scene allocates nothing.
        private static bool Keep(Waypoint point)
        {
            Found.Add(point);
            return false;
        }

        // Traffic that has stopped in the blocked lanes, on the apron or among the wrecks: taken off the
        // road if nobody is looking (the game does the same to any car that stands still for long).
        private void ClearStranded()
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
                Vector3 offset = car.transform.position - _scene;
                float along = Vector3.Dot(offset, _forward);
                float across = Vector3.Dot(offset, _right);
                if (along > _alongMin - Apron && along < _alongMax + (_bothWays ? Apron : Margin) && across > _bandMin && across < _bandMax
                    && !StreetSpots.IsInView(car.transform.position))
                {
                    TrafficTools.Remove(car, "the cordon: standing in a closed lane, out of view");
                }
            }
        }
    }
}
