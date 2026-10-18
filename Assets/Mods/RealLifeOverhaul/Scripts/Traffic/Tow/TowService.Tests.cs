using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The tow truck: its test button.</summary>
    public sealed partial class TowService : MonoBehaviour
    {
        /// <summary>Options > Tests: a traffic car near the player is stopped and a tow truck sent for it at once, as for a cleared wreck.</summary>
        public void Test()
        {
            const string test = "Tow truck";
            const int reach = 120;
            try
            {
                TestRefusal why = TestRefusals.Street();
                if (why == TestRefusal.None && !Settings.TowTrucks)
                {
                    why = TestRefusal.FeatureOff;
                }
                if (why == TestRefusal.None && (Busy || TrafficAccidents.IsActive))
                {
                    why = TestRefusal.SceneActive;
                }
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, test, why);
                    return;
                }
                if (!TowTruck.Available || _failed)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_test_tow_no_model", "the tow truck's model is not in the prop bundle");
                    return;
                }
                VehicleComponent car = NearCar(25f, reach);
                if (car == null)
                {
                    TestRefusals.Tell(Logger, test, TestRefusal.NoMovingCar, reach);
                    return;
                }
                Manager.SetVehicleIgnored(car.GetIndex(), true);
                if (!Take(car, null, out int minutes, hold: 2f))
                {
                    TrafficTools.ReturnToTraffic(car, "the tow truck test: no road for the truck");
                    TestRefusals.Tell(Logger, test, TestRefusal.NoRoad, reach, _plan.Refusal);
                    return;
                }
                Logger?.Info("Tow test: '" + TrafficTools.Name(car) + "' stopped where it stood; the truck comes in about " + minutes + " min.");
                TellComing(minutes, 1);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // An ordinary traffic car between these distances of the player that the mod holds for nothing else.
        private static VehicleComponent NearCar(float least, float most)
        {
            List<VehicleComponent> cars = Manager.GetVehicleList();
            Vector3 player = PlayerHelper.GetPosition();
            VehicleComponent best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; cars != null && i < cars.Count; i++)
            {
                VehicleComponent car = cars[i];
                if (car == null || !car.gameObject.activeInHierarchy || car.presetPath != null || TrafficTools.IsAmbulance(car) || TrafficTools.SpokenFor(car)
                    || TrafficManager.Instance.IsVehicleIgnored(car.GetIndex()) || car.rb == null)
                {
                    continue;
                }
                float sqr = (car.transform.position - player).sqrMagnitude;
                if (sqr < least * least || sqr > most * most || sqr >= bestSqr)
                {
                    continue;
                }
                bestSqr = sqr;
                best = car;
            }
            return best;
        }
    }
}
