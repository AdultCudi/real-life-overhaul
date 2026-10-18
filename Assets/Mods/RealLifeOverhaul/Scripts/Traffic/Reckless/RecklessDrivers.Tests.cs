using System;
using System.Collections.Generic;
using GleyTrafficSystem;
using Helpers;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Reckless drivers: the test buttons, and the marker over a test's car.</summary>
    public sealed partial class RecklessDrivers : MonoBehaviour
    {
        private const float MarkFor = 15f;           // seconds a test's car is marked

        public enum TestKind { Speeder, RedLight, Drunk, Jaywalker, HitSomeone, Crash, Patrol, StepRoom, StepClose, Dawdle, StepLate, Crowd }

        /// <summary>Options-menu test buttons: one thing this feature does, started now, or a notice saying why not.</summary>
        public void Test(TestKind kind)
        {
            try
            {
                bool solid = kind == TestKind.StepRoom || kind == TestKind.StepClose || kind == TestKind.Dawdle || kind == TestKind.StepLate || kind == TestKind.Crowd;
                bool people = kind == TestKind.Jaywalker || kind == TestKind.HitSomeone || kind == TestKind.StepLate;
                TestRefusal why = TestRefusals.Street();
                if (why == TestRefusal.None && solid && !SolidStreets.Working)
                {
                    why = TestRefusal.FeatureOff;   // these show what "Traffic sees people in the road" does
                }
                if (why == TestRefusal.None && (!_working || (!solid && !Settings.NaturalAccidents) || (people && !PeopleCanBeHit()) || (kind == TestKind.Patrol && !Settings.Police)))
                {
                    why = TestRefusal.FeatureOff;
                }
                if (why == TestRefusal.None && ((PolicePursuit.Instance != null && PolicePursuit.Instance.Busy) || PoliceAttention.IsBusy))
                {
                    why = TestRefusal.PoliceBusy;
                }
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Reckless driver", why);
                    return;
                }
                StreetCrime.EndActive();    // one incident at a time, and the tester asked for this one
                TrafficAccidents.EndActive();
                EndNow("a test button");

                float now = Time.time;
                float metres = 0f;
                bool started;
                string none = null;
                switch (kind)
                {
                    case TestKind.RedLight:
                        started = Begin(null, StreetRules.Kind.RedLight, false, false, false, true, now);
                        none = "reallife_streets_test_no_light";
                        break;
                    case TestKind.Drunk:
                        started = Begin(null, StreetRules.Kind.Drunk, false, false, false, true, now);
                        break;
                    case TestKind.Jaywalker:
                    case TestKind.StepRoom:
                        started = StepOut(false, 1, now, out metres);
                        none = "reallife_streets_test_no_pair";
                        break;
                    case TestKind.HitSomeone:
                        started = StepOut(true, 2, now, out metres);
                        none = "reallife_streets_test_no_pair";
                        break;
                    case TestKind.Crash:
                        started = Begin(null, StreetRules.Kind.Tailgater, true, false, true, true, now);
                        break;
                    case TestKind.StepClose:
                        started = StepOut(false, 3, now, out metres);
                        none = "reallife_streets_test_no_pair";
                        break;
                    case TestKind.Dawdle:
                        started = StepOut(false, 4, now, out metres);
                        none = "reallife_streets_test_no_pair";
                        break;
                    case TestKind.StepLate:
                        started = StepOut(true, 5, now, out metres);
                        none = "reallife_streets_test_no_pair";
                        break;
                    case TestKind.Crowd:
                        int sent = Crowd(now, out metres);
                        started = sent > 0;
                        none = "reallife_solid_test_no_crowd";
                        if (started)
                        {
                            Logger?.Info($"Jaywalker: a test sent {sent} people across the road at once, the nearest {metres:F0} m from the player.");
                            var crowd = new Dictionary<string, string>(1) { { "count", sent.ToString() } };   // the game keeps the toast's data
                            Notifications.Show(NotificationType.Info, "reallife_solid_test_crowd", crowd, 8f);
                            return;
                        }
                        break;
                    case TestKind.Patrol:
                        started = Begin(null, StreetRules.Kind.Speeder, false, false, false, true, now);
                        if (started)
                        {
                            _patrolAt = now + 2f;
                        }
                        break;
                    default:
                        started = Begin(null, StreetRules.Kind.Speeder, false, false, false, true, now);
                        break;
                }
                if (!started)
                {
                    if (none != null)
                    {
                        TestRefusals.TellKey(Logger, "Reckless driver", none, kind.ToString());
                    }
                    else
                    {
                        TestRefusals.Tell(Logger, "Reckless driver", TestRefusal.NoMovingCar, (int)TestWithin, kind.ToString());
                    }
                    return;
                }
                if (_car.InUse && _car.Car != null)
                {
                    metres = Vector3.Distance(PlayerHelper.GetPosition(), _car.Car.transform.position);
                    _marked = true;
                    _markUntil = now + MarkFor;
                    _markLabel = "reallife_streets_test_marker".GetLocalization();
                }
                var data = new Dictionary<string, string>(1) { { "metres", ((int)metres).ToString() } };   // the game keeps the toast's data
                Notifications.Show(NotificationType.Info, "reallife_streets_test_started", data, 8f);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private void Mark(float now)
        {
            if (now < _markUntil && _car.InUse && _car.Car != null)
            {
                _mark.Show(_car.Car.transform.position, _markLabel);
                return;
            }
            _marked = false;
            _mark.Hide();
        }
    }
}
