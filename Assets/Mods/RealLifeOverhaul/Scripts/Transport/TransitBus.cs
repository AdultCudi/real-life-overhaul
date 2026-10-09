using System;
using System.Collections.Generic;
using GleyTrafficSystem;
using Helpers;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One bus: a vehicle of the traffic pool that the mod holds and drives along its line itself,
    /// wearing the modelled <c>minibus</c> body (or, without the prop bundle, the game's panel
    /// van with a lit route board on its roof). The game has no bus.
    ///
    /// WHY THE MOD DRIVES IT. Up to 0.43.0 the bus was handed to the traffic system with a
    /// scripted route (<c>VehicleComponent.presetPath</c>), the way the game moves its
    /// private-driver car. Read against the traffic system's own code that cannot work for a
    /// line: a vehicle with a scripted route is given its next point as soon as it is within
    /// 5 m of the last one (TrafficManager.FixedUpdate, DrivingAI.WaypointRequested), before
    /// the code that holds a vehicle at a red light or a give-way point is ever reached, and
    /// the "stop here" a red light or a give-way point puts on it is taken off again only for
    /// vehicles still heading for that very point (WaypointManager.TrafficLightChanged). So
    /// such a vehicle drives through its first red light and from then on has a stop order
    /// that nothing lifts: it creeps from lane point to lane point, and the first time it
    /// comes to rest within 10 m of its next point (behind a queue, say) it never moves again
    /// (DriveJob.StopInPoint). Now the vehicle is taken out of the traffic AI's hands the way
    /// a response unit is (ignored by traffic, an empty scripted route as the mark that keeps
    /// the game from taking it off the road) and driven by <see cref="EmergencyDriver"/> along
    /// the line's own lane points: it stops behind whatever is ahead of it, never leaves its
    /// lane (no overtaking: it is a bus), reaches the end of its route to within a metre, and
    /// this class makes it obey what the driver does not know of: it pulls up at a red light
    /// and before lane points a scene has closed, and goes on when they open.
    ///
    /// WHICH VEHICLE. Not luck: an idle van of the pool if there is one; else a van that is
    /// driving somewhere out of sight (taken off the road there and put on the line); and,
    /// when the body is the modelled one, which hides the vehicle under it wholly, any other
    /// ordinary car or truck of 4.2 to 7.5 m in the same two ways. Never an ambulance, a taxi,
    /// a limousine, anybody's private driver or a vehicle a scene holds.
    ///
    /// Ticked four times a second by the director while it is out; the driver itself runs in
    /// the physics step only while it drives. Everything put on the vehicle comes off when it
    /// is let go, and by itself if the pool takes the vehicle back (<see cref="BusDress"/>).
    /// </summary>
    public sealed class TransitBus
    {
        public enum Phase { Idle, Approaching, Standing, Leaving }
        public enum Hold { None, Light, Traffic, Closure }

        // The modelled body (PropAssets/README.md, "Service vehicles"; PropAssets/scripts/checks_service.py holds the model to
        // these). It is the Mersaidi Dash's length and has that van's axles, so on a Dash its wheels are the van's own.
        public const float BodyLength = 6.66f;
        public const float BodyHalfWidth = 1.05f;
        private const float BodyAxleFront = 2.40f;   // its axles, from its own middle
        private const float BodyAxleRear = -1.62f;
        private const float BodyWheelbase = BodyAxleFront - BodyAxleRear;
        private const float BodyDoorAhead = 0.76f;   // its door's middle, ahead of the body's middle
        private const float BodyDoorOut = 0.30f;     // where somebody stands to step in (board_step: the step's edge 0.30 to 0.35 m ahead of the feet)
        private const float BodyBoardAhead = 2.22f;  // the flat of the roof the route board stands on
        private const float BodyNumberBack = 2.95f;  // the pod at the back the line number stands on
        private const float AxlesFit = 0.25f;        // a vehicle whose wheelbase is within this of the body's carries it with the wheels where its own are

        private const float Cruise = 11f;            // m/s, 40 km/h
        private const float JunctionSpeed = 7f;
        private const float NoPassing = 0.001f;      // EmergencyDriver.PullOver with next to nothing: stays in its lane, passes nobody
        private const float StartBack = 170f;        // metres of line before the stop it starts from, if it can
        private const float StartBackMost = 330f;
        private const float StartBackLeast = 60f;
        private const float StartApart = 14f;        // metres between two places tried to start from
        private const int StartTries = 8;
        private const float LookAhead = 45f;         // metres of its route looked along for a red light or a closed road
        private const float LitWithin = 170f;        // metres from the player within which a red light is waited at
        private const float LightPatience = 45f;     // seconds at a red light nobody is looking at before it is taken to be one the game is not running
        private const float FinalShort = 1.2f;       // the driver stops this far short of the last point of a route
        private const float OnwardMetres = 240f;     // of its line it follows after the stop
        private const float LeaveFor = 50f;          // seconds after which a leaving bus is let go wherever it is

        private static readonly string[] VanNames = { "MersaidiDash", "UMCDesert" };
        private static readonly string[] NeverNames = { "Ambulance", "Taxi", "Limo", "Police", "Fire" };
        private static readonly string[] Tiers =
        {
            "an idle van of the pool", "a van that was driving out of sight",
            "an idle van of the pool whose wheelbase is not the bus body's", "a van that was driving out of sight, whose wheelbase is not the bus body's",
            "an idle vehicle of the pool under the bus body", "a vehicle that was driving out of sight, under the bus body"
        };

        private readonly EmergencyDriver _driver;
        private readonly List<int> _marker = new List<int>(0);        // an empty scripted route: the mark that the vehicle is ours
        private readonly List<Vector3> _route = new List<Vector3>(96);
        private readonly List<int> _routePoints = new List<int>(96);  // the lane point each is (an index into the road graph), -1 for a place of our own
        private readonly List<int> _starts = new List<int>(StartTries);
        private TransitLine _line;
        private GameObject _dress;
        private AiCarRescueCheck _rescue;
        private bool _parked;
        private bool _wasKinematic;
        private bool _modelled;
        private int _gate = -1;          // the point of its route it has been told to pull up at, or -1
        private Hold _gateWhy;
        private float _since;
        private float _heldSince;
        private float _progressAt;
        private float _progressLeft;
        private float _sideHalf = BodyHalfWidth;
        private Vector3 _bodyAt;         // where the modelled body's origin is, in the vehicle's own space

        public TransitBus(GameObject host)
        {
            _driver = host.AddComponent<EmergencyDriver>();
        }

        public VehicleComponent Car { get; private set; }
        public TransitStop Stop { get; private set; }
        public Phase State { get; private set; }

        /// <summary>What it is standing for on its way, if anything.</summary>
        public Hold Waiting { get; private set; }

        /// <summary>Seconds it has been standing for that.</summary>
        public float WaitingFor => Waiting == Hold.None ? 0f : Time.time - _heldSince;

        /// <summary>Seconds since it last got a metre nearer the end of its route.</summary>
        public float NoProgressFor => State == Phase.Approaching || State == Phase.Leaving ? Time.time - _progressAt : 0f;

        /// <summary>Metres of its route still to drive (to its stop, while it is approaching).</summary>
        public float Remaining { get; private set; }

        /// <summary>Metres of line from the stop it was put on the road at.</summary>
        public float StartedBack { get; private set; }

        /// <summary>Seconds since it was put on the road or left its stop.</summary>
        public float Out => State == Phase.Idle ? 0f : Time.time - _since;

        /// <summary>Seconds it took from being put on the road to standing at its stop.</summary>
        public float Took { get; private set; }

        /// <summary>What carries it and how that was come by, for the log.</summary>
        public string Chassis { get; private set; }

        /// <summary>Set when the vehicle was lost on its way to the stop (the game or a scene took it); cleared by the next Send.</summary>
        public bool Lost { get; private set; }

        /// <summary>True once after the vehicle was lost on its way to the stop: asked by whoever must say so and send another.</summary>
        public bool TakeLost()
        {
            bool lost = Lost;
            Lost = false;
            return lost;
        }

        /// <summary>Seconds it has been standing at its stop; 0 otherwise.</summary>
        public float Standing => State == Phase.Standing ? Time.time - _since : 0f;

        /// <summary>True while its vehicle is still the one it took and still ours (the pool hands vehicles out again).</summary>
        public bool Held => Car != null && TrafficTools.IsHeld(Car) && Car.presetPath == _marker;

        /// <summary>True while the vehicle is in front of the camera within the distance things are looked at.</summary>
        public bool InView => Car != null && CitySpots.InView(Car.transform.position);

        // ---------------------------------------------------------------- putting it on the road

        /// <summary>Puts a bus on its line some way before a stop, heading for it. False, with why, if it cannot.</summary>
        /// <param name="mayAppearInView">Only after it could not be done out of view: it may then start in view, far off.</param>
        /// <param name="mostBack">No further back than this many metres of line (to move a bus up past what held it).</param>
        public bool Send(TransitStop stop, bool mayAppearInView, out string refusal, float mostBack = StartBackMost)
        {
            refusal = null;
            if (State != Phase.Idle)
            {
                refusal = "the bus is still out";
                return false;
            }
            if (!TrafficTools.Ready || stop == null || stop.Line >= TransitMap.Lines.Count)
            {
                refusal = "traffic is not running";
                return false;
            }
            TransitLine line = TransitMap.Lines[stop.Line];
            float closedBack = CollectStarts(line, stop, mayAppearInView, mostBack);
            if (_starts.Count == 0)
            {
                refusal = closedBack >= 0f
                    ? "the road is closed " + closedBack.ToString("F0") + " m before the stop and no plain lane out of view lies between"
                    : mayAppearInView ? "its line has no plain stretch of lane within " + mostBack.ToString("F0") + " m before this stop"
                    : "every plain stretch of lane it could start from is in view";
                return false;
            }
            int furthest = _starts[0];
            for (int i = 1; i < _starts.Count; i++)
            {
                furthest = Mathf.Min(furthest, _starts[i]);
            }
            VehicleComponent pick = PickChassis(line, furthest, stop.PathIndex, out int tier);
            if (pick == null)
            {
                refusal = "no vehicle of the traffic pool can carry it just now (no van idle or out of sight"
                          + (PropLibrary.TryGet("minibus", out _, out _) ? ", nor any other ordinary vehicle)" : "; the bus body that would let another vehicle carry it is not in the prop bundle)");
                return false;
            }
            GameObject prefab = pick.prefab;
            if (pick.gameObject.activeInHierarchy)
            {
                TrafficTools.Remove(pick, "taken from out of sight to carry a bus");
            }
            VehicleComponent car = null;
            int start = -1;
            for (int i = 0; i < _starts.Count && car == null; i++)
            {
                start = _starts[i];
                car = TrafficManager.Instance.LoadVehicle(prefab, TransitMap.Point(line.Path[start]));
            }
            if (car == null)
            {
                refusal = "traffic stands on all " + _starts.Count + " of the lane points it could start from";
                return false;
            }
            Take(car, stop, line, Tiers[tier]);
            Vector3 from = TransitMap.Point(line.Path[start]).position;
            for (int i = start + 1; i < stop.PathIndex; i++)
            {
                Add(line, i);
            }
            // The last point is a place of our own: where the vehicle's own origin must come to rest for the middle of its
            // body (its door) to stand beside the pole. The driver stops FinalShort before the last point of a route.
            TrafficTools.Footprint(car, out Vector3 centre, out _, out _, out _);
            float middleAhead = car.transform.InverseTransformPoint(centre).z;
            _route.Add(stop.Lane + stop.Along * (FinalShort - middleAhead));
            _routePoints.Add(-1);
            Face(car, _route[0] - from);
            StartedBack = stop.Metres - line.Metres[start];
            Dress(car, line);
            State = Phase.Approaching;
            Lost = false;
            _since = Time.time;
            Begin();
            Remaining = _driver.RouteLeft;
            return true;
        }

        /// <summary>Puts a bus straight onto the lane just past a stop, already leaving: what a rider sees pull away after getting off.</summary>
        public bool SendLeaving(TransitStop stop)
        {
            if (State != Phase.Idle || !TrafficTools.Ready || stop == null || stop.Line >= TransitMap.Lines.Count)
            {
                return false;
            }
            TransitLine line = TransitMap.Lines[stop.Line];
            int last = Mathf.Min(stop.PathIndex + 9, line.Path.Length - 3);
            VehicleComponent pick = PickChassis(line, stop.PathIndex, Mathf.Min(stop.PathIndex + 40, line.Path.Length - 1), out int tier);
            if (pick == null)
            {
                return false;
            }
            GameObject prefab = pick.prefab;
            if (pick.gameObject.activeInHierarchy)
            {
                TrafficTools.Remove(pick, "taken from out of sight to carry a bus");
            }
            VehicleComponent car = null;
            int at = -1;
            for (int i = stop.PathIndex + 2; i <= last && car == null; i += 2)
            {
                Waypoint point = TransitMap.Point(line.Path[i]);
                if (point != null && !point.temporaryDisabled && TrafficManager.IsNotIntersection(point))
                {
                    at = i;
                    car = TrafficManager.Instance.LoadVehicle(prefab, point);
                }
            }
            if (car == null)
            {
                return false;
            }
            Take(car, stop, line, Tiers[tier]);
            Face(car, stop.Along);
            Dress(car, line);
            Onward(line, at);
            return true;
        }

        // Where it may start: plain lane points of its line before the stop, out of view, none of them before a closed road;
        // the one about StartBack away first, then further back, then nearer. Returns how far back the road is closed, or -1.
        private float CollectStarts(TransitLine line, TransitStop stop, bool mayAppearInView, float mostBack)
        {
            _starts.Clear();
            float closedBack = -1f;
            int floor = 1;
            for (int i = stop.PathIndex; i >= 1 && stop.Metres - line.Metres[i] <= StartBackMost; i--)
            {
                Waypoint point = TransitMap.Point(line.Path[i]);
                if (point != null && point.temporaryDisabled)
                {
                    floor = i + 2;
                    closedBack = stop.Metres - line.Metres[i];
                    break;
                }
            }
            int ceiling = stop.PathIndex - 3;
            float prefer = Mathf.Min(StartBack, mostBack);
            int pivot = -1;
            for (int i = ceiling; i >= floor; i--)
            {
                if (stop.Metres - line.Metres[i] >= prefer)
                {
                    pivot = i;
                    break;
                }
            }
            for (int pass = 0; pass < (mayAppearInView ? 2 : 1); pass++)
            {
                bool unseen = pass == 0;
                float lastTaken = float.MinValue;
                for (int i = pivot; i >= floor && _starts.Count < StartTries; i--)   // from the preferred distance, further back
                {
                    float back = stop.Metres - line.Metres[i];
                    if (back > mostBack)
                    {
                        break;
                    }
                    if (back - lastTaken >= StartApart && CanStart(line, i, unseen))
                    {
                        _starts.Add(i);
                        lastTaken = back;
                    }
                }
                lastTaken = float.MaxValue;
                for (int i = Mathf.Max(pivot + 1, floor); i <= ceiling && _starts.Count < StartTries; i++)   // then nearer
                {
                    float back = stop.Metres - line.Metres[i];
                    if (back < StartBackLeast)
                    {
                        break;
                    }
                    if (back <= mostBack && lastTaken - back >= StartApart && CanStart(line, i, unseen))
                    {
                        _starts.Add(i);
                        lastTaken = back;
                    }
                }
                if (_starts.Count > 0)
                {
                    break;
                }
            }
            return closedBack;
        }

        private static bool CanStart(TransitLine line, int index, bool unseen)
        {
            Waypoint point = TransitMap.Point(line.Path[index]);
            if (point == null || point.temporaryDisabled || point.enter || point.exit || point.neighbors == null || point.neighbors.Count == 0
                || !TrafficManager.IsNotIntersection(point))
            {
                return false;
            }
            bool inView = CitySpots.InView(point.position);
            if (unseen)
            {
                return !inView;
            }
            // In view only as a last resort, and then never close: nothing appears within 60 m of the player.
            return inView && CitySpots.Flat(point.position - PlayerHelper.GetPosition()) > 60f * 60f;
        }

        // The vehicle that will carry it: see the summary. The vehicle itself (not its prefab): one that is driving is taken
        // off the road by the caller first.
        private static VehicleComponent PickChassis(TransitLine line, int from, int to, out int tier)
        {
            tier = 0;
            List<VehicleComponent> pool = Manager.GetVehicleList();
            TrafficManager traffic = TrafficManager.Instance;
            bool body = PropLibrary.TryGet("minibus", out Mesh mesh, out Material material) && mesh != null && material != null;
            Vector3 player = PlayerHelper.GetPosition();
            VehicleComponent best = null;
            int bestRank = int.MaxValue;
            for (int i = 0; i < pool.Count && bestRank > 0; i++)
            {
                VehicleComponent car = pool[i];
                if (car == null || car.prefab == null || car.presetPath != null)
                {
                    continue;   // (a scripted route, even an empty one, is somebody's: the game's private driver, a scene of the mod's)
                }
                string name = car.prefab.name;
                bool van = false;
                for (int n = 0; n < VanNames.Length && !van; n++)
                {
                    van = name.IndexOf(VanNames[n], StringComparison.OrdinalIgnoreCase) >= 0;
                }
                if (!van)
                {
                    if (!body || car.length < 4.2f || car.length > 7.5f)
                    {
                        continue;
                    }
                    bool never = false;
                    for (int n = 0; n < NeverNames.Length && !never; n++)
                    {
                        never = name.IndexOf(NeverNames[n], StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    if (never)
                    {
                        continue;
                    }
                }
                bool active = car.gameObject.activeInHierarchy;
                // Under the modelled body the van with the body's own wheelbase comes first: the body's wheels are then exactly
                // where the vehicle turns and nothing of it slides in a bend.
                bool fits = !body || Mathf.Abs(car.wheelDistance - BodyWheelbase) <= AxlesFit;
                int rank = (van ? fits ? 0 : 2 : 4) + (active ? 1 : 0);
                if (rank >= bestRank)
                {
                    continue;
                }
                if (active)
                {
                    Vector3 at = car.transform.position;
                    if (traffic.IsVehicleIgnored(car.GetIndex()) || car.visibilityScript == null || !car.visibilityScript.IsNotInView()
                        || CitySpots.InView(at) || CitySpots.Flat(at - player) < 70f * 70f)
                    {
                        continue;
                    }
                }
                if (car.GetComponent<TaxiController>() != null || car.GetComponent<Helpers.PrivateDriverVehicle>() != null || !Allowed(line, from, to, car.vehicleType))
                {
                    continue;
                }
                best = car;
                bestRank = rank;
            }
            tier = best != null ? bestRank : 0;
            return best;
        }

        private static bool Allowed(TransitLine line, int from, int to, VehicleTypes type)
        {
            for (int p = from; p <= to; p += 4)
            {
                Waypoint point = TransitMap.Point(line.Path[p]);
                if (point == null || point.allowedCars == null || !point.allowedCars.Contains(type))
                {
                    return false;
                }
            }
            return true;
        }

        // Ours from here on: the traffic AI leaves it alone, and the empty scripted route keeps the game from taking it off
        // the road by distance or for standing (the same hold a response unit has on its vehicle).
        private void Take(VehicleComponent car, TransitStop stop, TransitLine line, string how)
        {
            Car = car;
            Stop = stop;
            _line = line;
            _parked = false;
            _gate = -1;
            _gateWhy = Hold.None;
            Waiting = Hold.None;
            _route.Clear();
            _routePoints.Clear();
            Manager.SetVehicleIgnored(car.GetIndex(), true);
            _marker.Clear();
            car.presetPath = _marker;
            _rescue = TrafficTools.SuspendRescue(car);
            TrafficTools.Touch(car, "taken out of traffic as a bus");
            Chassis = "'" + TrafficTools.Name(car) + "' (" + how + "; " + car.length.ToString("F1") + " m long, " + car.colliderWidth.ToString("F1") + " wide, wheelbase "
                      + car.wheelDistance.ToString("F1") + ")";
        }

        private void Add(TransitLine line, int pathIndex)
        {
            Waypoint point = TransitMap.Point(line.Path[pathIndex]);
            if (point != null)
            {
                _route.Add(point.position);
                _routePoints.Add(line.Path[pathIndex]);
            }
        }

        private static void Face(VehicleComponent car, Vector3 heading)
        {
            heading.y = 0f;
            if (heading.sqrMagnitude > 0.01f)
            {
                car.transform.rotation = Quaternion.LookRotation(heading);
            }
        }

        // The driver sets off along what is left of the route (its first point is the next one ahead).
        private void Begin()
        {
            _gate = -1;
            _gateWhy = Hold.None;
            Waiting = Hold.None;
            _driver.BeginRoute(Car, _route, 0f);
            _driver.CruiseSpeed = Cruise;
            _driver.PullOver(NoPassing);
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
                bool wanted = State == Phase.Approaching || State == Phase.Standing;
                if (TrafficTools.Ready)
                {
                    TransitDirector.Logger?.Info("Transit: the bus's vehicle was taken from under it while it was " + State + " (" + (Car == null ? "gone" : TrafficTools.State(Car)) + ").");
                }
                Abandon();
                Lost = wanted;
                return;
            }
            switch (State)
            {
                case Phase.Approaching:
                    Drive(now);
                    if (_driver.Arrived && _gate < 0 && !_driver.Active)
                    {
                        Park();
                        State = Phase.Standing;
                        Took = now - _since;
                        _since = now;
                        Remaining = 0f;
                        Waiting = Hold.None;
                        TrafficTools.Touch(Car, "standing at its bus stop");
                    }
                    break;

                case Phase.Leaving:
                    Drive(now);
                    if ((_driver.Arrived && _gate < 0 && !_driver.Active) || now - _since > LeaveFor)
                    {
                        Release(remove: false);
                    }
                    break;
            }
        }

        // What the driver does not know of: red lights and lane points a scene has closed. And how it is getting on.
        private void Drive(float now)
        {
            if (_gate >= 0 && !_driver.Active)
            {
                // It has pulled up where it was told to.
                if (!_parked)
                {
                    Park();
                    Waiting = _gateWhy;
                    _heldSince = now;
                }
                if (GateOpen())
                {
                    Unpark();
                    int done = Mathf.Min(_driver.NextPoint, _route.Count - 1);
                    _route.RemoveRange(0, done);
                    _routePoints.RemoveRange(0, done);
                    float mark = _progressAt;
                    float left = _progressLeft;
                    Begin();
                    _progressAt = mark;
                    _progressLeft = left;
                }
                Remaining = _driver.RouteLeft;
                return;
            }
            if (!_driver.Active)
            {
                return;   // it has arrived at the end of its route: its owner's turn
            }
            Vector3 position = Car.transform.position;
            int next = _driver.NextPoint;
            int gate = -1;
            Hold why = Hold.None;
            bool junction = false;
            float ahead = 0f;
            for (int i = next; i < _route.Count; i++)
            {
                Vector3 leg = _route[i] - (i == next ? position : _route[i - 1]);
                leg.y = 0f;
                ahead += leg.magnitude;
                if (ahead > LookAhead)
                {
                    break;
                }
                Waypoint point = _routePoints[i] >= 0 ? TransitMap.Point(_routePoints[i]) : null;
                if (point == null)
                {
                    continue;
                }
                if (point.temporaryDisabled)
                {
                    gate = Mathf.Max(next, i - 2);   // a couple of points before the closed road
                    why = Hold.Closure;
                    break;
                }
                if (point.stop && Lit(point))
                {
                    // A light that turns as it reaches the line is driven through, as anybody would.
                    if (ahead > 7f || _driver.Speed < 4f)
                    {
                        gate = i;
                        why = Hold.Light;
                        break;
                    }
                }
                junction |= ahead < 16f && !TrafficManager.IsNotIntersection(point);
            }
            if (gate != _gate)
            {
                _gate = gate;
                _driver.PullUpAt(gate);
            }
            _gateWhy = why;
            _driver.CruiseSpeed = junction ? JunctionSpeed : Cruise;

            float heldUp = _driver.HeldFor;
            if (heldUp > 1f)
            {
                if (Waiting != Hold.Traffic)
                {
                    Waiting = Hold.Traffic;
                    _heldSince = now - heldUp;
                }
            }
            else
            {
                Waiting = Hold.None;
            }
            Remaining = _driver.RouteLeft;
            if (Remaining < _progressLeft - 1f)
            {
                _progressLeft = Remaining;
                _progressAt = now;
            }
        }

        // The game only runs the lights of junctions near the player; further off a light stays as it was for good. Those are not waited at.
        private static bool Lit(Waypoint point)
        {
            return CitySpots.Flat(point.position - PlayerHelper.GetPosition()) < LitWithin * LitWithin;
        }

        private bool GateOpen()
        {
            int from = Mathf.Clamp(_gate, 0, _route.Count - 1);
            float ahead = 0f;
            for (int i = from; i < _route.Count; i++)
            {
                if (i > from)
                {
                    ahead += Vector3.Distance(_route[i], _route[i - 1]);
                }
                if (ahead > LookAhead)
                {
                    break;
                }
                Waypoint point = _routePoints[i] >= 0 ? TransitMap.Point(_routePoints[i]) : null;
                if (point == null)
                {
                    continue;
                }
                if (point.temporaryDisabled)
                {
                    _gateWhy = Hold.Closure;
                    Waiting = Hold.Closure;
                    return false;
                }
                if (i == from && point.stop && Lit(point) && (WaitingFor < LightPatience || CitySpots.InView(point.position)))
                {
                    return false;   // (a light nobody is looking at that has not changed in all that time is one the game is not running)
                }
            }
            return true;
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
            TrafficTools.Touch(Car, "a bus standing (kinematic)");
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

        // ---------------------------------------------------------------- at the stop

        /// <summary>
        /// Where it came to rest, measured against its stop: metres its middle is along the lane from the pole (past it is
        /// positive), metres off the lane's line (towards the kerb is positive), degrees off the lane's direction, and
        /// metres of road between its side and the kerb's edge.
        /// </summary>
        public void Measure(out float along, out float aside, out float angle, out float toKerb)
        {
            along = aside = angle = toKerb = 0f;
            if (Car == null || Stop == null)
            {
                return;
            }
            TrafficTools.Footprint(Car, out Vector3 centre, out Vector3 forward, out _, out float halfWidth);
            Vector3 right = Vector3.Cross(Vector3.up, Stop.Along);
            Vector3 offset = centre - Stop.Lane;
            offset.y = 0f;
            along = Vector3.Dot(offset, Stop.Along);
            aside = Vector3.Dot(offset, right);
            angle = Vector3.Angle(forward, Stop.Along);
            Vector3 kerb = Stop.Kerb - Stop.Lane;
            kerb.y = 0f;
            toKerb = Vector3.Dot(kerb, right) - aside - (_modelled ? BodyHalfWidth : halfWidth);
        }

        /// <summary>Where somebody stands to step in at its door, on its kerb side, and the way they face then.</summary>
        public void DoorPlace(out Vector3 place, out Vector3 inwards)
        {
            TrafficTools.Footprint(Car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            inwards = -right;
            if (_modelled)
            {
                // From where the body itself stands (its axles are on the vehicle's, which need not be the middle of the colliders).
                Vector3 body = Car.transform.TransformPoint(_bodyAt);
                centre = new Vector3(body.x, centre.y, body.z);
            }
            place = _modelled
                ? centre + right * (BodyHalfWidth + BodyDoorOut) + forward * BodyDoorAhead
                : centre + right * (halfWidth + 0.30f) + forward * (halfLength * 0.12f);   // the van's own sliding door (job_car_enter: feet 0.30 m outside the side)
        }

        /// <summary>True when the body is the modelled bus (a door with a step: <c>board_step</c>), false for the plain van.</summary>
        public bool Modelled => _modelled;

        /// <summary>Doors closed: it pulls out and follows its line for a while.</summary>
        public void Depart()
        {
            if (State != Phase.Standing || !Held)
            {
                return;
            }
            Onward(_line, Stop.PathIndex);
        }

        private void Onward(TransitLine line, int fromIndex)
        {
            Unpark();
            _route.Clear();
            _routePoints.Clear();
            for (int i = fromIndex + 1; i < line.Path.Length && line.Metres[i] - line.Metres[fromIndex] < OnwardMetres; i++)
            {
                Add(line, i);
            }
            State = Phase.Leaving;
            _since = Time.time;
            if (_route.Count < 2)
            {
                Release(remove: false);   // the end of its line: nothing to follow
                return;
            }
            Begin();
            Remaining = _driver.RouteLeft;
            TrafficTools.Touch(Car, "leaving its bus stop");
        }

        // ---------------------------------------------------------------- letting it go

        /// <summary>
        /// Every way out. With <paramref name="remove"/>, or when nobody is looking its way, the vehicle is taken off the
        /// road; otherwise it is handed back to the traffic AI where it is and drives on as ordinary traffic, still dressed
        /// until the game takes it off the road out of sight (the dress removes itself then).
        /// </summary>
        public void Release(bool remove)
        {
            if (State == Phase.Idle)
            {
                return;
            }
            VehicleComponent car = Car;
            bool held = Held;
            bool running = TrafficManager.HasInstance && TrafficManager.IsInitialized;
            bool takeOff = remove || !held || !TrafficTools.Ready || (!CitySpots.InView(car.transform.position) && car.visibilityScript != null && car.visibilityScript.IsNotInView());
            if (held)
            {
                _driver.Stop();
            }
            else
            {
                _driver.Forget();
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
            if (_dress != null && takeOff)
            {
                Object.Destroy(_dress);
            }
            if (held && running)
            {
                if (takeOff)
                {
                    TrafficTools.Remove(car, "a bus taken in");
                }
                else
                {
                    TrafficTools.ReturnToTraffic(car, "a bus let go as traffic");
                }
            }
            Forget();
        }

        // The vehicle is no longer ours to touch (the pool reused it, traffic was shut down): nothing is undone on it but what is still ours.
        private void Abandon()
        {
            _driver.Forget();
            VehicleComponent car = Car;
            if (car != null)
            {
                if (car.presetPath == _marker)
                {
                    car.presetPath = null;
                    if (_parked && car.rb != null)
                    {
                        car.rb.isKinematic = _wasKinematic;   // still the same outing of the same vehicle: not left frozen
                    }
                    if (TrafficManager.HasInstance && TrafficManager.IsInitialized && car.gameObject.activeInHierarchy)
                    {
                        TrafficTools.Remove(car, "a bus that was no longer held");
                    }
                }
            }
            if (_rescue != null)
            {
                _rescue.enabled = true;
            }
            if (_dress != null)
            {
                Object.Destroy(_dress);
            }
            Forget();
        }

        private void Forget()
        {
            _rescue = null;
            _dress = null;
            _line = null;
            _parked = false;
            _gate = -1;
            _gateWhy = Hold.None;
            Waiting = Hold.None;
            _route.Clear();
            _routePoints.Clear();
            Car = null;
            Stop = null;
            Remaining = 0f;
            State = Phase.Idle;
        }

        // ---------------------------------------------------------------- what makes it a bus

        private void Dress(VehicleComponent car, TransitLine line)
        {
            _dress = null;
            _modelled = false;
            try
            {
                if (!Shapes.Available)
                {
                    return;
                }
                var root = new GameObject("RealLifeOverhaul Bus Dress");
                root.transform.SetParent(car.transform, false);
                var dress = root.AddComponent<BusDress>();
                TrafficTools.Footprint(car, out Vector3 centre, out _, out float halfLength, out float halfWidth);
                Vector3 middle = car.transform.InverseTransformPoint(centre);
                float roof = TrafficEffects.RoofHeight(car);
                float boardAhead = halfLength * 0.55f;
                float numberBack = halfLength * 0.80f;
                float floor = 0f;
                _sideHalf = halfWidth;

                // The modelled body: over the vehicle, whose own renderers are hidden until this object goes. It stands on its
                // own origin (the ground under the middle of the body), so it is put where the vehicle's wheels meet the road:
                // the lowest point of what the vehicle shows, not the vehicle's own origin, which need not be on the ground.
                if (PropLibrary.TryGet("minibus", out Mesh mesh, out Material material) && mesh != null && material != null)
                {
                    car.GetComponentsInChildren(true, dress.Scratch);
                    float lowest = float.MaxValue;
                    for (int i = 0; i < dress.Scratch.Count; i++)
                    {
                        Renderer renderer = dress.Scratch[i];
                        if (renderer is MeshRenderer && renderer.enabled && !renderer.forceRenderingOff && renderer.gameObject.activeInHierarchy
                            && !renderer.transform.IsChildOf(root.transform) && renderer.bounds.size.sqrMagnitude < 12f * 12f)
                        {
                            lowest = Mathf.Min(lowest, renderer.bounds.min.y);
                        }
                    }
                    float under = lowest - car.transform.position.y;
                    floor = lowest < float.MaxValue && under > -1.5f && under < 0.4f ? under : 0f;
                    // Along the vehicle the body goes where its axles are the vehicle's own (the middle between them on the middle
                    // between the vehicle's): its wheels are all it shows of wheels, and they must turn about the points the
                    // vehicle turns about. On the Mersaidi Dash both pairs coincide; on anything else each is off by half the
                    // difference in wheelbase. Only if the vehicle's wheels cannot be told, the middle of its colliders.
                    bool axles = TrafficTools.Axles(car, dress.Scratch, out float axleRear, out float axleFront);
                    if (axles)
                    {
                        middle.z = (axleRear + axleFront) * 0.5f - (BodyAxleRear + BodyAxleFront) * 0.5f;
                    }
                    _bodyAt = new Vector3(middle.x, floor, middle.z);
                    Transform body = Shapes.Piece("minibus", root.transform, mesh, material, _bodyAt, Vector3.one);
                    body.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
                    for (int i = 0; i < dress.Scratch.Count; i++)
                    {
                        Renderer renderer = dress.Scratch[i];
                        if (renderer != null && !renderer.forceRenderingOff && !renderer.transform.IsChildOf(root.transform))
                        {
                            renderer.forceRenderingOff = true;
                            dress.Hidden.Add(renderer);
                        }
                    }
                    dress.Scratch.Clear();
                    // The board and the number stand where the body's roof is flat for them, whatever carries the body.
                    roof = floor + PropLibrary.SizeOf("minibus").y;
                    boardAhead = BodyBoardAhead;
                    numberBack = BodyNumberBack;
                    _sideHalf = BodyHalfWidth;
                    _modelled = true;
                    Vector3 colliders = car.transform.InverseTransformPoint(centre);
                    Chassis += "; the body " + BodyLength.ToString("F2") + " m stands " + floor.ToString("F2") + " m from the vehicle's origin in height, "
                               + (axles ? "its axles " + Mathf.Abs((axleFront - axleRear - BodyWheelbase) * 0.5f).ToString("F2") + " m from the vehicle's (wheelbase " + (axleFront - axleRear).ToString("F2") + " under "
                                          + BodyWheelbase.ToString("F2") + ")" : "on the middle of its colliders (its wheels could not be told)")
                               + ", and reaches " + (middle.z + BodyLength * 0.5f - colliders.z - halfLength).ToString("F2") + " m beyond its colliders at the front and "
                               + (colliders.z - halfLength - (middle.z - BodyLength * 0.5f)).ToString("F2") + " m at the back";
                }

                // The route board across the front of the roof, and the number at the back.
                Material black = Shapes.Paint(new Color(0.03f, 0.03f, 0.03f));
                float width = Mathf.Min(1.5f, _sideHalf * 2f - 0.3f);
                var front = new Vector3(middle.x, roof + 0.13f, middle.z + boardAhead);
                Shapes.Piece("Board", root.transform, Shapes.Cube, black, front, new Vector3(width, 0.26f, 0.14f));
                Letter(root.transform, front + new Vector3(0f, 0f, 0.072f), true, width, line.Number + "  " + line.To);
                var back = new Vector3(middle.x, roof + 0.11f, middle.z - numberBack);
                Shapes.Piece("Number", root.transform, Shapes.Cube, black, back, new Vector3(0.5f, 0.22f, 0.10f));
                Letter(root.transform, back - new Vector3(0f, 0f, 0.052f), false, 0.5f, line.Number);
                _dress = root;
            }
            catch (Exception e)
            {
                TransitDirector.Logger?.Error(e);
            }
        }

        // Amber lettering on a face of a board. Text is read from behind its own forward.
        private static void Letter(Transform parent, Vector3 at, bool facingForward, float width, string words)
        {
            TMP_FontAsset font = Hud.Font;
            if (font == null)
            {
                return;
            }
            var label = new GameObject("Lettering");
            label.transform.SetParent(parent, false);
            label.transform.localPosition = at;
            label.transform.localRotation = facingForward ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
            var text = label.AddComponent<TextMeshPro>();
            text.font = font;
            text.text = words;
            text.color = new Color(1f, 0.72f, 0.12f);
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.overflowMode = TextOverflowModes.Truncate;
            text.rectTransform.sizeDelta = new Vector2(width * 0.94f, 0.20f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.4f;
            text.fontSizeMax = 1.8f;
            var renderer = label.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }

    /// <summary>
    /// On the object a bus's dress hangs from. When the vehicle is switched off (the traffic
    /// pool takes it back) or the object is destroyed, whatever of the vehicle was hidden is
    /// shown again and the dress goes: nothing of the mod's is ever left on a pooled vehicle.
    /// </summary>
    public sealed class BusDress : MonoBehaviour
    {
        public readonly List<Renderer> Hidden = new List<Renderer>(16);
        public readonly List<Renderer> Scratch = new List<Renderer>(32);

        private void OnDisable()
        {
            Restore();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            Restore();
        }

        private void Restore()
        {
            for (int i = 0; i < Hidden.Count; i++)
            {
                if (Hidden[i] != null)
                {
                    Hidden[i].forceRenderingOff = false;
                }
            }
            Hidden.Clear();
        }
    }
}
