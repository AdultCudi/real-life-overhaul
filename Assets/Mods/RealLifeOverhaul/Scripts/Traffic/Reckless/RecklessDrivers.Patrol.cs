using System.Collections.Generic;
using GleyTrafficSystem;
using Localizor;
using Streets.Pedestrians;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Reckless drivers: a patrol car that saw it, the stop or the chase, and the patrol car sent off again.</summary>
    public sealed partial class RecklessDrivers : MonoBehaviour
    {
        private const float PresenceSees = 60f;      // metres from the parked patrol car inside which its officers see one go by
        private const float ChaseGap = 9f;           // metres the patrol car keeps behind it (the accident scene's own figure)
        private const float PullOverMost = 40f;      // seconds the patrol car is given to close up
        private const float SceneClear = 30f;        // metres a patrol car's way must keep from where somebody lies in the road

        // True if a patrol car has just gone after it (the phase has changed).
        private bool Seen(float now)
        {
            if (!Settings.Police || !StreetRules.PatrolNotices(_car.Kind))
            {
                return false;
            }
            if (_patrolAt > 0f && now >= _patrolAt)
            {
                _patrolAt = 0f;
                return Patrol(now, false);
            }
            // The officers at a parked patrol car see one go by (asked once for each car, as it comes near).
            PolicePresence presence = PolicePresence.Instance;
            if (!_presenceLooked && presence != null && presence.CarWithin(_car.Car.transform.position, PresenceSees))
            {
                _presenceLooked = true;
                Logger?.Info("Reckless driver: it passed the parked patrol car; a patrol car is sent after it.");
                return Patrol(now, false);
            }
            return false;
        }

        // A patrol car 140 to 200 m behind it, lights on, sent after it; never seen appearing nor driving through where somebody lies.
        private bool Patrol(float now, bool hitAndRun)
        {
            if (!Settings.Police || _patrol.InUse || !_car.InUse)
            {
                return false;
            }
            Waypoint anchor = _car.Behind();
            if (anchor == null || !_patrol.Spawn(UnitKind.Police, anchor, Random.Range(140f, 200f), 60f))
            {
                Logger?.Info("Reckless driver: no patrol car could be put on the road behind it (" + (anchor == null ? "no road point" : _patrol.Refusal) + "); it is not chased.");
                NoPatrol();
                return false;
            }
            VehicleComponent unit = _patrol.Car;
            bool inSight = InSight(unit.transform.position);
            bool through = hitAndRun && PassesNear(_patrol.Path, _scene, SceneClear);
            if (inSight || through)
            {
                _patrol.Release(resumeTraffic: true, remove: true);
                Logger?.Info("Reckless driver: the patrol car would have " + (inSight ? "appeared in view" : "had to drive through where somebody lies") + "; none is sent, and it is not chased.");
                NoPatrol();
                return false;
            }
            // Its road: up to the point behind the car, then the car's own road from there on (the list is read live).
            List<Vector3> road = _car.Unit.Path;
            for (int i = _car.BehindIndex + 1; i < road.Count; i++)
            {
                _patrol.Path.Add(road[i]);
            }
            EmergencyDriver driver = _patrol.Driver;
            driver.Begin(unit, _patrol.Path, _patrol.Path[_patrol.Path.Count - 1], 6f);
            driver.Follow(_car.Car.rb, ChaseGap);

            _complies = !hitAndRun && StreetRules.PullsOver(Random.Range(0, 100));
            if (_complies)
            {
                // The driver sees the lights and eases off; the patrol car closes up and they stop.
                _car.Settle();
                driver.CruiseSpeed = 21f;
                _chase.Begin(_car.Unit, _patrol, _stop, true, now + PullOverMost, arrest: false, "reallife_streets_pulled_over");
                TrafficAccidents.Notice(NotificationType.Info, "reallife_streets_patrol_stop", 8f);
            }
            else
            {
                _car.Flee();
                driver.CruiseSpeed = _car.Cruise + 3.5f;
                bool caught = Random.value < 0.55f;
                _chase.Begin(_car.Unit, _patrol, _stop, caught, now + Random.Range(20f, 45f), arrest: true, hitAndRun ? "reallife_chase_caught" : "reallife_streets_chase_caught");
                TrafficAccidents.Notice(NotificationType.Warning, hitAndRun ? "reallife_streets_chase_hit_run" : "reallife_streets_patrol_chase", 8f);
            }
            _fledFromPeople = hitAndRun;
            _phase = Phase.Chased;
            Logger?.Info($"Reckless driver: a patrol car is {Vector3.Distance(unit.transform.position, _car.Car.transform.position):F0} m behind it, lights on; "
                         + (_complies ? "the driver will stop for it." : $"the driver runs and will be {(_chase.WillBeCaught ? "caught" : "lost")}."));
            return true;
        }

        private bool _fledFromPeople;

        private void NoPatrol()
        {
            if (_test)
            {
                Notifications.Show(NotificationType.Warning, "reallife_streets_test_no_patrol", null, 8f);
            }
        }

        private void EndChase(bool caught, float now)
        {
            _stop.Clear();
            SendPatrolOff();
            _chase.Reset();
            if (caught && _complies)
            {
                TrafficAccidents.Notice(NotificationType.Info, "reallife_streets_ticketed", 8f);
                Finish(now, false, "pulled over, written up and sent on its way");
            }
            else if (caught)
            {
                // As the accident scene's own chase ends: an arrested driver's car is taken away.
                Finish(now, true, "stopped, the driver arrested, the car taken away");
            }
            else
            {
                TrafficAccidents.Notice(NotificationType.Info, _fledFromPeople ? "reallife_chase_escaped" : "reallife_streets_chase_lost", 8f);
                Settle(now, "it lost the patrol car");
            }
        }

        // The patrol car goes back to traffic as it is, watched by TrafficWatch in case it then stands.
        private void SendPatrolOff()
        {
            if (!_patrol.InUse)
            {
                return;
            }
            VehicleComponent unit = _patrol.Car;
            if (unit != null && TrafficTools.IsHeld(unit))
            {
                TrafficWatch.Guard(unit, "patrol car after a roadside stop");
            }
            _patrol.Release(resumeTraffic: TrafficTools.Ready, remove: false, quiet: true);
        }

        private void CallOff()
        {
            _stop.Clear();
            _chase.Reset();
            SendPatrolOff();
        }

        private static bool PassesNear(List<Vector3> path, Vector3 place, float within)
        {
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 offset = path[i] - place;
                if (offset.x * offset.x + offset.z * offset.z < within * within)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
