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
    /// WHERE IT STANDS. At a stop with a bay (<see cref="TransitStop.HasBay"/>, <see cref="BusBay"/>)
    /// that is free, the end of its route is not the lane's line but a path of its own across
    /// the strip beside the lane (points a metre apart, followed by the same driver, which is told
    /// to keep to them closely, to go slowly, and where the kerb is): it stands parallel to the
    /// kerb, door side 0.2 m from it, its body beyond the lane's edge, and the lane is free. To
    /// leave it waits, standing, until the lane beside and behind it is clear (one box four times
    /// a second while it waits, nothing otherwise), then follows a path of its own back onto the
    /// lane's line. Where the stop has no bay, or the bay is taken when the bus sets out or as it
    /// comes up, it stops on the lane's line as it always has.
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
        private const float BayBraking = 2.5f;       // m/s2 it comes down to a bay's pace with, before the bay's path begins
        private const float BayLead = 6f;            // metres before a bay's path at which the driver is told of the kerb and to keep to its points
        private const float BayLookAt = 16f;         // metres before a bay's path at which the bay is looked at once more
        private const float BayNear = 9f;            // held up on a bay's path with no more than this left, it stops where it is and that is its stand
        private const float BayLeast = 0.6f;         // metres aside of the lane's line from which a standing bus pulls out along a path of its own
        private const float GapBack = 30f;           // metres of lane behind its tail that must be clear for it to pull out
        private const float GapBackLate = 12f;       // ...once it has waited GapPatience: it edges out as a bus does
        private const float GapAhead = 5f;           // ...and ahead of its nose, always
        private const float GapHalf = 1.2f;          // half the width of the lane that is looked at
        private const float GapPatience = 15f;
        private const float GapMost = 45f;           // seconds after which a bus that still cannot pull out is let go where it stands

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
        private float _middleAhead;      // the middle of what it fills, ahead of the vehicle's own origin (the point it is turned about)
        private float _noseAhead;        // its nose, ahead of that point
        private float _tailBehind;       // its tail, behind it
        private float _widthHalf = BodyHalfWidth;   // half of what it fills across: the body or the vehicle's colliders, whichever is wider
        private bool _bay;               // its route ends in its stop's bay
        private bool _bayMode;           // the driver has been told to keep to the bay's path and where the kerb is
        private bool _bayLooked;         // the bay has been looked at once more as it came up
        private bool _awaitGap;          // doors closed, standing in the bay until the lane is clear
        private bool _departed;
        private bool _pulledOut;
        private int _bayFrom;            // the first point of its route that is the bay's path
        private int _bayTo;              // ...and, leaving, the first that is the lane again
        private int _laneNext;           // the first point of its line not put on its route (the lane's points beside the bay)
        private float _bayRun;           // metres of route from the first point of the bay's path to its end
        private float _bayBehind;        // metres of the strip this vehicle needs free behind the pole to come in
        private float _bayAhead;         // ...and ahead of it to pull out
        private BusBay.Curve _curveIn;   // its own curve into the bay (by how far its nose reaches)
        private float _gapSince;

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

        /// <summary>True when its route ends in its stop's bay (from when it is put on the road; false again if the bay is found taken as it comes up).</summary>
        public bool InBay => _bay;

        /// <summary>Why it stops in the lane at a stop that has a bay; null when it uses the bay, or the stop has none.</summary>
        public string BayNote { get; private set; }

        /// <summary>How many of the game's parked cars, out of view, were taken in from the bay when it was put on the road.</summary>
        public int SentAway { get; private set; }

        /// <summary>True while it stands in its bay with its doors closed, waiting for the lane to be clear.</summary>
        public bool AwaitingGap => _awaitGap;

        /// <summary>
        /// True once after it has left its stop for good: pulled out of its bay (<paramref name="pulledOut"/>, after
        /// <paramref name="waited"/> seconds for a gap in the lane) or let go standing there because no gap came.
        /// A bus that stood in the lane has left the moment it is told to, and says nothing here.
        /// </summary>
        public bool TakeDeparted(out float waited, out bool pulledOut)
        {
            waited = _gapWaited;
            pulledOut = _pulledOut;
            bool departed = _departed;
            _departed = false;
            return departed;
        }

        private float _gapWaited;

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
            Dress(car, line);   // first: what it fills (the body, or the vehicle's own colliders) decides where it can stand
            Fill(car);
            _bay = BayOpen(stop, sendAway: true);
            Vector3 from = TransitMap.Point(line.Path[start]).position;
            if (_bay)
            {
                RouteToBay(line, stop, start + 1);
            }
            else
            {
                _laneNext = start + 1;
                RouteToLane(line, stop);
            }
            Face(car, _route[0] - from);
            StartedBack = stop.Metres - line.Metres[start];
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
            // (At a stop with a bay it is put where a bus that has just pulled out is: on the lane, past the bay's end.)
            float lead = stop.HasBay ? stop.BayAhead : 0f;
            int last = Mathf.Min(stop.PathIndex + (stop.HasBay ? 14 : 9), line.Path.Length - 3);
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
                if (point != null && !point.temporaryDisabled && TrafficManager.IsNotIntersection(point) && line.Metres[i] - stop.Metres >= lead)
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
            _bay = false;
            _bayMode = false;
            _bayLooked = false;
            _awaitGap = false;
            _departed = false;
            _pulledOut = false;
            _gapWaited = 0f;
            _bayFrom = 0;
            _bayTo = 0;
            _bayRun = 0f;
            BayNote = null;
            SentAway = 0;
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

        // What the bus fills, from the point the driver turns it about (the vehicle's own origin): the vehicle's colliders,
        // and the modelled body where it reaches further.
        private void Fill(VehicleComponent car)
        {
            TrafficTools.Footprint(car, out Vector3 centre, out _, out float halfLength, out float halfWidth);
            _middleAhead = car.transform.InverseTransformPoint(centre).z;
            _noseAhead = _middleAhead + halfLength;
            _tailBehind = halfLength - _middleAhead;
            _widthHalf = halfWidth;
            if (_modelled)
            {
                _noseAhead = Mathf.Max(_noseAhead, _bodyAt.z + BodyLength * 0.5f);
                _tailBehind = Mathf.Max(_tailBehind, BodyLength * 0.5f - _bodyAt.z);
                _widthHalf = Mathf.Max(_widthHalf, BodyHalfWidth);
            }
        }

        // Whether it can use its stop's bay now, and if not, why (BayNote). With sendAway, the game's parked cars that stand
        // in the bay out of view are taken in first (TransitBays).
        private bool BayOpen(TransitStop stop, bool sendAway)
        {
            BayNote = null;
            if (!stop.HasBay)
            {
                return false;
            }
            float offset = BusBay.Offset(stop.KerbAside, _widthHalf);
            if (offset <= 0f)
            {
                BayNote = "the vehicle that carries it is " + (_widthHalf * 2f).ToString("F2") + " m wide, too wide for the bay";
                return false;
            }
            // Its own curves, by how far it reaches either side of the point it is turned about, and the strip they take.
            _curveIn = BusBay.In(offset, _noseAhead, BusBay.RunMost - Mathf.Max(0f, _middleAhead));
            BusBay.Curve curveOut = BusBay.Out(offset, _tailBehind, BusBay.RunMost + Mathf.Min(0f, _middleAhead));
            _bayBehind = Mathf.Max(stop.BayBehind, BusBay.Behind(_curveIn, _middleAhead));
            _bayAhead = Mathf.Max(stop.BayAhead, BusBay.Ahead(curveOut, _middleAhead));
            if (_curveIn.Cut || curveOut.Cut || _bayBehind > stop.BayRoom + 0.01f || _bayAhead > stop.BayAhead + 0.01f)
            {
                BayNote = "the vehicle that carries it reaches " + _noseAhead.ToString("F1") + " m ahead and " + _tailBehind.ToString("F1") + " m behind the point it turns about and needs "
                          + _bayBehind.ToString("F0") + " m of bay to come in and " + _bayAhead.ToString("F0") + " to pull out, where the stop's has " + stop.BayRoom.ToString("F0") + " and " + stop.BayAhead.ToString("F0");
                return false;
            }
            bool free = TransitBays.Free(stop, _bayBehind, _bayAhead, Car != null ? Car.rb : null, sendAway, out string taken, out int sent);
            SentAway += sent;
            if (!free)
            {
                BayNote = "its bay is taken by " + taken;
            }
            return free;
        }

        // The end of a route on the lane's line. The last point is a place of our own: where the vehicle's own origin must
        // come to rest for the middle of its body (its door) to stand beside the pole. The driver stops FinalShort before
        // the last point of a route.
        private void RouteToLane(TransitLine line, TransitStop stop)
        {
            for (int i = _laneNext; i < stop.PathIndex; i++)
            {
                Add(line, i);
            }
            _laneNext = stop.PathIndex;
            _route.Add(stop.Lane + stop.Along * (FinalShort - _middleAhead));
            _routePoints.Add(-1);
        }

        // The end of a route in the stop's bay: the lane's points up to where the bay's path leaves the lane's line, then
        // the path (BusBay.In, a point every metre, places of our own), the standing place and the point beyond it that
        // the driver stops short of. The path is laid for the point the vehicle is turned about, which comes to rest
        // where the middle of the body is beside the pole.
        private void RouteToBay(TransitLine line, TransitStop stop, int from)
        {
            BusBay.Curve curve = _curveIn;
            float offset = curve.Offset;
            int points = curve.Points;
            float leaves = points * BusBay.Step + _middleAhead + 0.5f;   // metres of line before the stop at which the path leaves the lane
            int i = from;
            for (; i < stop.PathIndex && stop.Metres - line.Metres[i] > leaves; i++)
            {
                Add(line, i);
            }
            _laneNext = i;
            _bayFrom = _route.Count;
            Vector3 right = Vector3.Cross(Vector3.up, stop.Along);
            Vector3 rest = stop.Lane + stop.Along * -_middleAhead + right * offset;
            for (int k = points; k >= 1; k--)
            {
                float before = k * BusBay.Step;
                _route.Add(rest - stop.Along * before - right * curve.Aside(before));
                _routePoints.Add(-1);
            }
            _route.Add(rest);
            _routePoints.Add(-1);
            _route.Add(rest + stop.Along * FinalShort);
            _routePoints.Add(-1);
            _bayRun = points * BusBay.Step + FinalShort;
        }

        // The bay was free when it set out and is not now: the end of its route is put back on the lane's line. Only while
        // the bay's path is still ahead of it.
        private void BackToLane(string why)
        {
            int from = Mathf.Clamp(_bayFrom, 0, _route.Count);
            _route.RemoveRange(from, _route.Count - from);
            _routePoints.RemoveRange(from, _routePoints.Count - from);
            _bay = false;
            BayMode(false);
            BayNote = why;
            RouteToLane(_line, Stop);
        }

        // On a bay's path the driver keeps to the points (it cuts every corner of a route on the open lane by 4.5 m) and
        // knows where the kerb is, so that nobody on the pavement is taken to be in its way while its nose points at them.
        private void BayMode(bool on)
        {
            if (on == _bayMode || Stop == null)
            {
                return;
            }
            _bayMode = on;
            if (on)
            {
                Vector3 right = Vector3.Cross(Vector3.up, Stop.Along);
                _driver.Reach = BusBay.Reach;
                _driver.Kerb(Stop.Lane + right * Stop.KerbAside, right);
            }
            else
            {
                _driver.Reach = float.MaxValue;   // (held to what it is on the open road)
                _driver.NoKerb();
            }
        }

        // The driver sets off along what is left of the route (its first point is the next one ahead).
        private void Begin()
        {
            _gate = -1;
            _gateWhy = Hold.None;
            Waiting = Hold.None;
            // Held up on the last metres of a bay's path (somebody standing in the bay, a car nosing in), it stops there
            // and that is where it stands; on the lane it waits for the way to clear, as ever.
            _driver.BeginRoute(Car, _route, _bay && State != Phase.Leaving ? BayNear : 0f);
            _driver.CruiseSpeed = Cruise;
            _driver.PullOver(NoPassing);
            _bayMode = false;   // (a drive begun is a driver reset: told again when it is next on the bay's path)
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
                    if (_awaitGap)
                    {
                        AwaitGap(now);
                        break;
                    }
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
                    _bayFrom = Mathf.Max(0, _bayFrom - done);
                    _bayTo = Mathf.Max(0, _bayTo - done);
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
            float cruise = junction ? JunctionSpeed : Cruise;
            float toGo = _driver.RouteLeft;
            if (State == Phase.Approaching && _bay)
            {
                // Coming up to its bay: down to the bay's pace by where the path begins; the bay looked at once more while the
                // lane's line can still be had instead; the driver told of the path and the kerb just before it.
                float lead = toGo - _bayRun;
                if (!_bayLooked && lead <= BayLookAt)
                {
                    _bayLooked = true;
                    if (next < _bayFrom && !TransitBays.Free(Stop, _bayBehind, _bayAhead, Car.rb, false, out string taken, out _))
                    {
                        BackToLane("its bay was taken by " + taken + " as it came up");
                        toGo = _driver.RouteLeft;
                        lead = toGo - _bayRun;
                    }
                }
                if (_bay)
                {
                    cruise = Mathf.Min(cruise, Mathf.Sqrt(BusBay.Speed * BusBay.Speed + 2f * BayBraking * Mathf.Max(0f, lead)));
                    BayMode(lead <= BayLead);
                }
            }
            else if (State == Phase.Leaving && _bayTo > 0)
            {
                // Pulling out: gently from rest, at the bay's pace until it is on the lane's own points again.
                if (next >= _bayTo)
                {
                    _bayTo = 0;
                    BayMode(false);
                }
                else
                {
                    BayMode(true);
                    cruise = Mathf.Min(BusBay.Speed, 1.5f + 1.5f * (now - _since));
                }
            }
            _driver.CruiseSpeed = cruise;

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
            Remaining = toGo;
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
        /// positive), metres off the lane's line (towards the kerb is positive), degrees off the lane's direction, and,
        /// by the corner of what it fills that is nearest each, metres of road between it and the kerb's edge and metres
        /// between it and the lane's edge (negative: that much of it stands in the lane; on the lane's line, about -2.7).
        /// </summary>
        public void Measure(out float along, out float aside, out float angle, out float toKerb, out float toLane)
        {
            along = aside = angle = toKerb = toLane = 0f;
            if (Car == null || Stop == null)
            {
                return;
            }
            TrafficTools.Footprint(Car, out Vector3 centre, out Vector3 forward, out _, out _);
            Vector3 right = Vector3.Cross(Vector3.up, Stop.Along);
            Vector3 offset = centre - Stop.Lane;
            offset.y = 0f;
            along = Vector3.Dot(offset, Stop.Along);
            aside = Vector3.Dot(offset, right);
            angle = Vector3.Angle(forward, Stop.Along);
            float halfLength = (_noseAhead + _tailBehind) * 0.5f;
            toKerb = BusBay.KerbClear(Stop.KerbAside, aside, angle, halfLength, _widthHalf);
            toLane = BusBay.LaneClear(aside, angle, halfLength, _widthHalf);
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

        /// <summary>
        /// Doors closed. Standing on the lane's line it pulls away at once and follows its line for a while. Standing
        /// in its bay (or anywhere beside the lane's line) it is Leaving from now on but stays where it is until the
        /// lane is clear for it: see <see cref="TakeDeparted"/>.
        /// </summary>
        public void Depart()
        {
            if (State != Phase.Standing || !Held)
            {
                return;
            }
            Vector3 right = Vector3.Cross(Vector3.up, Stop.Along);
            if (Vector3.Dot(Car.transform.position - Stop.Lane, right) < BayLeast)
            {
                Onward(_line, Stop.PathIndex);
                return;
            }
            _awaitGap = true;
            _gapSince = Time.time;
            State = Phase.Leaving;
            _since = Time.time;
            TrafficTools.Touch(Car, "a bus in its bay, doors closed, waiting for the lane to be clear");
        }

        // Standing in the bay with the doors closed. It gives way to everything in the lane: it goes only when the lane is
        // clear from GapBack behind its tail to GapAhead beyond its nose; after GapPatience, when it is clear from
        // GapBackLate behind (it edges out, as a bus does); and never while anything is beside it. No gap in GapMost:
        // it is let go where it stands, out of the lane, and the next bus comes when it is due.
        private void AwaitGap(float now)
        {
            float waited = now - _gapSince;
            if (LaneClear(waited >= GapPatience ? GapBackLate : GapBack))
            {
                _gapWaited = waited;
                _awaitGap = false;
                _departed = true;
                _pulledOut = true;
                PullOut(now);
            }
            else if (waited > GapMost)
            {
                _gapWaited = waited;
                _awaitGap = false;
                _departed = true;
                _pulledOut = false;
                Release(remove: false);
            }
        }

        // Nothing but itself in the lane beside it: one box, from so far behind its tail to a little beyond its nose.
        private bool LaneClear(float back)
        {
            Vector3 middle = Car.transform.position - Stop.Lane;
            middle.y = 0f;
            float at = Vector3.Dot(middle, Stop.Along) + _middleAhead;
            float halfLength = (_noseAhead + _tailBehind) * 0.5f;
            Vector3 from = Stop.Lane + Stop.Along * (at - halfLength - back);
            Vector3 to = Stop.Lane + Stop.Along * (at + halfLength + GapAhead);
            return SceneParking.InStrip(from, to, GapHalf, Car.rb, TrafficTools.VehicleMask) == null;
        }

        // Out of the bay: a path of its own from where it really stands back onto the lane's line (BusBay.Out), then the
        // lane's own points from where that path ends.
        private void PullOut(float now)
        {
            TransitLine line = _line;
            Vector3 right = Vector3.Cross(Vector3.up, Stop.Along);
            Vector3 stands = Car.transform.position - Stop.Lane;
            stands.y = 0f;
            float along = Vector3.Dot(stands, Stop.Along);
            float aside = Vector3.Dot(stands, right);
            BusBay.Curve curve = BusBay.Out(aside, _tailBehind, BusBay.RunMost - Mathf.Max(0f, along));
            int points = curve.Points;
            Unpark();
            _route.Clear();
            _routePoints.Clear();
            for (int k = 1; k <= points; k++)
            {
                float gone = k * BusBay.Step;
                _route.Add(Stop.Lane + Stop.Along * (along + gone) + right * (aside - curve.Aside(gone)));
                _routePoints.Add(-1);
            }
            _bayFrom = 0;
            _bayTo = _route.Count;
            float rejoins = along + points * BusBay.Step + 0.5f;
            for (int i = Stop.PathIndex + 1; i < line.Path.Length && line.Metres[i] - Stop.Metres < OnwardMetres; i++)
            {
                if (line.Metres[i] - Stop.Metres > rejoins)
                {
                    Add(line, i);
                }
            }
            _since = now;
            if (_route.Count < 2)
            {
                Release(remove: false);   // nowhere to go: let go where it stands
                return;
            }
            // (At the end of its line there are no lane points to follow: it still pulls out onto the lane, and is let go there.)
            Begin();
            BayMode(true);
            _driver.CruiseSpeed = 1.5f;
            Remaining = _driver.RouteLeft;
            TrafficTools.Touch(Car, "pulling out of its bay");
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
            _bay = false;
            _bayMode = false;
            _awaitGap = false;
            _bayTo = 0;
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
            TMP_FontAsset font = GameUi.GameFont.Asset;
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
