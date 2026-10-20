using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Drives the mod's bus and tow truck (<see cref="IDrivenVehicle"/>) along a route by <see cref="BusPath"/>, adding the
    /// physics step, the road's height and what is in the way ahead (not beyond the kerb). Cost: one comparison a physics
    /// step while idle; driving, a pose a step, a box query each 0.15 s and two ground rays each 0.1 s.
    /// </summary>
    public sealed class BusDriver : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("transport", "BusDriver", typeof(BusDriver));

        private const float StopGap = 3f;            // metres between its nose and a vehicle it stands behind
        private const float PersonGap = 2.5f;        // metres between its nose and a person
        private const float GateShort = 1.0f;        // metres short of a pull-up point its nose stands
        private const float SenseEvery = 0.15f;      // seconds
        private const float SenseLeast = 8f;         // metres ahead of its nose it always looks
        private const float SenseBox = 3f;           // metres of path each box covers
        private const float SenseMargin = 0.2f;      // the look is this much wider than the bus, each side
        private const float StopPatience = 2f;       // seconds held up near the end of a route before that is the end
        private const float GroundEvery = 0.1f;      // seconds

        private static int _mask;
        private static int _peopleLayers;
        private static readonly Collider[] Hits = new Collider[16];

        private readonly BusPath _path = new BusPath();
        private readonly List<float> _rx = new List<float>(128);
        private readonly List<float> _ry = new List<float>(128);
        private readonly List<float> _rz = new List<float>(128);
        private IDrivenVehicle _bus;
        private float _s;              // metres along the path its front axle has come
        private float _gateAt = float.MaxValue;
        private float _speed;
        private float _pull;
        private float _obstacle = float.MaxValue;
        private Collider _obstacleHit;   // the nearest thing in its way when last looked
        private Collider _passing;       // what it is going round (BusDetour): not looked at until it is behind
        private float _passingUntil;     // metres along its route its front axle comes to before that is so
        private bool _person;
        private bool _blocked;
        private float _blockedSince;
        private float _senseAt;
        private float _groundAt;
        private float _nearEnough;
        private bool _kerb;
        private Vector3 _kerbPoint;
        private Vector3 _kerbTowards;
        private Vector3 _front;
        private Vector3 _rear;
        private float _frontY;
        private float _rearY;
        private float _heading;

        public bool Active { get; private set; }   // driving a route

        /// <summary>True once it has come to rest at the end of its route or where it was told to pull up.</summary>
        public bool Arrived { get; private set; }

        public float Speed => Active ? _speed : 0f;

        public float CruiseSpeed { get; set; } = 11f;   // m/s on an open road

        public float RouteLeft => Active ? Mathf.Max(0f, _path.End - _s) : 0f;   // metres

        /// <summary>Metres to the end of its last route, driving or not.</summary>
        public float PathLeft => _path.Count >= 2 ? Mathf.Max(0f, _path.End - _s) : 0f;

        /// <summary>Its front axle, the point its routes are laid for.</summary>
        public Vector3 Front => _front;

        /// <summary>Seconds it has stood behind something in its way; 0 when not held up.</summary>
        public float HeldFor => Active && _blocked ? Time.time - _blockedSince : 0f;

        /// <summary>What it stands behind while held up, else null.</summary>
        public Collider HeldBy => Active && _blocked ? _obstacleHit : null;

        /// <summary>Metres ahead of its nose of what holds it.</summary>
        public float HeldAhead => Active && _blocked ? _obstacle : float.MaxValue;

        /// <summary>The first point of the route its front axle has not reached.</summary>
        public int NextPoint => _path.NextPoint(_s);

        /// <summary>Seconds to rest at the end of its route, no faster than <paramref name="slowSpeed"/> over the last <paramref name="slowRun"/> metres.</summary>
        public float SecondsLeft(float cruise, float slowRun, float slowSpeed)
        {
            return Active ? _path.SecondsLeft(_s, _speed, cruise, slowRun > 0f ? _path.Total - slowRun : float.MaxValue, slowSpeed) : 0f;
        }

        private static int Mask
        {
            get
            {
                if (_mask == 0)
                {
                    _peopleLayers = TrafficTools.PeopleMask;
                    _mask = TrafficTools.VehicleMask | _peopleLayers;
                }
                return _mask;
            }
        }

        /// <summary>Takes the bus it drives from now on, where it has just been put.</summary>
        public void Take(IDrivenVehicle bus)
        {
            Forget();
            _bus = bus;
            Transform frame = bus.Frame;
            Vector3 forward = Flat(frame.forward);
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            _front = frame.position + forward * bus.FrontAxleAhead;
            _rear = frame.position + forward * bus.RearAxleAhead;
            _frontY = _front.y;
            _rearY = _rear.y;
            _heading = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            _groundAt = 0f;
        }

        /// <summary>
        /// Drives the bus along a route, coming to rest BusPath.FinalShort before its last point; held up with under
        /// <paramref name="nearEnough"/> metres left (0: never), where it is becomes the end.
        /// </summary>
        public void BeginRoute(IDrivenVehicle bus, List<Vector3> route, float nearEnough, List<float> caps = null)
        {
            if (_bus != bus)
            {
                Take(bus);
            }
            if (!Active)
            {
                _speed = 0f;
                _pull = 0f;
            }
            Arrived = false;
            _passing = null;
            _nearEnough = nearEnough;
            _gateAt = float.MaxValue;
            _blocked = false;
            _obstacle = float.MaxValue;
            _obstacleHit = null;
            _senseAt = 0f;
            _rx.Clear();
            _ry.Clear();
            _rz.Clear();
            for (int i = 0; i < route.Count; i++)
            {
                _rx.Add(route[i].x);
                _ry.Add(route[i].y);
                _rz.Add(route[i].z);
            }
            Vector3 facing = Flat(_front - _rear);
            _path.Build(_rx, _ry, _rz, route.Count, _front.x, _front.y, _front.z, facing.x, facing.z, caps);
            _s = 0f;
            Active = _path.Count >= 2 && _path.End > 0.01f;
            if (!Active)
            {
                Arrive();
            }
        }

        /// <summary>
        /// The first point of the route at least so many metres ahead of its nose, or the last: the place of something in its
        /// way, for the way round it (TransitBus.Round). -1 while it drives no route.
        /// </summary>
        public int PointAhead(float metres)
        {
            if (_path.Count < 2 || _bus == null)
            {
                return -1;
            }
            float at = _s + _bus.NoseBeyondAxle + Mathf.Max(0f, metres);
            for (int i = 0; i < _path.PointArc.Count; i++)
            {
                if (_path.PointArc[i] >= at)
                {
                    return i;
                }
            }
            return _path.PointArc.Count - 1;
        }

        /// <summary>
        /// Drives past something it is going round in a lane beside it: that collider is not looked at for the next
        /// <paramref name="metres"/> of its route (its corner would hold the bus as it pulls out beside it). Set after a route is begun.
        /// </summary>
        public void Pass(Collider by, float metres)
        {
            _passing = by;
            _passingUntil = _s + Mathf.Max(0f, metres);
        }

        /// <summary>Pulls up with its nose short of this point of the route; -1 for none.</summary>
        public void PullUpAt(int pathIndex)
        {
            _gateAt = pathIndex >= 0 && pathIndex < _path.PointArc.Count ? _path.PointArc[pathIndex] - (_bus != null ? _bus.NoseBeyondAxle : 0f) - GateShort : float.MaxValue;
        }

        /// <summary>Where the kerb is: what lies beyond it is not looked at.</summary>
        public void Kerb(Vector3 point, Vector3 towards)
        {
            _kerb = true;
            _kerbPoint = point;
            _kerbTowards = Flat(towards).normalized;
        }

        public void NoKerb()
        {
            _kerb = false;
        }

        public void Stop()
        {
            Active = false;
            _speed = 0f;
            _pull = 0f;
            _blocked = false;
        }

        public void Forget()
        {
            Stop();
            _bus = null;
            _kerb = false;
        }

        // At rest: the last pose stands, its brake lights on.
        private void Arrive()
        {
            Active = false;
            Arrived = true;
            _speed = 0f;
            _pull = 0f;
            _blocked = false;
        }

        private static readonly int HotFixedUpdate = HotSpots.Slot("Transport: BusDriver.FixedUpdate");
        private void FixedUpdate()
        {
            using var hotSpot = HotSpots.Time(HotFixedUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // its feature was switched off after its faults
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
            if (_bus == null || !_bus.Out)
            {
                Active = false;
                return;
            }
            float dt = Time.fixedDeltaTime;
            float now = Time.time;
            if (now >= _senseAt)
            {
                _senseAt = now + SenseEvery;
                Sense();
            }

            // A pull-up point it can no longer stop short of is driven past.
            float gate = _gateAt;
            if (gate < float.MaxValue && (gate < _s - 0.05f || _speed * _speed > 2f * BusPath.BrakingMost * Mathf.Max(0.01f, gate - _s)))
            {
                gate = float.MaxValue;
            }
            float stopAt = Mathf.Min(_path.End, gate);
            float room = stopAt - _s;
            _path.At(_s, out _, out _, out _, out _, out _);
            float wanted = Mathf.Min(CruiseSpeed, Mathf.Min(_path.LimitHere, BusPath.StopIn(room)));
            float gap = _person ? PersonGap : StopGap;
            if (_obstacle < float.MaxValue)
            {
                wanted = Mathf.Min(wanted, BusPath.StopIn(_obstacle - gap - _speed * SenseEvery));
            }
            float was = _speed;
            _speed = BusPath.Pace(_speed, ref _pull, wanted, dt);
            float moved = Mathf.Min(_speed * dt, Mathf.Max(0f, room));
            _s += moved;

            // Held up near the end of a route that allows it: where it is will do.
            bool held = _obstacle < gap + 1.5f && _speed < 0.3f;
            if (held && !_blocked)
            {
                _blockedSince = now;
            }
            _blocked = held;
            Move(moved, was, dt);
            if (_blocked && _nearEnough > 0f && _path.End - _s < _nearEnough && now - _blockedSince > StopPatience)
            {
                Arrive();
                return;
            }
            if (stopAt - _s <= 0.02f && _speed < 0.25f)
            {
                _s = stopAt;
                Arrive();
            }
        }

        // The front axle to its new place, the rear drawn after it, the road under both.
        private void Move(float moved, float was, float dt)
        {
            _path.At(_s, out float fx, out float fy, out float fz, out float roadX, out float roadZ);
            float rx = _rear.x, rz = _rear.z;
            BusPath.Trail(fx, fz, ref rx, ref rz, _bus.AxleSpan);
            var front = new Vector3(fx, fy, fz);
            var rear = new Vector3(rx, _rear.y, rz);
            if (Time.time >= _groundAt)
            {
                _groundAt = Time.time + GroundEvery;
                if (CitySpots.Ground(front, out float frontY))
                {
                    _frontY = frontY;
                }
                if (CitySpots.Ground(rear, out float rearY))
                {
                    _rearY = rearY;
                }
            }
            front.y = Mathf.Lerp(_front.y, _frontY, 0.3f);
            rear.y = Mathf.Lerp(_rear.y, _rearY, 0.3f);
            Vector3 body = Flat(front - rear).normalized;
            float heading = Mathf.Atan2(body.x, body.z) * Mathf.Rad2Deg;
            float turning = dt > 0f ? Mathf.DeltaAngle(_heading, heading) / dt : 0f;
            _heading = heading;
            float steer = Vector3.SignedAngle(body, new Vector3(roadX, 0f, roadZ), Vector3.up);
            _front = front;
            _rear = rear;
            _bus.Pose(front, rear, moved, steer, dt > 0f ? (_speed - was) / dt : 0f, turning, _speed, dt);
        }

        // What is in its way ahead, as far as it needs to stop in.
        private void Sense()
        {
            _obstacle = float.MaxValue;
            _obstacleHit = null;
            _person = false;
            if (_path.Count < 2)
            {
                return;
            }
            float reach = Mathf.Max(SenseLeast, _speed * _speed / (2f * BusPath.Braking) + StopGap + SenseBox);
            float nose = _s + _bus.NoseBeyondAxle;
            float total = _path.Total;
            int saved = _path.Cursor;
            var half = new Vector3(_bus.HalfBreadth + SenseMargin, 0.9f, SenseBox * 0.5f);
            for (float ahead = 0f; ahead < reach && _obstacle == float.MaxValue; ahead += SenseBox)
            {
                float middle = nose + ahead + SenseBox * 0.5f;
                _path.At(Mathf.Min(middle, total), out float x, out float y, out float z, out float dx, out float dz);
                var along = new Vector3(dx, 0f, dz);
                var centre = new Vector3(x, y, z) + along * Mathf.Max(0f, middle - total);
                int found = Physics.OverlapBoxNonAlloc(centre + Vector3.up * 1.2f, half, Hits, Quaternion.LookRotation(along, Vector3.up), Mask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < found; i++)
                {
                    Collider hit = Hits[i];
                    Hits[i] = null;
                    if (hit == null || (_bus != null && hit.attachedRigidbody == _bus.Body))
                    {
                        continue;
                    }
                    if (_passing != null && _s < _passingUntil && (hit == _passing || (hit.attachedRigidbody != null && hit.attachedRigidbody == _passing.attachedRigidbody)))
                    {
                        continue;   // what it is going round, in the lane it has left
                    }
                    if (_kerb && Vector3.Dot(Flat(hit.bounds.center - _kerbPoint), _kerbTowards) > 0f)
                    {
                        continue;   // on the pavement: somebody waiting at the stop
                    }
                    if (ahead < _obstacle)
                    {
                        _obstacle = ahead;
                        _obstacleHit = hit;
                        _person = ((_peopleLayers >> hit.gameObject.layer) & 1) != 0;
                    }
                }
            }
            _path.Seek(saved);
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }
    }
}
