using System.Collections.Generic;
using System.Globalization;
using UI.Notification;
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

        /// <summary>
        /// Options button: the car cost rules on the cars the player owns, and on the worst input each guard has (VehicleCostRules.Probe).
        /// Each car's premium, service, tyres, brakes, registration, tow and repair must be a real number within what the car is
        /// worth; the log has a line for each and for the probes, PASS or the fault. It charges nothing and changes nothing.
        /// </summary>
        public void TestCosts()
        {
            GameInstance save = SaveGameManager.Current;
            TestRefusal why = !ModState.Loaded || save == null ? TestRefusal.NotLoaded : CarPhone.OwnedCars(save) == 0 ? TestRefusal.NoOwnCar : TestRefusal.None;
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, "Vehicle costs", why);
                return;
            }
            int level = Prices.Level, cars = CarPhone.OwnedCars(save), failed = 0;
            for (int i = 0; i < cars; i++)
            {
                VehicleInstance car = CarPhone.Owned(save, i);
                float price = car.VehicleType.price;
                float[] values =
                {
                    CarRules.PremiumOf(price, 6, 4f), CarRules.Service(price), CarRules.TyreSet(price), CarRules.BrakeSet(price),
                    CarRules.Registration(price) + CarRules.RegistrationLateFee, CarRules.TowFee, VehicleCostRules.Repair(car.damage, price)
                };
                string bad = string.Empty;
                for (int v = 0; v < values.Length; v++)
                {
                    if (!VehicleCostRules.Within(values[v], price, level))
                    {
                        bad += (bad.Length > 0 ? ", " : string.Empty) + v + "=" + values[v].ToString("R", CultureInfo.InvariantCulture);
                    }
                }
                failed += bad.Length > 0 ? 1 : 0;
                Meta.Dev.Always(Logger, "Vehicle cost check (" + car.vehicleTypeName + ", list price " + price.ToString("F0", CultureInfo.InvariantCulture) + "): premium at the worst "
                                        + values[0].ToString("F0", CultureInfo.InvariantCulture) + ", service " + values[1].ToString("F0", CultureInfo.InvariantCulture) + ", tyres "
                                        + values[2].ToString("F0", CultureInfo.InvariantCulture) + ", brakes " + values[3].ToString("F0", CultureInfo.InvariantCulture) + ", registration late "
                                        + values[4].ToString("F0", CultureInfo.InvariantCulture) + ", tow " + values[5].ToString("F0", CultureInfo.InvariantCulture) + ", repair of all damage "
                                        + values[6].ToString("F0", CultureInfo.InvariantCulture) + ": " + (bad.Length == 0 ? "PASS" : "FAIL (not real numbers, or past the car's worth: " + bad + ")"));
            }
            int probes = VehicleCostRules.Probe(level, out string first);
            Meta.Dev.Always(Logger, "Vehicle cost check (the worst inputs: a huge distance, no time, NaN, many missed days, a save's wild numbers, a purse past an int): "
                                    + (probes == 0 ? "PASS" : "FAIL, " + probes + " probe(s); the first: " + first));
            failed += probes;
            Notifications.Show(failed == 0 ? NotificationType.Success : NotificationType.Error, failed == 0 ? "reallife_test_vehicle_costs_pass" : "reallife_test_vehicle_costs_fail",
                new Dictionary<string, string>(2) { { "count", cars.ToString(CultureInfo.InvariantCulture) }, { "failed", failed.ToString(CultureInfo.InvariantCulture) } }, 10f);
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
