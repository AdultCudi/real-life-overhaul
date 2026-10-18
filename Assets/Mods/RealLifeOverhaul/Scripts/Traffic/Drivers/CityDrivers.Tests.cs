using System;
using System.Collections.Generic;
using System.Globalization;
using GleyTrafficSystem;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The city's drivers: their test buttons. Each puts up the line the player would get by parking or pulling out, now, and
    /// sends a car up the lane behind if none is coming; or says why it cannot.
    /// </summary>
    public sealed partial class CityDrivers
    {
        private const float TestCarBack = 70f;       // metres back along the lane a car is put on the road for a test, when none is coming
        private const float ComingWithin = 120f;     // metres behind the player inside which a car already on the lane will do

        public enum TestKind { Park, LetOut }

        /// <summary>The options page's buttons: the test, or a notice that the feature is not there (switched off for the session).</summary>
        public static void TestNow(TestKind kind)
        {
            if (Instance == null)
            {
                TestRefusals.Tell(Logger, "City drivers", TestRefusal.FeatureOff, 0, kind.ToString());
                return;
            }
            Instance.Test(kind);
        }

        /// <summary>Options-menu test buttons: the room to park, or being let out of a space, here and now; or a notice saying why not.</summary>
        public void Test(TestKind kind)
        {
            try
            {
                string name = kind == TestKind.Park ? "A car stops behind your parking" : "A car lets you out of a space";
                TestRefusal why = TestRefusals.Street();
                if (why == TestRefusal.None && !Working)
                {
                    why = TestRefusal.FeatureOff;
                }
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, name, why);
                    return;
                }
                TrafficManager traffic = TrafficManager.Instance;
                float now = Time.time;
                ReadTestPlayer(traffic, now);
                if (!_playerDriving)
                {
                    TestRefusals.Tell(Logger, name, TestRefusal.NotDriving);
                    return;
                }
                float off = Mathf.Abs(_playerOff);
                bool beside = _playerLane != null && (kind == TestKind.Park ? off <= DriverRules.LaneHalf : off >= DriverRules.KerbFrom && off <= DriverRules.KerbMost);
                if (Mathf.Abs(_playerAlong) > 0.5f || !beside)
                {
                    string where = _playerLane == null ? "no lane near" : off.ToString("F1", CultureInfo.InvariantCulture) + " m from a lane's line";
                    TestRefusals.TellKey(Logger, name, kind == TestKind.Park ? "reallife_test_drivers_stop_first" : "reallife_test_drivers_kerb_first",
                        "speed " + _playerAlong.ToString("F1", CultureInfo.InvariantCulture) + " m/s, " + where);
                    return;
                }
                int slot = kind == TestKind.Park ? ReverseLine : LetOutLine;
                EndLine(slot, "a test button", now);
                Line line = _lines[slot];
                float alongHalf = DriverRules.Along(_playerHalfLength, _playerHalfWidth, Mathf.Cos(_playerAngle * Mathf.Deg2Rad), Mathf.Sin(_playerAngle * Mathf.Deg2Rad));
                Vector3 right = Vector3.Cross(Vector3.up, _laneWay);
                Vector3 rear = _playerMiddle - _laneWay * alongHalf - right * _playerOff;
                float back = kind == TestKind.Park ? DriverRules.ParkRoom : DriverRules.LetOutBack;
                Want(traffic, line, rear, back);
                if (!line.Pending)
                {
                    TestRefusals.TellKey(Logger, name, "reallife_test_drivers_no_room", "the lane forks or ends within " + back.ToString("F0", CultureInfo.InvariantCulture) + " m behind");
                    return;
                }
                line.Tested = true;
                line.WantedAt = now;
                _active = true;
                _next = 0f;   // the next frame looks
                string car = SendCarUp(traffic, rear);
                Logger?.Info(FormattableString.Invariant($"{name} test: a hold line is wanted {back:F1} m behind the player's car; ") + car + ".");
                var data = new Dictionary<string, string>(2)   // the game keeps the toast's data
                {
                    { "metres", Mathf.RoundToInt(back).ToString(CultureInfo.InvariantCulture) },
                    { "seconds", Mathf.RoundToInt(DriverRules.HoldMost).ToString(CultureInfo.InvariantCulture) }
                };
                Notifications.Show(NotificationType.Info, kind == TestKind.Park ? "reallife_test_drivers_park_started" : "reallife_test_drivers_letout_started", data, 10f);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        // The player as a look would read them, now: a look under way is dropped (the next frame begins another).
        private void ReadTestPlayer(TrafficManager traffic, float now)
        {
            _evaluating = false;
            _cursor = 0;
            _obstacles.Clear();
            ReadPlayer(traffic, Helpers.PlayerHelper.GetPosition(), now);
        }

        // A car to come up the lane behind the player: one already on it within reach, else one put on the road further back.
        private string SendCarUp(TrafficManager traffic, Vector3 rear)
        {
            List<VehicleComponent> pooled = Manager.GetVehicleList();
            for (int i = 0; i < pooled.Count; i++)
            {
                VehicleComponent car = pooled[i];
                if (car == null || !car.isActiveAndEnabled || traffic.IsVehicleIgnored(car.GetIndex()))
                {
                    continue;
                }
                Vector3 to = rear - car.transform.position;
                to.y = 0f;
                float along = Vector3.Dot(to, _laneWay);
                float aside = Vector3.Dot(to, Vector3.Cross(Vector3.up, _laneWay));
                if (along > 0f && along < ComingWithin && Mathf.Abs(aside) < DriverRules.LaneHalf && Vector3.Dot(car.transform.forward, _laneWay) > 0.7f)
                {
                    return "'" + TrafficTools.Name(car) + "' is coming up the lane " + along.ToString("F0", CultureInfo.InvariantCulture) + " m behind";
                }
            }
            Waypoint lane = DriverGame.LaneAt(rear, _laneWay);
            Waypoint start = lane != null ? DriverGame.PointBack(traffic, lane, TestCarBack) : null;
            GameObject prefab = IdlePrefab(pooled);
            VehicleComponent sent = start != null && prefab != null ? TrafficTools.Load(prefab, start) : null;
            return sent != null ? "'" + TrafficTools.Name(sent) + "' was put on the road " + TestCarBack.ToString("F0", CultureInfo.InvariantCulture) + " m back to come up the lane"
                : "no car was coming and none could be put on the road behind (" + (start == null ? "the lane forks or ends" : "no idle car in the pool") + ")";
        }

        // The prefab of a car the pool has idle, so that putting it on the road takes no car in use.
        private static GameObject IdlePrefab(List<VehicleComponent> pooled)
        {
            for (int i = 0; i < pooled.Count; i++)
            {
                VehicleComponent car = pooled[i];
                if (car != null && !car.gameObject.activeInHierarchy && car.prefab != null && !TrafficTools.IsAmbulance(car) && car.vehicleType == VehicleTypes.Car)
                {
                    return car.prefab;
                }
            }
            return null;
        }
    }
}
