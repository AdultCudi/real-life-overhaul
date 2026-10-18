using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One bus on its line: the mod's own bus (<see cref="BusVehicle"/>), put on the road out of sight before a stop, driven to
    /// it by <see cref="BusDriver"/>, stood there with its doors open, and driven on and away. This makes it obey what its
    /// driver does not know of: red lights, closed roads, its stop's bay. Ticked four times a second by the director while
    /// out; the driver runs in the physics step only while it drives.
    /// </summary>
    public sealed partial class TransitBus
    {
        public enum Phase { Idle, Approaching, Standing, Leaving }
        public enum Hold { None, Light, Traffic, Closure }

        // Its measurements (BusVehicle), which the bays are laid out for.
        public const float BodyLength = BusVehicle.Length;
        public const float BodyHalfWidth = BusVehicle.HalfWidth;

        private const float Cruise = 11f;            // m/s, 40 km/h
        private const float JunctionSpeed = 7f;      // m/s
        private const int StartTries = 8;            // places tried to start from
        private const float LookAhead = 60f;         // metres of its route looked along for a red light or a closed road: more than it stops in
        private const float LitWithin = 170f;        // metres from the player within which a red light is waited at
        private const float LightPatience = 45f;     // seconds at a red light nobody is looking at before it is taken for one the game does not run
        private const float OnwardMetres = 240f;     // metres of its line it follows after the stop
        private const float LeaveFor = 50f;          // seconds after which a leaving bus is let go wherever it is
        private const float BayBraking = 1.5f;       // m/s2 it comes down to a bay's pace with, before the bay's path begins
        private const float BayLead = 6f;            // metres before a bay's path at which the driver is told where the kerb is
        private const float BayLookAt = 16f;         // metres before a bay's path at which the bay is looked at once more
        private const float BayIndicate = 45f;       // and at which its indicator comes on
        private const float BayNear = 9f;            // metres: held up on a bay's path with no more left, it stops where it is and stands there
        private const float GapPatience = 15f;       // seconds
        private const float GoOutMost = 240f;        // seconds a bus let go where it is seen drives on before it is let go when nobody sees it, wherever it is
        private const float LightApart = 15f;        // metres between two lights' stop lines that are the same junction

        public static IModLogger Logger;

        private readonly RideRules.HoldClock _hold = new RideRules.HoldClock();

        private readonly GameObject _host;
        private readonly BusDriver _driver;
        private readonly List<Vector3> _route = new List<Vector3>(96);
        private readonly List<int> _routePoints = new List<int>(96);  // the lane point each is, -1 for a place of our own
        private readonly List<float> _caps = new List<float>(96);       // the most it may be doing at each (a junction's pace)
        private TransitLine _line;
        private bool _parked;
        private int _gate = -1;          // the point of its route it has been told to pull up at, or -1
        private Hold _gateWhy;
        private float _since;
        private float _heldSince;
        private float _progressAt;
        private float _progressLeft;
        private readonly float _middleAhead = -BusVehicle.AxleFront;   // metres the middle of the bus is ahead of its front axle, the point its driver drives
        private readonly float _noseAhead = BusVehicle.NoseAhead;      // its nose, ahead of that point
        private readonly float _tailBehind = BusVehicle.TailBehind;    // its tail, behind it
        private readonly float _widthHalf = BusVehicle.HalfWidth;
        private bool _bay;               // its route ends in its stop's bay
        private bool _bayMode;           // the driver has been told where the kerb is
        private bool _bayLooked;         // the bay has been looked at once more as it came up
        private bool _awaitGap;          // doors closed, standing in the bay until the lane is clear
        private bool _departed;
        private bool _pulledOut;
        private bool _goingOut;          // let go where somebody was looking: driving on until nobody is
        private int _bayFrom;            // the first point of its route that is the bay's path
        private int _bayTo;              // and, leaving, the first that is the lane again
        private int _laneNext;           // the first point of its line not put on its route (the lane's points beside the bay)
        private int _onwardTo;           // and, leaving, the one after the last put on it
        private float _bayRun;           // metres of route from the first point of the bay's path to its end
        private float _bayBehind;        // metres of the strip it needs free behind the pole to come in
        private float _bayAhead;         // and ahead of it to pull out
        private BusBay.Curve _curveIn;   // its curve into the bay
        private float _holdUntil;        // it stands at its start, out of sight, until then, to keep the timetable
        private float _driveSeconds;     // its own reckoning of the drive left, as last made while driving
        private float _tickAt;           // when it was last ticked
        private float _gapWaited;

        public TransitBus(GameObject host)
        {
            _host = host;
            _driver = host.AddComponent<BusDriver>();
        }

        /// <summary>The bus while it is out; null when it is not.</summary>
        public BusVehicle Vehicle { get; private set; }
        public TransitStop Stop { get; private set; }
        public Phase State { get; private set; }

        public Hold Waiting { get; private set; }

        /// <summary>Seconds it has been standing for what it is waiting for.</summary>
        public float WaitingFor => Waiting == Hold.None ? 0f : Time.time - _heldSince;

        /// <summary>Seconds since it last got a metre nearer the end of its route.</summary>
        public float NoProgressFor => State == Phase.Approaching || State == Phase.Leaving ? Time.time - _progressAt : 0f;

        public float Remaining { get; private set; }   // metres of its route still to drive

        /// <summary>Seconds to stand at its stop as it will drive there, waits not counted; -1 when not on its way. The panel counts this down.</summary>
        public float SecondsLeft => State == Phase.Approaching ? _driver.SecondsLeft(Cruise, _bay ? _bayRun : 0f, BusBay.Speed) : -1f;

        public float StartedBack { get; private set; }   // metres of line before the stop it was put on the road at

        /// <summary>Seconds since it was put on the road or left its stop.</summary>
        public float Out => State == Phase.Idle ? 0f : Time.time - _since;

        public float Took { get; private set; }          // seconds from being put on the road to standing at its stop

        /// <summary>Why it stands at its stop short of its place, for the arrival's line; null at its place.</summary>
        public string StoodShort { get; private set; }

        public string Chassis { get; private set; }      // what it is, for the log

        public bool Lost { get; private set; }           // the vehicle was taken on its way to the stop; cleared by the next Send

        /// <summary>True once after the vehicle was lost on its way to the stop.</summary>
        public bool TakeLost()
        {
            bool lost = Lost;
            Lost = false;
            return lost;
        }

        /// <summary>Seconds it has been standing at its stop; 0 otherwise.</summary>
        public float Standing => State == Phase.Standing ? Time.time - _since : 0f;

        /// <summary>True when its route ends in its stop's bay.</summary>
        public bool InBay => _bay;

        /// <summary>Why it stops in the lane at a stop that has a bay; null otherwise.</summary>
        public string BayNote { get; private set; }

        public int SentAway { get; private set; }        // the game's parked cars taken in from the bay when it was put on the road

        public bool AwaitingGap => _awaitGap;

        /// <summary>True once after it has left its bay for good: pulled out after <paramref name="waited"/> seconds, or let go there.</summary>
        public bool TakeDeparted(out float waited, out bool pulledOut)
        {
            waited = _gapWaited;
            pulledOut = _pulledOut;
            bool departed = _departed;
            _departed = false;
            return departed;
        }

        /// <summary>True while it is out on the street.</summary>
        public bool Held => Vehicle != null && Vehicle.Out;

        /// <summary>True while the player can see the bus (TransitSight).</summary>
        public bool InView
        {
            get
            {
                if (Vehicle == null)
                {
                    return false;
                }
                Vehicle.Footprint(out Vector3 centre, out Vector3 forward, out float halfLength, out _);
                return TransitSight.Seen(centre, forward, halfLength);
            }
        }

        /// <summary>True within TransitSight.NearKeep of the player, where it is never taken off the street.</summary>
        public bool Near => Vehicle != null && TransitSight.Near(Vehicle.transform.position, TransitSight.NearKeep);

        /// <summary>Seconds it still stands at its start, to keep the timetable.</summary>
        public float HoldLeft => State == Phase.Approaching ? Mathf.Max(0f, _holdUntil - Time.time) : 0f;

        public float Held0 { get; private set; }         // seconds it was held at its start, all told

        public float DriveAtStart { get; private set; }  // seconds of open road it reckoned when put on the road, which it is judged by

        public int LightsAtStart { get; private set; }   // lit junctions on its way then

        public float AtLights { get; private set; }      // seconds at red lights since it was put on the road

        public string DepartureNote;                     // the director's departure line for a bus waiting in its bay, written when it goes
        public bool DepartureSound;                      // and whether the doors' sound went with it

        public TransitLine Line => _line;

        /// <summary>
        /// Seconds until it stands at its stop by the director's estimate (TransitPlan.Pace): its own reckoning at the pace
        /// traffic makes, the lit junctions ahead, its hold, a red light it stands at. -1 when not on its way.
        /// </summary>
        public float Estimate(TransitPlan.Pace pace)
        {
            if (State != Phase.Approaching)
            {
                return -1f;
            }
            float drive = _driver.Active ? SecondsLeft : _driveSeconds + 2f;   // pulled up at a light: what was left when it stopped, and the pull away
            if (_driver.Active)
            {
                _driveSeconds = drive;
            }
            return pace.Seconds(drive, LightsAhead(), HoldLeft, Waiting == Hold.Light, WaitingFor);
        }

        /// <summary>The lit junctions on its route still ahead, of those the game runs the lights of near the player.</summary>
        public int LightsAhead()
        {
            int count = 0;
            float run = 0f;
            float lastAt = -1000f;
            int next = Mathf.Max(0, _driver.Active ? _driver.NextPoint : _gate >= 0 ? _gate : 0);
            for (int i = next; i < _routePoints.Count; i++)
            {
                if (i > next)
                {
                    run += Vector3.Distance(_route[i], _route[i - 1]);
                }
                Waypoint point = _routePoints[i] >= 0 ? TransitMap.Point(_routePoints[i]) : null;
                if (point == null || !point.stop || !Lit(point) || (Waiting == Hold.Light && i == _gate))
                {
                    continue;
                }
                if (run - lastAt > LightApart)
                {
                    count++;
                    lastAt = run;
                }
            }
            return count;
        }

        /// <summary>Holds it where it was put for this many seconds before it sets off: it would be early otherwise.</summary>
        public void HoldFor(float seconds)
        {
            if (State != Phase.Approaching || seconds <= 0f)
            {
                return;
            }
            _holdUntil = Time.time + seconds;
            Held0 = seconds;
        }

        /// <summary>Set while the player rides at the game's fast forward: the stopped traffic counts as running, and no light is waited at.</summary>
        public static bool FastRide;

        /// <summary>True when the street can have a bus: traffic runs, or is stopped only for the game's fast forward.</summary>
        public static bool StreetReady => TrafficTools.Ready || TrafficTools.Paused;

        public bool DoorsOpen => Vehicle != null && !Vehicle.DoorsShut;

        /// <summary>Why a bus could not drive its line between two of its points in real time (a gap the road graph does not join), or null.</summary>
        public static string GapWhy(TransitLine line, int fromIndex, int toIndex)
        {
            if (line == null || line.Path == null || fromIndex < 0 || toIndex >= line.Path.Length || fromIndex > toIndex)
            {
                return "the line's points are not there";
            }
            Waypoint last = null;
            for (int i = fromIndex; i <= toIndex; i++)
            {
                Waypoint point = TransitMap.Point(line.Path[i]);
                if (point == null)
                {
                    return "lane point " + line.Path[i] + " of the line is missing from the road graph";
                }
                if (last != null)
                {
                    bool joined = (last.neighbors != null && last.neighbors.Contains(line.Path[i])) || (last.otherLanes != null && last.otherLanes.Contains(line.Path[i]));
                    float apart = CitySpots.Flat(point.position - last.position);
                    if ((!joined && apart > LooseGap * LooseGap) || apart > MostGap * MostGap)
                    {
                        return "a gap of " + Mathf.Sqrt(apart).ToString("F0") + " m in the line's path at lane point " + line.Path[i] + (joined ? "" : " that the road graph does not join");
                    }
                }
                last = point;
            }
            return null;
        }

        private const float MostGap = 60f;     // metres between two lane points of a line, at the most
        private const float LooseGap = 12f;    // and between two the road graph does not join (the line's search crossed between them)

        /// <summary>
        /// With a rider aboard who goes further: once out on its lane leaving its stop, it drives on to the next stop of its line
        /// and stands there as at any. False, with why, if it cannot now.
        /// </summary>
        public bool CarryTo(TransitStop next, out string refusal)
        {
            refusal = null;
            if (State != Phase.Leaving || _awaitGap || _bayTo > 0 || _goingOut || _line == null || Stop == null || Vehicle == null || !Held)
            {
                refusal = "it is not out on its lane leaving its stop";
                return false;
            }
            if (next == null || next.Line != Stop.Line || next.PathIndex <= Stop.PathIndex)
            {
                refusal = "the stop is not further along its line";
                return false;
            }
            TransitLine line = _line;
            int from = LineIndexAhead(line);
            if (from < 0 || from >= next.PathIndex - 1)
            {
                refusal = "it is already past the place to turn in for the stop";
                return false;
            }
            refusal = GapWhy(line, from, next.PathIndex);
            if (refusal != null)
            {
                return false;
            }
            Unpark();
            Stop = next;
            _route.Clear();
            _routePoints.Clear();
            _gate = -1;
            _gateWhy = Hold.None;
            Waiting = Hold.None;
            _bayFrom = 0;
            _bayTo = 0;
            _bayLooked = false;
            _bayMode = false;
            _bay = BayOpen(next, sendAway: true);
            StoodShort = null;
            if (_bay)
            {
                RouteToBay(line, next, from);
            }
            else
            {
                _laneNext = from;
                RouteToLane(line, next);
            }
            StartedBack = next.Metres - line.Metres[from];
            State = Phase.Approaching;
            Lost = false;
            Took = 0f;
            _since = Time.time;
            Vehicle.Indicate(0);
            Begin();
            Remaining = _driver.RouteLeft;
            return true;
        }

        // The first point of its line ahead of its front axle: the next lane point of its route, else the nearest ahead of it.
        private int LineIndexAhead(TransitLine line)
        {
            for (int k = Mathf.Max(0, _driver.NextPoint); k < _routePoints.Count; k++)
            {
                if (_routePoints[k] < 0)
                {
                    continue;
                }
                for (int i = Stop.PathIndex + 1; i < line.Path.Length; i++)
                {
                    if (line.Path[i] == _routePoints[k])
                    {
                        return i;
                    }
                }
            }
            Vector3 front = _driver.Front;
            for (int i = Stop.PathIndex + 1; i + 1 < line.Path.Length; i++)
            {
                Waypoint point = TransitMap.Point(line.Path[i]);
                Waypoint after = TransitMap.Point(line.Path[i + 1]);
                if (point == null || after == null)
                {
                    return -1;
                }
                Vector3 way = after.position - point.position;
                way.y = 0f;
                Vector3 to = point.position - front;
                to.y = 0f;
                if (Vector3.Dot(to, way) > 0.5f && to.sqrMagnitude > 4f)
                {
                    return i;
                }
            }
            return -1;
        }

        private void Begin()
        {
            _gate = -1;
            _gateWhy = Hold.None;
            Waiting = Hold.None;
            // A junction's pace is part of the path it drives, and of what it reckons the drive will take.
            _caps.Clear();
            for (int i = 0; i < _routePoints.Count; i++)
            {
                Waypoint point = _routePoints[i] >= 0 ? TransitMap.Point(_routePoints[i]) : null;
                _caps.Add(point != null && !TrafficManager.IsNotIntersection(point) ? JunctionSpeed : float.MaxValue);
            }
            _driver.BeginRoute(Vehicle, _route, _bay && State != Phase.Leaving ? BayNear : 0f, _caps);
            _driver.CruiseSpeed = Cruise;
            _driver.NoKerb();
            _bayMode = false;   // a drive begun resets the driver: told again on the bay's path
            _progressAt = Time.time;
            _progressLeft = float.MaxValue;
        }

        /// <summary>Four times a second while it is out.</summary>
        public void Tick(float now)
        {
            if (State == Phase.Idle)
            {
                return;
            }
            if (!Held || !StreetReady)
            {
                // Taken off the street under it (traffic shut down for a building or a time skip).
                bool wanted = State == Phase.Approaching || State == Phase.Standing;
                Abandon();
                Lost = wanted;
                return;
            }
            float dt = Mathf.Clamp(now - _tickAt, 0f, 1f);
            _tickAt = now;
            if (Waiting == Hold.Light)
            {
                AtLights += dt;
            }
            switch (State)
            {
                case Phase.Approaching:
                    Drive(now);
                    if (_driver.Arrived && _gate < 0 && !_driver.Active)
                    {
                        StandAtStop(now, null);
                    }
                    else
                    {
                        OnTheWay(now);
                    }
                    break;

                case Phase.Leaving:
                    if (_awaitGap)
                    {
                        _hold.Clear();   // in its bay, doors shut, waiting for the lane: the departure's line says how long
                        AwaitGap(now);
                        break;
                    }
                    Drive(now);
                    OnTheWay(now);
                    if (_goingOut)
                    {
                        // Let go where somebody could see it or near the player: it goes once nobody can and the player is not
                        // near, driving on along the road; after GoOutMost only being seen keeps it, standing.
                        bool seen = InView || (Near && now - _since < GoOutMost);
                        if (!seen)
                        {
                            Release(remove: true);
                        }
                        else if (_driver.Arrived && !_driver.Active && _gate < 0 && now - _since < GoOutMost)
                        {
                            DriveOn();
                        }
                    }
                    else if ((_driver.Arrived && _gate < 0 && !_driver.Active) || now - _since > LeaveFor)
                    {
                        Release(remove: false);
                    }
                    break;

                default:
                    _hold.Clear();
                    break;
            }
        }

        // At rest at its stop: at its place, or where it was held up short of it (shortWhy).
        private void StandAtStop(float now, string shortWhy)
        {
            if (_driver.Active)
            {
                _driver.Stop();
            }
            _gate = -1;
            _gateWhy = Hold.None;
            Park();
            State = Phase.Standing;
            Took = now - _since;
            _since = now;
            Remaining = 0f;
            Waiting = Hold.None;
            StoodShort = shortWhy;
            _hold.Clear();
            Vehicle.Indicate(0);
            Vehicle.Doors(true);
        }

        // What the driver does not know of: red lights and closed lane points. And how it is getting on.
        private void Drive(float now)
        {
            if (State == Phase.Approaching && now < _holdUntil && _driver.Active)
            {
                // Held at its start, out of sight, to keep the timetable: it stands, engine running, then sets off.
                _driver.CruiseSpeed = 0f;
                _progressAt = now;
                Waiting = Hold.None;
                Remaining = _driver.RouteLeft;
                return;
            }
            if (_gate >= 0 && !_driver.Active)
            {
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
                Remaining = _driver.Active ? _driver.RouteLeft : _driver.PathLeft;   // pulled up: still that far from its stop
                return;
            }
            if (!_driver.Active)
            {
                return;   // arrived at the end of its route: its owner's turn
            }
            Vector3 position = _driver.Front;
            int next = _driver.NextPoint;
            int gate = -1;
            Hold why = Hold.None;
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
            }
            if (gate != _gate)
            {
                _gate = gate;
                _driver.PullUpAt(gate);
            }
            _gateWhy = why;
            float cruise = Cruise;   // a junction's pace is in the driver's path (Begin)
            float toGo = _driver.RouteLeft;
            if (State == Phase.Approaching && _bay)
            {
                // Coming up to its bay: down to the bay's pace, the bay looked at once more, the driver told of the kerb just before it.
                float lead = toGo - _bayRun;
                if (!_bayLooked && lead <= BayLookAt)
                {
                    _bayLooked = true;
                    if (next < _bayFrom && !TransitBays.Free(Stop.At, _bayBehind, _bayAhead, Vehicle.Body, false, out string taken, out _))
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
                    Vehicle.Indicate(lead <= BayIndicate ? 1 : 0);
                }
                else
                {
                    BayMode(ByStop(position));   // put back on the lane's line as it came up
                }
            }
            else if (State == Phase.Approaching || (State == Phase.Leaving && _bayTo == 0 && !_goingOut))
            {
                // On the lane's line by its stop, whoever waits on the pavement is not in its way.
                BayMode(ByStop(position));
            }
            else if (State == Phase.Leaving && _bayTo > 0)
            {
                // Pulling out: gently, at the bay's pace until it is on the lane's own points again.
                if (next >= _bayTo)
                {
                    _bayTo = 0;
                    BayMode(false);
                    Vehicle.Indicate(0);
                }
                else
                {
                    BayMode(true);
                    cruise = Mathf.Min(BusBay.Speed, 1.5f + 1.5f * (now - _since));
                }
            }
            if (State == Phase.Leaving && !Vehicle.DoorsShut)
            {
                cruise = 0f;   // it pulls away once its doors are shut
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

        // The game runs only the lights near the player; one further off stays as it was for good. In fast forward they all stand still.
        private static bool Lit(Waypoint point)
        {
            return !FastRide && !TrafficTools.Paused && CitySpots.Flat(point.position - PlayerHelper.GetPosition()) < LitWithin * LitWithin;
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
                    return false;   // a light nobody looks at that has not changed in all that time is one the game is not running
                }
            }
            return true;
        }

        private void Park()
        {
            if (_parked || Vehicle == null)
            {
                return;
            }
            _driver.Stop();
            _parked = true;
        }

        private void Unpark()
        {
            _parked = false;
        }

        /// <summary>
        /// Where it came to rest against its stop: metres along from its place, off the lane's line (towards the kerb positive),
        /// degrees off the lane, and by its nearest corners metres to the kerb's edge and to the lane's (negative: in the lane).
        /// </summary>
        public void Measure(out float along, out float aside, out float angle, out float toKerb, out float toLane)
        {
            along = aside = angle = toKerb = toLane = 0f;
            if (Vehicle == null || Stop == null)
            {
                return;
            }
            Vehicle.Footprint(out Vector3 centre, out Vector3 forward, out _, out _);
            Vector3 right = Vector3.Cross(Vector3.up, Stop.Along);
            Vector3 offset = centre - Stop.Lane;
            offset.y = 0f;
            along = Vector3.Dot(offset, Stop.Along) - Stop.Stand;   // from where it is to stand
            aside = Vector3.Dot(offset, right);
            angle = Vector3.Angle(forward, Stop.Along);
            float halfLength = (_noseAhead + _tailBehind) * 0.5f;
            toKerb = BusBay.KerbClear(Stop.KerbAside, aside, angle, halfLength, _widthHalf);
            toLane = BusBay.LaneClear(aside, angle, halfLength, _widthHalf);
        }

        /// <summary>Where somebody stands to step in at its front door, and the way they face then.</summary>
        public void DoorPlace(out Vector3 place, out Vector3 inwards)
        {
            Vehicle.Footprint(out Vector3 centre, out Vector3 forward, out _, out _);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            inwards = -right;
            place = centre + right * (BusVehicle.HalfWidth + BusVehicle.DoorOut) + forward * BusVehicle.FrontDoorAhead;
        }

        /// <summary>Where somebody steps down onto the road from its middle door, and the way into the bus from there.</summary>
        public void MiddleDoorPlace(out Vector3 place, out Vector3 inwards)
        {
            Vehicle.Footprint(out Vector3 centre, out Vector3 forward, out _, out _);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            inwards = -right;
            place = centre + right * (BusVehicle.HalfWidth + BusVehicle.DoorOut) + forward * BusVehicle.MiddleDoorAhead;
        }
    }
}
