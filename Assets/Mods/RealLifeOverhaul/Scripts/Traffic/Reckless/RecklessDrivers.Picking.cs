using System;
using System.Collections.Generic;
using GleyTrafficSystem;
using Helpers;
using Streets.Pedestrians;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Reckless drivers: picking the car to drive recklessly, and the people to send across the road.</summary>
    public sealed partial class RecklessDrivers : MonoBehaviour
    {
        private const float PatrolSees = 0.12f;      // of the speeders and light runners: a passing patrol car happens to see it

        // Makes one traffic car the reckless one (car, or null to pick); false if none. struck: one that just hit somebody.
        private bool Begin(VehicleComponent car, StreetRules.Kind kind, bool accidentDue, bool blind, bool never, bool test, float now, bool struck = false)
        {
            if (_phase != Phase.Idle)
            {
                return false;
            }
            Vector3 player = PlayerHelper.GetPosition();
            using var piece = SessionWatch.Piece("streets: a reckless driver being picked");
            // (A car whose road ahead is too short is passed over: the next one is tried, a few times.)
            for (int attempt = 0; attempt < 4; attempt++)
            {
                VehicleComponent picked = car != null ? car : Pick(player, kind, test);
                if (picked == null)
                {
                    return false;
                }
                if (_car.Take(picked, kind, accidentDue, blind, never))
                {
                    break;
                }
                if (car != null || attempt == 3)
                {
                    return false;
                }
            }
            VehicleComponent taken = _car.Car;
            PedestrianIncidents incidents = PedestrianIncidents.Instance;
            _phase = Phase.Driving;
            _test = test;
            _until = now + (never ? 90f : StreetRules.Lasts(Random.value));
            _nextTick = now;
            _nextScare = now + ScareEvery;
            _nextHorn = now;
            _hurtSeen = incidents != null ? incidents.TrafficHurt : 0;
            _lightsSeen = 0;
            _hurtAt = 0f;
            _presenceLooked = false;
            _chaseWanted = false;
            _complies = false;
            _patrolAt = !test && !struck && Settings.Police && StreetRules.PatrolNotices(kind) && Random.value < PatrolSees ? now + Random.Range(4f, 10f) : 0f;
            if (struck)
            {
                Logger?.Info($"Reckless driver: '{TrafficTools.Name(taken)}', an ordinary car that has just hit somebody {Vector3.Distance(player, taken.transform.position):F0} m from the player, "
                             + "is taken over where it is: its driver stops for them or runs.");
                return true;
            }
            Logger?.Info($"Reckless driver: '{TrafficTools.Name(taken)}' {Vector3.Distance(player, taken.transform.position):F0} m from the player is now a {kind}"
                         + $" ({(never ? "test: brakes for no vehicle" : accidentDue ? "an accident is due: no margin" : "with a margin: gets away with it unless somebody else errs")}"
                         + $"{(blind ? ", does not see people on foot" : string.Empty)}), cruising at {_car.Cruise * 3.6f:F0} km/h for up to {_until - now:F0} s."
                         + FormattableString.Invariant($" Owing: {_carsDue:F2} car accident(s), {_peopleDue:F2} pedestrian(s)."));
            return true;
        }

        // An ordinary private car on the move, in view or coming the player's way, far enough for the change not to show.
        private static VehicleComponent Pick(Vector3 player, StreetRules.Kind kind, bool test)
        {
            List<VehicleComponent> pool = Manager.GetVehicleList();
            if (pool.Count == 0)
            {
                return null;
            }
            const float least = 12f;   // a test's own range: the rule's (StreetRules.InPickRange) is for the cars nobody asked for
            const float most = TestWithin;
            float speed = test ? 6f : StreetRules.PickSpeedKmh;
            int start = Random.Range(0, pool.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                VehicleComponent car = pool[(start + i) % pool.Count];
                if (!CrimeCars.IsOrdinary(car) || car.GetCurrentSpeed() < speed)
                {
                    continue;
                }
                Vector3 at = car.transform.position;
                Vector3 toPlayer = player - at;
                float sqr = toPlayer.sqrMagnitude;
                if (test ? sqr < least * least || sqr > most * most : !StreetRules.InPickRange(Mathf.Sqrt(sqr)))
                {
                    continue;
                }
                if (!test && !StreetSpots.IsInView(at) && Vector3.Dot(car.transform.forward, toPlayer) <= 0f)
                {
                    continue;
                }
                if (kind == StreetRules.Kind.RedLight && !LightOnItsLane(car, test ? 220f : 130f))
                {
                    continue;   // nothing for it to run
                }
                return car;
            }
            return null;
        }

        // True if the car's own lane reaches a traffic light within so many metres (a lane does not fork before its stop line).
        private static bool LightOnItsLane(VehicleComponent car, float within)
        {
            TrafficManager traffic = TrafficManager.Instance;
            Waypoint point = traffic.GetTargetWaypoint(car.GetIndex());
            float walked = 0f;
            for (int step = 0; step < 60 && point != null && walked < within; step++)
            {
                if (point.enter)
                {
                    return !point.giveWay;
                }
                if (point.neighbors == null || point.neighbors.Count == 0)
                {
                    return false;
                }
                Waypoint next = traffic.GetWaypoint(point.neighbors[0]);
                if (next == null)
                {
                    return false;
                }
                walked += Vector3.Distance(point.position, next.position);
                point = next;
            }
            return false;
        }

        // Sends somebody across the road in front of a car. mode 0: as it comes (a near miss unless one is due); 1: a test, any
        // car coming; 2: a test that ends with them hit (they stop in the lane, the car made reckless and blind to them); 3: a
        // test, they step out close (a hard brake, the horn); 4: they stand in the lane until the car behind hoots; 5: they step
        // out too late for an ordinary car (the hit is its own). False if no pedestrian and car stand right for it.
        private bool StepOut(bool late, int mode, float now, out float metres)
        {
            metres = 0f;
            if (_jay.Active || _phase != Phase.Idle)
            {
                return false;
            }
            Vector3 player = PlayerHelper.GetPosition();
            using var piece = SessionWatch.Piece("streets: somebody to step into the road being picked");
            if (!Jaywalker.Find(player, mode == 0 ? 10f : 6f, mode == 0 ? 60f : 90f, late, mode, out Pedestrian walker, out VehicleComponent car, out Vector3 across, out Vector3 lane))
            {
                return false;
            }
            Jaywalker.Step step = mode == 2 ? Jaywalker.Step.Stay : mode == 3 ? Jaywalker.Step.Close : mode == 4 ? Jaywalker.Step.Dawdle
                : mode == 5 ? Jaywalker.Step.Late : Jaywalker.Step.Across;
            if (!_jay.Begin(walker, car, across, lane, step, now))
            {
                return false;
            }
            metres = Vector3.Distance(player, walker.transform.position);
            _jayAt = now;
            _nextTick = now;
            PedestrianIncidents incidents = PedestrianIncidents.Instance;
            _hurtSeen = incidents != null ? incidents.TrafficHurt : 0;
            CarStrikes strikes = CarStrikes.Instance;
            if (strikes != null && Settings.Ragdolls)
            {
                strikes.Watch(car);
            }
            if ((late && mode == 0) || mode == 5)
            {
                // Somebody is to be hit by an ordinary car: Traffic/Solid lets this one car reach this one person.
                SolidStreets.Owe(car, walker);
            }
            if (mode == 2 && !Begin(car, StreetRules.Kind.Tailgater, true, true, false, true, now))
            {
                _jay.End("the car could not be made the reckless one");
                return false;
            }
            return true;
        }

        private int Crowd(float now, out float metres)
        {
            metres = 0f;
            Vector3 player = PlayerHelper.GetPosition();
            using var piece = SessionWatch.Piece("streets: a crowd to cross the road being picked");
            int sent = 0;
            for (int i = 0; i < _crowd.Length; i++)
            {
                if (_crowd[i].Active)
                {
                    continue;
                }
                // Whoever is in the road for us already is passed over by the search: their own script is off.
                if (!Jaywalker.FindCrosser(player, 5f, 70f, out Pedestrian walker, out Vector3 across, out Vector3 lane))
                {
                    break;
                }
                if (_crowd[i].Begin(walker, null, across, lane, Jaywalker.Step.Across, now))
                {
                    metres = sent == 0 ? Vector3.Distance(player, walker.transform.position) : metres;
                    sent++;
                }
            }
            _crowdOut = 0;
            for (int i = 0; i < _crowd.Length; i++)
            {
                _crowdOut += _crowd[i].Active ? 1 : 0;
            }
            _nextTick = now;
            return sent;
        }
    }
}
