using System.Collections.Generic;
using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Drives one vehicle like an emergency vehicle: it follows the road along a list of points,
    /// ignoring traffic lights, and never touches anyone or anything. It looks ahead for
    /// vehicles and for people (pedestrians, the player, the mod's own people, anyone lying in
    /// the road), slows as it closes on them and stops a couple of metres short. Past a stopped
    /// vehicle it may move over, but only into another lane of the road itself, and it comes
    /// back when its own is clear. It never leaves the road points to reach its goal (the one
    /// thing it does beside them is pull in to the kerb side of its own lane to stop, where its
    /// owner has found the carriageway there free: PullOver) and never forces a way through: held up close to the goal it stops in its lane and counts that as
    /// arriving (the crew walks the rest); held up further away it waits, and says what is in
    /// its way (Blocker, HeldFor) so that its owner can do something about it.
    /// The traffic system's own driver is switched off for the vehicle while this runs. The
    /// goal is the end of its route (BeginRoute: a place to stand, reached exactly), a fixed
    /// place (Begin: an incident), a point on its route to pull up at, or another vehicle to
    /// stay behind (a chase). One short physics query every 0.15 s and a handful of distance
    /// checks; nothing else recurring. Told where the kerb is (Kerb: a bus turning in to its bay),
    /// it leaves out of that look whatever is beyond it, on the pavement.
    /// Asked to cruise faster than a city street allows (above FastFrom: a pursuit of the player's car),
    /// it drives like a fast car instead of a quick one: it picks up speed more slowly the faster
    /// it goes, looks as far ahead as it needs to stop in, comes up behind the car it follows no
    /// faster than it could brake to that car's speed, and takes each bend of its route no faster
    /// than a car's tyres would hold (looked up along the route with the look ahead, every 0.15 s).
    /// At or below FastFrom nothing of that applies and it drives as it always has.
    /// It can also be told to drive like somebody who is not an emergency driver at all (Careless: a
    /// reckless car of Traffic/Reckless): to brake with less than it has, to take in less of the road
    /// ahead, not to see people on foot, to plan its bends with a grip of its owner's choosing, to
    /// wander in its lane (Sway) and to stop at a point of its route while a light there is red (HoldAt).
    /// Those are a few floats read in the steps that run anyway: no further query, and Begin puts every
    /// one of them back, so no other drive is changed by them.
    /// </summary>
    public sealed class EmergencyDriver : MonoBehaviour
    {
        public const float DefaultCruiseSpeed = 14f;    // m/s, about 50 km/h
        private const float CarefulSpeed = 6.5f;    // while turning hard or passing
        private const float TurnRate = 120f;        // degrees per second
        private const float PointReach = 4.5f;
        private const float FinalReach = 1.2f;      // the last point of a route is driven right up to
        private const float SenseInterval = 0.15f;
        private const float SenseDistance = 12f;
        private const float SenseMargin = 0.15f;    // metres the look-ahead is wider than the vehicle, each side
        private const float SenseHeight = 0.9f;     // metres above the road the look-ahead runs
        private const float StopGap = 2.5f;         // metres left between the nose and whatever is ahead
        private const float PersonGap = 2f;         // ...and this much more if it is a person
        private const float Braking = 4f;           // m/s2 it plans its slowing down with
        private const float PassOffset = 3.3f;      // one lane sideways
        private const float PassRoom = 30f;         // metres from where it will stop inside which it no longer pulls out to pass
        private const float LaneSnap = 1.7f;        // a road point this close to where a pass would run makes it a lane
        private const float StopPatience = 2f;      // seconds held up near the goal before stopping there for good
        private const float PassLookInterval = 1f;  // seconds between looks for a lane to pass in, while held up
        private const float FollowEase = 10f;       // metres over which a follower matches its target's speed
        private const float VerticalDamping = 11f;  // per second, on the vertical speed of a vehicle the traffic system no longer steadies
        private const float Pickup = 6f;            // m/s2 it gathers speed with
        private const float Slowing = 24f;          // m/s2 it can lose speed with

        /// <summary>A cruise speed above this (m/s, about 86 km/h) is a fast drive: see the summary.</summary>
        public const float FastFrom = 24f;
        private const float FastPickupLeast = 1.2f; // m/s2 left near its top speed (0-100 km/h in about 5.5 s, 100-200 in about 13)
        private const float FastGrip = 8.5f;        // m/s2 sideways through a bend
        private const float FastBraking = 9f;       // m/s2 it plans its slowing for a bend, or for the car it follows, with

        private static int _obstacleMask;
        private static int _blindMask;              // the same without the people the game walks about: for a driver who does not see them
        private static int _solidMask;              // buildings and street furniture: what a car that leaves its lane ends against
        private static readonly RaycastHit[] Hits = new RaycastHit[32];

        private VehicleComponent _car;
        private Rigidbody _body;
        private Rigidbody _target;
        private Rigidbody _blocker;
        private List<Vector3> _path;
        private int _next;
        private int _stopAt;            // index of the road point to pull up at, or -1
        private Vector3 _goal;
        private float _stopDistance;
        private float _stopDistanceSqr;
        private float _nearEnough;
        private float _speed;
        private float _limit;           // the fastest it may go for what is ahead of it
        private float _bend;            // ...and for the bends of its route ahead (a fast drive only)
        private float _offset;
        private float _targetOffset;
        private float _halfWidth;
        private float _nextSense;
        private float _clearSince;
        private float _blockedSince;
        private float _nextPassLook;
        private bool _routeOnly;        // the goal is the end of the route, not a place near it
        private bool _reverse;          // backing along the route: it looks, senses and moves tail first
        private bool _pulledOver;       // moved over to the kerb to stop: it stays over, and passes nothing
        private bool _blocked;
        private bool _following;
        private bool _braking;
        private bool _active;
        private bool _kerbed;           // the carriageway's edge on one side is known: see Kerb
        private Vector3 _kerbTowards;
        private float _kerbAt;
        private float _reach = PointReach;
        private float _stopping = Slowing;          // m/s2 it can lose speed with (a careless drive: less)
        private float _foresight = 1f;              // how much of its look ahead it takes in, 0 to 1
        private bool _seesPeople = true;            // false: the game's pedestrians are not in its look ahead
        private float _bendGrip;                    // m/s2 it plans its bends with; 0: only a fast drive plans them
        private float _careful = CarefulSpeed;      // m/s while turning hard or out of its lane
        private bool _gridLanes;                    // a lane to pass in is looked up in RoadMap's grid, not in the whole road graph
        private float _sway;                        // metres it wanders either side of its line; 0: none
        private float _swayEvery = 4f;              // ...once in this many seconds
        private int _holdAt = -1;                   // the point of its route it must not pass for now, or -1
        private float _hold = float.MaxValue;       // the fastest it may go for that point
        private float _room = float.MaxValue;       // metres clear ahead of its nose at the last look
        private Rigidbody _ahead;                   // what was there, if it has a body
        private bool _aheadPerson;                  // ...or whether it was somebody on foot

        /// <summary>True while a drive is under way. The component only runs then.</summary>
        public bool Active
        {
            get => _active;
            private set
            {
                _active = value;
                enabled = value;
            }
        }

        public bool Arrived { get; private set; }

        /// <summary>How fast the driver is currently making the vehicle go, in m/s.</summary>
        public float Speed => Active ? _speed : 0f;

        /// <summary>Speed on a clear, straight road, in m/s. Begin resets it to the default.</summary>
        public float CruiseSpeed { get; set; } = DefaultCruiseSpeed;

        /// <summary>
        /// False to keep it in its own lane whatever is ahead: it closes up and waits, and never pulls out into the
        /// lane beside it. For a unit leaving through a scene by a lane that was checked for it: the lane beside is
        /// where the other unit stands, and pulling out to pass put the two nose to nose. Begin sets it back to true.
        /// </summary>
        public bool MayPass { get; set; } = true;

        /// <summary>
        /// How near a point of its route counts as reached, in metres. A route is cut that much at every corner, which is
        /// what the open road wants; a path that must be kept to (in to a kerb and out again, a hand's width from it)
        /// asks for less. Begin sets it back to what it has always been.
        /// </summary>
        public float Reach
        {
            get => _reach;
            set => _reach = Mathf.Clamp(value, FinalReach, PointReach);
        }

        /// <summary>
        /// Tells it where the carriageway ends on one side: a point of the kerb and the way across the road towards
        /// it. What stands or walks beyond that line is on the pavement and is not in its way, however its nose
        /// points while it turns in beside it: people waiting at a stop, somebody walking past. On the carriageway
        /// everything counts as ever. Begin, and <see cref="NoKerb"/>, undo it.
        /// </summary>
        public void Kerb(Vector3 point, Vector3 towards)
        {
            towards.y = 0f;
            _kerbed = towards.sqrMagnitude > 0.01f;
            if (_kerbed)
            {
                _kerbTowards = towards.normalized;
                _kerbAt = point.x * _kerbTowards.x + point.z * _kerbTowards.z;
            }
        }

        public void NoKerb()
        {
            _kerbed = false;
        }

        /// <summary>
        /// Makes this drive a careless one: a driver who brakes with <paramref name="stopping"/> m/s2 at the most
        /// (and plans to, so they brake late), takes in only that share of the road ahead, may not see people on
        /// foot at all, and plans every bend with the grip given (more than the road has: they arrive too fast).
        /// It also takes hard turns and passes at whatever that grip allows instead of at a careful pace, and looks
        /// for a lane to pass in in RoadMap's grid (a handful of points) instead of asking the whole road graph.
        /// The mod's own people, bodies in the road and the player on foot are always seen. Call after Begin, which undoes it.
        /// </summary>
        public void Careless(float stopping, float foresight, bool seesPeople, float bendGrip)
        {
            _stopping = Mathf.Clamp(stopping, 1f, Slowing);
            _foresight = Mathf.Clamp(foresight, 0.05f, 1f);
            _seesPeople = seesPeople;
            _bendGrip = Mathf.Max(0f, bendGrip);
            _careful = _bendGrip > 0f ? Mathf.Max(CarefulSpeed, Mathf.Sqrt(_bendGrip * 9f)) : CarefulSpeed;   // (a junction's turn is about 9 m round)
            _gridLanes = true;
            if (_blindMask == 0)
            {
                _blindMask = TrafficTools.VehicleMask | LayerMask.GetMask("Player");
            }
        }

        /// <summary>
        /// The look ahead also takes in what stands beside the road: buildings and street furniture (lamp posts,
        /// hydrants, benches, bins), on top of the vehicles, parked ones among them, that it has always seen. The
        /// same one query every 0.15 s with a wider mask; nothing more is asked. Only for a driver who may leave
        /// the lane (a reckless one with no margin). Such a thing is not braked for while the car is being steered:
        /// a car turning at a junction always has a wall or a lamp post somewhere ahead of its nose and is about to
        /// turn away from it, and one that stopped for it would stand in the junction. It is only measured
        /// (<see cref="SolidRoom"/>), so that whoever drives the car knows the moment its nose reaches one. Once
        /// the car is no longer steered (Slide) it counts like anything else in its way. Begin undoes it.
        /// </summary>
        public void SeeSolids()
        {
            if (_solidMask == 0)
            {
                _solidMask = LayerMask.GetMask("Buildings", "StreetProps");
            }
            _solids = true;
        }

        /// <summary>
        /// The car has been taken into a bend faster than the road holds: from here it is not steered. It carries
        /// straight on the way it points, off its line, losing speed with whatever braking it has, and its look
        /// ahead goes on measuring what is in front of its nose (Room, Ahead), so that whoever drives it sees it
        /// reach something. It ends by itself when it has all but stopped (Active goes false). Begin undoes it.
        /// </summary>
        public void Slide()
        {
            _sliding = true;
            _nextSense = 0f;
        }

        /// <summary>True while it is carrying straight on, unsteered (Slide).</summary>
        public bool Sliding => Active && _sliding;

        /// <summary>Metres of clear road between its nose and a building or a piece of street furniture straight ahead of it; float.MaxValue if there is none in its look, or it was not told to look (SeeSolids).</summary>
        public float SolidRoom => Active ? _solidRoom : float.MaxValue;

        /// <summary>
        /// Takes over a vehicle that is moving: it carries on from that speed instead of from rest, so nothing
        /// shows at the moment it changes hands. Call after Begin and after setting <see cref="CruiseSpeed"/>.
        /// </summary>
        public void Rolling(float speed)
        {
            _speed = Mathf.Clamp(speed, 0f, Mathf.Max(CruiseSpeed, DefaultCruiseSpeed));
        }

        /// <summary>
        /// Makes it wander so many metres either side of its line, once in so many seconds: a driver who has
        /// had a drink. 0 ends it. It is part of where it aims, nothing else. Begin undoes it.
        /// </summary>
        public void Sway(float metres, float every)
        {
            _sway = Mathf.Max(0f, metres);
            _swayEvery = Mathf.Max(1f, every);
        }

        /// <summary>
        /// The point of its route it must stop short of for now (a red light there), or -1 for none: it slows
        /// into it at an ordinary driver's pace and waits, and sets off when this is set back to -1. Looked at
        /// with the look ahead (every 0.15 s): a walk of the few route points up to it. Begin sets it to -1.
        /// </summary>
        public int HoldAt
        {
            get => _holdAt;
            set
            {
                _holdAt = value;
                if (value < 0)
                {
                    _hold = float.MaxValue;
                }
            }
        }

        /// <summary>True while it stands at the point it was told to hold at.</summary>
        public bool Holding => Active && _holdAt >= 0 && _hold <= 0f && _speed < 0.3f;

        /// <summary>Metres clear ahead of its nose at the last look; float.MaxValue if nothing was in reach.</summary>
        public float Room => Active ? _room : float.MaxValue;

        /// <summary>What was ahead at the last look, if that is a thing with a body (a vehicle); null for a person, something fixed, or nothing.</summary>
        public Rigidbody Ahead => Active ? _ahead : null;

        /// <summary>True if the nearest thing ahead at the last look was somebody on foot (or lying in the road), not a vehicle or anything fixed.</summary>
        public bool AheadIsPerson => Active && _aheadPerson;

        /// <summary>True while it is out of its own lane, passing (not while pulled in to a kerb).</summary>
        public bool OutOfLane => Active && !_pulledOver && (_targetOffset != 0f || Mathf.Abs(_offset) > 0.3f);

        /// <summary>Seconds the vehicle has been standing behind something in its way; 0 while it is moving.</summary>
        public float HeldFor => Active && _blocked ? Time.time - _blockedSince : 0f;

        /// <summary>What it is standing behind, if that is a thing with a body (a vehicle); null for a person or nothing.</summary>
        public Rigidbody Blocker => Active && _blocked ? _blocker : null;

        /// <summary>Metres of its route still ahead of the vehicle. Walks the route: for a log line or a decision, not per frame.</summary>
        public float RouteLeft
        {
            get
            {
                if (_path == null || _body == null || _next >= _path.Count)
                {
                    return 0f;
                }
                float left = Flat(_path[_next] - _body.position).magnitude;
                for (int i = _next + 1; i < _path.Count; i++)
                {
                    left += Flat(_path[i] - _path[i - 1]).magnitude;
                }
                return left;
            }
        }

        private void Awake()
        {
            enabled = false;   // nothing to drive yet
        }

        /// <summary>
        /// Starts a drive. The path is read live, so the caller may keep adding points to the
        /// end of the list while the vehicle is on its way.
        /// </summary>
        /// <param name="path">Road points to follow, in order. The vehicle stops at the last one.</param>
        /// <param name="goal">The destination; the vehicle pulls up once it is within stopDistance of it.</param>
        /// <param name="nearEnough">
        /// If above zero: a vehicle held up this close to the goal stops in its lane and has
        /// arrived. Further away, and with zero, it waits for the way to clear.
        /// </param>
        public void Begin(VehicleComponent car, List<Vector3> path, Vector3 goal, float stopDistance, float nearEnough = 0f)
        {
            if (_obstacleMask == 0)
            {
                _obstacleMask = TrafficTools.VehicleMask | TrafficTools.PeopleMask;
                if (_obstacleMask == 0)
                {
                    _obstacleMask = Physics.DefaultRaycastLayers;
                }
            }

            _car = car;
            _body = car.rb;
            _target = null;
            _blocker = null;
            _path = path;
            _next = 0;
            _stopAt = -1;
            _goal = goal;
            _stopDistance = stopDistance;
            _stopDistanceSqr = stopDistance * stopDistance;
            _nearEnough = nearEnough;
            _speed = 0f;
            _limit = float.MaxValue;
            _bend = float.MaxValue;
            _offset = 0f;
            _targetOffset = 0f;
            _halfWidth = Mathf.Max(0.8f, car.colliderWidth * 0.5f);
            _nextSense = 0f;
            _clearSince = 0f;
            _blockedSince = 0f;
            _nextPassLook = 0f;
            _routeOnly = false;
            _reverse = false;
            _pulledOver = false;
            _blocked = false;
            _following = false;
            _braking = false;
            _kerbed = false;
            _reach = PointReach;
            _stopping = Slowing;
            _foresight = 1f;
            _seesPeople = true;
            _bendGrip = 0f;
            _careful = CarefulSpeed;
            _gridLanes = false;
            _solids = false;
            _sliding = false;
            _solidRoom = float.MaxValue;
            _sway = 0f;
            _holdAt = -1;
            _hold = float.MaxValue;
            _room = float.MaxValue;
            _ahead = null;
            _aheadPerson = false;
            MayPass = true;
            CruiseSpeed = DefaultCruiseSpeed;
            Arrived = false;
            Active = true;
            TrafficTools.Touch(car, "driven by the mod");
        }

        /// <summary>
        /// Starts a drive whose goal is the end of the route itself: the vehicle slows down
        /// into the last point and stops on it, in its lane. Distances are measured along the
        /// route, so a place that is near as the crow flies but on another street is not near.
        /// </summary>
        /// <param name="nearEnough">
        /// If above zero: held up with no more than this much of its route left, the vehicle
        /// stops where it is and has arrived. With more left, it waits.
        /// </param>
        public void BeginRoute(VehicleComponent car, List<Vector3> path, float nearEnough)
        {
            Begin(car, path, path.Count > 0 ? path[path.Count - 1] : car.transform.position, 0f, nearEnough);
            _routeOnly = true;
        }

        /// <summary>
        /// Backs the vehicle along a route, tail first, to its last point: out of a place it drove
        /// into nose first (an ambulance at the tape). Everything else is as for any drive: it
        /// looks where it is going, which is behind it, closes up on vehicles and people there and
        /// stops short of them (HeldFor then says how long it has stood), and never leaves its
        /// points. It does not pull out to pass anything.
        /// </summary>
        /// <param name="path">Points behind the vehicle, nearest first.</param>
        public void BeginReverse(VehicleComponent car, List<Vector3> path, float speed)
        {
            BeginRoute(car, path, 0f);
            _reverse = true;
            CruiseSpeed = speed;
        }

        /// <summary>Moves the destination of a drive that is under way.</summary>
        public void SetGoal(Vector3 goal)
        {
            _goal = goal;
        }

        /// <summary>
        /// Names the road point to pull up at: the vehicle stops there, in its lane, whatever
        /// its distance to the goal. For a parking place chosen along its own route.
        /// </summary>
        public void PullUpAt(int pathIndex)
        {
            _stopAt = pathIndex;
        }

        /// <summary>The road point the vehicle is heading for; every point before it is behind.</summary>
        public int NextPoint => _next;

        /// <summary>
        /// Turns the drive into a pursuit: the goal becomes the target vehicle, wherever it goes.
        /// The driver settles in behind it at about the given gap instead of arriving, never
        /// treats it as an obstacle to pass, and stops behind it when it stops. Ends with Stop.
        /// </summary>
        public void Follow(Rigidbody target, float gap)
        {
            _target = target;
            _following = target != null;
            _routeOnly = false;
            _stopAt = -1;
            _stopDistance = gap;
            _stopDistanceSqr = gap * gap;
            _nearEnough = 0f;
            Arrived = false;
        }

        /// <summary>
        /// Moves the vehicle over within the carriageway, by so many metres to the right of its
        /// road points (negative: to the left), and keeps it there: pulling in to the kerb before
        /// it is braked to a halt. It goes at a careful pace while it is over. The caller has made
        /// sure the road there is carriageway and free (SceneParking.KerbRoom). Begin undoes it.
        /// </summary>
        public void PullOver(float offset)
        {
            _targetOffset = offset;
            _pulledOver = offset != 0f;
        }

        /// <summary>Brakes to a halt where the vehicle is, then goes inactive. Arrived stays false.</summary>
        public void Brake()
        {
            _braking = true;
        }

        /// <summary>
        /// Ends the drive with the vehicle standing dead still. From here on nothing damps its springs (see
        /// FixedUpdate), so it is left with no motion at all, and whoever holds it parks it (kinematic) if it
        /// is to stand for more than a moment. A body that is already parked is not touched.
        /// </summary>
        public void Stop()
        {
            Active = false;
            _blocked = false;
            if (_body != null && !_body.isKinematic)
            {
                _body.velocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>Lets go of a vehicle that is no longer ours to move (the pool reused it).</summary>
        public void Forget()
        {
            Active = false;
            _blocked = false;
            _car = null;
            _body = null;
            _target = null;
            _blocker = null;
            _ahead = null;
        }

        private static readonly int HotFixedUpdate = HotSpots.Slot("Traffic: EmergencyDriver.FixedUpdate");
        private void FixedUpdate()
        {
            using var hotSpot = HotSpots.Time(HotFixedUpdate);
            if (!Active)
            {
                return;
            }
            if (_car == null || _body == null || _path == null || !_car.gameObject.activeInHierarchy)
            {
                Active = false;
                return;
            }
            if (_body.isKinematic)
            {
                return;   // frozen under us (parked by its owner, or by TrafficWatch): nothing to steer until it is let go
            }

            Vector3 position = _body.position;
            if (_sliding)
            {
                SlideOn(position);
                return;
            }
            if (_following)
            {
                if (_target != null)
                {
                    _goal = _target.position;
                }
                else
                {
                    // The vehicle being followed is gone: pull up where it was last seen.
                    _following = false;
                }
            }

            Vector3 toGoal = _goal - position;
            toGoal.y = 0f;
            float goalSqr = toGoal.sqrMagnitude;
            if (!_routeOnly && goalSqr < _stopDistanceSqr && !_following && _stopAt < 0)
            {
                Arrive();
                return;
            }
            if (_braking && _speed <= 0.05f)
            {
                Stop();
                return;
            }

            // The next road point not yet reached. The route is all there is: at its end, or at the
            // point chosen to pull up at, the vehicle stops where it is, in its lane. The end of a
            // route that is itself the goal is driven right up to (or counted as reached once passed).
            int last = _path.Count - 1;
            Vector3 heading = _body.rotation * (_reverse ? Vector3.back : Vector3.forward);
            while (_next < _path.Count)
            {
                Vector3 to = Flat(_path[_next] - position);
                bool final = _routeOnly && _next == last;
                float reach = final ? FinalReach : _reach;
                if (to.sqrMagnitude >= reach * reach && !(final && Vector3.Dot(to, heading) < 0f))
                {
                    break;
                }
                _next++;
            }
            if (!_following && (_next >= _path.Count || (_stopAt >= 0 && _next > _stopAt)))
            {
                Arrive();
                return;
            }
            Vector3 toPoint = Flat((_next < _path.Count ? _path[_next] : _goal) - position);
            if (toPoint.sqrMagnitude < 0.01f)
            {
                return;
            }

            Vector3 roadDirection = toPoint.normalized;
            Vector3 roadRight = Vector3.Cross(Vector3.up, roadDirection);
            if (Time.time >= _nextSense)
            {
                _nextSense = Time.time + SenseInterval;
                Sense(position, roadRight, goalSqr);
                _bend = CruiseSpeed > FastFrom || _bendGrip > 0f ? BendLimit(position) : float.MaxValue;
                _hold = _holdAt >= 0 ? HoldLimit(toPoint) : float.MaxValue;
                if (_blocked && !_following && _nearEnough > 0f && Time.time - _blockedSince > StopPatience
                    && (_routeOnly ? RouteLeft < _nearEnough : goalSqr < _nearEnough * _nearEnough))
                {
                    Arrive();
                    return;
                }
            }
            _offset = Mathf.MoveTowards(_offset, _targetOffset, 4f * Time.fixedDeltaTime);

            // (A driver who wanders does so about wherever its line is; it is not out of its lane for that, and not slowed.)
            float aside = _sway > 0f ? _offset + _sway * Mathf.Sin(Time.time * (2f * Mathf.PI / _swayEvery)) : _offset;
            Vector3 aim = Flat(toPoint + roadRight * aside);
            if (aim.sqrMagnitude < 0.01f)
            {
                return;
            }
            Quaternion look = Quaternion.LookRotation(_reverse ? -aim.normalized : aim.normalized, Vector3.up);
            float turn = Quaternion.Angle(_body.rotation, look);
            _body.MoveRotation(Quaternion.RotateTowards(_body.rotation, look, TurnRate * Time.fixedDeltaTime));

            bool fast = CruiseSpeed > FastFrom;
            float wanted = 0f;
            if (!_blocked && !_braking)
            {
                wanted = turn > 22f || Mathf.Abs(_offset) > 0.3f ? Mathf.Min(_careful, CruiseSpeed) : CruiseSpeed;
                if (_following)
                {
                    // Ease off over the last few metres so the follower matches speed instead of ramming.
                    float room = Mathf.Sqrt(goalSqr) - _stopDistance;
                    wanted *= Mathf.Clamp01(room / FollowEase);
                    if (fast && _target != null)
                    {
                        // From speed those few metres are not enough: never faster than it can brake to the other car's speed in the room there is.
                        wanted = Mathf.Min(wanted, Flat(_target.velocity).magnitude + Mathf.Sqrt(2f * FastBraking * Mathf.Max(0f, room)));
                    }
                }
                else
                {
                    // Slow down into the place it will stop at, instead of stopping dead from speed.
                    float toStop = StopDistance(position, toPoint, goalSqr, last);
                    if (toStop < 30f)
                    {
                        wanted = Mathf.Min(wanted, Mathf.Max(1.5f, Mathf.Sqrt(2f * Braking * toStop)));
                    }
                }
                wanted = Mathf.Min(wanted, Mathf.Min(_limit, Mathf.Min(_bend, _hold)));
            }
            float pickup = Pickup;
            if (fast && _speed > DefaultCruiseSpeed)
            {
                // A car pulls less the faster it goes: all of it up to city speed, a fifth of it at its top speed.
                pickup = Mathf.Max(FastPickupLeast, Pickup * (1f - 0.85f * (_speed - DefaultCruiseSpeed) / (CruiseSpeed - DefaultCruiseSpeed)));
            }
            _speed = Mathf.MoveTowards(_speed, wanted, (wanted < _speed ? _stopping : pickup) * Time.fixedDeltaTime);
            // The traffic system's springs have no damper of their own: for its own cars it replaces the whole
            // velocity every step, and that is all that stops them bouncing. It does not do that for a car
            // it has been told to ignore, so here the vertical speed is bled off instead (about 0.7 of
            // critical for those springs): the body follows the road and settles, and never hops on the spot.
            if (_solidRoom < float.MaxValue)
            {
                _solidRoom = Mathf.Max(0f, _solidRoom - _speed * Time.fixedDeltaTime);   // between two looks: what it has covered since is off the room it had
            }
            Vector3 velocity = _body.rotation * Vector3.forward * (_reverse ? -_speed : _speed);
            velocity.y = _body.velocity.y * Mathf.Exp(-VerticalDamping * Time.fixedDeltaTime);
            _body.velocity = velocity;
            _body.angularVelocity = Vector3.zero;
        }

        // A fast drive: the fastest it may go now for the bends of its route ahead. Each bend allows what a car's
        // tyres would hold through it (the turn there over the length of road it is made in), and the vehicle may
        // be going as much faster now as it can lose on the way there. Looks only as far as it needs to slow
        // down in: a few dozen route points at its fastest, a handful in town. No physics, no allocation.
        // Only the route's own shape is read (from the point before the one it is heading for): where the vehicle
        // itself happens to be beside its line says nothing about the road.
        private float BendLimit(Vector3 position)
        {
            if (_next < 1 || _next >= _path.Count)
            {
                return float.MaxValue;
            }
            float limit = float.MaxValue;
            // (A careless drive plans with the grip it was given and with no more braking than it has.)
            float grip = _bendGrip > 0f ? _bendGrip : FastGrip;
            float braking = _bendGrip > 0f ? Mathf.Min(FastBraking, _stopping) : FastBraking;
            float look = _speed * _speed / (2f * braking) + 25f;
            Vector3 from = _path[_next];
            Vector3 before = Flat(from - _path[_next - 1]);
            float lastLeg = before.magnitude;
            float travelled = Flat(from - position).magnitude;   // metres of route to the bend being looked at
            for (int i = _next + 1; i < _path.Count && travelled < look; i++)
            {
                Vector3 leg = Flat(_path[i] - from);
                float length = leg.magnitude;
                if (length < 1f)
                {
                    continue;   // (points on top of one another say nothing about the road)
                }
                if (lastLeg >= 1f)
                {
                    float bend = Vector3.Angle(before, leg) * Mathf.Deg2Rad;
                    if (bend > 0.01f)
                    {
                        float through = Mathf.Sqrt(grip * (lastLeg + length) * 0.5f / bend);
                        limit = Mathf.Min(limit, Mathf.Sqrt(through * through + 2f * braking * travelled));
                    }
                }
                travelled += length;
                lastLeg = length;
                before = leg;
                from = _path[i];
            }
            return limit;
        }

        private const float HoldShort = 3f;         // metres short of the point it holds at that its nose stops (the point is the stop line's own)

        // The fastest it may go now for the point it has been told to hold at: what an ordinary driver's braking
        // brings to a stand a little short of it, measured along the route. 0 once it is there; no limit once
        // the point is behind it (the light changed too late: it is over the line and carries on).
        private float HoldLimit(Vector3 toPoint)
        {
            if (_holdAt < _next || _holdAt >= _path.Count)
            {
                return float.MaxValue;
            }
            float along = toPoint.magnitude;
            for (int i = _next + 1; i <= _holdAt; i++)
            {
                along += Flat(_path[i] - _path[i - 1]).magnitude;
            }
            float room = along - _car.length * 0.5f - HoldShort;
            return room > 0.1f ? Mathf.Sqrt(2f * Mathf.Min(Braking, _stopping) * room) : 0f;
        }

        // Metres to where the vehicle will stop, when that is close: the point it pulls up at, the end
        // of its route, or its stopping distance from the goal. A large number while it is far off.
        private float StopDistance(Vector3 position, Vector3 toPoint, float goalSqr, int last)
        {
            int stopIndex = _stopAt >= 0 ? _stopAt : last;
            float toStop = float.MaxValue;
            if (_next == stopIndex)
            {
                toStop = toPoint.magnitude;
            }
            else if (_next == stopIndex - 1)
            {
                toStop = toPoint.magnitude + Flat(_path[stopIndex] - _path[_next]).magnitude;
            }
            if (!_routeOnly && _stopAt < 0)
            {
                toStop = Mathf.Min(toStop, Mathf.Max(0f, Mathf.Sqrt(goalSqr) - _stopDistance));
            }
            return toStop;
        }

        private void Arrive()
        {
            Arrived = true;
            Stop();
        }

        // Looks ahead and picks a lane and a speed: carry on, move over to another lane of the road,
        // or close up on whatever is ahead and wait behind it.
        private void Sense(Vector3 position, Vector3 roadRight, float goalSqr)
        {
            Vector3 forward = _body.rotation * (_reverse ? Vector3.back : Vector3.forward);   // the way it is going
            Vector3 middle = position + Vector3.up * SenseHeight;

            float free = FreeAhead(middle, forward, beside: false, out Rigidbody blocker);
            _room = free;
            _ahead = blocker;
            if (free == float.MaxValue)
            {
                _blocked = false;
                _blocker = null;
                _limit = float.MaxValue;
                // Back into our own lane once it has been clear there for a moment (not when pulled in to the kerb).
                if (_targetOffset != 0f && !_pulledOver)
                {
                    if (IsBlocked(middle - roadRight * _offset, forward))
                    {
                        _clearSince = Time.time;
                    }
                    else if (Time.time - _clearSince > 1.2f)
                    {
                        _targetOffset = 0f;
                    }
                }
                return;
            }

            _clearSince = Time.time;
            // Out of its lane and held up there: if its own lane is clear now (or it may no longer pass at all), it goes
            // back into it instead of standing nose to tail with whatever is in the other one. (It used to look at its
            // own lane only once the lane it had pulled out into was clear: behind something standing there, never.)
            if (_targetOffset != 0f && !_pulledOver && !_reverse && Time.time >= _nextPassLook)
            {
                _nextPassLook = Time.time + PassLookInterval;
                if (!MayPass || !IsBlocked(middle - roadRight * _offset, forward))
                {
                    _targetOffset = 0f;
                }
            }
            // Already out of lane and still held up: there is no third lane to try. Close to where it
            // will stop there is nothing to gain by pulling out. Otherwise the lanes either side are
            // looked at, but not at every glance: finding a lane means asking the road.
            bool look = MayPass && !_reverse && !_pulledOver && _targetOffset == 0f && Mathf.Abs(_offset) < 0.3f && Time.time >= _nextPassLook && HasRoomToPass(goalSqr);
            if (look && CanPass(middle, forward, roadRight, -PassOffset))
            {
                _targetOffset = -PassOffset;   // on the left
                Unblock();
            }
            else if (look && CanPass(middle, forward, roadRight, PassOffset))
            {
                _targetOffset = PassOffset;
                Unblock();
            }
            else
            {
                if (look)
                {
                    _nextPassLook = Time.time + PassLookInterval;
                }
                // Close up to a couple of metres behind it, then wait.
                float room = free - StopGap;
                // (A careless driver plans with all the braking they have, which is to say late.)
                float plan = _stopping < Slowing ? _stopping : CruiseSpeed > FastFrom ? FastBraking : Braking;
                _limit = room > 0.1f ? Mathf.Sqrt(2f * plan * room) : 0f;
                _blocker = blocker;
                if (_limit > 0f)
                {
                    _blocked = false;
                }
                else if (!_blocked)
                {
                    _blocked = true;
                    _blockedSince = Time.time;
                }
            }
        }

        private void Unblock()
        {
            _blocked = false;
            _blocker = null;
            _limit = float.MaxValue;
        }

        // Far enough from where the vehicle will stop for a pass to be worth it. Following, always.
        private bool HasRoomToPass(float goalSqr)
        {
            if (_following)
            {
                return true;
            }
            if (_routeOnly)
            {
                return RouteLeft > PassRoom;
            }
            float near = _stopDistance + PassRoom;
            return goalSqr > near * near;
        }

        // A pass is only ever into a lane of the road: there must be a road point where the vehicle
        // would run, and nothing and nobody in it.
        private bool CanPass(Vector3 middle, Vector3 forward, Vector3 roadRight, float offset)
        {
            Vector3 beside = middle + roadRight * offset;
            Vector3 lane = beside + forward * (_car.length * 0.5f + 6f) - Vector3.up * SenseHeight;
            if (IsBlocked(beside, forward))
            {
                return false;
            }
            if (_gridLanes && RoadMap.Usable)
            {
                // The squares of RoadMap's grid round the place: a handful of points, where the call below walks every one in the city.
                return RoadMap.NearestLane(lane, out float off, out _, out _) && off < LaneSnap;
            }
            return TrafficManager.HasInstance && TrafficManager.Instance.GetClosestWaypoint(lane, LaneSnap) != null;
        }

        // True if a lane beside the vehicle (or its own, seen from out of it) has anything in it, alongside or ahead.
        private bool IsBlocked(Vector3 origin, Vector3 direction)
        {
            return Physics.CheckSphere(origin, _halfWidth + SenseMargin, Mask, QueryTriggerInteraction.Ignore)
                   || FreeAhead(origin, direction, beside: true, out _) < float.MaxValue;
        }

        // Metres of clear road ahead of the vehicle's nose, or float.MaxValue if there is nothing in
        // reach. Sweeps a ball as wide as the vehicle from its middle to well ahead of its nose:
        // further the faster it goes; at the default cruise speed, SenseDistance. A sweep ignores
        // whatever the ball starts inside: for our own lane that is our own vehicle, as wanted.
        private float FreeAhead(Vector3 origin, Vector3 direction, bool beside, out Rigidbody blocker)
        {
            blocker = null;
            float halfLength = _car.length * 0.5f;
            // (From a fast drive's speeds that is not enough to stop in: there, as far as it takes to stop, and a little over.)
            // (A careless driver takes in only a share of it.)
            float reach = halfLength + Mathf.Max(SenseDistance + Mathf.Max(0f, _speed - DefaultCruiseSpeed), _speed * _speed / (2f * Slowing) * 1.15f + StopGap + 4f) * _foresight;
            float halfWidth = _halfWidth + SenseMargin;
            float free = float.MaxValue;
            bool onFoot = false;
            if (!beside)
            {
                _solidRoom = float.MaxValue;   // measured afresh by this look, if there is anything to measure
            }
            if (_kerbed)
            {
                // Beside a kerb it has been told of: the nearest thing that is on the carriageway. Every hit of the sweep
                // is looked at, since the first may be somebody on the pavement. What the ball starts inside is no hit,
                // as for the single sweep below; nor is any part of the vehicle itself.
                int count = Physics.SphereCastNonAlloc(origin, halfWidth, direction, Hits, reach, Mask, QueryTriggerInteraction.Ignore);
                float nearest = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    RaycastHit seen = Hits[i];
                    Hits[i] = default;
                    if (seen.distance <= 0f || seen.rigidbody == _body || (_target != null && seen.rigidbody == _target)
                        || seen.point.x * _kerbTowards.x + seen.point.z * _kerbTowards.z > _kerbAt)
                    {
                        continue;
                    }
                    if (seen.distance < nearest)
                    {
                        nearest = seen.distance;
                        blocker = seen.rigidbody;
                    }
                }
                if (nearest < float.MaxValue)
                {
                    free = Mathf.Max(0f, nearest + halfWidth * 0.5f - halfLength);
                }
            }
            else if (Physics.SphereCast(origin, halfWidth, direction, out RaycastHit hit, reach, Mask | (_solids && !beside ? _solidMask : 0), QueryTriggerInteraction.Ignore))
            {
                float travelled = hit.distance;
                bool found = true;
                if (hit.rigidbody == _body)
                {
                    // A part of our own vehicle the ball did not start inside: look again from just ahead of the nose.
                    float skip = halfLength + 0.4f;
                    found = Physics.SphereCast(origin + direction * skip, halfWidth, direction, out hit, reach - skip, Mask | (_solids && !beside ? _solidMask : 0), QueryTriggerInteraction.Ignore)
                            && hit.rigidbody != _body;
                    travelled = skip + hit.distance;
                }
                if (found && _solids && !beside && !_sliding && hit.collider != null && ((1 << hit.collider.gameObject.layer) & _solidMask) != 0)
                {
                    // A wall or a lamp post while the car is being steered: measured, not braked for (see SeeSolids).
                    _solidRoom = Mathf.Max(0f, travelled + halfWidth * 0.5f - halfLength);
                }
                else if (found && (_target == null || hit.rigidbody != _target))
                {
                    blocker = hit.rigidbody;
                    free = Mathf.Max(0f, travelled + halfWidth * 0.5f - halfLength);
                    onFoot = hit.collider != null && ((1 << hit.collider.gameObject.layer) & TrafficTools.PeopleMask) != 0;
                }
            }
            // People a physics query cannot see: those standing still, kneeling or lying have no collider.
            float person = _kerbed
                ? PeopleAhead.Nearest(origin, direction, beside ? -halfLength : halfLength, reach, halfWidth + 0.3f, _kerbTowards, _kerbAt)
                : PeopleAhead.Nearest(origin, direction, beside ? -halfLength : halfLength, reach, halfWidth + 0.3f);
            if (person < float.MaxValue)
            {
                float room = Mathf.Max(0f, person - halfLength - PersonGap);
                if (room < free)
                {
                    free = room;
                    blocker = null;
                    onFoot = true;
                }
            }
            if (!beside)
            {
                _aheadPerson = onFoot;
            }
            return free;
        }

        // What its look ahead is made against: everything solid and everybody, or (a driver who does not see them) everything but the game's pedestrians.
        // (For a driver told to, SeeSolids, the one sweep ahead of its own nose also takes in what stands beside the road.)
        private int Mask => _seesPeople || _blindMask == 0 ? _obstacleMask : _blindMask;

        private bool _solids;
        private bool _sliding;
        private float _solidRoom = float.MaxValue;

        // A car that has lost it: straight on the way it points, braking, with the look ahead still made.
        private void SlideOn(Vector3 position)
        {
            if (Time.time >= _nextSense)
            {
                _nextSense = Time.time + SenseInterval;
                _room = FreeAhead(position + Vector3.up * SenseHeight, _body.rotation * Vector3.forward, beside: false, out _ahead);
            }
            else if (_room < float.MaxValue)
            {
                _room = Mathf.Max(0f, _room - _speed * Time.fixedDeltaTime);   // between two looks: what it has covered since is off the room it had
            }
            _speed = Mathf.MoveTowards(_speed, 0f, _stopping * Time.fixedDeltaTime);
            if (_speed <= 0.3f)
            {
                Stop();
                return;
            }
            Vector3 velocity = _body.rotation * Vector3.forward * _speed;
            velocity.y = _body.velocity.y * Mathf.Exp(-VerticalDamping * Time.fixedDeltaTime);
            _body.velocity = velocity;
            _body.angularVelocity = Vector3.zero;
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }
    }
}
