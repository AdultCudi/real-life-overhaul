using System;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The driven car's handling with the custom shop's parts: they come off before the handling changes and go back on
        /// top after, so they always act on it, never under it; and when the Garage lays them on the car or takes them off
        /// itself, the handling is worked out again for them.
        /// </summary>
        public static class DrivingLinks
        {
            internal static void Connect()
            {
                DrivingBridges.LiftParts = LiftParts;
                DrivingBridges.LayParts = LayParts;
                GarageBridges.PartsLaid = PartsLaid;
            }

            internal static void Disconnect()
            {
                DrivingBridges.LiftParts = null;
                DrivingBridges.LayParts = null;
                GarageBridges.PartsLaid = null;
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

            private static void PartsLaid(VehicleController vehicle)
            {
                try
                {
                    CarTuning.PartsLaid(vehicle);
                }
                catch (Exception e)
                {
                    Fault("DrivingLinks.PartsLaid", e);
                }
            }
        }
    }
}
