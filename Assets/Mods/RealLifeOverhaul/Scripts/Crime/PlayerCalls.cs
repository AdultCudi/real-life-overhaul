using Helpers;
using UI.Guiders;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Whether something of the mod's or the game's is calling the player to where they stand, so that the police do not fine the
    /// step into the road it asks for (<see cref="PlayerOffences.OnFoot"/>). One rule for every scene and task: an accident, a
    /// person struck by a car, a crime or a street event (each director's Calls, with its taped road), a mark on one of the game's
    /// mission guiders (a job, a quest, a taxi or a private driver, a delivery, a tow) and one of the player's own vehicles at the
    /// kerb. A new kind of scene or task shows a mark or is a director above, and needs nothing here. COST: asked twice a second,
    /// only on foot with an officer in sight and the player walking: a few field reads and one pass over the player's vehicles.
    /// </summary>
    internal static class PlayerCalls
    {
        /// <summary>True if any scene, mark or own vehicle calls the player at this point.</summary>
        public static bool Within(Vector3 point)
        {
            return TrafficAccidents.Calls(point) || CrimeDirector.Calls(point) || Events.EventDirector.Calls(point)
                   || PedestrianIncidents.LyingNear(point, CallRules.SceneReach) || Marked(point) || OwnCar(point);
        }

        // The game's own guiders for what it asks of the player. The destination guider is left out: that is the player's own pin.
        private static bool Marked(Vector3 point)
        {
            GuidersManager guiders = InstanceBehavior<GuidersManager>.Instance;
            if (guiders == null)
            {
                return false;
            }
            return Marks(guiders.jobDestinationGuider, point) || Marks(guiders.mainQuestGuider, point)
                   || Marks(guiders.sideQuestGuider, point) || Marks(guiders.privateDriverGuider, point);
        }

        private static bool Marks(DirectionGuider guider, Vector3 point)
        {
            Transform target = guider != null ? guider.target : null;
            if (target == null)
            {
                return false;
            }
            Vector3 offset = point - target.position;
            return CallRules.Near(offset.x, offset.z, CallRules.MarkReach);
        }

        private static bool OwnCar(Vector3 point)
        {
            var cars = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < cars.Count; i++)
            {
                VehicleController car = cars[i];
                if (car == null)
                {
                    continue;
                }
                Vector3 offset = point - car.transform.position;
                if (CallRules.Near(offset.x, offset.z, CallRules.CarReach))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
