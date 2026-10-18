using System;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The driven car's handling with the custom shop's parts:
        ///
        ///   DrivingBridges.LiftParts, LayParts   the Garage's springs, dampers, bars, weight, brakes and tyres are taken off a
        ///                                        car before its handling changes and put back on top after, so they always
        ///                                        act on the handling, never under it (Driving/CarHandling, Garage/GarageCar)
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
