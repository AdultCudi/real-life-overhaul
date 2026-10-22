using System;
using Helpers;
using RealLifeOverhaul.Economy.Dealership;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Car dealerships of either kind, the mod's own (Economy/Dealer) or Empire Motors (another mod), for every place
    /// that keeps its hands off a dealership's stock: such a car is the dealership's to sell, not the player's to run.
    /// COST: nothing per frame; a field read, then a dictionary look-up.
    /// </summary>
    public static class Dealerships
    {
        public static bool IsDealership(BuildingRegistration reg)
        {
            return DealerType.Is(reg) || EmpireMotors.IsDealership(reg);
        }

        /// <summary>True for a car of a dealership's stock. Never per frame.</summary>
        public static bool Stock(VehicleInstance vehicle)
        {
            return vehicle != null && (Own(vehicle) || EmpireMotors.Stock(vehicle));
        }

        /// <summary>True for a car of the mod's own dealerships: held by id, or parked at one.</summary>
        public static bool Own(VehicleInstance vehicle)
        {
            if (vehicle == null || string.IsNullOrEmpty(vehicle.id))
            {
                return false;
            }
            DealerDirector director = DealerDirector.Instance;
            if (director != null && director.Holds(vehicle.id))
            {
                return true;
            }
            try
            {
                return !string.IsNullOrEmpty(vehicle.streetName) && DealerType.Is(BuildingHelper.GetBuildingRegistration(vehicle.Address));
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
