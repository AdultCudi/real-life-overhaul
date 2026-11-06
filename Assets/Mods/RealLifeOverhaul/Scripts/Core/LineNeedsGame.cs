using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Items;
using Buildings;
using Helpers;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's side of <see cref="LineNeeds"/>: whether what a line takes for granted is so, read from the game's own data.
    /// The homes are the player's residential registrations (another Going Public player's are never theirs); a home's
    /// things from its own furniture as the save keeps it, so a home the player is not in is read as well as the one they
    /// stand in, which alone counts while they stand in it. Vehicles are the save's own (another player's car is not in
    /// it). A fault is logged once and reads as not so. COST: nothing per frame; when a listed line is drawn, a walk over
    /// the player's registrations and one home's furniture, or over their vehicles.
    /// </summary>
    internal static class LineNeedsGame
    {
        private const string Residential = "ba:buildingtype_residential";
        private const string EmptyBusinessType = "ba:businesstype_empty";

        public static IModLogger Logger;

        private static bool _told;   // the one fault line a session

        /// <summary>True if all of these hold now (LineNeeds.Read).</summary>
        public static bool Holds(LineNeed needs)
        {
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (save == null)
                {
                    return false;
                }
                if ((needs & LineNeed.Car) != 0 && !HasCar(save))
                {
                    return false;
                }
                if ((needs & LineNeed.InCar) != 0 && !InCar())
                {
                    return false;
                }
                if ((needs & LineNeed.Business) != 0 && !OwnsBusiness(save))
                {
                    return false;
                }
                BuildingRegistration here = HomeHere();
                if ((needs & LineNeed.Away) != 0 && here != null)
                {
                    return false;
                }
                LineNeed home = needs & LineNeeds.AtHome;
                if (home == LineNeed.None)
                {
                    return true;
                }
                if (here != null)
                {
                    return HomeHas(here, home);
                }
                List<BuildingRegistration> all = save.BuildingRegistrations;
                for (int i = 0; all != null && i < all.Count; i++)
                {
                    if (IsHome(all[i]) && HomeHas(all[i], home))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception e)
            {
                if (!_told)
                {
                    _told = true;
                    Logger?.Warn("Line needs: what the player has could not be read (" + e.GetType().Name + ": " + e.Message
                                 + "); lines that need something are left out.");
                }
                return false;
            }
        }

        /// <summary>
        /// True if this home has all of a home's needs among these: a flat in a block, rented, and each piece of furniture.
        /// Household staff ask it of the home they work at.
        /// </summary>
        public static bool HomeHas(BuildingRegistration home, LineNeed needs)
        {
            if (!IsHome(home))
            {
                return false;
            }
            if ((needs & LineNeed.Flat) != 0)
            {
                Building building = GameRegistrations.BuildingOf(home);
                if (building == null || building.IsHamptonsHouse())
                {
                    return false;
                }
            }
            if ((needs & LineNeed.Rented) != 0 && home.BuildingOwnedByPlayer)
            {
                return false;
            }
            LineNeed wanted = needs & (LineNeed.Fridge | LineNeed.Sink | LineNeed.Bath | LineNeed.Bed | LineNeed.Computer);
            if (wanted == LineNeed.None)
            {
                return true;
            }
            LineNeed found = LineNeed.None;
            if (home.itemInstances != null)
            {
                foreach (KeyValuePair<string, ItemInstance> pair in home.itemInstances)
                {
                    found |= pair.Value != null ? LineNeeds.ItemIs(pair.Value.itemName) : LineNeed.None;
                    if ((found & wanted) == wanted)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>The test the game uses when it counts the player's businesses: a registration of theirs with a business type and a name.</summary>
        public static bool OwnsBusiness(GameInstance save)
        {
            if (save == null || save.BuildingRegistrations == null)
            {
                return false;
            }
            foreach (BuildingRegistration registration in save.BuildingRegistrations)
            {
                if (registration != null && GoingPublic.Mine(registration) && registration.businessTypeName != EmptyBusinessType
                    && !string.IsNullOrEmpty(registration.BusinessName))
                {
                    return true;
                }
            }
            return false;
        }

        // A home of the player's own: theirs, and of the game's residential type (a flat or a Hamptons house).
        private static bool IsHome(BuildingRegistration reg)
        {
            return reg != null && GoingPublic.Mine(reg) && GameRegistrations.BuildingType(reg) == Residential;
        }

        // The home of the player's they stand in, or null.
        private static BuildingRegistration HomeHere()
        {
            if (!BuildingManager.IsInsideBuilding)
            {
                return null;
            }
            var manager = InstanceBehavior<BuildingManager>.Instance;
            BuildingRegistration reg = manager != null ? manager.buildingRegistration : null;
            return IsHome(reg) ? reg : null;
        }

        // A closed motor vehicle of the save's: a car, van or lorry. The mod's bicycles and motorbikes are the game's scooter.
        private static bool HasCar(GameInstance save)
        {
            for (int i = 0; save.VehicleInstances != null && i < save.VehicleInstances.Count; i++)
            {
                if (IsCar(save.VehicleInstances[i]))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool InCar()
        {
            if (!PlayerHelper.IsUsingVehicle)
            {
                return false;
            }
            var gm = InstanceBehavior<GameManager>.Instance;
            VehicleController vehicle = gm != null ? gm.selectedVehicle : null;
            return vehicle != null && IsCar(vehicle.vehicleInstance);
        }

        private static bool IsCar(VehicleInstance vehicle)
        {
            return vehicle != null && vehicle.VehicleType != null && vehicle.VehicleType.enclosed && vehicle.VehicleType.IsMotorVehicle;
        }
    }
}
