using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One bus on its line: the mod's own city bus (<see cref="BusVehicle"/>), put on the road out
    /// of sight some way before a stop, driven to it by its own driver (<see cref="BusDriver"/>),
    /// stood there with its doors open, and driven on and away. The game has no bus.
    ///
    /// WHY ITS OWN. Until this version the bus was a van of the traffic pool wearing a bus body:
    /// whatever van was free, with wheels of its own under the body's (so the body's never turned)
    /// and a wheelbase that was seldom the body's (so the body slid round bends), held by the
    /// traffic system's springs and frozen to stand. A bus of the mod's own has its own measurements
    /// for everything that is worked out from them (where it stands in a bay, the curve in and out,
    /// the door), wheels that roll and steer, doors, lights, and nothing of the traffic pool to come
    /// and go under it.
    ///
    /// WHAT IT OBEYS. Its driver keeps to the line's lane points (it never overtakes: it is a bus),
    /// slows for bends and stops behind whatever is in its way; this class makes it obey what the
    /// driver does not know of: it pulls up at a red light and before lane points a scene has
    /// closed, and goes on when they open.
    ///
    /// WHERE IT STANDS. At a stop with a bay (<see cref="TransitStop.HasBay"/>, <see cref="BusBay"/>)
    /// that is free, the end of its route is not the lane's line but a path of its own across the
    /// strip beside the lane (points a metre apart, for its front axle, which its driver keeps to
    /// exactly): it stands parallel to the kerb, door side BusBay.KerbGap from it, its body beside
    /// the lane, and the lane is free. To leave it waits, standing, until the lane beside and
    /// behind it is clear (one box four times a second while it waits, nothing otherwise), then
    /// follows a path of its own back onto the lane's line. Where the stop has no bay, or the bay is
    /// taken when the bus sets out or as it comes up, it stops on the lane's line as it always has.
    /// It shows what it does: the right indicator as it comes up to its bay, the left one as it
    /// waits to pull out, its doors open while it stands.
    ///
    /// HELD UP ON ITS WAY. Its driver brings it to rest exactly at its place when nothing is in its way.
    /// When something is (a car, somebody in the road, the player), it stands behind it; once it has
    /// stood still for two seconds with a door by the stop's kerb, or held up by the player near its
    /// place, it is at the stop where it is (RideRules.StandsWhereItIs): nobody waits for a bus that
    /// waits for them. Coming up to a stop on the lane's line and pulling away from it, its driver
    /// knows where the stop's kerb is, so whoever waits on the pavement is never in its way. A car of
    /// the traffic that stands for the bus while the bus stands for it is told, after four seconds,
    /// that the bus is not in its way (RideRules.Yield), unless the bus is on its own way ahead. Every
    /// hold of more than five seconds on the road is written to the log once, with what holds it and
    /// how far it is from its place (HoldClock: a few comparisons four times a second, the words only
    /// then).
    ///
    /// Ticked four times a second by the director while it is out; the driver runs in the physics
    /// step only while it drives. Let go where somebody is looking, it drives on along its line
    /// and goes when nobody is.
    /// </summary>
    public sealed class TransitBus
    {
        public enum Phase { Idle, Approaching, Standing, Leaving }
        public enum Hold { None, Light, Traffic, Closure }

        // Its measurements (BusVehicle): the bays are laid out for them (TransitMap.LayBay).
        public const float BodyLength = BusVehicle.Length;
        public const float BodyHalfWidth = BusVehicle.HalfWidth;

        private const float Cruise = 11f;            // m/s, 40 km/h
        private const float JunctionSpeed = 7f;
        private const float StartBack = 125f;        // metres of line before the stop it starts from, if it can
        private const float StartBackMost = 330f;
        private const float StartBackLeast = 60f;
        private const float StartApart = 14f;        // metres between two places tried to start from
        private const int StartTries = 8;
        private const float LookAhead = 60f;         // metres of its route looked along for a red light or a closed road: more than it stops in
        private const float LitWithin = 170f;        // metres from the player within which a red light is waited at
        private const float LightPatience = 45f;     // seconds at a red light nobody is looking at before it is taken to be one the game is not running
        private const float FinalShort = 1.2f;       // the driver stops this far short of the last point of a route
        private const float OnwardMetres = 240f;     // of its line it follows after the stop
        private const float LeaveFor = 50f;          // seconds after which a leaving bus is let go wherever it is
        private const float BayBraking = 1.5f;       // m/s2 it comes down to a bay's pace with, before the bay's path begins
        private const float BayLead = 6f;            // metres before a bay's path at which the driver is told where the kerb is
        private const float BayLookAt = 16f;         // metres before a bay's path at which the bay is looked at once more
        private const float BayIndicate = 45f;       // ...and at which its indicator comes on
        private const float BayNear = 9f;            // held up on a bay's path with no more than this left, it stops where it is and that is its stand
        private const float BayLeast = 0.6f;         // metres aside of the lane's line from which a standing bus pulls out along a path of its own
        private const float GapBack = 30f;           // metres of lane behind its tail that must be clear for it to pull out
        private const float GapBackLate = 12f;       // ...once it has waited GapPatience: it edges out as a bus does
        private const float GapAhead = 5f;           // ...and ahead of its nose, always
        private const float GapHalf = 1.2f;          // half the width of the lane that is looked at
        private const float GapPatience = 15f;
        private const float GapMost = 45f;           // seconds after which a bus that still cannot pull out is let go where it stands
        private const float OnwardMore = 160f;       // metres of line more for a bus let go where somebody is looking: it drives on out of sight
        private const float StartBackFar = 520f;     // metres of line before the stop it may start from once nothing nearer is out of sight
        private const float GoOutMost = 240f;        // seconds a bus let go where somebody can see it drives on before it is let go when nobody can, wherever it is
        private const float LightApart = 15f;        // metres between two lights' stop lines that are the same junction
        private const float KerbNear = 20f;          // metres along the lane either side of a stop's lane point within which its driver knows where its kerb is
        private const float StillSpeed = 0.3f;       // m/s under which it is standing still
        private const float CarClear = 6f;           // metres beyond a car's nose, and
        private const float CarHalfWay = 1.5f;       // ...either side of its middle, that are the car's own way ahead (RideRules.Yield)

        public static IModLogger Logger;

        private static FieldInfo _carObstacles;      // VehicleComponent.obstacleList: what is in a traffic car's sensor (read once a hold at most)
        private static bool _carObstaclesLooked;
        private static int _playerLayer = -2;
        private readonly RideRules.HoldClock _hold = new RideRules.HoldClock();

        private readonly GameObject _host;
        private readonly BusDriver _driver;
        private readonly List<Vector3> _route = new List<Vector3>(96);
        private readonly List<int> _routePoints = new List<int>(96);  // the lane point each is (an index into the road graph), -1 for a place of our own
        private readonly List<float> _caps = new List<float>(96);       // the most it may be doing at each (a junction's pace), for its driver
        private readonly List<int> _starts = new List<int>(StartTries);
        private BusVehicle _made;        // the one bus this keeps, made the first time it is sent
        private TransitLine _line;
        private bool _parked;
        private int _gate = -1;          // the point of its route it has been told to pull up at, or -1
        private Hold _gateWhy;
        private float _since;
        private float _heldSince;
        private float _progressAt;
        private float _progressLeft;
        private readonly float _middleAhead = -BusVehicle.AxleFront;   // the middle of the bus, ahead of the point its driver drives (its front axle)
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
        private int _bayTo;              // ...and, leaving, the first that is the lane again
        private int _laneNext;           // the first point of its line not put on its route (the lane's points beside the bay)
        private int _onwardTo;           // ...and, leaving, the one after the last put on it
        private float _bayRun;           // metres of route from the first point of the bay's path to its end
        private float _bayBehind;        // metres of the strip it needs free behind the pole to come in
        private float _bayAhead;         // ...and ahead of it to pull out
        private BusBay.Curve _curveIn;   // its curve into the bay
        private float _gapSince;
        private float _holdUntil;        // it stands at its start, out of sight, until then: it keeps the timetable (TransitDirector)
        private float _driveSeconds;     // its own reckoning of the drive left, as last made while it was driving
        private float _tickAt;           // when it was last ticked

        public TransitBus(GameObject host)
        {
            _host = host;
            _driver = host.AddComponent<BusDriver>();
        }

        /// <summary>The bus while it is out; null when it is not.</summary>
        public BusVehicle Vehicle { get; private set; }
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

        /// <summary>
        /// Seconds it will take to stand at its stop as it will drive there from where it is (its own pace, its bends, its bay),
        /// waits on the way not counted; -1 when it is not on its way to the stop. What the stop's panel counts down.
        /// </summary>
        public float SecondsLeft => State == Phase.Approaching ? _driver.SecondsLeft(Cruise, _bay ? _bayRun : 0f, BusBay.Speed) : -1f;

        /// <summary>Metres of line from the stop it was put on the road at.</summary>
        public float StartedBack { get; private set; }

        /// <summary>Seconds since it was put on the road or left its stop.</summary>
        public float Out => State == Phase.Idle ? 0f : Time.time - _since;

        /// <summary>Seconds it took from being put on the road to standing at its stop.</summary>
        public float Took { get; private set; }

        /// <summary>
        /// Why it stands at its stop where it is and not at its place (held up short of it: RideRules.StandsWhereItIs), for
        /// the arrival's line; null when it came to rest at its place.
        /// </summary>
        public string StoodShort { get; private set; }

        /// <summary>What it is, for the log.</summary>
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

        /// <summary>True while it is out on the street.</summary>
        public bool Held => Vehicle != null && Vehicle.Out;

        /// <summary>True while the player can see the bus: on the screen, not too far, no building between (TransitSight).</summary>
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

        /// <summary>True while the bus is within TransitSight.NearKeep of the player: it is not taken off the street then, seen or not.</summary>
        public bool Near => Vehicle != null && TransitSight.Near(Vehicle.transform.position, TransitSight.NearKeep);

        /// <summary>Seconds it still stands at its start before it sets off (it keeps the timetable).</summary>
        public float HoldLeft => State == Phase.Approaching ? Mathf.Max(0f, _holdUntil - Time.time) : 0f;

        /// <summary>The seconds it was held at its start, all told.</summary>
        public float Held0 { get; private set; }

        /// <summary>Its own reckoning of the open road to its stop when it was put on the road, and the lit junctions on the way then: what it is judged by when it comes (TransitPlan.Pace.Learn).</summary>
        public float DriveAtStart { get; private set; }
        public int LightsAtStart { get; private set; }

        /// <summary>Seconds it has stood at red lights since it was put on the road.</summary>
        public float AtLights { get; private set; }

        /// <summary>The director's departure line for a bus that waits in its bay for the lane to be clear: written when it goes.</summary>
        public string DepartureNote;
        public bool DepartureSound;

        /// <summary>The line it is on while it is out.</summary>
        public TransitLine Line => _line;

        /// <summary>
        /// Seconds until it stands at its stop, as the director's estimate has it: its own reckoning of the road (its pace, its
        /// bends, its bay) at the pace traffic has been making, each lit junction still ahead, what is left of its hold at its
        /// start, and at a red light what is still to be expected of it (TransitPlan.Pace). -1 when it is not on its way.
        /// </summary>
        public float Estimate(TransitPlan.Pace pace)
        {
            if (State != Phase.Approaching)
            {
                return -1f;
            }
            float drive = _driver.Active ? SecondsLeft : _driveSeconds + 2f;   // (pulled up at a light: what was left when it stopped, and the pull away)
            if (_driver.Active)
            {
                _driveSeconds = drive;
            }
            return pace.Seconds(drive, LightsAhead(), HoldLeft, Waiting == Hold.Light, WaitingFor);
        }

        /// <summary>The lit junctions on its route still ahead of it (those the game runs the lights of: near the player).</summary>
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

        /// <summary>It stands where it was put for this many seconds before it sets off: it would be early otherwise.</summary>
        public void HoldFor(float seconds)
        {
            if (State != Phase.Approaching || seconds <= 0f)
            {
                return;
            }
            _holdUntil = Time.time + seconds;
            Held0 = seconds;
        }

        /// <summary>
        /// Set while the player rides a bus through the city at the game's fast forward (TransitDirector.Journey): the game has
        /// taken its traffic off and stopped its traffic system meanwhile, so the street counts as running all the same, and no
        /// light is waited at (the lights stand still with the traffic system).
        /// </summary>
        public static bool FastRide;

        /// <summary>The street can have a bus on it: the game's traffic is running, or stopped only for the game's fast forward (its own cars are
        /// taken off then; the buses drive on at its pace, so that a bus the player is watching does not vanish when they speed time up).</summary>
        public static bool StreetReady => TrafficTools.Ready || TrafficTools.Paused;

        /// <summary>True while its doors stand open.</summary>
        public bool DoorsOpen => Vehicle != null && !Vehicle.DoorsShut;

        /// <summary>
        /// Why a bus could not drive its line between two of the line's points in real time, or null if it can: every lane
        /// point there in the road graph, and no gap between two of them that the graph does not join.
        /// </summary>
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
        private const float LooseGap = 12f;    // ...and between two the road graph does not join (the line's search crossed between them)

        /// <summary>
        /// With a rider aboard who goes further: once it has left its stop and is out on its lane, it drives on along its line
        /// to the next stop instead of out of sight, and stands there as at any stop (in its bay if that is free). False, with
        /// why, if it cannot now.
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

        // The first point of its line ahead of its front axle: the next lane point of its route, else the nearest ahead by
        // where it is (on the path out of a bay its route's points are its own).
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

        // ---------------------------------------------------------------- putting it on the road

        /// <summary>Puts a bus on its line some way before a stop, heading for it. False, with why, if it cannot.</summary>
        /// <param name="mayAppearInView">Only after it could not be done out of view: it may then start in view, far off.</param>
        /// <param name="mostBack">No further back than this many metres of line (to move a bus up past what held it).</param>
        /// <param name="prefer">Metres of line before the stop it is best started at (the director's: what the seconds to the timetable's call take to drive).</param>
        public bool Send(TransitStop stop, bool mayAppearInView, out string refusal, float mostBack = StartBackMost, float prefer = StartBack)
        {
            refusal = null;
            if (State != Phase.Idle)
            {
                refusal = "the bus is still out";
                return false;
            }
            if (!StreetReady || stop == null || stop.Line >= TransitMap.Lines.Count)
            {
                refusal = "traffic is not running";
                return false;
            }
            TransitLine line = TransitMap.Lines[stop.Line];
            float closedBack = CollectStarts(line, stop, mayAppearInView, mostBack, prefer);
            if (_starts.Count == 0)
            {
                refusal = closedBack >= 0f
                    ? "the road is closed " + closedBack.ToString("F0") + " m before the stop and no plain lane out of view lies between"
                    : mayAppearInView ? "its line has no plain stretch of lane within " + mostBack.ToString("F0") + " m before this stop"
                    : "every plain stretch of lane it could start from is in view";
                return false;
            }
            BusVehicle bus = Bus();
            if (bus == null)
            {
                refusal = "the bus cannot be drawn (the game has no lit shader)";
                return false;
            }
            int start = -1;
            for (int i = 0; i < _starts.Count && start < 0; i++)
            {
                if (RoomAt(line, _starts[i]))
                {
                    start = _starts[i];
                }
            }
            if (start < 0)
            {
                refusal = "traffic stands on all " + _starts.Count + " of the lane points it could start from";
                return false;
            }
            Vector3 from = TransitMap.Point(line.Path[start]).position;
            Take(bus, stop, line);
            bus.Place(from, TransitMap.Point(line.Path[start + 1]).position - from, line);
            _driver.Take(bus);
            _bay = BayOpen(stop, sendAway: true);
            if (_bay)
            {
                RouteToBay(line, stop, start + 1);
            }
            else
            {
                _laneNext = start + 1;
                RouteToLane(line, stop);
            }
            StartedBack = stop.Metres - line.Metres[start];
            State = Phase.Approaching;
            Lost = false;
            _since = Time.time;
            _holdUntil = 0f;
            Held0 = 0f;
            AtLights = 0f;
            _tickAt = Time.time;
            Begin();
            Remaining = _driver.RouteLeft;
            _driveSeconds = SecondsLeft;
            DriveAtStart = _driveSeconds;
            LightsAtStart = LightsAhead();
            return true;
        }

        /// <summary>Puts a bus straight onto the lane just past a stop, already leaving: what a rider sees pull away after getting off.</summary>
        public bool SendLeaving(TransitStop stop)
        {
            if (State != Phase.Idle || !StreetReady || stop == null || stop.Line >= TransitMap.Lines.Count)
            {
                return false;
            }
            TransitLine line = TransitMap.Lines[stop.Line];
            // (At a stop with a bay it is put where a bus that has just pulled out is: on the lane, past the bay's end.)
            float lead = stop.HasBay ? stop.BayAhead : 0f;
            int last = Mathf.Min(stop.PathIndex + (stop.HasBay ? 14 : 9), line.Path.Length - 3);
            BusVehicle bus = Bus();
            if (bus == null)
            {
                return false;
            }
            int at = -1;
            for (int i = stop.PathIndex + 2; i <= last && at < 0; i += 2)
            {
                Waypoint point = TransitMap.Point(line.Path[i]);
                if (point != null && !point.temporaryDisabled && TrafficManager.IsNotIntersection(point) && line.Metres[i] - stop.Metres >= lead && RoomAt(line, i))
                {
                    at = i;
                }
            }
            if (at < 0)
            {
                return false;
            }
            Take(bus, stop, line);
            bus.Place(TransitMap.Point(line.Path[at]).position, stop.Along, line);
            _driver.Take(bus);
            Onward(line, at);
            return true;
        }

        /// <summary>
        /// Puts a bus standing at a stop, as one that has just pulled in would stand: in its bay where the bay is free, else on
        /// the lane's line where nothing stands. Its doors shut (whoever asked opens them). For the bus a rider arrives on,
        /// set out while the screen is dark. False, with why, if it cannot stand there now.
        /// </summary>
        public bool PutStanding(TransitStop stop, out string refusal)
        {
            refusal = null;
            if (State != Phase.Idle || !StreetReady || stop == null || stop.Line >= TransitMap.Lines.Count)
            {
                refusal = State != Phase.Idle ? "the bus is still out" : "traffic is not running";
                return false;
            }
            BusVehicle bus = Bus();
            if (bus == null)
            {
                refusal = "the bus cannot be drawn (the game has no lit shader)";
                return false;
            }
            TransitLine line = TransitMap.Lines[stop.Line];
            Vector3 right = Vector3.Cross(Vector3.up, stop.Along);
            bool bay = BayOpen(stop, sendAway: true);
            string bayNote = BayNote;
            Vector3 front;
            if (bay)
            {
                front = stop.Lane + stop.Along * (stop.Stand - _middleAhead) + right * _curveIn.Offset;   // where RouteToBay has it come to rest
            }
            else
            {
                front = stop.Lane + stop.Along * (stop.Stand - _middleAhead);            // ...and RouteToLane
                if (SceneParking.InStrip(stop.Lane - stop.Along * (BodyLength * 0.5f + 1f), stop.Lane + stop.Along * (BodyLength * 0.5f + 1f), BodyHalfWidth + 0.2f,
                        _made != null ? _made.Body : null, TrafficTools.VehicleMask) != null)
                {
                    refusal = "traffic stands on the lane at the stop" + (bayNote != null ? " and " + bayNote : "");
                    return false;
                }
            }
            Take(bus, stop, line);
            _bay = bay;
            BayNote = bayNote;
            bus.Place(front, stop.Along, line);
            _driver.Take(bus);
            Park();
            State = Phase.Standing;
            Lost = false;
            _since = Time.time;
            Took = 0f;
            StartedBack = 0f;
            Remaining = 0f;
            return true;
        }

        // Where it may start: plain lane points of its line before the stop, out of view, none of them before a closed road;
        // the one about StartBack away first, then further back, then nearer. Returns how far back the road is closed, or -1.
        private float CollectStarts(TransitLine line, TransitStop stop, bool mayAppearInView, float mostBack, float preferBack)
        {
            _starts.Clear();
            float closedBack = -1f;
            int floor = 1;
            for (int i = stop.PathIndex; i >= 1 && stop.Metres - line.Metres[i] <= Mathf.Max(mostBack, StartBackMost); i--)
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
            float prefer = Mathf.Clamp(preferBack, StartBackLeast, mostBack);
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

        private bool CanStart(TransitLine line, int index, bool unseen)
        {
            Waypoint point = TransitMap.Point(line.Path[index]);
            Waypoint next = index + 1 < line.Path.Length ? TransitMap.Point(line.Path[index + 1]) : null;
            if (point == null || next == null || point.temporaryDisabled || point.enter || point.exit || point.neighbors == null || point.neighbors.Count == 0
                || !TrafficManager.IsNotIntersection(point))
            {
                return false;
            }
            // Never within TransitSight.NearKeep of the player, seen or not; out of sight first (the whole body, buildings
            // taken into account: TransitSight), and in sight only as the test button's last resort, far off.
            if (TransitSight.Near(point.position, TransitSight.NearKeep))
            {
                return false;
            }
            Vector3 along = next.position - point.position;
            along.y = 0f;
            Vector3 middle = point.position + (along.sqrMagnitude > 0.0001f ? along.normalized : Vector3.forward) * _middleAhead;
            bool seen = TransitSight.Seen(middle, along, BodyLength * 0.5f + 0.5f);
            return unseen ? !seen : seen && !TransitSight.Near(point.position, 90f);
        }

        // The one bus this keeps, made the first time it is asked for.
        private BusVehicle Bus()
        {
            if (_made == null)
            {
                _made = BusVehicle.Make(_host.transform, "RealLifeOverhaul City Bus");
            }
            return _made;
        }

        // Nothing stands where its body would be put with its front axle at this point of its line (the traffic, a parked
        // car, the player's): one box.
        private bool RoomAt(TransitLine line, int index)
        {
            Waypoint point = TransitMap.Point(line.Path[index]);
            Waypoint next = index + 1 < line.Path.Length ? TransitMap.Point(line.Path[index + 1]) : null;
            if (point == null || next == null)
            {
                return false;
            }
            Vector3 along = next.position - point.position;
            along.y = 0f;
            if (along.sqrMagnitude < 0.01f)
            {
                return false;
            }
            along.Normalize();
            Vector3 middle = point.position + along * _middleAhead;
            return SceneParking.InStrip(middle - along * (BodyLength * 0.5f + 1f), middle + along * (BodyLength * 0.5f + 1f), BodyHalfWidth + 0.2f, _made != null ? _made.Body : null,
                       TrafficTools.VehicleMask) == null;
        }

        // Ours from here on, for one outing.
        private void Take(BusVehicle bus, TransitStop stop, TransitLine line)
        {
            Vehicle = bus;
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
            _goingOut = false;
            _gapWaited = 0f;
            _bayFrom = 0;
            _bayTo = 0;
            _bayRun = 0f;
            BayNote = null;
            SentAway = 0;
            StoodShort = null;
            _hold.Clear();
            Chassis = "the mod's own city bus (" + BodyLength.ToString("F1") + " m long, " + (BodyHalfWidth * 2f).ToString("F2") + " wide, wheelbase "
                      + BusVehicle.Wheelbase.ToString("F1") + "; " + (bus.Modelled ? "modelled" : "in plain shapes: the prop bundle has no city_bus yet") + ")";
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

        // Whether it can use its stop's bay now, and if not, why (BayNote). With sendAway, the game's parked cars that stand
        // in the bay out of view are taken in first (TransitBays).
        private bool BayOpen(TransitStop stop, bool sendAway)
        {
            BayNote = null;
            if (!stop.HasBay)
            {
                return false;
            }
            // Its curves and the strip they take, as the stop's bay was laid out (the one bus is what bays are laid out for).
            if (!BusBay.Lay(stop.KerbAside, out _, out _curveIn, out _, out float behind, out float ahead))
            {
                BayNote = "the bus is " + (_widthHalf * 2f).ToString("F2") + " m wide, too wide for the bay";
                return false;
            }
            _bayBehind = Mathf.Max(stop.BayBehind, behind);
            _bayAhead = Mathf.Max(stop.BayAhead, ahead);
            if (_bayBehind > stop.BayRoom + 0.01f || _bayAhead > stop.BayAhead + 0.01f)
            {
                BayNote = "the bus reaches " + _noseAhead.ToString("F1") + " m ahead of its front axle and " + _tailBehind.ToString("F1") + " m behind it and needs "
                          + _bayBehind.ToString("F0") + " m of bay to come in and " + _bayAhead.ToString("F0") + " to pull out, where the stop's has " + stop.BayRoom.ToString("F0") + " and " + stop.BayAhead.ToString("F0");
                return false;
            }
            bool free = TransitBays.Free(stop.At, _bayBehind, _bayAhead, Vehicle != null ? Vehicle.Body : null, sendAway, out string taken, out int sent);   // (the place's bay: its host's zone)
            SentAway += sent;
            if (!free)
            {
                BayNote = "its bay is taken by " + taken;
            }
            return free;
        }

        // The end of a route on the lane's line. The last point is a place of our own: where its front axle must come to
        // rest for the middle of the bus to stand beside the pole. The driver stops FinalShort before the last point of a route.
        private void RouteToLane(TransitLine line, TransitStop stop)
        {
            for (int i = _laneNext; i < stop.PathIndex; i++)
            {
                Add(line, i);
            }
            _laneNext = stop.PathIndex;
            _route.Add(stop.Lane + stop.Along * (stop.Stand + FinalShort - _middleAhead));   // (Stand: where neither door lines up with what stays on the kerb)
            _routePoints.Add(-1);
        }

        // The end of a route in the stop's bay: the lane's points up to where the bay's path leaves the lane's line, then
        // the path (BusBay.In, a point every metre, places of our own), the standing place and the point beyond it that
        // the driver stops short of. The path is laid for its front axle, which its driver keeps to exactly, and which
        // comes to rest where the middle of the bus is beside the pole.
        private void RouteToBay(TransitLine line, TransitStop stop, int from)
        {
            BusBay.Curve curve = _curveIn;
            float offset = curve.Offset;
            int points = curve.Points;
            float settle = BusBay.Settle;
            float leaves = points * BusBay.Step + settle + _middleAhead + 0.5f - stop.Stand;   // metres of line before the stop at which the path leaves the lane
            int i = from;
            for (; i < stop.PathIndex && stop.Metres - line.Metres[i] > leaves; i++)
            {
                Add(line, i);
            }
            _laneNext = i;
            _bayFrom = _route.Count;
            Vector3 right = Vector3.Cross(Vector3.up, stop.Along);
            Vector3 rest = stop.Lane + stop.Along * (stop.Stand - _middleAhead) + right * offset;
            for (int k = points; k >= 1; k--)
            {
                float before = k * BusBay.Step;
                _route.Add(rest - stop.Along * (before + settle) - right * curve.Aside(before));
                _routePoints.Add(-1);
            }
            // Along the bay's line while its rear comes in behind it (BusBay.Settle), to where it stands.
            for (float along = settle; along > 0.01f; along -= BusBay.Step)
            {
                _route.Add(rest - stop.Along * along);
                _routePoints.Add(-1);
            }
            _route.Add(rest);
            _routePoints.Add(-1);
            _route.Add(rest + stop.Along * FinalShort);
            _routePoints.Add(-1);
            _bayRun = points * BusBay.Step + settle + FinalShort;
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

        // Its front axle on the lane's straight by its stop: within KerbNear along of the stop's lane point, and on the lane.
        private bool ByStop(Vector3 front)
        {
            if (Stop == null)
            {
                return false;
            }
            Vector3 off = front - Stop.Lane;
            float along = off.x * Stop.Along.x + off.z * Stop.Along.z;
            float aside = off.x * Stop.Along.z - off.z * Stop.Along.x;   // (to the right of the lane, towards the kerb, positive)
            return along > -KerbNear && along < KerbNear && aside > -2f && aside < Stop.KerbAside;
        }

        // On a bay's path, and on the lane's line by its stop, the driver knows where the kerb is, so that nobody on the
        // pavement is taken to be in its way while its nose points at them.
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
                _driver.Kerb(Stop.Lane + right * Stop.KerbAside, right);
            }
            else
            {
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
            // A junction's pace is part of the path it drives (and of what it reckons the drive will take): not a change of mind as it comes up to one.
            _caps.Clear();
            for (int i = 0; i < _routePoints.Count; i++)
            {
                Waypoint point = _routePoints[i] >= 0 ? TransitMap.Point(_routePoints[i]) : null;
                _caps.Add(point != null && !TrafficManager.IsNotIntersection(point) ? JunctionSpeed : float.MaxValue);
            }
            _driver.BeginRoute(Vehicle, _route, _bay && State != Phase.Leaving ? BayNear : 0f, _caps);
            _driver.CruiseSpeed = Cruise;
            _driver.NoKerb();
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
                        _hold.Clear();   // (in its bay, doors shut, waiting for the lane: the departure's line says how long)
                        AwaitGap(now);
                        break;
                    }
                    Drive(now);
                    OnTheWay(now);
                    if (_goingOut)
                    {
                        // Let go where somebody could see it, or near the player: it goes once nobody can and the player is not
                        // near. At the end of its route it drives on along the road, out of sight; only after GoOutMost, still
                        // seen, does it stand where it is (it is never taken off where it can be seen).
                        bool seen = InView || (Near && now - _since < GoOutMost);   // (after GoOutMost only being seen keeps it)
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

        // It has come to rest at its stop: at its place, or where it was held up short of it (shortWhy).
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

        // On the road, four times a second: how long it has stood still. Held up near its stop with a door by its kerb (or by
        // the player near its place), it is at the stop; behind a car that waits for it, that car is told it is not in its way;
        // and a hold of more than a few seconds is written to the log once. Nothing but a few comparisons while it moves.
        private void OnTheWay(float now)
        {
            if ((State != Phase.Approaching && State != Phase.Leaving) || Vehicle == null)
            {
                _hold.Clear();
                return;
            }
            Vector3 at = Vehicle.transform.position;
            float still = _hold.At(at.x, at.z, now);
            if (still < RideRules.StandShortAfter || (_driver.Active && _driver.Speed >= StillSpeed))
            {
                return;
            }
            Collider by = _driver.HeldBy;
            if (State == Phase.Approaching && now >= _holdUntil && Stop != null)
            {
                bool player = by != null && IsPlayer(by);
                bool door = DoorOnKerb();
                if (RideRules.StandsWhereItIs(still, door, player, _driver.PathLeft))
                {
                    string why = "held up " + _driver.PathLeft.ToString("F1") + " m of its route short of its place by " + WaitsFor() + "; "
                                 + (door ? "a door of it by the stop's kerb" : "the player in front of it near its place") + ", it stands there";
                    StandAtStop(now, why);
                    return;
                }
            }
            if (by != null && Waiting == Hold.Traffic && !_hold.Yielded && still >= RideRules.YieldAfter)
            {
                YieldTo(by, still);
            }
            if (!_hold.Logged && still >= RideRules.HoldLogAfter)
            {
                _hold.Logged = true;
                Logger?.Info("Transit check (hold): " + HoldWords(still) + ".");
            }
        }

        // A door of it by the stop's kerb (RideRules.DoorOnKerb), as it stands now.
        private bool DoorOnKerb()
        {
            DoorsAlong(out float front, out float middle, out float aside, out float angle);
            return RideRules.DoorOnKerb(front, middle, aside, angle, Stop.Stand, Stop.KerbAside);
        }

        // Its doors' middles along the stop's lane from its lane point, the side they are in off the lane's line (towards the
        // kerb positive), and its angle to the lane.
        private void DoorsAlong(out float front, out float middle, out float aside, out float angle)
        {
            Vehicle.Footprint(out Vector3 centre, out Vector3 forward, out _, out _);
            Vector3 right = Vector3.Cross(Vector3.up, Stop.Along);
            Vector3 side = Vector3.Cross(Vector3.up, forward) * BusVehicle.HalfWidth;
            Vector3 frontDoor = centre + forward * BusVehicle.FrontDoorAhead + side - Stop.Lane;
            Vector3 middleDoor = centre + forward * BusVehicle.MiddleDoorAhead + side - Stop.Lane;
            front = frontDoor.x * Stop.Along.x + frontDoor.z * Stop.Along.z;
            middle = middleDoor.x * Stop.Along.x + middleDoor.z * Stop.Along.z;
            aside = ((frontDoor.x + middleDoor.x) * right.x + (frontDoor.z + middleDoor.z) * right.z) * 0.5f;
            angle = Vector3.Angle(forward, Stop.Along);
        }

        private static bool IsPlayer(Collider hit)
        {
            if (_playerLayer == -2)
            {
                _playerLayer = LayerMask.NameToLayer("Player");
            }
            if (_playerLayer >= 0 && hit.gameObject.layer == _playerLayer)
            {
                return true;
            }
            PlayerController player = PlayerHelper.PlayerController;
            return player != null && hit.transform.root == player.transform.root;
        }

        // Standing behind a car of the traffic for some seconds: if the car stands for the bus (the bus is in its sensor) and
        // the bus is not on the car's own way ahead, the car is told the bus is not in its way, as the game tells its cars of
        // a pedestrian put away (VehicleComponent.ColliderRemoved). Only this machine's own traffic, never a Going Public
        // guest's (TrafficTools.Ready), and nothing else of the car is touched. Once a hold.
        private void YieldTo(Collider by, float still)
        {
            Rigidbody body = by.attachedRigidbody;
            VehicleComponent car = body != null ? body.GetComponent<VehicleComponent>() : null;
            Collider solid = Vehicle.Solid;
            if (car == null || solid == null)
            {
                return;
            }
            bool standing = body.velocity.sqrMagnitude < 0.25f;
            bool waits = WaitsForBus(car);
            bool onItsWay = OnCarsWay(car, body);
            if (!RideRules.Yield(still, TrafficTools.Ready, standing, waits, onItsWay, _hold.Yielded))
            {
                return;
            }
            _hold.Yielded = true;
            SpecialDriveActionTypes was = car.GetCurrentAction();
            car.ColliderRemoved(solid);
            Logger?.Info("Transit check (hold): the " + (_line != null ? _line.Number : "bus") + " had stood " + still.ToString("F0") + " s behind '" + TrafficTools.Name(car)
                         + "', a car of the traffic that stood for the bus (" + was + ", the bus in its sensor), and is not on its way ahead: the car is told the bus is not in its way and "
                         + (WaitsForBus(car) ? "still has it in its sensor: FAIL" : "drives on when nothing else holds it: PASS") + ".");
        }

        // True when the bus is in the car's sensor (the game's own list of what it stands for).
        private bool WaitsForBus(VehicleComponent car)
        {
            if (!_carObstaclesLooked)
            {
                _carObstaclesLooked = true;
                _carObstacles = typeof(VehicleComponent).GetField("obstacleList", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            Collider solid = Vehicle != null ? Vehicle.Solid : null;
            return solid != null && _carObstacles != null && _carObstacles.GetValue(car) is List<Collider> seen && seen.Contains(solid);
        }

        // True when the bus is on the car's own way ahead of it: told to go on, it would drive into the bus.
        private bool OnCarsWay(VehicleComponent car, Rigidbody body)
        {
            Vector3 forward = car.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                return true;
            }
            forward.Normalize();
            Vector3 centre = body.worldCenterOfMass;
            Vector3 nearest = Vehicle.Solid.ClosestPoint(centre) - centre;
            nearest.y = 0f;
            float ahead = Vector3.Dot(nearest, forward);
            float aside = Vector3.Dot(nearest, Vector3.Cross(Vector3.up, forward));
            return ahead > 0f && ahead < Mathf.Max(car.length, 4f) * 0.5f + CarClear && Mathf.Abs(aside) < CarHalfWay;
        }

        /// <summary>What it is standing for now, in words for the log: what holds it up, a red light, its doors, or nothing.</summary>
        public string WaitsFor()
        {
            if (State == Phase.Approaching && Time.time < _holdUntil)
            {
                return "nothing on the road: it is held at its start, out of sight, to keep the timetable (" + HoldLeft.ToString("F0") + " s more)";
            }
            if (_awaitGap)
            {
                return "a gap in the lane to pull out of its bay into";
            }
            if (State == Phase.Leaving && Vehicle != null && !Vehicle.DoorsShut)
            {
                return "its doors to shut";
            }
            if (_gate >= 0 && !_driver.Active)
            {
                return _gateWhy == Hold.Light ? "a red light at the junction ahead" : _gateWhy == Hold.Closure ? "a road ahead that a scene has closed" : "the place it was told to pull up at";
            }
            Collider by = _driver.HeldBy;
            if (by != null)
            {
                return Describe(by) + " within " + (_driver.HeldAhead + 3f).ToString("F0") + " m of its nose";
            }
            return _driver.Active ? "nothing in its way (its driver's pace " + _driver.CruiseSpeed.ToString("F1") + " m/s, at " + _driver.Speed.ToString("F1") + ")" : "nothing: its driver has stopped";
        }

        // What a collider in its way is, for the log.
        private string Describe(Collider hit)
        {
            Rigidbody body = hit.attachedRigidbody;
            string name = (body != null ? body.gameObject : hit.gameObject).name;
            string layer = LayerMask.LayerToName(hit.gameObject.layer);
            VehicleComponent car = body != null ? body.GetComponent<VehicleComponent>() : null;
            if (car != null)
            {
                float speed = body.velocity.magnitude;
                return "a car of the traffic ('" + TrafficTools.Name(car) + "', " + (speed < 0.5f ? "standing" : "at " + (speed * 3.6f).ToString("F0") + " km/h") + ", its driver: "
                       + car.GetCurrentAction() + ", " + (WaitsForBus(car) ? "the bus in its sensor: it stands for the bus" : "the bus not in its sensor") + ")";
            }
            if (IsPlayer(hit))
            {
                return "the player, in the road in front of it";
            }
            if (body != null && body.GetComponent<BusVehicle>() != null)
            {
                return "another of the mod's buses ('" + name + "')";
            }
            switch (layer)
            {
                case "Human": return "somebody on foot in the road ('" + name + "')";
                case "ParkedVehicles": return "a parked car reaching into the lane ('" + name + "')";
                case "PlayerVehicles": return "a player's car ('" + name + "')";
                default: return "'" + name + "' on the layer " + layer;
            }
        }

        // The hold, in words for the log: where it is against its place, what holds it, who can see it.
        private string HoldWords(float still)
        {
            string line = _line != null ? _line.Number : "bus";
            string label = Stop != null ? TransitDirector.StopLabel(Stop) : "its stop";
            string where;
            if (State == Phase.Approaching && Stop != null)
            {
                Measure(out float along, out float aside, out float angle, out _, out _);
                bool door = DoorOnKerb();
                where = "on its way to " + label + ", " + _driver.PathLeft.ToString("F1") + " m of its route from where it is to stand (its middle " + Mathf.Abs(along).ToString("F1") + " m "
                        + (along >= 0f ? "past" : "short of") + " its place, " + Mathf.Abs(aside).ToString("F1") + " m " + (aside >= 0f ? "towards the kerb" : "away from the kerb")
                        + " off the lane's line, " + angle.ToString("F0") + " degrees off its direction; " + (door ? "a door by the stop's kerb" : "no door by the stop's kerb") + ")";
            }
            else
            {
                float from = Stop != null ? Mathf.Sqrt(CitySpots.Flat(Vehicle.transform.position - Stop.Lane)) : 0f;
                where = (_goingOut ? "driving away out of sight from " : "leaving ") + label + ", " + from.ToString("F0") + " m from where it stood there";
            }
            return "the " + line + " has stood still for " + still.ToString("F0") + " s " + where + "; it waits for " + WaitsFor() + "; seen: " + (InView ? "yes" : "no")
                   + ", " + Mathf.Sqrt(CitySpots.Flat(Vehicle.transform.position - PlayerHelper.GetPosition())).ToString("F0") + " m from the player";
        }

        // What the driver does not know of: red lights and lane points a scene has closed. And how it is getting on.
        private void Drive(float now)
        {
            if (State == Phase.Approaching && now < _holdUntil && _driver.Active)
            {
                // Held at its start, out of sight, so as to keep the timetable: it stands, engine running, and then sets off.
                _driver.CruiseSpeed = 0f;
                _progressAt = now;
                Waiting = Hold.None;
                Remaining = _driver.RouteLeft;
                return;
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
                Remaining = _driver.Active ? _driver.RouteLeft : _driver.PathLeft;   // (pulled up: still that far from its stop)
                return;
            }
            if (!_driver.Active)
            {
                return;   // it has arrived at the end of its route: its owner's turn
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
            float cruise = Cruise;   // (a junction's pace is in the driver's path: Begin)
            float toGo = _driver.RouteLeft;
            if (State == Phase.Approaching && _bay)
            {
                // Coming up to its bay: down to the bay's pace by where the path begins; the bay looked at once more while the
                // lane's line can still be had instead; the driver told of the path and the kerb just before it.
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
                    BayMode(ByStop(position));   // (put back on the lane's line as it came up)
                }
            }
            else if (State == Phase.Approaching || (State == Phase.Leaving && _bayTo == 0 && !_goingOut))
            {
                // Coming up to its stop on the lane's line, or pulling away from it: whoever waits on the pavement there is not in its way.
                BayMode(ByStop(position));
            }
            else if (State == Phase.Leaving && _bayTo > 0)
            {
                // Pulling out: gently from rest, at the bay's pace until it is on the lane's own points again.
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
                cruise = 0f;   // (it pulls away once its doors are shut)
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
        // Nor are any while the game fast-forwards a ride: its traffic system, the lights with it, stands still meanwhile.
        private static bool Lit(Waypoint point)
        {
            return !FastRide && !TrafficTools.Paused && CitySpots.Flat(point.position - PlayerHelper.GetPosition()) < LitWithin * LitWithin;   // (in the game's fast forward its lights stand still)
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

        // Standing: the driver lets go of it until it is told to drive again.
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
            if (Vehicle == null || Stop == null)
            {
                return;
            }
            Vehicle.Footprint(out Vector3 centre, out Vector3 forward, out _, out _);
            Vector3 right = Vector3.Cross(Vector3.up, Stop.Along);
            Vector3 offset = centre - Stop.Lane;
            offset.y = 0f;
            along = Vector3.Dot(offset, Stop.Along) - Stop.Stand;   // (from where it is to stand: Stop.Stand on from the pole)
            aside = Vector3.Dot(offset, right);
            angle = Vector3.Angle(forward, Stop.Along);
            float halfLength = (_noseAhead + _tailBehind) * 0.5f;
            toKerb = BusBay.KerbClear(Stop.KerbAside, aside, angle, halfLength, _widthHalf);
            toLane = BusBay.LaneClear(aside, angle, halfLength, _widthHalf);
        }

        /// <summary>Where somebody stands to step in at its front door, on its kerb side, and the way they face then.</summary>
        public void DoorPlace(out Vector3 place, out Vector3 inwards)
        {
            Vehicle.Footprint(out Vector3 centre, out Vector3 forward, out _, out _);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            inwards = -right;
            place = centre + right * (BusVehicle.HalfWidth + BusVehicle.DoorOut) + forward * BusVehicle.FrontDoorAhead;
        }

        /// <summary>Where somebody steps down onto the road from its middle door, on its kerb side, and the way into the bus from there.</summary>
        public void MiddleDoorPlace(out Vector3 place, out Vector3 inwards)
        {
            Vehicle.Footprint(out Vector3 centre, out Vector3 forward, out _, out _);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            inwards = -right;
            place = centre + right * (BusVehicle.HalfWidth + BusVehicle.DoorOut) + forward * BusVehicle.MiddleDoorAhead;
        }

        /// <summary>True when its door has a step to it (<c>board_step</c>): the bus's always has, low floor or not.</summary>
        public bool Modelled => true;

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
            Vehicle.Doors(false);
            Vector3 right = Vector3.Cross(Vector3.up, Stop.Along);
            if (Vector3.Dot(Vehicle.transform.position - Stop.Lane, right) < BayLeast)
            {
                Onward(_line, Stop.PathIndex);
                return;
            }
            _awaitGap = true;
            _gapSince = Time.time;
            State = Phase.Leaving;
            _since = Time.time;
            Vehicle.Indicate(-1);
        }

        // Standing in the bay with the doors closed. It gives way to everything in the lane: it goes only when the lane is
        // clear from GapBack behind its tail to GapAhead beyond its nose; after GapPatience, when it is clear from
        // GapBackLate behind (it edges out, as a bus does); and never while anything is beside it. No gap in GapMost:
        // it is let go where it stands, out of the lane, and the next bus comes when it is due.
        private void AwaitGap(float now)
        {
            if (!Vehicle.DoorsShut)
            {
                return;   // (its doors first)
            }
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
            Vector3 middle = Vehicle.transform.position - Stop.Lane;
            middle.y = 0f;
            float at = Vector3.Dot(middle, Stop.Along);
            float halfLength = BodyLength * 0.5f;
            Vector3 from = Stop.Lane + Stop.Along * (at - halfLength - back);
            Vector3 to = Stop.Lane + Stop.Along * (at + halfLength + GapAhead);
            return SceneParking.InStrip(from, to, GapHalf, Vehicle.Body, TrafficTools.VehicleMask) == null;
        }

        // Out of the bay: a path of its own for its front axle from where it really stands back onto the lane's line
        // (BusBay.Out), then the lane's own points from where that path ends.
        private void PullOut(float now)
        {
            TransitLine line = _line;
            Vector3 right = Vector3.Cross(Vector3.up, Stop.Along);
            Vector3 stands = _driver.Front - Stop.Lane;
            stands.y = 0f;
            float along = Vector3.Dot(stands, Stop.Along);
            float aside = Vector3.Dot(stands, right);
            BusBay.Curve curve = BusBay.Out(aside, BusBay.BusLeave, BusBay.RunMost - Mathf.Max(0f, along));
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
            int i = Stop.PathIndex + 1;
            for (; i < line.Path.Length && line.Metres[i] - Stop.Metres < OnwardMetres; i++)
            {
                if (line.Metres[i] - Stop.Metres > rejoins)
                {
                    Add(line, i);
                }
            }
            _onwardTo = i;
            if (i >= line.Path.Length)
            {
                Extend(OnwardMetres - RouteMetres());   // (the end of its line: on along the road)
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
        }

        private void Onward(TransitLine line, int fromIndex)
        {
            Unpark();
            _route.Clear();
            _routePoints.Clear();
            int i = fromIndex + 1;
            for (; i < line.Path.Length && line.Metres[i] - line.Metres[fromIndex] < OnwardMetres; i++)
            {
                Add(line, i);
            }
            _onwardTo = i;
            if (i >= line.Path.Length)
            {
                Extend(OnwardMetres - RouteMetres());   // (the end of its line: on along the road, not left standing in it)
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
        }

        // ---------------------------------------------------------------- letting it go

        /// <summary>
        /// Every way out. With <paramref name="remove"/>, or when nobody is looking its way, the bus is taken off the street
        /// at once; otherwise it drives on along its line and goes once nobody is looking (or it has driven it out).
        /// </summary>
        public void Release(bool remove)
        {
            if (State == Phase.Idle)
            {
                return;
            }
            // Seen, or near the player: it never just goes. It drives on along the road and is taken off once nobody can see it.
            if (!remove && Held && StreetReady && (InView || Near) && GoOut())
            {
                return;
            }
            _driver.Forget();
            if (Vehicle != null)
            {
                Vehicle.Hide();
            }
            Forget();
        }

        // Let go where somebody is looking: on along its line from the point after the last on its route, out of sight.
        private bool GoOut()
        {
            if (_line == null || _goingOut)
            {
                return _goingOut;
            }
            _goingOut = true;
            Unpark();
            Vehicle.Doors(false);
            Vehicle.Indicate(0);
            _route.Clear();
            _routePoints.Clear();
            // On along its line from where it is: the next point of its line ahead of it (on its way to its stop, that is not
            // the point after the stop: it would cut across to it), and on along the road where the line ends.
            int from = Mathf.Clamp(Mathf.Max(_onwardTo, Stop != null ? Stop.PathIndex + 1 : 0), 0, _line.Path.Length);
            int ahead = LineIndexFromFront(_line);
            if (ahead >= 0 && (State == Phase.Approaching || ahead > from))
            {
                from = ahead;
            }
            for (int i = from; i < _line.Path.Length && _line.Metres[i] - _line.Metres[Mathf.Max(0, from - 1)] < OnwardMore; i++)
            {
                Add(_line, i);
            }
            Extend(OnwardMore - RouteMetres());
            State = Phase.Leaving;
            _since = Time.time;
            _awaitGap = false;
            if (_route.Count < 2)
            {
                _gate = -1;        // (pulled up at a light, say: it is no longer waiting for one)
                _gateWhy = Hold.None;
                _driver.Stop();
                return true;   // nowhere at all to drive to: it stands there until nobody can see it
            }
            Begin();
            Remaining = _driver.RouteLeft;
            return true;
        }

        // Driving off, at the end of its route and still seen or near: on along the road, out of sight.
        private void DriveOn()
        {
            int had = _route.Count;
            if (had > 0)
            {
                Vector3 last = _route[had - 1];
                int lane = _routePoints[had - 1];
                _route.Clear();
                _routePoints.Clear();
                _route.Add(last);
                _routePoints.Add(lane);
            }
            Extend(OnwardMore);
            if (_route.Count >= 2)
            {
                _route.RemoveAt(0);
                _routePoints.RemoveAt(0);
                Begin();
                Remaining = _driver.RouteLeft;
            }
        }

        // The first point of its line ahead of where its front axle is, or -1.
        private int LineIndexFromFront(TransitLine line)
        {
            Vector3 front = _driver.Front;
            int best = -1;
            float nearest = 30f * 30f;
            for (int i = 0; i + 1 < line.Path.Length; i++)
            {
                Waypoint point = TransitMap.Point(line.Path[i]);
                Waypoint after = TransitMap.Point(line.Path[i + 1]);
                if (point == null || after == null)
                {
                    continue;
                }
                Vector3 to = point.position - front;
                to.y = 0f;
                Vector3 way = after.position - point.position;
                way.y = 0f;
                if (Vector3.Dot(to, way) > 0.5f && to.sqrMagnitude > 4f && to.sqrMagnitude < nearest)
                {
                    nearest = to.sqrMagnitude;
                    best = i;
                }
            }
            return best;
        }

        private float RouteMetres()
        {
            float metres = 0f;
            for (int i = 1; i < _route.Count; i++)
            {
                metres += Vector3.Distance(_route[i], _route[i - 1]);
            }
            return metres;
        }

        // On along the road graph from the last lane point of its route, as straight on as the road goes, for so many metres:
        // where its line ends (a terminus by a station) the bus drives on along the street and out of sight, as a bus going to
        // its depot does, and is not left standing in the road.
        private void Extend(float metres)
        {
            if (metres <= 1f)
            {
                return;
            }
            int lane = -1;
            for (int i = _routePoints.Count - 1; i >= 0 && lane < 0; i--)
            {
                lane = _routePoints[i];
            }
            if (lane < 0 && _route.Count == 0 && _line != null && Stop != null && Stop.PathIndex < _line.Path.Length)
            {
                lane = _line.Path[Mathf.Min(_line.Path.Length - 1, Stop.PathIndex)];   // (nothing of its line after the stop: on from the stop's own lane point)
            }
            Waypoint point = TransitMap.Point(lane);
            if (point == null)
            {
                return;
            }
            Vector3 heading = _route.Count >= 2 ? _route[_route.Count - 1] - _route[_route.Count - 2] : Vehicle != null ? Vehicle.transform.forward : Vector3.forward;
            heading.y = 0f;
            heading = heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector3.forward;
            float done = 0f;
            for (int guard = 0; done < metres && guard < 300; guard++)
            {
                int best = -1;
                float straightest = -0.2f;   // (never back the way it came)
                for (int k = 0; point.neighbors != null && k < point.neighbors.Count; k++)
                {
                    Waypoint next = TransitMap.Point(point.neighbors[k]);
                    if (next == null || next.temporaryDisabled || !RoadGraph.Drivable(next))
                    {
                        continue;
                    }
                    Vector3 way = next.position - point.position;
                    way.y = 0f;
                    if (way.sqrMagnitude < 0.01f)
                    {
                        continue;
                    }
                    float dot = Vector3.Dot(way.normalized, heading);
                    if (dot > straightest)
                    {
                        straightest = dot;
                        best = point.neighbors[k];
                    }
                }
                if (best < 0)
                {
                    break;
                }
                Waypoint chosen = TransitMap.Point(best);
                Vector3 step = chosen.position - point.position;
                step.y = 0f;
                done += step.magnitude;
                heading = step.normalized;
                _route.Add(chosen.position);
                _routePoints.Add(best);
                point = chosen;
            }
        }

        // Taken off the street under it: nothing to undo but the bus itself.
        private void Abandon()
        {
            _driver.Forget();
            if (Vehicle != null)
            {
                Vehicle.Hide();
            }
            Forget();
        }

        private void Forget()
        {
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
            _goingOut = false;
            _bayTo = 0;
            _onwardTo = 0;
            Vehicle = null;
            Stop = null;
            Remaining = 0f;
            State = Phase.Idle;
        }
    }
}
