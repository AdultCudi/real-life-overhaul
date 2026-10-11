using System;
using System.Collections.Generic;
using GleyTrafficSystem;
using Helpers;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Finding cars for scenes: one of the game's parked cars to stand beside (looked at, never
    /// touched), or a traffic car to take over. Emergency and working vehicles are never picked.
    /// Everything here is asked once when a scene starts.
    /// </summary>
    internal static class CrimeCars
    {
        private static readonly Collider[] Hits = new Collider[24];

        /// <summary>A parked car near a point: its middle, heading and half its width. False if there is none.</summary>
        public static bool FindParked(Vector3 near, float radius, out Vector3 centre, out Vector3 forward, out float halfWidth)
        {
            centre = default;
            forward = Vector3.forward;
            halfWidth = 0.95f;
            int count = Physics.OverlapSphereNonAlloc(near, radius, Hits, 1 << LayerHelper.ParkedVehiclesLayerIndex, QueryTriggerInteraction.Ignore);
            if (count == 0)
            {
                return false;
            }
            Collider picked = Hits[Random.Range(0, count)];
            Array.Clear(Hits, 0, count);
            if (picked == null)
            {
                return false;
            }
            // The game puts the collider on the parked car's own root object.
            Transform body = picked.transform;
            centre = picked.bounds.center;
            forward = body.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.01f ? Vector3.forward : forward.normalized;
            // The narrower of the two ground extents is near enough the width for standing beside it.
            Vector3 size = picked.bounds.size;
            halfWidth = Mathf.Clamp(Mathf.Min(size.x, size.z) * 0.5f, 0.8f, 1.3f);
            return true;
        }

        /// <summary>
        /// An ordinary private car in traffic between two distances from the player, moving
        /// (or, with <paramref name="stopped"/>, standing at a light), or null. Starts at a
        /// random car so the same one is not always picked.
        /// </summary>
        public static VehicleComponent FindTrafficCar(Vector3 player, float minDistance, float maxDistance, bool stopped)
        {
            if (!TrafficTools.Ready)
            {
                return null;
            }
            List<VehicleComponent> pool = Manager.GetVehicleList();
            int start = Random.Range(0, pool.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                VehicleComponent car = pool[(start + i) % pool.Count];
                if (!IsOrdinary(car))
                {
                    continue;
                }
                float speed = car.GetCurrentSpeed();   // km/h
                if (stopped ? speed > 2f || car.GetCurrentAction() != SpecialDriveActionTypes.StopInPoint : speed < 12f)
                {
                    continue;
                }
                float sqr = (car.transform.position - player).sqrMagnitude;
                if (sqr > minDistance * minDistance && sqr < maxDistance * maxDistance)
                {
                    return car;
                }
            }
            return null;
        }

        /// <summary>
        /// On the road as plain traffic, a car and not a truck, nobody's taxi, held by nobody of the mod's, and not a
        /// vehicle with a job to do (the names are StreetRules.IsOrdinaryCarName's). The reckless drivers pick by it too.
        /// </summary>
        internal static bool IsOrdinary(VehicleComponent car)
        {
            if (car == null || !car.gameObject.activeInHierarchy || car.presetPath != null || car.prefab == null
                || car.vehicleType != VehicleTypes.Car || TrafficManager.Instance.IsVehicleIgnored(car.GetIndex())
                || car.GetComponent<TaxiController>() != null)
            {
                return false;
            }
            return StreetRules.IsOrdinaryCarName(car.prefab.name);
        }

        /// <summary>The road ahead of a traffic car, for it to be driven along. False if it has too little road in front of it.</summary>
        public static bool RoadAhead(VehicleComponent car, float length, float minimum, List<Waypoint> scratch, List<Vector3> path)
        {
            path.Clear();
            Waypoint anchor = TrafficManager.Instance.GetTargetWaypoint(car.GetIndex());
            if (anchor == null || TrafficTools.WalkForward(anchor, length, 500, scratch) < minimum)
            {
                scratch.Clear();
                return false;
            }
            for (int i = 0; i < scratch.Count; i++)
            {
                path.Add(scratch[i].position);
            }
            scratch.Clear();
            return true;
        }
    }
}
