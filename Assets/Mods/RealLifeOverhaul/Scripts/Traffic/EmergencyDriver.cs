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
    /// checks; nothing else recurring.
    /// Asked to cruise faster than a city street allows (above FastFrom: a pursuit of the player's car),
    /// it drives like a fast car instead of a quick one: it picks up speed more slowly the faster
    /// it goes, looks as far ahead as it needs to stop in, comes up behind the car it follows no
    /// faster than it could brake to that car's speed, and takes each bend of its route no faster
    /// than a car's tyres would hold (looked up along the route with the look ahead, every 0.15 s).
    /// At or below FastFrom nothing of that applies and it drives as it always has.
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
        }

        private void FixedUpdate()
        {
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
                float reach = final ? FinalReach : PointReach;
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
                _bend = CruiseSpeed > FastFrom ? BendLimit(position) : float.MaxValue;
                if (_blocked && !_following && _nearEnough > 0f && Time.time - _blockedSince > StopPatience
                    && (_routeOnly ? RouteLeft < _nearEnough : goalSqr < _nearEnough * _nearEnough))
                {
                    Arrive();
                    return;
                }
            }
            _offset = Mathf.MoveTowards(_offset, _targetOffset, 4f * Time.fixedDeltaTime);

            Vector3 aim = Flat(toPoint + roadRight * _offset);
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
                wanted = turn > 22f || Mathf.Abs(_offset) > 0.3f ? Mathf.Min(CarefulSpeed, CruiseSpeed) : CruiseSpeed;
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
                wanted = Mathf.Min(wanted, Mathf.Min(_limit, _bend));
            }
            float pickup = Pickup;
            if (fast && _speed > DefaultCruiseSpeed)
            {
                // A car pulls less the faster it goes: all of it up to city speed, a fifth of it at its top speed.
                pickup = Mathf.Max(FastPickupLeast, Pickup * (1f - 0.85f * (_speed - DefaultCruiseSpeed) / (CruiseSpeed - DefaultCruiseSpeed)));
            }
            _speed = Mathf.MoveTowards(_speed, wanted, (wanted < _speed ? Slowing : pickup) * Time.fixedDeltaTime);
            // The traffic system's springs have no damper of their own: for its own cars it replaces the whole
            // velocity every step, and that is all that stops them bouncing. It does not do that for a car
            // it has been told to ignore, so here the vertical speed is bled off instead (about 0.7 of
            // critical for those springs): the body follows the road and settles, and never hops on the spot.
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
            float look = _speed * _speed / (2f * FastBraking) + 25f;
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
                        float through = Mathf.Sqrt(FastGrip * (lastLeg + length) * 0.5f / bend);
                        limit = Mathf.Min(limit, Mathf.Sqrt(through * through + 2f * FastBraking * travelled));
                    }
                }
                travelled += length;
                lastLeg = length;
                before = leg;
                from = _path[i];
            }
            return limit;
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
                _limit = room > 0.1f ? Mathf.Sqrt(2f * (CruiseSpeed > FastFrom ? FastBraking : Braking) * room) : 0f;
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
            return !IsBlocked(beside, forward) && TrafficManager.HasInstance
                   && TrafficManager.Instance.GetClosestWaypoint(lane, LaneSnap) != null;
        }

        // True if a lane beside the vehicle (or its own, seen from out of it) has anything in it, alongside or ahead.
        private bool IsBlocked(Vector3 origin, Vector3 direction)
        {
            return Physics.CheckSphere(origin, _halfWidth + SenseMargin, _obstacleMask, QueryTriggerInteraction.Ignore)
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
            float reach = halfLength + Mathf.Max(SenseDistance + Mathf.Max(0f, _speed - DefaultCruiseSpeed), _speed * _speed / (2f * Slowing) * 1.15f + StopGap + 4f);
            float halfWidth = _halfWidth + SenseMargin;
            float free = float.MaxValue;
            if (Physics.SphereCast(origin, halfWidth, direction, out RaycastHit hit, reach, _obstacleMask, QueryTriggerInteraction.Ignore))
            {
                float travelled = hit.distance;
                bool found = true;
                if (hit.rigidbody == _body)
                {
                    // A part of our own vehicle the ball did not start inside: look again from just ahead of the nose.
                    float skip = halfLength + 0.4f;
                    found = Physics.SphereCast(origin + direction * skip, halfWidth, direction, out hit, reach - skip, _obstacleMask, QueryTriggerInteraction.Ignore)
                            && hit.rigidbody != _body;
                    travelled = skip + hit.distance;
                }
                if (found && (_target == null || hit.rigidbody != _target))
                {
                    blocker = hit.rigidbody;
                    free = Mathf.Max(0f, travelled + halfWidth * 0.5f - halfLength);
                }
            }
            // People a physics query cannot see: those standing still, kneeling or lying have no collider.
            float person = PeopleAhead.Nearest(origin, direction, beside ? -halfLength : halfLength, reach, halfWidth + 0.3f);
            if (person < float.MaxValue)
            {
                float room = Mathf.Max(0f, person - halfLength - PersonGap);
                if (room < free)
                {
                    free = room;
                    blocker = null;
                }
            }
            return free;
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }
    }
}
