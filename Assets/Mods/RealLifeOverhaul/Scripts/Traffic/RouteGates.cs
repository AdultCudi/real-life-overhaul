using System.Collections.Generic;
using GleyTrafficSystem;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What a vehicle the mod drives is standing for on its way.</summary>
    public enum RoadHold { None, Light, Traffic, Closure }

    /// <summary>
    /// What the mod's driver (<see cref="EmergencyDriver"/>) does not know of, for a vehicle that is to drive like
    /// anybody else: red lights, and lane points a scene has closed. Whoever holds such a vehicle asks here four
    /// times a second where on its route it must pull up (<see cref="Find"/>) and, once it stands there, whether
    /// it may go on (<see cref="Open"/>). The rules are the bus's (Transport/TransitBus.Drive and GateOpen, which
    /// keep their own copy: they were proven in the game as they stand and are not touched): a light that turns
    /// as the vehicle reaches the line is driven through; a light further from the player than the game runs
    /// lights is not waited at; one nobody is looking at that has not changed in a long while is taken to be one
    /// the game is not running.
    ///
    /// A route is two lists side by side: the places, and the lane point each is (-1 for a place of the
    /// vehicle's own). A walk along at most <see cref="LookAhead"/> metres of it; no physics, no allocation.
    /// </summary>
    public static class RouteGates
    {
        public const float LookAhead = 45f;        // metres of route looked along
        public const float LitWithin = 170f;       // metres from the player within which a red light is waited at
        public const float LightPatience = 45f;    // seconds at a red light nobody is looking at before it is taken to be one the game is not running
        private const float JunctionWithin = 16f;  // metres ahead within which a junction's lane point slows the vehicle
        private const float LineClose = 7f;        // a light that turns red this near the line, at speed, is driven through
        private const float LineSpeed = 4f;

        /// <summary>
        /// The place of its route a vehicle must pull up at, or -1. <paramref name="junction"/> says a junction is
        /// close ahead (it goes slower through one).
        /// </summary>
        /// <param name="next">The first place of the route still ahead of it (the driver's NextPoint).</param>
        public static int Find(List<Vector3> route, List<int> points, int next, Vector3 position, float speed, Waypoint[] lanePoints, out RoadHold why, out bool junction)
        {
            why = RoadHold.None;
            junction = false;
            float ahead = 0f;
            for (int i = next; i < route.Count; i++)
            {
                Vector3 leg = route[i] - (i == next ? position : route[i - 1]);
                leg.y = 0f;
                ahead += leg.magnitude;
                if (ahead > LookAhead)
                {
                    break;
                }
                Waypoint point = At(lanePoints, points[i]);
                if (point == null)
                {
                    continue;
                }
                if (point.temporaryDisabled)
                {
                    why = RoadHold.Closure;
                    return Mathf.Max(next, i - 2);   // a couple of places before the closed road
                }
                if (point.stop && Lit(point) && (ahead > LineClose || speed < LineSpeed))
                {
                    why = RoadHold.Light;
                    return i;
                }
                junction |= ahead < JunctionWithin && !TrafficManager.IsNotIntersection(point);
            }
            return -1;
        }

        /// <summary>True when a vehicle standing at <paramref name="gate"/> may go on. False with why it may not.</summary>
        /// <param name="waitedFor">Seconds it has stood there.</param>
        public static bool Open(List<Vector3> route, List<int> points, int gate, Waypoint[] lanePoints, float waitedFor, out RoadHold why)
        {
            why = RoadHold.None;
            int from = Mathf.Clamp(gate, 0, route.Count - 1);
            float ahead = 0f;
            for (int i = from; i < route.Count; i++)
            {
                if (i > from)
                {
                    ahead += Vector3.Distance(route[i], route[i - 1]);
                }
                if (ahead > LookAhead)
                {
                    break;
                }
                Waypoint point = At(lanePoints, points[i]);
                if (point == null)
                {
                    continue;
                }
                if (point.temporaryDisabled)
                {
                    why = RoadHold.Closure;
                    return false;
                }
                if (i == from && point.stop && Lit(point) && (waitedFor < LightPatience || CitySpots.InView(point.position)))
                {
                    why = RoadHold.Light;
                    return false;
                }
            }
            return true;
        }

        // The game only runs the lights of junctions near the player; further off a light stays as it was for good.
        private static bool Lit(Waypoint point)
        {
            return CitySpots.Flat(point.position - PlayerHelper.GetPosition()) < LitWithin * LitWithin;
        }

        private static Waypoint At(Waypoint[] lanePoints, int index)
        {
            return lanePoints != null && index >= 0 && index < lanePoints.Length ? lanePoints[index] : null;
        }
    }
}
