using System.Collections.Generic;
using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Drives one vehicle like an emergency vehicle along a list of road points, through red lights and touching nothing:
    /// it slows for vehicles and people ahead and stops short, may pass in another lane of the road, and never forces a way
    /// through (held up near its goal it has arrived; further off it waits, saying what is in its way). Above FastFrom it
    /// drives like a fast car; Careless, Sway and HoldAt make it a reckless one. The traffic system's driver is off for the
    /// vehicle meanwhile. Cost: disabled when not driving; driving, a few multiplications a physics step and one short
    /// physics query every 0.15 s.
    /// </summary>
    public sealed partial class EmergencyDriver : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("traffic", "EmergencyDriver", typeof(EmergencyDriver));

        public const float DefaultCruiseSpeed = 14f;    // m/s, about 50 km/h
        private const float CarefulSpeed = 6.5f;    // m/s while turning hard or passing
        private const float TurnRate = 120f;        // degrees per second
        private const float PointReach = 4.5f;      // metres: a route point this near is reached
        private const float FinalReach = 1.2f;      // metres: the last point of a route is driven right up to
        private const float SenseInterval = 0.15f;  // seconds
        private const float SenseHeight = 0.9f;     // metres above the road the look-ahead runs
        private const float StopGap = 2.5f;         // metres left between the nose and whatever is ahead
        private const float Braking = 4f;           // m/s2 it plans its slowing down with
        private const float StopPatience = 2f;      // seconds held up near the goal before stopping there for good
        private const float FollowEase = 10f;       // metres over which a follower matches its target's speed
        private const float VerticalDamping = 11f;  // per second, on the vertical speed of a vehicle the traffic system no longer steadies
        private const float Pickup = 6f;            // m/s2 it gathers speed with
        private const float Slowing = 24f;          // m/s2 it can lose speed with

        /// <summary>A cruise speed above this (m/s, about 86 km/h) is a fast drive: see the summary.</summary>
        public const float FastFrom = 24f;
        private const float FastPickupLeast = 1.2f; // m/s2 left near its top speed (0-100 km/h in about 5.5 s, 100-200 in about 13)
        private const float FastBraking = 9f;       // m/s2 it plans its slowing for a bend, or for the car it follows, with

        private static int _obstacleMask;
        private static int _blindMask;              // the same without the people the game walks about: for a driver who does not see them
        private static int _solidMask;              // buildings and street furniture: what a car that leaves its lane ends against

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
                Listed(value);
            }
        }

        // Every driver with a drive under way, for whoever watches for a unit coming (an accident's officer on traffic).
        private static readonly EmergencyDriver[] Driving = new EmergencyDriver[16];

        private void Listed(bool driving)
        {
            int free = -1;
            for (int i = 0; i < Driving.Length; i++)
            {
                if (ReferenceEquals(Driving[i], this))
                {
                    if (!driving)
                    {
                        Driving[i] = null;
                    }
                    return;
                }
                if (free < 0 && Driving[i] == null)
                {
                    free = i;   // (a driver destroyed with its host counts as gone)
                }
            }
            if (driving && free >= 0)
            {
                Driving[free] = this;
            }
        }

        /// <summary>Places in the list of drivers with a drive under way.</summary>
        public static int ListPlaces => Driving.Length;

        /// <summary>The driver in that place of the list if it drives a vehicle with its emergency lights on now; null otherwise. Field reads.</summary>
        public static EmergencyDriver LitAt(int place)
        {
            EmergencyDriver driver = Driving[place];
            return driver != null && driver._active && driver.Lit && driver._car != null ? driver : null;
        }

        /// <summary>True while the vehicle it drives shows emergency lights (an ambulance, a patrol car, the fire engine): its unit says so.</summary>
        public bool Lit { get; set; }

        /// <summary>The vehicle it drives while a drive is under way; null otherwise.</summary>
        public VehicleComponent Car => _active ? _car : null;

        public bool Arrived { get; private set; }

        /// <summary>How fast the driver is currently making the vehicle go, in m/s.</summary>
        public float Speed => Active ? _speed : 0f;

        /// <summary>Speed on a clear, straight road, in m/s. Begin resets it to the default.</summary>
        public float CruiseSpeed { get; set; } = DefaultCruiseSpeed;

        /// <summary>False keeps it in its own lane whatever is ahead: it closes up and waits. Begin sets it back to true.</summary>
        public bool MayPass { get; set; } = true;

        /// <summary>Metres within which a route point counts as reached (a route is cut that much at each corner). Begin resets it.</summary>
        public float Reach
        {
            get => _reach;
            set => _reach = Mathf.Clamp(value, FinalReach, PointReach);
        }

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

        /// <summary>Starts a drive to a place; the path is read live, so the caller may keep adding points while it drives.</summary>
        /// <param name="path">Road points to follow, in order. The vehicle stops at the last one.</param>
        /// <param name="goal">The destination; the vehicle pulls up within stopDistance of it.</param>
        /// <param name="nearEnough">Above zero: held up this close to the goal, it stops in its lane and has arrived; else it waits.</param>
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
            ResetWay();
            MayPass = true;
            CruiseSpeed = DefaultCruiseSpeed;
            Arrived = false;
            Active = true;
            TrafficTools.Touch(car, "driven by the mod");
        }

        /// <summary>A drive whose goal is the end of the route itself, distances measured along it: it stops on the last point, in its lane.</summary>
        /// <param name="nearEnough">Above zero: held up with no more than this much route left, it stops and has arrived.</param>
        public void BeginRoute(VehicleComponent car, List<Vector3> path, float nearEnough)
        {
            Begin(car, path, path.Count > 0 ? path[path.Count - 1] : car.transform.position, 0f, nearEnough);
            _routeOnly = true;
        }

        /// <summary>Backs the vehicle tail first along a route to its last point: it looks behind, stops short of whatever is there, never passes.</summary>
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

        /// <summary>The road point to pull up at: it stops there in its lane, whatever its distance to the goal.</summary>
        public void PullUpAt(int pathIndex)
        {
            _stopAt = pathIndex;
        }

        /// <summary>The road point the vehicle is heading for; every point before it is behind.</summary>
        public int NextPoint => _next;

        /// <summary>A pursuit: it settles in behind the target at about the gap, never passes it, and stops when it stops. Ends with Stop.</summary>
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

        /// <summary>Pulls it in to the kerb by so many metres right of its road points (negative: left); the caller checked the room. Begin undoes it.</summary>
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

        /// <summary>Ends the drive with the vehicle standing dead still; nothing damps its springs after, so its holder parks it.</summary>
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
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnFixedUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnFixedUpdate()
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

            // The next road point not yet reached. At the route's end, or the point to pull up at, it stops in its lane; a route
            // that is itself the goal is driven right up to its end (or counts once passed).
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
                TellWay(position, roadRight);
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
            // The traffic system's springs have no damper: it replaces its own cars' whole velocity every step, but not an
            // ignored car's, so the vertical speed is bled off here (about 0.7 of critical) and the body settles, never hopping.
            if (_solidRoom < float.MaxValue)
            {
                _solidRoom = Mathf.Max(0f, _solidRoom - _speed * Time.fixedDeltaTime);   // between two looks: what it has covered since is off the room it had
            }
            Move(_reverse ? -_speed : _speed);
        }

        private void Move(float speed)
        {
            Vector3 velocity = _body.rotation * Vector3.forward * speed;
            velocity.y = _body.velocity.y * Mathf.Exp(-VerticalDamping * Time.fixedDeltaTime);
            _body.velocity = velocity;
            _body.angularVelocity = Vector3.zero;
        }

        private void Arrive()
        {
            Arrived = true;
            Stop();
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }
    }
}
