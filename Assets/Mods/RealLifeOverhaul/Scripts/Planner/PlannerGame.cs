using System;
using System.Collections.Generic;
using Helpers;
using PlayerActivity;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Planner
{
    /// <summary>The game's state the Planner reads for the way about the city: lookups asked with the texts, never per frame.</summary>
    public static class PlannerGame
    {
        /// <summary>Where the player is in the city; indoors, the door they went in by (PlayerHelper.GetCityPosition).</summary>
        public static bool Player(out float x, out float z)
        {
            x = z = 0f;
            try
            {
                if (SaveGameManager.Current == null)
                {
                    return false;
                }
                Vector3 at = PlayerHelper.GetCityPosition();
                x = at.x;
                z = at.z;
                return !float.IsNaN(x) && !float.IsNaN(z);
            }
            catch (Exception)
            {
                return false;   // no player yet: a load under way
            }
        }

        /// <summary>The straight line from the player to an entry's place, in metres.</summary>
        public static bool MetresTo(PlannerEntry entry, out float metres)
        {
            metres = 0f;
            if (entry == null || !entry.Placed || !Player(out float x, out float z))
            {
                return false;
            }
            metres = PlannerRules.Metres(x, z, entry.PlaceX, entry.PlaceZ);
            return true;
        }

        /// <summary>Whether the game's own sleep is the activity under way.</summary>
        public static bool Asleep()
        {
            try
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                IPlayerActivity activity = uis != null && uis.playerActivityUI != null ? uis.playerActivityUI.GetCurrentActivity : null;
                if (!(activity is SleepActivity))
                {
                    return false;
                }
                PlayerActivityState state = activity.GetState();
                return state == PlayerActivityState.Running || state == PlayerActivityState.Started;
            }
            catch (Exception)
            {
                return false;   // no panel to ask: awake
            }
        }

        /// <summary>The door of a building by its address (CityManager.FindCityBuildingController); false if there is none.</summary>
        public static bool Door(string street, int number, out float x, out float z)
        {
            x = z = 0f;
            if (string.IsNullOrEmpty(street))
            {
                return false;
            }
            try
            {
                CityManager city = InstanceBehavior<CityManager>.IsInitialized ? InstanceBehavior<CityManager>.Instance : null;
                CityBuildingController controller = city != null ? city.FindCityBuildingController(new Address(street, number)) : null;
                if (controller == null || controller.entranceDoors == null || controller.entranceDoors.Length == 0 || controller.entranceDoors[0] == null
                    || controller.entranceDoors[0].doorTransform == null)
                {
                    return false;
                }
                Vector3 door = controller.entranceDoors[0].doorTransform.position;
                x = door.x;
                z = door.z;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>At the wheel now, or with a car of their own in the city (in Going Public only the player's own).</summary>
        public static bool ByCar()
        {
            try
            {
                if (PlayerHelper.IsUsingVehicle)
                {
                    GameManager game = InstanceBehavior<GameManager>.Instance;
                    VehicleController driven = game != null ? game.selectedVehicle : null;
                    if (driven != null && driven.vehicleType != null && driven.vehicleType.IsMotorVehicle)
                    {
                        return true;
                    }
                }
                List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
                for (int i = 0; all != null && i < all.Count; i++)
                {
                    if (all[i] is CarController && all[i].vehicleInstance != null && GoingPublic.MineVehicle(all[i]))
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                // no vehicles to ask: on foot
            }
            return false;
        }
    }
}
