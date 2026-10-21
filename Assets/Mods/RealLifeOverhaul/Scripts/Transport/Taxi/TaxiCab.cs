using System.Collections.Generic;
using GleyTrafficSystem;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The taxi that answers a call: one of the game's own taxis from the pool, unchanged, so its click, map, fare and ride
    /// work. Held as a bus is and driven by <see cref="EmergencyDriver"/>, stopped at red lights by <see cref="RouteGates"/>
    /// (the traffic's own scripted route runs its first). Ticked four times a second by the director while out.
    /// </summary>
    public sealed class TaxiCab
    {
        public enum Phase { Idle, Approaching, Standing }

        /// <summary>How it is let go: taken off the road, handed back to the traffic AI, or whichever nobody sees.</summary>
        public enum Leave { Remove, AsTraffic, Unseen }

        private const float Cruise = 11f;            // m/s, 40 km/h
        private const float JunctionSpeed = 7f;      // m/s
        private const float NearEnough = 14f;        // metres: held up with no more of its route left, it stops where it is and has arrived
        private const float KerbLookAt = 17f;        // metres of route left at which the roadside is looked at, once
        private const float KerbStraight = 12f;      // degrees it may be off the lane's line then, so it is not pulled in out of a bend
        private const float KerbBrakeAt = 4.5f;      // metres left at which it is braked, pulled in
        private const float NotNearer = 70f;         // metres: a taxi driving nearer the player is not taken off the road, seen or not

        private readonly EmergencyDriver _driver;
        private readonly List<int> _marker = new List<int>(0);       // an empty scripted route: the mark that the vehicle is ours
        private readonly List<Vector3> _route = new List<Vector3>(160);
        private readonly List<int> _routePoints = new List<int>(160);
        private Waypoint[] _lanePoints;
        private AiCarRescueCheck _rescue;
        private bool _parked;
        private bool _wasKinematic;
        private bool _kerbLooked;
        private bool _braking;
        private int _gate = -1;          // the place of its route it has been told to pull up at, or -1
        private RoadHold _gateWhy;
        private float _since;
        private float _heldSince;
        private float _progressAt;
        private float _progressLeft;
        private bool _lost;

        public TaxiCab(GameObject host)
        {
            _driver = host.AddComponent<EmergencyDriver>();
        }

        public VehicleComponent Car { get; private set; }

        /// <summary>The game's own taxi on the vehicle, which the game's map holds while a destination is picked.</summary>
        public TaxiController Controller { get; private set; }

        public Phase State { get; private set; }

        public Vector3 Stop { get; private set; }    // the lane point it stops at
        public Vector3 Along { get; private set; }   // the way the lane runs there

        public RoadHold Waiting { get; private set; }
        public float WaitingFor => Waiting == RoadHold.None ? 0f : Time.time - _heldSince;   // seconds

        public float NoProgressFor => State == Phase.Approaching ? Time.time - _progressAt : 0f;

        public float Remaining { get; private set; }     // metres of its route still to drive

        public float StartedBack { get; private set; }   // metres of road from where it was put to where it stops

        /// <summary>Seconds since it was put on the road; standing, since it came to rest.</summary>
        public float Out => State == Phase.Idle ? 0f : Time.time - _since;

        public float Took { get; private set; }          // seconds from being put on the road to standing

        public bool AtKerb { get; private set; }         // it pulled in to the kerb to stand

        public string Chassis { get; private set; }      // which vehicle it is and how it was come by, for the log

        /// <summary>True while its vehicle is still the one it took and still ours: the pool hands vehicles out again.</summary>
        public bool Held => Car != null && TrafficTools.IsHeld(Car) && Car.presetPath == _marker;

        public bool InView => Car != null && Seen(Car.transform.position);

        /// <summary>True once after the game or a scene took the vehicle from under it.</summary>
        public bool TakeLost()
        {
            bool lost = _lost;
            _lost = false;
            return lost;
        }

        /// <summary>A taxi of the pool that can answer, idle or driving out of sight, never one somebody holds; null if none.</summary>
        public static VehicleComponent Pick(out bool idle)
        {
            idle = false;
            if (!TrafficTools.Ready)
            {
                return null;
            }
            List<VehicleComponent> pool = Manager.GetVehicleList();
            TrafficManager traffic = TrafficManager.Instance;
            Vector3 player = PlayerHelper.GetPosition();
            VehicleComponent driving = null;
            for (int i = 0; i < pool.Count; i++)
            {
                VehicleComponent car = pool[i];
                if (car == null || car.prefab == null || car.presetPath != null || car.GetComponent<TaxiController>() == null
                    || car.GetComponent<PrivateDriverVehicle>() != null)
                {
                    continue;   // a scripted route, even an empty one, is somebody's
                }
                if (!car.gameObject.activeInHierarchy)
                {
                    idle = true;
                    return car;
                }
                if (driving != null)
                {
                    continue;
                }
                Vector3 at = car.transform.position;
                if (traffic.IsVehicleIgnored(car.GetIndex()) || car.visibilityScript == null || !car.visibilityScript.IsNotInView()
                    || Seen(at) || CitySpots.Flat(at - player) < NotNearer * NotNearer)
                {
                    continue;
                }
                driving = car;
            }
            return driving;
        }

        private static bool Seen(Vector3 place)
        {
            return CitySpots.InView(place) || StreetSpots.IsInView(place);
        }

        /// <summary>Puts the picked taxi on the start of a plan. False with why; <paramref name="startTaken"/>: traffic stood there.</summary>
        public bool Send(TaxiPlan plan, TaxiRoads roads, VehicleComponent pick, bool idle, out string refusal, out bool startTaken)
        {
            refusal = null;
            startTaken = false;
            if (State != Phase.Idle)
            {
                refusal = "a taxi is still out";
                return false;
            }
            Waypoint start = plan != null && plan.Valid && roads != null ? roads.Point(plan.Start) : null;
            if (!TrafficTools.Ready || start == null)
            {
                refusal = "traffic is not running";
                return false;
            }
            if (pick == null || pick.prefab == null)
            {
                refusal = "no taxi of the traffic pool is idle or driving out of sight";
                return false;
            }
            GameObject prefab = pick.prefab;
            if (!idle)
            {
                TrafficTools.Remove(pick, "a taxi taken from out of sight to answer a call");
            }
            VehicleComponent car = TrafficTools.Load(prefab, start);   // an idle taxi of that prefab, never one taken by its model
            if (car == null)
            {
                startTaken = true;
                refusal = "traffic stands on the lane point it was to start from";
                return false;
            }
            var controller = car.GetComponent<TaxiController>();
            if (controller == null)
            {
                TrafficTools.Remove(car, "a vehicle the pool gave for a taxi that is none");
                refusal = "the vehicle the traffic pool gave is not a taxi";
                return false;
            }
            Take(car, controller, idle ? "an idle taxi of the pool" : "a taxi that was driving out of sight");
            _lanePoints = roads.Points;
            _route.AddRange(plan.Route);
            _routePoints.AddRange(plan.Points);
            Vector3 heading = _route[0] - plan.StartAt;
            heading.y = 0f;
            if (heading.sqrMagnitude > 0.01f)
            {
                car.transform.rotation = Quaternion.LookRotation(heading);
            }
            Stop = plan.Stop;
            Along = plan.Along;
            StartedBack = plan.Metres;
            State = Phase.Approaching;
            _since = Time.time;
            Begin();
            Remaining = _driver.RouteLeft;
            return true;
        }

        // Ours from here on: the empty scripted route keeps the game from taking it off the road.
        private void Take(VehicleComponent car, TaxiController controller, string how)
        {
            Car = car;
            Controller = controller;
            _parked = false;
            _kerbLooked = false;
            _braking = false;
            _lost = false;
            _gate = -1;
            _gateWhy = RoadHold.None;
            Waiting = RoadHold.None;
            AtKerb = false;
            Took = 0f;
            _route.Clear();
            _routePoints.Clear();
            Manager.SetVehicleIgnored(car.GetIndex(), true);
            _marker.Clear();
            car.presetPath = _marker;
            _rescue = TrafficTools.SuspendRescue(car);
            TrafficTools.Touch(car, "taken out of traffic as a called taxi");
            Chassis = "'" + TrafficTools.Name(car) + "' (" + how + ")";
        }

        private void Begin()
        {
            _gate = -1;
            _gateWhy = RoadHold.None;
            Waiting = RoadHold.None;
            _driver.BeginRoute(Car, _route, NearEnough);
            _driver.CruiseSpeed = Cruise;
            _driver.MayPass = false;   // it keeps its lane and waits behind what is ahead, as traffic does
            _progressAt = Time.time;
            _progressLeft = float.MaxValue;
        }

        public void Tick(float now)
        {
            if (State == Phase.Idle)
            {
                return;
            }
            if (!Held || !TrafficTools.Ready)
            {
                // Taken by the game (traffic shut down, the pool reused it) or by a scene.
                Abandon();
                _lost = true;
                return;
            }
            if (State != Phase.Approaching)
            {
                return;
            }
            Drive(now);
            if (!_driver.Active && _gate < 0 && (_driver.Arrived || _braking))
            {
                Park();
                State = Phase.Standing;
                Took = now - _since;
                _since = now;
                Remaining = 0f;
                Waiting = RoadHold.None;
                TrafficTools.Touch(Car, "a called taxi standing for its caller");
            }
        }

        // What the driver does not know of (red lights, closed roads), the kerb at the end, and progress.
        private void Drive(float now)
        {
            if (_braking)
            {
                Remaining = _driver.RouteLeft;
                return;   // pulled in and coming to rest
            }
            if (_gate >= 0 && !_driver.Active)
            {
                if (!_parked)
                {
                    Park();
                    Waiting = _gateWhy;
                    _heldSince = now;
                }
                if (RouteGates.Open(_route, _routePoints, _gate, _lanePoints, now - _heldSince, out RoadHold still))
                {
                    Unpark();
                    int done = Mathf.Min(_driver.NextPoint, _route.Count - 1);
                    _route.RemoveRange(0, done);
                    _routePoints.RemoveRange(0, done);
                    float mark = _progressAt;
                    float left = _progressLeft;
                    AtKerb = false;        // a drive begun again resets the driver: the roadside is looked at again
                    _kerbLooked = false;
                    Begin();
                    _progressAt = mark;
                    _progressLeft = left;
                }
                else
                {
                    Waiting = still;
                }
                Remaining = _driver.RouteLeft;
                return;
            }
            if (!_driver.Active)
            {
                return;   // arrived at the end of its route: Tick's turn
            }
            int gate = RouteGates.Find(_route, _routePoints, _driver.NextPoint, Car.transform.position, _driver.Speed, _lanePoints, out RoadHold why, out bool junction);
            if (gate != _gate)
            {
                _gate = gate;
                _driver.PullUpAt(gate);
            }
            _gateWhy = why;
            _driver.CruiseSpeed = junction ? JunctionSpeed : Cruise;
            float toGo = _driver.RouteLeft;
            if (gate < 0)
            {
                if (!_kerbLooked && toGo <= KerbLookAt)
                {
                    _kerbLooked = true;
                    PullIn();
                }
                if (AtKerb && toGo <= KerbBrakeAt)
                {
                    _braking = true;
                    _driver.Brake();
                }
            }
            float heldUp = _driver.HeldFor;
            if (heldUp > 1f)
            {
                if (Waiting != RoadHold.Traffic)
                {
                    Waiting = RoadHold.Traffic;
                    _heldSince = now - heldUp;
                }
            }
            else
            {
                Waiting = RoadHold.None;
            }
            Remaining = toGo;
            if (Remaining < _progressLeft - 1f)
            {
                _progressLeft = Remaining;
                _progressAt = now;
            }
        }

        // In to the kerb for the last metres if it comes straight along the lane and the roadside is free.
        private void PullIn()
        {
            Vector3 forward = Car.transform.forward;
            forward.y = 0f;
            Vector3 toStop = Stop - Car.transform.position;
            toStop.y = 0f;
            if (forward.sqrMagnitude < 0.01f || toStop.sqrMagnitude < 1f
                || Vector3.Angle(forward, Along) > KerbStraight || Vector3.Angle(forward, toStop) > KerbStraight)
            {
                return;
            }
            float over = SceneParking.KerbRoom(Car, null);
            if (over > 0f)
            {
                _driver.PullOver(over);
                AtKerb = true;
            }
        }

        // Frozen while it stands: the traffic system's springs have no damper.
        private void Park()
        {
            if (_parked || Car == null)
            {
                return;
            }
            _driver.Stop();
            Rigidbody body = Car.rb;
            _wasKinematic = body.isKinematic;
            body.isKinematic = true;
            _parked = true;
            TrafficTools.Touch(Car, "a called taxi standing (kinematic)");
        }

        private void Unpark()
        {
            if (!_parked)
            {
                return;
            }
            _parked = false;
            if (Car != null && Car.rb != null)
            {
                Car.rb.isKinematic = _wasKinematic;
            }
        }

        /// <summary>Where it came to rest against its stop: metres along and across the lane, degrees off it.</summary>
        public void Measure(out float along, out float aside, out float angle)
        {
            along = aside = angle = 0f;
            if (Car == null)
            {
                return;
            }
            TrafficTools.Footprint(Car, out Vector3 centre, out Vector3 forward, out _, out _);
            Vector3 right = Vector3.Cross(Vector3.up, Along);
            Vector3 offset = centre - Stop;
            offset.y = 0f;
            along = Vector3.Dot(offset, Along);
            aside = Vector3.Dot(offset, right);
            angle = Vector3.Angle(forward, Along);
        }

        /// <summary>Every way out while ours: back to the traffic AI (always for the game's ride), or off the road out of view.</summary>
        public void Release(Leave how, string by)
        {
            if (State == Phase.Idle)
            {
                return;
            }
            VehicleComponent car = Car;
            bool held = Held;
            OverlayGuard.Release(Controller);   // the game's card over it goes first: the pool keeps the vehicle, so the card would not see it gone
            bool running = TrafficManager.HasInstance && TrafficManager.IsInitialized;
            bool takeOff = how == Leave.Remove || !held || !TrafficTools.Ready
                           || (how == Leave.Unseen && !Seen(car.transform.position) && car.visibilityScript != null && car.visibilityScript.IsNotInView());
            if (_driver != null)   // gone already when the city is closing
            {
                if (held)
                {
                    _driver.Stop();
                }
                else
                {
                    _driver.Forget();
                }
            }
            if (car != null)
            {
                if (_parked && car.rb != null)
                {
                    car.rb.isKinematic = _wasKinematic;
                }
                if (car.presetPath == _marker)
                {
                    car.presetPath = null;
                }
            }
            if (_rescue != null)
            {
                _rescue.enabled = true;
            }
            if (held && running)
            {
                if (takeOff)
                {
                    TrafficTools.Remove(car, by);
                }
                else
                {
                    TrafficTools.ReturnToTraffic(car, by);
                }
            }
            LeftAsTraffic = held && running && !takeOff;
            Forget();
        }

        public bool LeftAsTraffic { get; private set; }  // the last release handed the vehicle back to traffic, for the log

        // The vehicle is no longer ours: only what is still ours on it is undone.
        private void Abandon()
        {
            OverlayGuard.Release(Controller);
            if (_driver != null)
            {
                _driver.Forget();
            }
            VehicleComponent car = Car;
            if (car != null && car.presetPath == _marker)
            {
                car.presetPath = null;
                if (_parked && car.rb != null)
                {
                    car.rb.isKinematic = _wasKinematic;   // the same outing of the same vehicle: not left frozen
                }
                if (TrafficManager.HasInstance && TrafficManager.IsInitialized && car.gameObject.activeInHierarchy)
                {
                    TrafficTools.Remove(car, "a called taxi that was no longer held");
                }
            }
            if (_rescue != null)
            {
                _rescue.enabled = true;
            }
            Forget();
        }

        private void Forget()
        {
            _rescue = null;
            _lanePoints = null;
            _parked = false;
            _kerbLooked = false;
            _braking = false;
            _gate = -1;
            _gateWhy = RoadHold.None;
            Waiting = RoadHold.None;
            _route.Clear();
            _routePoints.Clear();
            Car = null;
            Controller = null;
            Remaining = 0f;
            State = Phase.Idle;
        }
    }
}
