using NWH.VehiclePhysics2.Powertrain;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How the player got to the people they meet, as one outing's scene sees it: the last time it saw them drive a car (and whether it
    /// runs on a battery, and is a luxury car), ride a two wheeler, or get off the bus or the subway (<see cref="SocialHooks.RodeTransit"/>).
    /// What they arrived in is judged at the hello: the last of those within a few game minutes, else on foot. Nothing is kept in the
    /// save. COST: a few field reads at the director's five ticks a second while a plan waits, nothing allocated.
    /// </summary>
    public static class SocialArrivalNote
    {
        public const float CarMinutes = 6f;        // game minutes a parked car still counts as the way they came
        public const float BikeMinutes = 6f;
        public const float TransitMinutes = 14f;   // the walk from the stop or the station

        private static float _carAt = -1000f, _bikeAt = -1000f, _transitAt = -1000f;
        private static bool _electric, _luxury;

        /// <summary>Forgotten when a scene is begun: an earlier outing's arrival is no part of this one.</summary>
        public static void Forget()
        {
            _carAt = _bikeAt = _transitAt = -1000f;
            _electric = _luxury = false;
        }

        /// <summary>The player got off a bus or out of the subway at this game minute.</summary>
        public static void Transit(float nowMinutes)
        {
            _transitAt = nowMinutes;
        }

        /// <summary>One look at how the player is travelling, at this game minute. <paramref name="car"/> is the car they drive, if any.</summary>
        public static void Sample(bool onFoot, CarController car, float nowMinutes)
        {
            if (car != null)
            {
                _carAt = nowMinutes;
                ReadCar(car);
            }
            else if (!onFoot)
            {
                _bikeAt = nowMinutes;   // not on foot and not in a car: the game's scooter, or the mod's bicycle and motorbike
            }
        }

        /// <summary>What the player arrived in, judged now (the hello), and whether the car runs on a battery and is a luxury one.</summary>
        public static SocialArrival Read(float nowMinutes, out bool electric, out bool luxury)
        {
            electric = _electric;
            luxury = _luxury;
            if (nowMinutes - _carAt <= CarMinutes)
            {
                return SocialArrival.Car;
            }
            electric = luxury = false;
            if (nowMinutes - _bikeAt <= BikeMinutes)
            {
                return SocialArrival.Bike;
            }
            return nowMinutes - _transitAt <= TransitMinutes ? SocialArrival.Transit : SocialArrival.Foot;
        }

        // Whether the car runs on a battery (the engine's own type), and whether the game calls it a luxury car.
        private static void ReadCar(CarController car)
        {
            _luxury = car.vehicleType != null && car.vehicleType.isLuxuryCar;
            EngineComponent engine = car.vehicleController != null && car.vehicleController.powertrain != null ? car.vehicleController.powertrain.engine : null;
            _electric = engine != null && engine.engineType == EngineComponent.EngineType.Electric;
        }
    }
}
