using System.Collections.Generic;
using GleyTrafficSystem;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The taxi that answers a call: one of the game's own taxis, taken out of the traffic pool's hands, driven to
    /// the caller by the mod's driver, and stood there for them.
    ///
    /// WHAT IT IS. A vehicle of the traffic pool whose prefab carries the game's <c>TaxiController</c>: the same
    /// object the player clicks in the street. Nothing is added to it and nothing on it is changed, so when it
    /// stands the game's own outline, its "click to use taxi", its map in taxi mode, its fare and its ride work on
    /// it as on any taxi. Which one: an idle taxi of the pool if there is one, else one that is driving somewhere
    /// out of sight, which is taken off the road there. It is put on a lane point out of view
    /// (<see cref="TaxiRoads"/>) with the game's own <c>TrafficManager.LoadVehicle</c>, the call the game's taxi
    /// uses to come back after a ride.
    ///
    /// HOW IT COMES. Not by the traffic system's scripted route (<c>VehicleComponent.presetPath</c>, which the
    /// game's private driver uses): a vehicle on one drives through its first red light and stalls behind the first
    /// queue (the README, "Buses", has the reading). It is held the way a bus is (ignored by the traffic AI, an
    /// empty scripted route as the mark that keeps the game from taking it off the road, the game's check that
    /// removes a standing car switched off) and driven by <see cref="EmergencyDriver"/> along the lane points of
    /// its route, in its lane, behind whatever is ahead of it; <see cref="RouteGates"/> makes it pull up at red
    /// lights and before closed roads.
    ///
    /// WHERE IT STOPS. On the caller's lane, at the lane point nearest them. If the roadside there is free
    /// carriageway (<see cref="SceneParking.KerbRoom"/>, the test a car stopped by the police is given), it pulls
    /// in to the kerb over its last metres and is braked there; else it stops on the lane's line. Held up within
    /// a few metres of its stopping place (the caller standing in the road for it), it stops where it is.
    ///
    /// HOW IT STANDS. Still held and frozen (the traffic system's springs have no damper), so nothing moves it,
    /// the game does not take it off the road and no traffic rule drives it away, for as long as its owner keeps
    /// it. The game's click does its own "stop now" on it, which changes nothing on a held vehicle.
    ///
    /// Ticked four times a second by the director while it is out; the driver runs in the physics step only
    /// while it drives. Everything put on the vehicle comes off in every way it can end (<see cref="Release"/>,
    /// and <see cref="Abandon"/> when the pool has taken the vehicle back).
    /// </summary>
    public sealed class TaxiCab
    {
        public enum Phase { Idle, Approaching, Standing }

        /// <summary>How it is let go: taken off the road, handed back to the traffic AI where it is, or whichever of the two nobody sees.</summary>
        public enum Leave { Remove, AsTraffic, Unseen }

        private const float Cruise = 11f;            // m/s, 40 km/h
        private const float JunctionSpeed = 7f;
        private const float NearEnough = 14f;        // held up with no more of its route left than this, it stops where it is and has arrived
        private const float KerbLookAt = 17f;        // metres of route left at which the roadside is looked at, once
        private const float KerbStraight = 12f;      // degrees it may be off the lane's line then: it is not pulled in out of a bend
        private const float KerbBrakeAt = 4.5f;      // pulled in, it is braked with this much left
        private const float NotNearer = 70f;         // a taxi driving nearer the player than this is not taken off the road, seen or not

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

        public TaxiCab(GameObject host)
        {
            _driver = host.AddComponent<EmergencyDriver>();
        }

        public VehicleComponent Car { get; private set; }

        /// <summary>The game's own taxi on the vehicle: what the game's map holds while a destination is picked from it.</summary>
        public TaxiController Controller { get; private set; }

        public Phase State { get; private set; }

        /// <summary>The lane point it stops at, and the way the lane runs there.</summary>
        public Vector3 Stop { get; private set; }
        public Vector3 Along { get; private set; }

        /// <summary>What it is standing for on its way, if anything, and for how many seconds.</summary>
        public RoadHold Waiting { get; private set; }
        public float WaitingFor => Waiting == RoadHold.None ? 0f : Time.time - _heldSince;

        /// <summary>Seconds since it last got a metre nearer the end of its route.</summary>
        public float NoProgressFor => State == Phase.Approaching ? Time.time - _progressAt : 0f;

        /// <summary>Metres of its route still to drive.</summary>
        public float Remaining { get; private set; }

        /// <summary>Metres of road from where it was put to where it stops.</summary>
        public float StartedBack { get; private set; }

        /// <summary>Seconds since it was put on the road; standing, since it came to rest.</summary>
        public float Out => State == Phase.Idle ? 0f : Time.time - _since;

        /// <summary>Seconds it took from being put on the road to standing.</summary>
        public float Took { get; private set; }

        /// <summary>True when it pulled in to the kerb to stand; false when it stands on the lane's line.</summary>
        public bool AtKerb { get; private set; }

        /// <summary>Which vehicle it is and how it was come by, for the log.</summary>
        public string Chassis { get; private set; }

        /// <summary>True while its vehicle is still the one it took and still ours (the pool hands vehicles out again).</summary>
        public bool Held => Car != null && TrafficTools.IsHeld(Car) && Car.presetPath == _marker;

        /// <summary>True while the vehicle is where the camera looks, by either of the mod's tests of that.</summary>
        public bool InView => Car != null && Seen(Car.transform.position);

        /// <summary>True once after the game or a scene took the vehicle from under it: asked by whoever must say so.</summary>
        public bool TakeLost()
        {
            bool lost = _lost;
            _lost = false;
            return lost;
        }

        private bool _lost;

        // ---------------------------------------------------------------- which taxi

        /// <summary>
        /// A taxi of the traffic pool that can answer a call, or null: an idle one, else one that is driving out of
        /// sight (which the caller takes off the road before it is put on its route). Never one somebody holds: the
        /// game's private driver, a scene of the mod's, a taxi the player has hailed (those are in view).
        /// One pass over the pool, when a call is made.
        /// </summary>
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
                    continue;   // (a scripted route, even an empty one, is somebody's)
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

        // ---------------------------------------------------------------- putting it on the road

        /// <summary>
        /// Puts a taxi on the start of a plan, heading for the caller. False with why. <paramref name="startTaken"/>
        /// says traffic stood on the lane point it was to start from: another plan may do.
        /// </summary>
        /// <param name="pick">The taxi <see cref="Pick"/> gave, in this same frame, and whether it was idle.</param>
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
            VehicleComponent car = TrafficTools.Load(prefab, start);   // an idle taxi of that prefab (the one just taken off is one), never one taken by its model
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

        // Ours from here on: the traffic AI leaves it alone, and the empty scripted route keeps the game from taking it
        // off the road by distance or for standing (the same hold a bus and a response unit have on their vehicles).
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

        // The driver sets off along what is left of the route (its first place is the next one ahead).
        private void Begin()
        {
            _gate = -1;
            _gateWhy = RoadHold.None;
            Waiting = RoadHold.None;
            _driver.BeginRoute(Car, _route, NearEnough);
            _driver.CruiseSpeed = Cruise;
            _driver.MayPass = false;   // it keeps its lane: closes up behind what is ahead and waits, as traffic does
            _progressAt = Time.time;
            _progressLeft = float.MaxValue;
        }

        // ---------------------------------------------------------------- on the road

        /// <summary>Four times a second while it is out.</summary>
        public void Tick(float now)
        {
            if (State == Phase.Idle)
            {
                return;
            }
            if (!Held || !TrafficTools.Ready)
            {
                // Taken by the game (traffic shut down for a building or a time skip, the pool reused it) or by a scene.
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

        // What the driver does not know of (red lights, closed roads), the roadside at the end, and how it is getting on.
        private void Drive(float now)
        {
            if (_braking)
            {
                Remaining = _driver.RouteLeft;
                return;   // pulled in and coming to rest: nothing more is asked of it
            }
            if (_gate >= 0 && !_driver.Active)
            {
                // It has pulled up where it was told to.
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
                    AtKerb = false;        // (a drive begun again is a driver reset: the roadside is looked at again when it comes up)
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
                return;   // it has arrived at the end of its route: Tick's turn
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

        // The last metres: in to the kerb if it is coming straight along the lane and the roadside there is free
        // carriageway (not pavement, not another lane, nothing standing in it). Asked once.
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

        // Standing for more than a moment: the traffic system's springs have no damper, so a vehicle of ours is either driven or frozen.
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

        /// <summary>
        /// Where it came to rest: metres its middle is from the lane point it was to stop at, along the lane (past
        /// it is positive) and across it (towards the kerb is positive), and degrees off the lane's direction.
        /// </summary>
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

        // ---------------------------------------------------------------- letting it go

        /// <summary>
        /// Every way out while the vehicle is ours. Handed back to the traffic AI it drives on from where it is as
        /// the ordinary taxi it is (always so when the game's ride begins: the game then does with it what it does
        /// with any taxi). Taken off the road it goes back to the pool: only out of view, or when the city is closing.
        /// </summary>
        /// <param name="by">Who is letting it go, for the log (a constant).</param>
        public void Release(Leave how, string by)
        {
            if (State == Phase.Idle)
            {
                return;
            }
            VehicleComponent car = Car;
            bool held = Held;
            bool running = TrafficManager.HasInstance && TrafficManager.IsInitialized;
            bool takeOff = how == Leave.Remove || !held || !TrafficTools.Ready
                           || (how == Leave.Unseen && !Seen(car.transform.position) && car.visibilityScript != null && car.visibilityScript.IsNotInView());
            if (_driver != null)   // (gone already when the city is closing)
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

        /// <summary>True if the last release handed the vehicle back to traffic; false if it was taken off the road. For the log.</summary>
        public bool LeftAsTraffic { get; private set; }

        // The vehicle is no longer ours to touch (the pool reused it, traffic was shut down): nothing is undone on it but what is still ours.
        private void Abandon()
        {
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
                    car.rb.isKinematic = _wasKinematic;   // still the same outing of the same vehicle: not left frozen
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
