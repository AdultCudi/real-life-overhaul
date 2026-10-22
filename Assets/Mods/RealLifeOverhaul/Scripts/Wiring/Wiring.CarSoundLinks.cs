namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The recorded car sounds with the Garage: the exhaust and turbo a car's build fits (the cart while it is on the
        /// turntable, stock with the Garage's tuning off), and the car in the open custom shop.
        /// </summary>
        public static class CarSoundLinks
        {
            internal static void Connect()
            {
                CarSoundBridges.Parts = PartsOf;
                CarSoundBridges.Turntable = Turntable;
            }

            internal static void Disconnect()
            {
                CarSoundBridges.Parts = null;
                CarSoundBridges.Turntable = null;
            }

            private static VehicleController Turntable()
            {
                GarageShop shop = GarageDirector.Instance != null ? GarageDirector.Instance.Shop : null;
                return shop != null && shop.IsOpen ? shop.Car : null;
            }

            private static int PartsOf(VehicleController vehicle)
            {
                if (vehicle == null || vehicle.vehicleInstance == null)
                {
                    return 0;
                }
                GarageBuild build;
                if (Turntable() == vehicle)
                {
                    GarageCar shown = GarageCar.Of(vehicle, false);
                    build = shown != null ? shown.Shown : null;   // the cart, as it is shown on the car: heard before it is paid for
                }
                else
                {
                    build = Settings.Garage && Settings.GarageTuning ? GarageState.Of(vehicle.vehicleInstance.id) : null;
                }
                if (build == null)
                {
                    return 0;
                }
                PartDef exhaust = GarageCatalogue.Part(build.Parts[(int)PartSlot.Exhaust]);
                PartDef turbo = GarageCatalogue.Part(build.Parts[(int)PartSlot.Turbo]);
                return CarSoundBridges.Pack(exhaust != null ? exhaust.Loud : 0, turbo != null ? turbo.Boost : 0);
            }
        }
    }
}
