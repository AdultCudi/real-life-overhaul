using System;
using Helpers;
using RealLifeOverhaul.Economy.Dealership;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What a car dealership is to the rest of the mod, whoever runs it: this mod's own (Economy/Dealer) or Empire Motors,
    /// another author's mod (Core/EmpireMotors). Every place that keeps its hands off a dealership's stock asks here, so
    /// that both kinds are treated alike: a car of a dealership's stock is the dealership's to sell, not the player's to
    /// run. It has no Garage page, no record of wear, registration or insurance, no roadside call; it cannot go on hire
    /// or into the custom shop, is never taken for a car the player bought on car finance and does not count as a car
    /// the player owns for a first licence; and a dealership is not sold while its stock is on the lot.
    ///
    /// The cost: nothing per frame. Each answer is a field read while neither kind is running, then a dictionary lookup.
    /// </summary>
    public static class Dealerships
    {
        /// <summary>True for a dealership of either kind.</summary>
        public static bool IsDealership(BuildingRegistration reg)
        {
            return DealerType.Is(reg) || EmpireMotors.IsDealership(reg);
        }

        /// <summary>True for a car of either kind of dealership's stock. Never per frame.</summary>
        public static bool Stock(VehicleInstance vehicle)
        {
            return vehicle != null && (Own(vehicle) || EmpireMotors.Stock(vehicle));
        }

        /// <summary>True for a car of this mod's own dealerships' stock: one of its cars by id, or one parked at one of its dealerships.</summary>
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
