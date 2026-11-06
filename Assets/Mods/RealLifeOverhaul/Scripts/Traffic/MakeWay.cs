using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Ordinary traffic standing in the way of a unit with its lights on is moved on: the car is taken off the road, as an accident scene's
    /// units do (TrafficAccidents.ClearTheWay), sooner out of view than in view (MakeWayRules.Clear). Only a car of the traffic, standing,
    /// that the mod's scenes do not leave alone (TrafficAccidents.IsUsable); the mod's own vehicles, people and parked cars are not touched.
    /// Cost: a field read while nothing holds the unit up; held up, a component lookup and two comparisons a call.
    /// </summary>
    public static class MakeWay
    {
        /// <summary>
        /// Takes the standing traffic car that holds a unit up off the road once it has done so long enough. True if one was moved.
        /// </summary>
        /// <param name="blocker">What the unit stands behind (its driver's Blocker, or the body of what holds the tow truck).</param>
        /// <param name="heldFor">Seconds the unit has stood behind it.</param>
        /// <param name="unit">Who is held up, for the log.</param>
        /// <param name="moved">Cars moved on so far for this unit; counted up here.</param>
        /// <param name="most">The most cars to move on for one unit.</param>
        public static bool ClearStanding(Rigidbody blocker, float heldFor, string unit, ref int moved, int most)
        {
            if (blocker == null || moved >= most || heldFor < MakeWayRules.ClearUnseen || !TrafficTools.Ready)
            {
                return false;
            }
            VehicleComponent car = blocker.GetComponentInParent<VehicleComponent>();
            if (!TrafficAccidents.IsUsable(car) || car.GetCurrentSpeed() > 1f || !MakeWayRules.Clear(heldFor, StreetSpots.IsInView(car.transform.position)))
            {
                return false;
            }
            moved++;
            TrafficTools.Remove(car, "standing in the way of " + unit);
            return true;
        }
    }
}
