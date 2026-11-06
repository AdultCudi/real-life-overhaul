using System;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The driven car's handling with the custom shop's parts: they come off before the handling changes and go back on
        /// top after, so they always act on it, never under it.
        /// </summary>
        public static class DrivingLinks
        {
            internal static void Connect()
            {
                DrivingBridges.LiftParts = LiftParts;
                DrivingBridges.LayParts = LayParts;
            }

            internal static void Disconnect()
            {
                DrivingBridges.LiftParts = null;
                DrivingBridges.LayParts = null;
            }

            private static bool LiftParts(VehicleController vehicle)
            {
                try
                {
                    GarageCar car = GarageCar.Of(vehicle, false);
                    return car != null && car.LiftHandling();
                }
                catch (Exception e)
                {
                    Fault("DrivingLinks.LiftParts", e);
                    return false;
                }
            }

            private static void LayParts(VehicleController vehicle)
            {
                try
                {
                    GarageCar car = GarageCar.Of(vehicle, false);
                    if (car != null)
                    {
                        car.LayHandling();
                    }
                }
                catch (Exception e)
                {
                    Fault("DrivingLinks.LayParts", e);
                }
            }
        }
    }
}
