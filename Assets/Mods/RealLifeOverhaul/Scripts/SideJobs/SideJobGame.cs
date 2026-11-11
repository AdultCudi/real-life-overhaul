using System;
using System.Collections.Generic;
using Localizor;
using PlayerActivity;
using UI;
using UI.Notification;
using UnityEngine;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul.SideJobs
{
    /// <summary>
    /// The only file of the taxi and delivery shifts that reads the game: the day, where the player is, a vehicle's seats, room,
    /// condition and tank as plain figures, the fuel in a tank, and the game's notifications. Every call is behind a try at its
    /// caller (the director's one-second look and its presses). Nothing here runs a frame.
    /// </summary>
    internal static class SideJobGame
    {
        public static int Day => TimeHelper.CurrentDay;

        /// <summary>True while the player is on foot in the street, where a desk can be walked up to.</summary>
        public static bool OnFoot(GameManager game)
        {
            return game.selectedVehicle == null && !BuildingManager.IsInsideBuilding && TrafficTools.Street;
        }

        /// <summary>True while one of the game's screens has the keys: a menu, a conversation, the work panel.</summary>
        public static bool UiOpen()
        {
            if (ThirdPersonCamera.IsUiOpen() || PlayerActivityUI.IsPanelOpen)
            {
                return true;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null && uis.playerHUD != null && uis.playerHUD.dialogUI != null && uis.playerHUD.dialogUI.isPanelOpen;
        }

        /// <summary>
        /// True once the game's work panel has closed, its clock has stopped and the street is running again (the game shuts
        /// traffic down while its clock runs): only then can a shift start on the street.
        /// </summary>
        public static bool StreetReady()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null && !PlayerActivityUI.IsPanelOpen && (uis.timeMachine == null || !uis.timeMachine.isRunning) && TrafficTools.Street;
        }

        /// <summary>
        /// What a desk reads of a vehicle: whether it has an engine, the licences its driver holds for it, its seats (the mod's
        /// table: the game keeps none), the boxes the game says it holds, its condition from the game's damage and its tank.
        /// </summary>
        public static SideCar Read(CarController car, out string name, out string id)
        {
            var facts = new SideCar();
            name = null;
            id = null;
            if (car == null || car.vehicleInstance == null || car.vehicleType == null)
            {
                return facts;
            }
            VehicleType type = car.vehicleType;
            facts.Found = true;
            facts.Motor = type.IsMotorVehicle;
            facts.Licensed = !Licences.Driving.ClassOf(car, out LicenceClass needs) || Licences.Driving.Has(needs);
            facts.Endorsed = Licences.Driving.Has(LicenceClass.Taxi);
            var traits = new CarTraits(type.IsMotorVehicle, type.spawnInPlayerObject, type.maxSpeed, type.maxCargoCapacity, type.isLuxuryCar, 1400f);
            facts.Seats = CarCatalogue.Seats(type.vehicleTypeName, CarCatalogue.For(type.vehicleTypeName, traits).Kind);
            facts.Cargo = Math.Max(0, type.maxCargoCapacity);
            facts.Condition = SideJobRules.Condition(car.vehicleInstance.damage);
            facts.Fuel = SideJobRules.FuelShare(Fuel(car), type.maxFuel);
            name = string.IsNullOrEmpty(type.vehicleTypeName) ? car.name : type.vehicleTypeName.GetLocalization();
            id = car.vehicleInstance.id;
            return facts;
        }

        /// <summary>The litres in a vehicle's tank now: its fuel module's own figure, else the save's last.</summary>
        public static float Fuel(CarController car)
        {
            try
            {
                return Live(car);
            }
            catch (Exception)
            {
                return car != null && car.vehicleInstance != null ? car.vehicleInstance.fuel : 0f;   // a vehicle without the module
            }
        }

        // Alone in a method of its own: a vehicle without a fuel module throws here, and only here.
        private static float Live(CarController car)
        {
            return car.GetCurrentFuel();
        }

        /// <summary>One of the game's notifications with its names filled in: pairs of a name and what goes in its place.</summary>
        public static void Tell(NotificationType type, string key, float seconds, params string[] pairs)
        {
            Dictionary<string, string> data = null;
            if (pairs != null && pairs.Length >= 2)
            {
                data = new Dictionary<string, string>(pairs.Length / 2);   // a new one each time: the game keeps it while the toast is listed
                for (int i = 0; i + 1 < pairs.Length; i += 2)
                {
                    data[pairs[i]] = pairs[i + 1];
                }
            }
            Notifications.Show(type, key, data, seconds);
        }
    }
}
