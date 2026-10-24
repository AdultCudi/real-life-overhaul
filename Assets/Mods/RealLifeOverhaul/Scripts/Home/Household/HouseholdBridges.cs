using System;

namespace RealLifeOverhaul.Household
{
    /// <summary>The doors to the features household staff lean on, filled by Wiring/HouseholdLinks; with a door left null it does without.</summary>
    public static class HouseholdBridges
    {
        /// <summary>The city's wage market for staff in thousandths (Economy: the price level, the cycle); null: the price level alone.</summary>
        public static Func<int> WageMarket;

        /// <summary>True for a car of the save's that is not the player's to have seen to: a dealership's stock, the partner's own car.</summary>
        public static Func<VehicleInstance, bool> NotOurs;

        /// <summary>
        /// Car ownership's service of a car where it stands (the repair bay's workshop at its prices: the service, tyres,
        /// brakes, the inspection): what it comes to, and with true, done. Null: car ownership is not there, nothing is due.
        /// </summary>
        public static Func<VehicleInstance, bool, float> Service;

        public static void Reset()
        {
            WageMarket = null;
            NotOurs = null;
            Service = null;
        }
    }
}
