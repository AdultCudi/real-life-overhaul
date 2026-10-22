using System;
using System.Collections.Generic;
using GleyTrafficSystem;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Events
{
    /// <summary>
    /// What fires and street events need from the road: the lane beside a place, the stretch to close, and vehicles that
    /// drive up, stand and leave, through Scripts/Traffic's public surface only. Roads close only through the director's
    /// own SceneCordon, which reopens exactly what it closed. Asked on events, never per frame.
    /// </summary>
    internal static class EventRoad
    {
        private static readonly List<Waypoint> Scratch = new List<Waypoint>(160);
        private static Vector3 _against;
        private static readonly Predicate<Waypoint> PlainLane = IsPlainLane;
        private static readonly Predicate<Waypoint> PlainLaneAgainst = IsPlainLaneAgainst;

        /// <summary>The lane nearest a place that is not in a junction, and the way it runs. False if no road is that near.</summary>
        public static bool Lane(Vector3 near, float within, out Waypoint point, out Vector3 direction)
        {
            direction = default;
            point = TrafficTools.Ready ? TrafficTools.ClosestWaypoint(near, within, PlainLane) : null;
            return point != null && Direction(point, out direction);
        }

        /// <summary>The middle of the lane running the other way beside a lane, if there is one within a carriageway's width.</summary>
        public static bool Oncoming(Waypoint lane, Vector3 direction, out Vector3 middle)
        {
            middle = default;
            _against = direction;
            Waypoint other = TrafficTools.ClosestWaypoint(lane.position, 9f, PlainLaneAgainst);
            if (other == null)
            {
                return false;
            }
            // The point of that lane abreast of ours, not merely its nearest road point.
            Vector3 offset = other.position - lane.position;
            offset.y = 0f;
            Vector3 across = offset - direction * Vector3.Dot(offset, direction);
            if (across.sqrMagnitude < 1.5f * 1.5f)
            {
                return false;
            }
            middle = lane.position + across;
            return true;
        }

        public static bool Direction(Waypoint point, out Vector3 direction)
        {
            direction = default;
            if (point == null || point.neighbors == null || point.neighbors.Count == 0)
            {
                return false;
            }
            Waypoint next = TrafficManager.Instance.GetWaypoint(point.neighbors[0]);
            if (next == null)
            {
                return false;
            }
            direction = next.position - point.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                return false;
            }
            direction.Normalize();
            return true;
        }

        private static bool IsPlainLane(Waypoint point)
        {
            return point.neighbors != null && point.neighbors.Count == 1 && point.prev != null && point.prev.Count == 1 && !point.temporaryDisabled
                   && TrafficManager.IsNotIntersection(point);
        }

        private static bool IsPlainLaneAgainst(Waypoint point)
        {
            return IsPlainLane(point) && Direction(point, out Vector3 direction) && Vector3.Dot(direction, _against) < -0.6f;
        }

        /// <summary>A stretch of carriageway as the cordon wants it described: something lying in the road that it goes round.</summary>
        public static Footprint Stretch(Vector3 centre, Vector3 direction, float halfLength, float halfWidth)
        {
            return new Footprint { Centre = centre, Forward = direction, HalfLength = halfLength, HalfWidth = halfWidth, Vehicle = false };
        }

        public static Footprint Of(VehicleComponent car)
        {
            TrafficTools.Footprint(car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            return new Footprint { Centre = centre, Forward = forward, HalfLength = halfLength, HalfWidth = halfWidth, Vehicle = true };
        }

        /// <summary>Sets a parked unit driving off along the road ahead; false with no road ahead, and the caller releases it.</summary>
        public static bool DriveOff(ResponseUnit unit, bool quiet)
        {
            VehicleComponent car = unit.Car;
            if (car != null)
            {
                SceneObstacles.Remove(car);   // whatever becomes of it, it stands here no longer
            }
            if (car == null || unit.IsLost)
            {
                return false;
            }
            Waypoint ahead = TrafficTools.ClosestWaypointAlong(car.transform.position, car.transform.forward, 30f);
            if (ahead == null || TrafficTools.WalkForward(ahead, 130f, 60, Scratch) <= 20f)
            {
                Scratch.Clear();
                return false;
            }
            unit.Path.Clear();
            for (int i = 0; i < Scratch.Count; i++)
            {
                unit.Path.Add(Scratch[i].position);
            }
            Scratch.Clear();
            unit.Unpark(quiet);
            unit.Driver.Begin(car, unit.Path, unit.Path[unit.Path.Count - 1], 6f, 90f);
            return true;
        }

        /// <summary>
        /// Walks the road back from an anchor and puts a pooled vehicle of a prefab at the far end, held by the unit with its
        /// route filled in; null, with the reason, if it cannot.
        /// </summary>
        public static VehicleComponent Bring(ResponseUnit unit, GameObject prefab, Waypoint anchor, float routeLength, float minimum, out string refusal)
        {
            refusal = null;
            if (unit.InUse || anchor == null || prefab == null || !TrafficTools.Ready)
            {
                refusal = unit.InUse ? "the unit is still out" : anchor == null ? "there is no road point to head for"
                          : prefab == null ? "the traffic pool has no truck or van free" : "traffic is not running";
                return null;
            }
            float walked = 0f;
            for (int attempt = 0; attempt < 4 && walked < minimum; attempt++)
            {
                walked = TrafficTools.WalkBack(anchor, routeLength, 160, Scratch);
            }
            if (walked < minimum || Scratch.Count < 3)
            {
                refusal = "the road leading there is only " + walked.ToString("F0") + " m long";
                Scratch.Clear();
                return null;
            }
            VehicleComponent car = null;
            int start = 0;
            for (int i = 0; i < 3 && i < Scratch.Count - 2 && car == null; i++)
            {
                start = i;
                car = TrafficTools.Load(prefab, Scratch[i]);   // an idle car of that prefab, never one taken by its model
            }
            if (car == null || !unit.Take(car))
            {
                if (car != null)
                {
                    TrafficTools.Remove(car, "a fire engine that could not be taken");
                }
                refusal = "the first road points of its route are all taken";
                Scratch.Clear();
                return null;
            }
            for (int i = start + 1; i < Scratch.Count; i++)
            {
                unit.Path.Add(Scratch[i].position);
            }
            Scratch.Clear();
            return car;
        }

        /// <summary>
        /// The pooled vehicle a fire engine is made of, idle ones only: with the modelled body, the truck it is made to (the
        /// same axles and length); otherwise a truck, the freight truck first, else a van, both repainted.
        /// </summary>
        public static GameObject PickTruck(out bool van)
        {
            van = false;
            List<VehicleComponent> pool = Manager.GetVehicleList();
            bool body = PropLibrary.TryGet(EngineLivery.Body, out _, out _);
            GameObject freight = null;
            GameObject truck = null;
            GameObject fallback = null;
            for (int i = 0; i < pool.Count; i++)
            {
                VehicleComponent car = pool[i];
                if (car == null || car.prefab == null || car.gameObject.activeInHierarchy || TrafficTools.IsAmbulance(car))
                {
                    continue;
                }
                string name = car.prefab.name;
                if (body && EngineLivery.Carries(car))
                {
                    return car.prefab;
                }
                if (name.IndexOf("Freight", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (!body)
                    {
                        return car.prefab;
                    }
                    freight = freight != null ? freight : car.prefab;
                }
                else if (truck == null && (car.vehicleType == VehicleTypes.Truck || name.IndexOf("Truck", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    truck = car.prefab;
                }
                else if (fallback == null && (name.IndexOf("Van", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Dash", StringComparison.OrdinalIgnoreCase) >= 0
                                              || name.IndexOf("Desert", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    fallback = car.prefab;
                }
            }
            truck = freight != null ? freight : truck;
            van = truck == null && fallback != null;
            return truck != null ? truck : fallback;
        }
    }
}
