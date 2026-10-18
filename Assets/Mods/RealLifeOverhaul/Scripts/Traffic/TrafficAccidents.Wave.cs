using System;
using GleyTrafficSystem;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Traffic accidents: the officer on traffic waves an emergency unit through the open lane. Four times a second while the
    /// tape is up with an officer on the post, the units driving with their lights on (an ambulance, a patrol car, the fire
    /// engine: EmergencyDriver's list, a handful) are asked whether one comes up the open lane within reach of the post; the
    /// scene's own ambulance leaving round the wrecks by that lane is waved through as it sets off. The officer steps out of
    /// the lane until the unit is past (CordonRules.PassOver), or a few seconds at most, then goes back to the post.
    /// </summary>
    public sealed partial class TrafficAccidents : MonoBehaviour
    {
        private const float WaveLookEvery = 0.25f;   // seconds between two looks for a unit coming up the open lane
        private const float WaveUnitBack = 110f;     // metres back along the open lane a test unit starts
        private const float WaveUnitOn = 60f;        // ...and on past the scene it drives before it is handed back

        private VehicleComponent _waveCar;           // the unit being waved through, while one is
        private float _waveHalfLength;
        private string _waveWhat;
        private float _nextWaveLook;
        private ResponseUnit _testUnit;              // the patrol car the test button sends through the open lane

        // Four times a second in a scene: a pass under way is watched to its end; otherwise a lit unit coming up the open lane starts one.
        private void WatchWave(float now)
        {
            if (now < _nextWaveLook)
            {
                return;
            }
            _nextWaveLook = now + WaveLookEvery;
            TickTestUnit();
            if (_people.Passing)
            {
                bool gone = _waveCar == null || !_waveCar.gameObject.activeInHierarchy || !TrafficTools.IsHeld(_waveCar);
                float tail = float.MinValue;
                if (!gone && _cordon.ToPost(_waveCar.transform.position, _waveCar.transform.forward, _waveHalfLength, out float toPost, out _, out _))
                {
                    tail = -toPost - 2f * _waveHalfLength;
                }
                if (CordonRules.PassOver(tail, gone, now - _people.PassSince))
                {
                    _people.UnitPast(now, gone ? "it is gone" : tail >= CordonRules.PassBeyond ? "it is past the post" : "the most a pass lasts is up");
                    _waveCar = null;
                }
                return;
            }
            if (!_people.OnPost)
            {
                return;
            }
            for (int i = 0; i < EmergencyDriver.ListPlaces; i++)
            {
                EmergencyDriver driver = EmergencyDriver.LitAt(i);
                VehicleComponent car = driver != null ? driver.Car : null;
                if (car == null || car.rb == null)
                {
                    continue;
                }
                float halfLength = Mathf.Max(1.5f, car.length * 0.5f);
                Transform body = car.transform;
                if (_cordon.ToPost(body.position, body.forward, halfLength, out float toPost, out float fromLane, out float squarely)
                    && CordonRules.PassDue(toPost, fromLane, squarely))
                {
                    StartWave(car, now, false);
                    return;
                }
            }
        }

        // An officer steps out of the open lane for this unit (the scene's own leaving, or one coming up it). False if none could.
        private bool StartWave(VehicleComponent car, float now, bool fromScene)
        {
            if (car == null || _people.Passing)
            {
                return false;
            }
            TrafficTools.Footprint(car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            string what = car == _ambulance.Unit.Car || TrafficTools.IsAmbulance(car) ? "ambulance"
                : car == _police.Unit.Car || (_testUnit != null && car == _testUnit.Car) ? "patrol car" : "unit";
            if (!_people.LetUnitPast(now, centre, forward, halfLength, halfWidth, fromScene, what))
            {
                return false;
            }
            _waveCar = car;
            _waveHalfLength = halfLength;
            _waveWhat = what;
            return true;
        }

        // The scene is over: a pass under way ends with it (the cast goes with the scene).
        private void EndWave()
        {
            _waveCar = null;
            ReleaseTestUnit();
        }

        /// <summary>
        /// Options-menu test: with an accident's tape up and an officer on traffic, a patrol car is sent through the open lane
        /// past the scene, lights on, so the officer waves it through; otherwise a notice says why not.
        /// </summary>
        public static void TestWaveThrough()
        {
            TrafficAccidents scene = Instance;
            try
            {
                TestRefusal why = TestRefusals.Street();
                if (why == TestRefusal.None && (scene == null || !Settings.TrafficAccidents || !Settings.People))
                {
                    why = TestRefusal.FeatureOff;
                }
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Wave-through", why);
                    return;
                }
                if (!scene.HasActiveAccident || !scene._people.OnPost)
                {
                    TestRefusals.TellKey(Logger, "Wave-through", "reallife_test_wave_none",
                        scene.HasActiveAccident ? "the tape is not up or nobody is on the traffic post yet" : "no accident is on");
                    return;
                }
                scene.SendTestUnit();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // A patrol car on a route along the open lane from well back, past the scene and on.
        private void SendTestUnit()
        {
            Waypoint anchor = _cordon.OpenLanePoint(WaveUnitOn);
            _testUnit = _testUnit ?? new ResponseUnit(gameObject);
            if (anchor == null || _testUnit.InUse || !_testUnit.Spawn(UnitKind.Police, anchor, WaveUnitBack + WaveUnitOn, WaveUnitBack * 0.6f))
            {
                string refusal = anchor == null ? "no lane is open beside the scene" : _testUnit.InUse ? "the test's patrol car is still out" : _testUnit.Refusal;
                TestRefusals.TellKey(Logger, "Wave-through", "reallife_test_wave_no_unit", refusal);
                return;
            }
            _testUnit.Driver.BeginRoute(_testUnit.Car, _testUnit.Path, 0f);
            _testUnit.Driver.MayPass = false;   // it keeps to the open lane, as a unit going by would
            TrafficWatch.Guard(_testUnit.Car, "test patrol car");
            Logger?.Info($"Wave-through test: a patrol car comes along the open lane from about {WaveUnitBack:F0} m back, past the scene and {WaveUnitOn:F0} m on.");
            Notifications.Show(NotificationType.Info, "reallife_test_wave_started", null, 8f);
        }

        // The test's patrol car is handed back to traffic at the end of its route, or taken off the road if it cannot go on.
        private void TickTestUnit()
        {
            if (_testUnit == null || !_testUnit.InUse)
            {
                return;
            }
            EmergencyDriver driver = _testUnit.Driver;
            if (_testUnit.IsLost)
            {
                _testUnit.Release(resumeTraffic: false, remove: true, quiet: true);
            }
            else if (!driver.Active || driver.Arrived || driver.HeldFor > BoxedIn)
            {
                bool moving = driver.Active && driver.Speed > MovingOff;
                _testUnit.Release(resumeTraffic: true, remove: !moving && !Seen(_testUnit.Car.transform.position), quiet: true);
            }
        }

        private void ReleaseTestUnit()
        {
            if (_testUnit != null && _testUnit.InUse)
            {
                _testUnit.Release(TrafficTools.Ready, remove: true, quiet: true);
            }
        }
    }
}
