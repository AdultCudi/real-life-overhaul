using System.Collections.Generic;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The Garage's test buttons on the options page.</summary>
    public sealed partial class GarageDirector
    {
        // The car the player is in, or the nearest one of theirs within 20 m.
        private static VehicleController TestCar(GameManager gm, out TestRefusal why, out string detail)
        {
            why = TestRefusal.None;
            detail = null;
            if (!ModState.Loaded || !GarageState.Loaded || SaveGameManager.Current == null || gm == null || gm.playerController == null)
            {
                why = TestRefusal.NotLoaded;
                return null;
            }
            if (!Settings.Garage)
            {
                why = TestRefusal.FeatureOff;
                return null;
            }
            VehicleController best = gm.selectedVehicle as CarController;
            if (best == null)
            {
                float bestSqr = 20f * 20f;
                Vector3 from = gm.playerController.transform.position;
                List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] is CarController && GoingPublic.MineVehicle(all[i]))
                    {
                        float sqr = (all[i].transform.position - from).sqrMagnitude;
                        if (sqr < bestSqr)
                        {
                            bestSqr = sqr;
                            best = all[i];
                        }
                    }
                }
            }
            if (best == null)
            {
                why = gm.selectedVehicle != null ? TestRefusal.NotACar : TestRefusal.NoCarNear;
                return null;
            }
            string refusal = GarageShop.Refusal(best);
            if (refusal != null)
            {
                why = refusal == "reallife_garage_refused_not_yours" ? TestRefusal.NotYours : TestRefusal.NotACar;
                detail = best.name;
                return null;
            }
            return best;
        }

        /// <summary>Opens the custom shop on the car the player is in, or the nearest of their cars, wherever it stands.</summary>
        public void TestShop()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            VehicleController car = TestCar(gm, out TestRefusal why, out string detail);
            if (car != null && (_shop.IsOpen || _failed))
            {
                why = TestRefusal.SceneActive;
                car = null;
            }
            if (car == null)
            {
                TestRefusals.Tell(Logger, "Custom shop", why, 20, detail);
                return;
            }
            if (GarageList.MenuUp)
            {
                // Pressed on the options page: the shop's list stands on the HUD, which is not shown under a menu.
                _shopWanted = car;
                Notifications.Show(NotificationType.Info, "reallife_garage_summary_waits", null, 6f);
                return;
            }
            if (!_shop.Open(car))
            {
                TestRefusals.Tell(Logger, "Custom shop", TestRefusal.NothingFits, 0, "the shop could not be opened on " + car.name);
            }
        }

        /// <summary>A full build picked at random, free, on the current or nearest car.</summary>
        public void TestRandomBuild()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            VehicleController car = TestCar(gm, out TestRefusal why, out string detail);
            if (car != null && _shop.IsOpen)
            {
                why = TestRefusal.SceneActive;
                car = null;
            }
            if (car == null)
            {
                TestRefusals.Tell(Logger, "Random build", why, 20, detail);
                return;
            }
            GarageBuild build = GarageShop.RandomBuild(car);
            GarageState.Set(car.vehicleInstance.id, build);
            Logger?.Info("Garage: random build on " + car.name + ": [" + build.Write() + "].");
            Notifications.Show(NotificationType.Success, "reallife_garage_test_built", null, 6f);
            _nextSlow = 0f;   // put on the car on the next frame
        }

        /// <summary>The current or nearest car back to stock: its build is forgotten.</summary>
        public void TestStock()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            VehicleController car = TestCar(gm, out TestRefusal why, out string detail);
            if (car != null && _shop.IsOpen)
            {
                why = TestRefusal.SceneActive;
                car = null;
            }
            if (car == null)
            {
                TestRefusals.Tell(Logger, "Restore stock", why, 20, detail);
                return;
            }
            GarageState.Set(car.vehicleInstance.id, null);
            Logger?.Info("Garage: " + car.name + " is stock again.");
            Notifications.Show(NotificationType.Success, "reallife_garage_test_stock", null, 6f);
            _nextSlow = 0f;
        }

        /// <summary>The custom shop opened on the figures page of the current or nearest car, with its check line in Player.log.</summary>
        public void TestFigures()
        {
            if (_shop.IsOpen && !_failed)
            {
                Figures();   // already open: turned to the page
                return;
            }
            _figuresWanted = true;
            TestShop();
            if (_shop.IsOpen)
            {
                Figures();
            }
            else if (_shopWanted == null)
            {
                _figuresWanted = false;   // refused, and said why
            }

        }

        // The figures page and its line, once the shop is open.
        private void Figures()
        {
            _figuresWanted = false;
            string line = _shop.ToFigures();
            if (line != null)
            {
                Meta.Dev.Always(Logger, line);
            }
        }

        /// <summary>
        /// The car being driven against its build: the gear change the physics has against the one the build asks, and the
        /// engine's peak as it drives now, in a notice and a check line.
        /// </summary>
        public void TestTuning()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            var car = gm != null ? gm.selectedVehicle as CarController : null;
            NWH.VehiclePhysics2.VehicleController nwh = car != null ? car.vehicleController : null;
            if (!ModState.Loaded || !GarageState.Loaded || _failed)
            {
                TestRefusals.Tell(Logger, "Tuning check", TestRefusal.NotLoaded);
                return;
            }
            if (!Settings.Garage || !Settings.GarageTuning)
            {
                TestRefusals.Tell(Logger, "Tuning check", TestRefusal.FeatureOff);
                return;
            }
            if (nwh == null || nwh.powertrain == null || nwh.powertrain.transmission == null || nwh.powertrain.engine == null || car.vehicleInstance == null)
            {
                TestRefusals.Tell(Logger, "Tuning check", TestRefusal.NotDriving);
                return;
            }
            if (GarageShop.Refusal(car) != null || !GoingPublic.MineVehicle(car))
            {
                TestRefusals.Tell(Logger, "Tuning check", TestRefusal.NotYours, 0, car.name);
                return;
            }
            GarageModifiers m = GarageTuning.Of(car.vehicleInstance.id);
            CarTuning tuning = CarTuning.Instance;
            bool modelled = tuning != null && tuning.Tuned == nwh;
            float standard = modelled ? CarCatalogue.ShiftSeconds(GarageShop.KindOf(car)) : GarageTuning.StandardShift(nwh);
            float wanted = TuningRules.ShiftSeconds(standard, m.Shift);
            float has = nwh.powertrain.transmission.shiftDuration;
            bool pass = Mathf.Abs(has - wanted) < 0.001f;
            string name = car.vehicleType != null ? car.vehicleType.vehicleTypeName : car.name;
            Meta.Dev.Always(Logger, string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Tuning check (car driven): {0}, {1}: gear change {2:F3} s, wanted {3:F3} s ({4:F3} s standard x{5:F2}); engine peak {6:F0} kW, {7:F0} Nm; {8}: {9}",
                name, modelled ? "car performance drives it" : "the game's own engine and gearbox", has, wanted, standard, m.Shift,
                nwh.powertrain.engine.EstimatedPeakPower, nwh.powertrain.engine.EstimatedPeakTorque, GarageState.Of(car.vehicleInstance.id) != null ? "with its build" : "stock",
                pass ? "PASS" : "FAIL, the gearbox does not have the change the build asks for"));
            Notifications.Show(pass ? NotificationType.Success : NotificationType.Warning, pass ? "reallife_tuning_test_car_pass" : "reallife_tuning_test_car_fail",
                new Dictionary<string, string>(2) { { "has", has.ToString("F2") }, { "wanted", wanted.ToString("F2") } }, 9f);
        }

        /// <summary>What selling the current or nearest car now would fetch, the game's price and the work's, without selling it.</summary>
        public void TestSale()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            VehicleController car = TestCar(gm, out TestRefusal why, out string detail);
            if (car != null && !Settings.GarageResale)
            {
                why = TestRefusal.FeatureOff;
                car = null;
            }
            if (car == null)
            {
                TestRefusals.Tell(Logger, "Tuning sale", why, 20, detail);
                return;
            }
            GarageBuild build = GarageState.Of(car.vehicleInstance.id);
            if (build == null || build.Spent <= 0)
            {
                TestRefusals.TellKey(Logger, "Tuning sale", "reallife_tuning_test_sale_none", car.name);
                return;
            }
            int worth = GarageSaleGame.WorthNow(car, out float price);
            int most = Mathf.RoundToInt(build.Spent * GarageCatalogue.Resale);
            bool pass = GarageSaleGame.Listening && worth >= 0 && worth <= most && (price > 0f || worth == 0);
            Meta.Dev.Always(Logger, "Tuning check (sale): " + car.name + ": the game pays " + Mathf.RoundToInt(price) + ", the work fetches " + worth + " of at most " + most
                                    + " (" + build.Spent + " spent), the sale " + (GarageSaleGame.Listening ? "is" : "is NOT") + " listened for: "
                                    + (pass ? "PASS" : "FAIL"));
            Notifications.Show(NotificationType.Info, "reallife_tuning_test_sale_said", new Dictionary<string, string>(2)
                { { "price", GarageText.Money(price) }, { "worth", GarageText.Money(worth) } }, 10f);
        }

        public void TestBike(BikeKind kind)

        {
            if (_failed || !GarageState.Loaded)
            {
                TestRefusals.Tell(Logger, "Two-wheeler (" + kind + ")", TestRefusal.NotLoaded);
                return;
            }
            _bikes.Test(kind);
        }
    }
}
