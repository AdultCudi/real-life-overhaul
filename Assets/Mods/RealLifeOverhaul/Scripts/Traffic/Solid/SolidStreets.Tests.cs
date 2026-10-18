using System;
using System.Collections.Generic;
using GleyTrafficSystem;
using Helpers;
using Streets.Pedestrians;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Traffic sees people: its test buttons. Five put somebody in the road in front of ordinary traffic through the reckless
    /// drivers' director (RecklessDrivers.Test); the sixth reads what the cars sense again.
    /// </summary>
    public sealed partial class SolidStreets
    {
        public enum TestKind { Room, Close, Dawdle, Late, Crowd, Senses }

        /// <summary>Options-menu test buttons: one of the things this feature does, started here and now, or a notice saying why not.</summary>
        public void Test(TestKind kind)
        {
            try
            {
                if (kind == TestKind.Senses)
                {
                    ShowSenses();
                    return;
                }
                RecklessDrivers drivers = RecklessDrivers.Instance;
                if (drivers == null)
                {
                    TestRefusals.Tell(Logger, "Traffic sees people", TestRefusal.FeatureOff, 0, kind.ToString());
                    return;
                }
                drivers.Test(kind == TestKind.Room ? RecklessDrivers.TestKind.StepRoom
                    : kind == TestKind.Close ? RecklessDrivers.TestKind.StepClose
                    : kind == TestKind.Dawdle ? RecklessDrivers.TestKind.Dawdle
                    : kind == TestKind.Late ? RecklessDrivers.TestKind.StepLate
                    : RecklessDrivers.TestKind.Crowd);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        // Reads a traffic car and a walking pedestrian again, says what was found in Player.log (one line) and which
        // way the cars are made to see people in a notice.
        private void ShowSenses()
        {
            TestRefusal why = TestRefusals.Street();
            if (why == TestRefusal.None && !Working)
            {
                why = TestRefusal.FeatureOff;
            }
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, "Traffic sees people", why, 0, "what the cars sense");
                return;
            }
            var city = InstanceBehavior<CityManager>.Instance;
            List<Pedestrian> walkers = city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
            List<VehicleComponent> pooled = Manager.GetVehicleList();
            _game.Pool(TrafficManager.Instance, pooled.Count);
            if (!_game.Read(pooled, walkers))
            {
                TestRefusals.TellKey(Logger, "Traffic sees people", "reallife_solid_senses_none", "what the cars sense");
                return;
            }
            string key = _game.How == SolidGame.Mode.Native ? "reallife_solid_senses_native"
                : _game.How == SolidGame.Mode.StandIns ? "reallife_solid_senses_marked"
                : "reallife_solid_senses_brakes";
            var data = new Dictionary<string, string>(1) { { "metres", Mathf.RoundToInt(_game.SensorLength).ToString() } };   // the game keeps the toast's data
            Notifications.Show(NotificationType.Info, key, data, 12f);
        }
    }
}
