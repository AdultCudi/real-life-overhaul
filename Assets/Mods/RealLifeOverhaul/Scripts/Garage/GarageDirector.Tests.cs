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
