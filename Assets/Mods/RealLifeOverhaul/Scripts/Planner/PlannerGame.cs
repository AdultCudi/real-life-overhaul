using System;
using System.Collections.Generic;
using Helpers;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Planner
{
    /// <summary>
    /// What the Planner reads of the game for the way about the city: where the player is, the door of an address, and
    /// whether the player has a car of their own. Only read, never changed. Asked when the app writes a page or the
    /// Planner looks with its texts (opening, a click, a reminder), never per frame: each is a lookup in one of the
    /// game's own tables or a walk over the player's few vehicles.
    /// </summary>
    public static class PlannerGame
    {
        /// <summary>Where the player is in the city: indoors, the door they went in by (the game's own answer, PlayerHelper.GetCityPosition).</summary>
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
                return false;   // (no player yet: a load under way)
            }
        }

        /// <summary>The straight line from the player to an entry's place, in metres; false if either is not known.</summary>
        public static bool MetresTo(PlannerEntry entry, out float metres)
        {
            metres = 0f;
            float x, z;
            if (entry == null || !entry.Placed || !Player(out x, out z))
            {
                return false;
            }
            metres = PlannerRules.Metres(x, z, entry.PlaceX, entry.PlaceZ);
            return true;
        }

        /// <summary>The door of a building by its address, as the game finds it (CityManager.FindCityBuildingController, a table lookup); false if the city has no such building.</summary>
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

        /// <summary>
        /// Whether the player goes by car: at the wheel now, or with a car of their own in the city (in a Going Public
        /// session only the player's own count, GoingPublic.MineVehicle). Otherwise the way is reckoned on foot.
        /// </summary>
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
                // (the game's vehicles are not there to ask: on foot)
            }
            return false;
        }
    }
}
