using System.Globalization;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Car ownership's test buttons on the options page.</summary>
    public sealed partial class CarOwnership
    {
        private bool Driving(string test, bool ownOnly)
        {
            TestRefusal why = !ModState.Loaded || SaveGameManager.Current == null ? TestRefusal.NotLoaded
                : !Settings.CarOwnership || _failed ? TestRefusal.FeatureOff
                : _car == null ? TestRefusal.NotDriving
                : ownOnly && _record == null ? TestRefusal.NoOwnCar : TestRefusal.None;
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, test, why);
                return false;
            }
            return true;
        }

        /// <summary>Options button: the driven car's tank at five percent.</summary>
        public void TestFuel()
        {
            _nextTick = 0f;
            Tick(Time.time);
            if (!Driving("Fuel", false))
            {
                return;
            }
            _car.SetFuel(_car.vehicleType.maxFuel * 0.05f);
            _lowFuelTold = false;
            Logger?.Info("Fuel test: the tank is at 5 % (" + (_car.vehicleType.maxFuel * 0.05f).ToString("F1", CultureInfo.InvariantCulture) + " of " + _car.vehicleType.maxFuel.ToString("F0")
                + " litres).");
        }

        /// <summary>Options button: the driven car breaks down now.</summary>
        public void TestBreakdown()
        {
            _nextTick = 0f;
            Tick(Time.time);
            if (!Driving("Breakdown", true))
            {
                return;
            }
            if (Settings.CarEasy)
            {
                TestRefusals.Tell(Logger, "Breakdown", TestRefusal.FeatureOff, 0, "easy car ownership is on: no breakdowns");
                return;
            }
            if (!_record.Broken)
            {
                BreakDown();
            }
        }

        /// <summary>Options button: the Garage page, as the phone shows it.</summary>
        public void TestGarage()
        {
            GameInstance save = SaveGameManager.Current;
            TestRefusal why = !ModState.Loaded || save == null ? TestRefusal.NotLoaded
                : !Settings.CarOwnership ? TestRefusal.FeatureOff
                : !LifeEvents.IsSafeForEvent() ? TestRefusal.SceneActive
                : CarPhone.OwnedCars(save) == 0 ? TestRefusal.NoOwnCar : TestRefusal.None;
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, "Garage", why);
                return;
            }
            CarPhone.OpenGarage();
        }
    }
}
