using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Drives the mod's own bus (<see cref="BusVehicle"/>) along a route the way a bus is driven.
    /// The path, the pace and the way the rear axle follows the front are <see cref="BusPath"/>'s,
    /// which the build's NavCheck drives through a bay as it ships; this class is what the game
    /// adds: the physics step, the road's height under the axles, and what is in the bus's way.
    ///
    /// THE ROAD IT TAKES. The route is a list of points (the line's lane points, a bay's path a
    /// metre at a time: TransitBus lays them for the front axle); the front axle drives a smooth
    /// curve through them and the rear axle follows at the wheelbase: a bus's rear wheels cut
    /// inside a bend, and its body lags the road coming out of one, as here. Nothing slides: the
    /// wheels roll by the distance driven and the front ones steer by the angle between the body
    /// and the road ahead.
    ///
    /// ITS PACE. Never faster than it is told (TransitBus: 40 km/h, slower at junctions and into
    /// a bay) nor than a bend allows, slowed by the time it gets there. Every stop is planned at a
    /// pace standing passengers keep their feet at: the end of its route (exactly: it comes to rest
    /// where it should, not short and not past), a point it is told to pull up at with its nose
    /// short of it (a red light's stop line: one it can no longer stop short of with all it has is
    /// driven past, as anybody would), and whatever is ahead of it on its own way, a few metres
    /// short. It eases away from rest.
    ///
    /// WHAT IT LOOKS AT. Every 0.15 s, along its own path ahead of its nose (round a bend as well as
    /// down a straight), boxes as wide as the bus over as far as it needs to stop in: one physics
    /// query each. Told where the kerb is (Kerb: a bus pulling in to its bay), it leaves out what is
    /// beyond it, on the pavement. Held up close to the end of a route that allows it (nearEnough),
    /// it takes where it is as the end after a couple of seconds.
    ///
    /// The bus is moved in the physics step (a kinematic body: it pushes, it is not pushed), and the
    /// road under its axles is looked at ten times a second. Nothing else recurring.
    /// </summary>
    public sealed class BusDriver : MonoBehaviour
    {
        private const float StopGap = 3f;            // metres between its nose and a vehicle it stands behind
        private const float PersonGap = 2.5f;        // ...and a person
        private const float GateShort = 1.0f;        // its nose this far short of a point it is told to pull up at
        private const float SenseEvery = 0.15f;
        private const float SenseLeast = 8f;         // metres ahead of its nose it always looks
        private const float SenseBox = 3f;           // metres of path each box covers
        private const float SenseMargin = 0.2f;      // the look is this much wider than the bus, each side
        private const float StopPatience = 2f;       // seconds held up near the end of a route before that is the end
        private const float GroundEvery = 0.1f;

        private static int _mask;
        private static int _peopleLayers;
        private static readonly Collider[] Hits = new Collider[16];

        private readonly BusPath _path = new BusPath();
        private readonly List<float> _rx = new List<float>(128);
        private readonly List<float> _ry = new List<float>(128);
        private readonly List<float> _rz = new List<float>(128);
        private BusVehicle _bus;
        private float _s;              // metres along the path its front axle has come
        private float _gateAt = float.MaxValue;
        private float _speed;
        private float _pull;
        private float _obstacle = float.MaxValue;
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

        /// <summary>True while it drives a route (false once it has come to rest at the end or where it was told to pull up).</summary>
        public bool Active { get; private set; }

        /// <summary>True once it has come to rest at the end of its route, or where it was told to pull up.</summary>
        public bool Arrived { get; private set; }

        public float Speed => Active ? _speed : 0f;

        /// <summary>m/s it drives at on an open road.</summary>
        public float CruiseSpeed { get; set; } = 11f;

        /// <summary>Metres of route still to drive.</summary>
        public float RouteLeft => Active ? Mathf.Max(0f, _path.End - _s) : 0f;

        /// <summary>Where its front axle is: the point its routes are laid for.</summary>
        public Vector3 Front => _front;

        /// <summary>Seconds it has stood behind something in its way; 0 when it is not held up.</summary>
        public float HeldFor => Active && _blocked ? Time.time - _blockedSince : 0f;

        /// <summary>The first point of the route as given that its front axle has not yet reached.</summary>
        public int NextPoint => _path.NextPoint(_s);

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

        // ---------------------------------------------------------------- what it is told

        /// <summary>The bus it drives from now on, standing where it has just been put (BusVehicle.Place).</summary>
        public void Take(BusVehicle bus)
        {
            Forget();
            _bus = bus;
            Transform frame = bus.transform;
            Vector3 forward = Flat(frame.forward);
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            _front = frame.position + forward * BusVehicle.AxleFront;
            _rear = frame.position + forward * BusVehicle.AxleRear;
            _frontY = _front.y;
            _rearY = _rear.y;
            _heading = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            _groundAt = 0f;
        }

        /// <summary>
        /// Drives the bus along a route from where it stands: its first point is the next one ahead. The bus comes to rest
        /// BusPath.FinalShort before the last point (TransitBus lays the last one that far beyond where the bus is to stand).
        /// A route begun while it is moving (the end changed under it) carries on at the speed it has. Its axles are where
        /// this driver last put them (Take put them where the bus was placed): exact, not read back from the body.
        /// </summary>
        /// <param name="nearEnough">Held up with no more than this left, it stands where it is and that is the end; 0 for never.</param>
        public void BeginRoute(BusVehicle bus, List<Vector3> route, float nearEnough)
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
            _nearEnough = nearEnough;
            _gateAt = float.MaxValue;
            _blocked = false;
            _obstacle = float.MaxValue;
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
            _path.Build(_rx, _ry, _rz, route.Count, _front.x, _front.y, _front.z, facing.x, facing.z);
            _s = 0f;
            Active = _path.Count >= 2 && _path.End > 0.01f;
            if (!Active)
            {
                Arrive();
            }
        }

        /// <summary>Pull up at this point of the route, its nose short of it; -1 for none.</summary>
        public void PullUpAt(int pathIndex)
        {
            _gateAt = pathIndex >= 0 && pathIndex < _path.PointArc.Count ? _path.PointArc[pathIndex] - BusVehicle.NoseAhead - GateShort : float.MaxValue;
        }

        /// <summary>Where the kerb is (a point on its edge, and the way across it onto the pavement): what lies beyond it is not looked at.</summary>
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

        /// <summary>Stands where it is now.</summary>
        public void Stop()
        {
            Active = false;
            _speed = 0f;
            _pull = 0f;
            _blocked = false;
        }

        /// <summary>The bus is no longer this driver's.</summary>
        public void Forget()
        {
            Stop();
            _bus = null;
            _kerb = false;
        }

        // At rest where it is: the last pose stands (its wheels as they were turned, its brake lights on: BusVehicle.Pose).
        private void Arrive()
        {
            Active = false;
            Arrived = true;
            _speed = 0f;
            _pull = 0f;
            _blocked = false;
        }

        // ---------------------------------------------------------------- driving

        private void FixedUpdate()
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

            // A pull-up point it can no longer stop short of with all it has is driven past, as anybody would.
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

            // Held up: what it waits behind is said, and near the end of a route that allows it, where it is will do.
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

        // The front axle to its new place on the path, the rear drawn after it, the road under both, and the bus posed.
        private void Move(float moved, float was, float dt)
        {
            _path.At(_s, out float fx, out float fy, out float fz, out float roadX, out float roadZ);
            float rx = _rear.x, rz = _rear.z;
            BusPath.Trail(fx, fz, ref rx, ref rz, BusVehicle.Wheelbase);
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

        // What is in its way along its own path ahead of its nose, as far as it needs to stop in: one box each SenseBox metres.
        private void Sense()
        {
            _obstacle = float.MaxValue;
            _person = false;
            if (_path.Count < 2)
            {
                return;
            }
            float reach = Mathf.Max(SenseLeast, _speed * _speed / (2f * BusPath.Braking) + StopGap + SenseBox);
            float nose = _s + BusVehicle.NoseAhead;
            float total = _path.Total;
            int saved = _path.Cursor;
            var half = new Vector3(BusVehicle.HalfWidth + SenseMargin, 0.9f, SenseBox * 0.5f);
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
                    if (_kerb && Vector3.Dot(Flat(hit.bounds.center - _kerbPoint), _kerbTowards) > 0f)
                    {
                        continue;   // on the pavement: somebody waiting at the stop, not in the way
                    }
                    if (ahead < _obstacle)
                    {
                        _obstacle = ahead;
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
