using System.Collections.Generic;
using BigAmbitions.Tags;
using Helpers;
using UI.Notification;
using UI.Smartphone;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A scooter rented from the game's stands lasts the game day it was rented on. The game takes a scooter nobody is on away once
    /// it has not been seen for its type's number of minutes (AutoDestroyVehicle, every frame, never asking where the player is), and
    /// a building is out of its sight, so a long class or a film came out to no scooter. Until midnight, while the player is inside,
    /// each such scooter on the street is marked as seen now, once a second, so the game's clock starts again the moment they step
    /// out. After the midnight that ends its day it is no longer held: the first second it is out of sight with nobody on it, asleep
    /// or awake, it is taken back the way the game takes one (the object destroyed, the instance removed from the save's list) and the
    /// player is told once. One being ridden stays until it is left out of sight. The day each was rented is kept in the save
    /// (<see cref="ScooterRentals"/>). Nothing per frame: the director's second, a few comparisons for each vehicle of the player's,
    /// no allocation but on a return. Only the save's own list is read and only the player's own vehicle object is destroyed, so in a
    /// Going Public session another player's vehicle is never touched.
    /// </summary>
    public static class ScooterHold
    {
        /// <summary>Holds the player's rented scooters of today while they are indoors and takes back those of earlier days; returns how many it held.</summary>
        public static int Tick(GameInstance save)
        {
            if (!Settings.GarageScooterHold || save == null || save.VehicleInstances == null)
            {
                return 0;
            }
            bool indoors = BuildingManager.IsInsideBuilding;
            bool gameWaits = FullMenu.IsOpen || CityMap.IsOpen || GameManager.isCitySceneBeingUnloaded;   // the game's own AutoDestroyVehicle waits then, and so does this
            int today = TimeHelper.CurrentDay;
            int held = 0;
            int taken = 0;
            for (int i = 0; i < save.VehicleInstances.Count; i++)
            {
                VehicleInstance vehicle = save.VehicleInstances[i];
                VehicleType type = vehicle != null ? vehicle.VehicleType : null;
                if (type == null || !type.HasTag(TagRef.Vehicletag.isscooter) || type.autoDestroyAfterMinutes < 0 || GarageState.Bike(vehicle.id) != null)
                {
                    continue;
                }
                bool rentedToday = ScooterHoldRules.RentedToday(ScooterRentals.DayOf(vehicle.id, today), today);
                bool counting = vehicle.lastSeen != null && vehicle.lastSeen.GetTotalMinutes() != 0f;
                bool onStreet = string.IsNullOrEmpty(vehicle.streetName);
                bool ridden = vehicle.id == save.ActiveVehicleId;
                if (ScooterHoldRules.Holds(true, indoors, true, type.autoDestroyAfterMinutes, counting, onStreet, ridden, false, rentedToday))
                {
                    vehicle.lastSeen.SetCurrentTime();
                    held++;
                }
                else if (!gameWaits && ScooterHoldRules.Returns(true, true, type.autoDestroyAfterMinutes, counting, onStreet, ridden, false, rentedToday)
                         && TakeBack(save, vehicle))
                {
                    i--;   // the list is one shorter
                    taken++;
                }
            }
            if (ScooterHoldRules.Notices(taken) > 0)
            {
                Notifications.Show(NotificationType.Warning, "reallife_garage_scooter_returned", null, 9f);
                GarageDirector.Logger?.Info("Garage: " + taken + " rented scooter(s) taken back, the day's rental ended at midnight.");
            }
            return held;
        }

        /// <summary>
        /// Takes the scooter back as the game's AutoDestroyVehicle does: its object destroyed and its instance removed from the save's
        /// list, unless the game would keep it (paid cargo). Only a vehicle of the player's own is taken, found among the player's vehicles
        /// by id; one with no object in the scene is left until it has one.
        /// </summary>
        private static bool TakeBack(GameInstance save, VehicleInstance vehicle)
        {
            if (!AutoDestroyVehicle.ShouldDestroyVehicle(vehicle, -1f))
            {
                return false;
            }
            List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < all.Count; i++)
            {
                VehicleController controller = all[i];
                if (controller != null && controller.vehicleInstance == vehicle && GoingPublic.MineVehicle(controller))
                {
                    save.VehicleInstances.Remove(vehicle);
                    ScooterRentals.Forget(vehicle.id);
                    UnityEngine.Object.Destroy(controller.gameObject);
                    return true;
                }
            }
            return false;
        }
    }
}
