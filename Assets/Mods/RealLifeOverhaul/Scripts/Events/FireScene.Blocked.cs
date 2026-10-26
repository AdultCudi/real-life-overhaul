using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RealLifeOverhaul.Events
{
    /// <summary>
    /// A fire's units held up on the road: standing traffic in their way moved on (MakeWay), and the Options > Tests check that puts a
    /// stopped bus of the mod's own in the engine's lane and times how long the engine takes to come to its place or, held up behind
    /// the bus, to stand where it is with its crew walking the rest (MakeWayRules.Stands). Once a second while a unit is on the road.
    /// </summary>
    internal sealed partial class FireScene
    {
        private const float BusBefore = 40f;         // metres of the engine's route before its place that the test bus stands: room to pass it, and more
        private const float BusTestLimit = 45f;      // seconds after it sets off the engine has to come to a stop for the test to pass
        private const int ClearMost = 6;             // ordinary cars moved on for one unit at the most

        private bool _busTest;           // this fire is the test with a stopped bus in the engine's lane
        private bool _busReported;       // ...and its result has been written
        private BusVehicle _testBus;
        private Vector3 _busAt, _busHeading;
        private float _engineSetOff;
        private float _engineWayAt, _patrolWayAt;
        private int _engineMoved, _patrolMoved;

        private void ResetBlocked(bool blockEngine)
        {
            RemoveBlockingBus();
            _busTest = blockEngine;
            _busReported = false;
            _engineWayAt = _patrolWayAt = 0f;
            _engineMoved = _patrolMoved = 0;
        }

        // Once a second at most: standing traffic that holds a unit up is moved on, the first time said in the log.
        private static void ClearTheWay(EmergencyDriver driver, string unit, ref int moved, float now, ref float next)
        {
            if (now < next || driver == null || !driver.Active)
            {
                return;
            }
            next = now + 1f;
            float held = driver.HeldFor;
            int before = moved;
            if (MakeWay.ClearStanding(driver.Blocker, held, unit, ref moved, ClearMost) && before == 0)
            {
                EventDirector.Logger?.Info($"Fire: {unit} has been held up {held:F0} s by standing traffic with {driver.RouteLeft:F0} m to go; what is in its way is moved on.");
            }
        }

        // The test: a bus stands still in the engine's lane, BusBefore metres of its route before its place, as the engine is sent.
        private void PlaceBlockingBus(FireEngine engine, float now)
        {
            _engineSetOff = now;
            if (!_busTest || _testBus != null)
            {
                return;
            }
            List<Vector3> path = engine.Unit.Path;
            if (path.Count < 4)
            {
                BlockedFailed("the engine's route was too short to stand a bus on");
                return;
            }
            float walked = 0f;
            int at = path.Count - 1;
            while (at > 1 && walked < BusBefore)
            {
                walked += Mathf.Sqrt(CitySpots.Flat(path[at] - path[at - 1]));
                at--;
            }
            Vector3 heading = path[at + 1] - path[at];
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.01f)
            {
                BlockedFailed("no heading could be read from the engine's route");
                return;
            }
            heading.Normalize();
            BusVehicle bus = BusVehicle.Make(_director.transform, "RealLifeOverhaul Test Bus");
            if (bus == null)
            {
                BlockedFailed("the game has no shader to draw a bus with");
                return;
            }
            bus.Place(path[at], heading, null);
            _testBus = bus;
            _busAt = path[at];
            _busHeading = heading;
            EventDirector.Logger?.Info($"Fire check (stopped bus): a bus of the mod's own stands still in the engine's lane {walked:F0} m of its route before its place; "
                                       + $"the engine has {BusTestLimit:F0} s from setting off to come to a stop.");
        }

        // The engine has stopped: the test says how and how soon.
        private void BlockedArrival(FireEngine engine, float now, float away, bool walks)
        {
            if (!_busTest || _testBus == null || _busReported || engine.Car == null)
            {
                return;
            }
            _busReported = true;
            float took = now - _engineSetOff;
            Vector3 off = engine.Car.transform.position - _busAt;
            off.y = 0f;
            bool past = Vector3.Dot(off, _busHeading) > 0f;
            bool ok = took <= BusTestLimit;
            string how = past ? "went past the bus and" : "stood behind the bus and";
            EventDirector.Logger?.Info($"Fire check (stopped bus): the engine {how} came to a stop {away:F0} m from the fire {took:F0} s after it set off"
                                       + $"{(walks && !past ? ", its crew walking the rest" : "")} (wanted within {BusTestLimit:F0} s): {(ok ? "PASS" : "FAIL")}.");
        }

        private void BlockedFailed(string why)
        {
            if (!_busTest || _busReported)
            {
                return;   // not the test, or its result is written
            }
            _busReported = true;
            EventDirector.Logger?.Info($"Fire check (stopped bus): the engine did not come to a stop: {why}: FAIL.");
        }

        // The test's bus is taken off the road and thrown away when the scene ends (or a new one is staged).
        private void RemoveBlockingBus()
        {
            BlockedFailed("the scene ended before the engine had come to a stop");
            if (_testBus == null)
            {
                return;
            }
            BusVehicle bus = _testBus;
            _testBus = null;
            bus.Hide();
            Object.Destroy(bus.gameObject);
        }
    }
}
