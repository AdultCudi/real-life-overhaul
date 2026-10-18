using System;
using System.Collections.Generic;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using Localizor;
using Streets.Pedestrians;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Reckless drivers: what comes of a hit, somebody run down or a collision.</summary>
    public sealed partial class RecklessDrivers : MonoBehaviour
    {
        private const float BlamelessFor = 5f;       // seconds after it hit the player's car in which that crash is still its fault
        private const float HurtScene = 20f;         // seconds after it ran somebody down in which the scene round them has not begun or has only just
        private const float StrikerFor = 20f;        // seconds an ordinary car that hit somebody is remembered for the scene
        private const float StrikerSlow = 6f;        // km/h: slower than this its driver has pulled up

        /// <summary>From CarStrikes' physics step: a traffic car it watches has thrown somebody down. Noted; OnHurt decides next frame.</summary>
        public void Struck(VehicleComponent car, float speed)
        {
            _struckBy = car;
            _struckSpeed = speed;
            _struck = true;
        }

        // A traffic car has thrown somebody down. If they are hurt the debt is paid and the driver stops or runs: the reckless
        // car as it is, an ordinary one taken over now, or (a reckless car out already, or no road) remembered for the scene.
        private void OnHurt(float now)
        {
            VehicleComponent car = _struckBy;
            float speed = _struckSpeed;
            _struckBy = null;
            PedestrianIncidents incidents = PedestrianIncidents.Instance;
            if (incidents == null || car == null)
            {
                return;
            }
            bool hurt = incidents.TrafficHurt != _hurtSeen;
            _hurtSeen = incidents.TrafficHurt;
            bool ours = _phase != Phase.Idle && _car.InUse && _car.Car == car;
            if (!hurt)
            {
                if (ours && _phase == Phase.Driving)
                {
                    Logger?.Info("Reckless driver: knocked somebody over at walking pace; they are getting up, and the driver has had a fright.");
                    Settle(now, "it knocked somebody over");
                }
                else if (!ours)
                {
                    Logger?.Info($"Traffic check (struck): '{TrafficTools.Name(car)}' knocked somebody over at {speed * 3.6f:F0} km/h; they are getting up, and it drives on.");
                }
                return;
            }
            _peopleDue = Mathf.Max(0f, _peopleDue - 1f);
            bool fatal = incidents.TrafficKilled;
            if (!ours)
            {
                Logger?.Info(FormattableString.Invariant($"Traffic check (struck): '{TrafficTools.Name(car)}' ran somebody down at {speed * 3.6f:F0} km/h ")
                             + FormattableString.Invariant($"({(fatal ? "fatal" : "injured")}). Still owing: {_peopleDue:F2} pedestrian(s)."));
                _striker = car;
                _jayAt = now;
                if (_phase != Phase.Idle || !_working || !car.gameObject.activeInHierarchy || !CrimeCars.IsOrdinary(car)
                    || !Begin(car, StreetRules.Kind.Tailgater, false, false, false, false, now, struck: true))
                {
                    Logger?.Info("Reckless driver: the car that hit them could not be taken over ("
                                 + (_phase != Phase.Idle ? "a reckless car is out already" : "no road ahead of it to drive on, or not an ordinary car")
                                 + "); it is given to the scene if its driver has pulled up beside them.");
                    return;
                }
                _striker = null;
            }
            _hurtAt = now;
            if (_phase != Phase.Driving)
            {
                Logger?.Info("Reckless driver: somebody else was run down as it ran.");
                return;
            }
            _scene = _car.Car.transform.position;
            if (StreetRules.Flees(_car.Kind, fatal, Random.Range(0, 100)))
            {
                _phase = Phase.Fleeing;
                _car.Flee();
                _until = now + FleeMost;
                _chaseWanted = Settings.Police;
                _patrolAt = 0f;
                News.Report(NewsKind.HitAndRun, _scene);
                TrafficAccidents.Notice(NotificationType.Warning, "reallife_streets_hit_run", 8f);
                Logger?.Info($"Reckless driver: ran somebody down ({(fatal ? "fatal" : "injured")}) and is not stopping"
                             + (_chaseWanted ? $"; a patrol car goes after it once it is {StreetRules.ChaseAfter:F0} m clear of the scene." : "; no police to go after it."));
            }
            else
            {
                _phase = Phase.Stopping;
                _car.Driver.Brake();
                _waitUntil = now + 8f;
                _patrolAt = 0f;
                Logger?.Info($"Reckless driver: ran somebody down ({(fatal ? "fatal" : "injured")}) and is stopping; the car stays for the scene.");
            }
        }

        /// <summary>
        /// From PedestrianIncidents as a scene starts round somebody a traffic car hit: the car that did it if its driver
        /// stopped (the scene's from here, passed on as it stands), else null.
        /// </summary>
        public VehicleComponent GiveUpStriker()
        {
            try
            {
                float now = Time.time;
                if (_phase == Phase.Stopping || _phase == Phase.Waiting)
                {
                    CallOff();
                    VehicleComponent car = _car.HandOver();
                    Idle(now);
                    Logger?.Info("Reckless driver: the driver who stopped is part of the scene now" + (car == null ? " (the car was no longer ours to give)." : "."));
                    return car;
                }
                // An ordinary car that hit them and was not taken over, if its driver has pulled up beside them.
                VehicleComponent last = _striker;
                if (last != null && (_phase == Phase.Idle || _car.Car != last) && now - _jayAt < StrikerFor && last.gameObject.activeInHierarchy
                    && CrimeCars.IsOrdinary(last) && last.GetCurrentSpeed() < StrikerSlow)
                {
                    _striker = null;
                    return last;
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
            return null;
        }

        // RecklessCar has met something (its box another vehicle's, a bend too fast, something fixed): the car is passed to
        // the accident scene as it stands, with the other car if there is one and the speed they met at.
        private void OnStruck(float now)
        {
            RecklessCar.Hit hit = _car.Struck;
            VehicleComponent other = hit == RecklessCar.Hit.Car ? _car.Other : null;
            float closing = _car.Closing;
            StreetRules.Kind kind = _car.Kind;
            if (_hurtAt > 0f && now - _hurtAt < HurtScene && hit != RecklessCar.Hit.Player)
            {
                // It had just run somebody down and has now hit something as well: one scene, round the person (a crash scene
                // here would end the moment they come to rest), so the car stays and is taken into it as the car that hit them.
                CallOff();
                _car.Stay();
                _phase = Phase.Waiting;
                _waitUntil = now + WaitForScene;
                Logger?.Info($"Reckless driver: having just run somebody down it hit something at {closing * 3.6f:F0} km/h and goes no further; it stays for the scene round them.");
                return;
            }
            CallOff();
            // What the cars keep for a moment after they met: what each had, or a little back the way it came for one that met
            // something that does not give. Read before the car is handed over.
            Vector3 own = hit == RecklessCar.Hit.Solid || hit == RecklessCar.Hit.Player ? _car.Velocity * -0.15f : _car.Velocity;
            Vector3 theirs = _car.OtherVelocity;
            VehicleComponent mine = _car.HandOver();
            Idle(now);
            if (mine == null)
            {
                Logger?.Info("Reckless driver: it hit something, but the car was no longer ours to hand to a scene.");
                return;
            }
            if (hit == RecklessCar.Hit.Player)
            {
                _blameless = mine;
                _blamelessUntil = now + BlamelessFor;
            }
            string notice = hit == RecklessCar.Hit.Player ? "reallife_streets_crash_player"
                : hit == RecklessCar.Hit.Bend ? "reallife_streets_crash_bend"
                : kind == StreetRules.Kind.Speeder ? "reallife_streets_crash_speeder"
                : kind == StreetRules.Kind.RedLight ? "reallife_streets_crash_redlight"
                : kind == StreetRules.Kind.Drunk ? "reallife_streets_crash_drunk"
                : "reallife_streets_crash_tailgater";
            TrafficAccidents accidents = TrafficAccidents.Instance;
            string what = hit == RecklessCar.Hit.Car ? "met '" + TrafficTools.Name(other) + "'"
                : hit == RecklessCar.Hit.Player ? "ran into the player's car"
                : hit == RecklessCar.Hit.Bend ? "took a bend faster than the road holds"
                : "ran into something fixed";
            if (accidents != null && accidents.BeginAt(mine, other, closing, notice, own, theirs))
            {
                _carsDue = Mathf.Max(0f, _carsDue - 1f);
                Logger?.Info(FormattableString.Invariant(
                    $"Reckless driver: the {kind} {what} at {closing * 3.6f:F0} km/h; the accident scene has both from here. Still owing: {_carsDue:F2} car accident(s)."));
            }
            else
            {
                TrafficTools.ReturnToTraffic(mine, "a reckless car that hit something while no scene could start");
                Logger?.Info($"Reckless driver: the {kind} {what} at {closing * 3.6f:F0} km/h, but no accident scene could start (another scene is on); it drives on.");
            }
        }

        /// <summary>True if that car is the reckless one, or was until it hit the player a moment ago: the player owes nothing for it.</summary>
        public static bool AtFault(VehicleComponent car)
        {
            RecklessDrivers drivers = Instance;
            if (drivers == null || car == null)
            {
                return false;
            }
            return (drivers._car != null && drivers._car.InUse && drivers._car.Car == car)
                   || (drivers._blameless == car && Time.time < drivers._blamelessUntil);
        }

        /// <summary>The game's own collision of the player's car with the reckless one, come before this saw the boxes meet: the same crash.</summary>
        public void HitPlayer(VehicleComponent car, float closing)
        {
            if (_phase != Phase.Idle && _car != null && _car.InUse && _car.Car == car)
            {
                _car.Report(RecklessCar.Hit.Player, null, closing);
            }
        }
    }
}
