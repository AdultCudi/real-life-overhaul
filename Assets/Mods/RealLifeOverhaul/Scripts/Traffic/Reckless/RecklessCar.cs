using System.Collections.Generic;
using GleyTrafficSystem;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The one reckless car: an ordinary traffic car already on the road, taken over where it drives
    /// and driven on along its own lanes by the driver every unit of the mod has (ResponseUnit and
    /// EmergencyDriver), told to drive carelessly. Nothing about the car changes but how it is driven:
    /// no lights, no body, no sound of the mod's. This class holds what its kind does that the driver
    /// does not know about (which lights it stops at, the drunk's uneven pace) and notices, by position
    /// alone, what really comes of it: its box meeting another vehicle's, a bend taken faster than the
    /// road holds, something solid a hand's width ahead of its nose. For a driver with no margin that
    /// includes what stands beside the road (buildings, lamp posts and the like: EmergencyDriver.SeeSolids,
    /// the same look with a wider mask), and one who takes a bend faster than the road holds is no longer
    /// stopped dead in it: the car carries straight on, unsteered, for a second and a half at the most
    /// (EmergencyDriver.Slide), and ends against whatever is there, a wall, a lamp post, a parked car
    /// or another car, or comes to rest across the road if nothing is. Who reacts to it and what
    /// scene follows is RecklessDrivers' business.
    ///
    /// WHICH OF THE ROAD'S SAFETY RULES ARE RELAXED, FOR THIS CAR ONLY. The traffic system stops its cars
    /// for each other with a sensor box ahead of each and for lights with a flag on the stop line. This
    /// car is out of that system while it is ours (as every unit is), so those rules reach it only
    /// through its driver's look ahead, and that look is what is weakened: less braking, a shorter look,
    /// and for some no eye for people (EmergencyDriver.Careless). Every other car keeps its own sensor
    /// and still brakes for this one. A red light is read from the stop line's own flag on its route
    /// (Waypoint.stop); only the red-light runner does not hold there.
    ///
    /// COST. Not on the road: the component is switched off and costs nothing. On the road: four times a
    /// second (Tick) a walk of the traffic list for the cars within 35 m (one squared distance each), a
    /// look at the next dozen route points for a light, and the bend at the next point; every other
    /// physics step the box test against those few cars and the player's (three points each, no physics
    /// query, nothing allocated). The driver's own look ahead is the one it has always had.
    ///
    /// GIVING IT BACK. Release hands it back to traffic or takes it off the road through ResponseUnit, in
    /// the state every unit is given back in; HandOver passes it to an accident scene as it stands. A car
    /// the pool took back under us is simply forgotten (IsLost).
    /// </summary>
    public sealed class RecklessCar : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("streets", "RecklessCar", typeof(RecklessCar));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        public enum Hit { None, Car, Player, Solid, Bend }

        private const float RouteLength = 1100f;     // metres of road it is given ahead of it
        private const float RouteLeast = 140f;       // ...and the least that makes it worth taking
        private const int RoutePoints = 420;
        private const float NearWithin = 35f;        // metres: traffic this near is tested for contact (nothing further can reach it before the next list walk)
        private const int NearMost = 8;
        private const int LightLook = 14;            // route points ahead in which a light is looked for
        private const float Touch = 0.1f;            // metres the two boxes are grown by: contact, not a near miss
        private const float SolidTouch = 0.35f;      // metres ahead of its nose at which something fixed has been hit...
        private const float SolidSpeed = 2.5f;       // ...if it is still doing this much, m/s
        private const float BendWithin = 5f;         // metres from a route point at which the bend there is judged
        private const float SettledPace = 11f;       // m/s once it has settled down: the traffic's pace
        private const float SlideMost = 1.5f;        // seconds a car that has lost a bend carries straight on before it has simply come to rest across the road
        private const float RollLeast = 2f;          // m/s: slower than this it is handed back standing

        private readonly List<Waypoint> _route = new List<Waypoint>(RoutePoints);
        private readonly VehicleComponent[] _near = new VehicleComponent[NearMost];
        private readonly float[] _nearSqr = new float[NearMost];
        private int _nearCount;
        private ResponseUnit _unit;
        private Vector3 _centre = new Vector3(0f, 0.8f, 0f);
        private Vector3 _half = new Vector3(0.95f, 0.8f, 2.2f);
        private float _cruise;
        private float _stopping;
        private float _foresight;
        private float _grip;
        private bool _seesPeople;
        private bool _obeysLights;
        private bool _uneven;
        private bool _skip;
        private int _lightAt = -1;       // the next light on its route, and what it showed when last looked at
        private bool _lightRed;
        private float _slideUntil;       // while it carries straight on out of a bend: when that ends; 0 otherwise
        private float _slideFrom;        // the speed it lost the bend at

        public StreetRules.Kind Kind { get; private set; }
        public bool AccidentDue { get; private set; }
        public bool Settled { get; private set; }

        /// <summary>What it has run into, if anything; the director takes it from here.</summary>
        public Hit Struck { get; private set; }

        /// <summary>The traffic car it met, when that is what it struck.</summary>
        public VehicleComponent Other { get; private set; }

        /// <summary>How fast the two were closing at that moment (or how fast it was going, alone), in m/s.</summary>
        public float Closing { get; private set; }

        /// <summary>Its own velocity at the moment it struck (before it was stopped), for the scene to let it carry on a little from.</summary>
        public Vector3 Velocity { get; private set; }

        /// <summary>The velocity of the traffic car it met, at that moment.</summary>
        public Vector3 OtherVelocity { get; private set; }

        /// <summary>Red lights it has driven through since it was taken (counted as its route passes each stop line).</summary>
        public int LightsRun { get; private set; }

        public bool InUse => _unit != null && _unit.InUse;
        public bool IsLost => _unit != null && _unit.IsLost;
        public VehicleComponent Car => _unit != null ? _unit.Car : null;
        public ResponseUnit Unit => _unit;
        public EmergencyDriver Driver => _unit != null ? _unit.Driver : null;
        public float HalfWidth => _half.x;

        private void Awake()
        {
            _unit = new ResponseUnit(gameObject);
            enabled = false;   // nothing to watch yet
        }

        /// <summary>
        /// Takes a traffic car over as it drives and sends it on along its own road as that kind of driver.
        /// False (and the car untouched) if it has too little road ahead of it or cannot be had.
        /// </summary>
        /// <param name="accidentDue">The driver has no margin: see StreetRules.Stopping, Foresight and PlannedGrip.</param>
        /// <param name="blind">The driver does not see people on foot at all.</param>
        /// <param name="never">For a test: a driver who never brakes for a vehicle and overcooks every bend.</param>
        public bool Take(VehicleComponent car, StreetRules.Kind kind, bool accidentDue, bool blind, bool never)
        {
            if (_unit.InUse || car == null || !TrafficTools.Ready)
            {
                return false;
            }
            Waypoint anchor = TrafficManager.Instance.GetTargetWaypoint(car.GetIndex());
            if (anchor == null || TrafficTools.WalkForward(anchor, RouteLength, RoutePoints, _route) < RouteLeast)
            {
                _route.Clear();
                return false;
            }
            float rolling = car.GetCurrentSpeed() / 3.6f;
            if (!_unit.Take(car))
            {
                _route.Clear();
                return false;
            }
            for (int i = 0; i < _route.Count; i++)
            {
                _unit.Path.Add(_route[i].position);
            }

            Kind = kind;
            AccidentDue = accidentDue;
            Settled = false;
            Struck = Hit.None;
            Other = null;
            Closing = 0f;
            Velocity = Vector3.zero;
            OtherVelocity = Vector3.zero;
            LightsRun = 0;
            _lightAt = -1;
            _nearCount = 0;
            _slideUntil = 0f;
            float grip = Events.Weather.GripFactor;
            _cruise = StreetRules.Cruise(kind, Random.value);
            _stopping = never ? 3f : StreetRules.Stopping(accidentDue, Random.value);
            _foresight = never ? 0.05f : StreetRules.Foresight(kind, accidentDue, Random.value);
            _grip = never ? StreetRules.DryGrip * 1.9f : StreetRules.PlannedGrip(kind, accidentDue, Random.value, grip);
            _seesPeople = !blind;
            _obeysLights = StreetRules.ObeysLights(kind);
            _uneven = kind == StreetRules.Kind.Drunk;

            EmergencyDriver driver = _unit.Driver;
            // (Its goal is the end of its road, measured along it: a road that comes back past its own end is not mistaken for arriving.)
            driver.BeginRoute(car, _unit.Path, 0f);
            driver.CruiseSpeed = _cruise;
            driver.Careless(_stopping, _foresight, _seesPeople, _grip);
            if (accidentDue || never)
            {
                driver.SeeSolids();    // no margin: what stands beside the road is what it may end against
            }
            driver.Rolling(rolling);   // it carries on from the speed it had: nothing shows as it changes hands
            if (kind == StreetRules.Kind.Drunk)
            {
                driver.Sway(StreetRules.Sway(accidentDue, Random.value), Random.Range(3f, 5f));
            }
            RoadMap.Ensure();   // the grid a lane to pass in is looked up in

            // Its size, as CarStrikes measures it (it is watched for people from the same moment).
            CarStrikes strikes = CarStrikes.Instance;
            if (strikes != null && Settings.Ragdolls)
            {
                strikes.Watch(car);
                strikes.Box(car, out _centre, out _half);
            }
            else
            {
                _centre = new Vector3(0f, 0.8f, 0f);
                _half = new Vector3(Mathf.Max(0.8f, car.colliderWidth * 0.5f), 0.8f, Mathf.Max(1.5f, car.length * 0.5f));
            }
            _skip = false;
            enabled = true;
            return true;
        }

        /// <summary>From here it drives like anybody: the traffic's pace, every light, all the braking there is. It is handed back when that will not show.</summary>
        public void Settle()
        {
            if (!InUse || Settled)
            {
                return;
            }
            Settled = true;
            _obeysLights = true;
            _uneven = false;
            _seesPeople = true;
            EmergencyDriver driver = _unit.Driver;
            driver.CruiseSpeed = Mathf.Min(_cruise, SettledPace);
            driver.Careless(StreetRules.CarefulStopping, StreetRules.CarefulForesight, true, StreetRules.PlannedGrip(Kind, false, 0f, Events.Weather.GripFactor));
            driver.Sway(0f, 4f);
        }

        /// <summary>The driver has hurt somebody and is not stopping: faster, and no light holds it.</summary>
        public void Flee()
        {
            if (!InUse)
            {
                return;
            }
            Settled = false;
            _obeysLights = false;
            _cruise = Mathf.Max(_cruise, 19f);
            _unit.Driver.CruiseSpeed = _cruise;
        }

        /// <summary>A change of pace asked for by the director (a patrol car behind it: faster, or slower).</summary>
        public void Pace(float cruise)
        {
            if (InUse)
            {
                _cruise = cruise;
                _unit.Driver.CruiseSpeed = cruise;
            }
        }

        public float Cruise => _cruise;

        /// <summary>The road point it has last passed, for something to be put on the road behind it; null if it has none.</summary>
        public Waypoint Behind()
        {
            if (!InUse || _route.Count == 0)
            {
                return null;
            }
            int index = Mathf.Clamp(_unit.Driver.NextPoint - 1, 0, _route.Count - 1);
            return _route[index];
        }

        /// <summary>The index of that point in its route, which is also its index in the unit's path.</summary>
        public int BehindIndex => InUse ? Mathf.Clamp(_unit.Driver.NextPoint - 1, 0, Mathf.Max(0, _route.Count - 1)) : 0;

        /// <summary>True if a traffic light stands on its route within so many metres of road ahead.</summary>
        public bool LightAhead(float within)
        {
            if (!InUse)
            {
                return false;
            }
            float walked = 0f;
            for (int i = Mathf.Max(1, _unit.Driver.NextPoint); i < _route.Count && walked < within; i++)
            {
                walked += Vector3.Distance(_route[i].position, _route[i - 1].position);
                if (_route[i].enter && !_route[i].giveWay)
                {
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- four times a second

        /// <summary>One look at where it is on its route: the light ahead, the traffic round it, the bend it is in. Called by the director.</summary>
        public void Tick(float now)
        {
            if (!InUse || Struck != Hit.None)
            {
                return;
            }
            VehicleComponent car = _unit.Car;
            EmergencyDriver driver = _unit.Driver;
            if (_slideUntil > 0f)
            {
                // Carrying straight on out of a bend it lost: if nothing has been in its way by now (FixedUpdate sees
                // that), it has come to rest across the road, and that is the crash.
                if (car != null && (now >= _slideUntil || !driver.Active))
                {
                    Hold(Hit.Bend, null, _slideFrom);
                }
                return;
            }
            if (car == null || !driver.Active)
            {
                return;
            }
            Lights(driver);
            if (_uneven)
            {
                driver.CruiseSpeed = _cruise * (0.82f + 0.28f * Mathf.Sin(now * 0.9f));   // a foot that cannot hold a pace
            }
            Neighbours(car);
            Bend(car, driver);
        }

        // The first traffic light on the road ahead. A stop line's flag is true while its light is not green
        // (TrafficLightsIntersection sets it through WaypointManager.TrafficLightChanged); a point with giveWay set
        // belongs to a junction without lights. Whoever obeys lights holds there; the runner is counted through.
        private void Lights(EmergencyDriver driver)
        {
            int next = driver.NextPoint;
            if (_lightAt >= 0 && next > _lightAt)
            {
                if (_lightRed && !_obeysLights)
                {
                    LightsRun++;
                }
                _lightAt = -1;
            }
            int hold = -1;
            int last = Mathf.Min(next + LightLook, _route.Count);
            for (int i = next; i < last; i++)
            {
                Waypoint point = _route[i];
                if (point.enter && !point.giveWay)
                {
                    _lightAt = i;
                    _lightRed = point.stop;
                    if (point.stop)
                    {
                        hold = i;
                    }
                    break;
                }
            }
            driver.HoldAt = _obeysLights ? hold : -1;
        }

        // The traffic near enough to be met before the next walk of the list: at 35 m and a quarter of a second,
        // nothing further off can be. One squared distance for each car on the road.
        private void Neighbours(VehicleComponent car)
        {
            Vector3 at = car.transform.position;
            List<VehicleComponent> pool = Manager.GetVehicleList();
            int count = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                VehicleComponent other = pool[i];
                if (other == null || other == car || !other.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Vector3 offset = other.transform.position - at;
                float sqr = offset.x * offset.x + offset.z * offset.z;
                if (sqr >= NearWithin * NearWithin || offset.y > 4f || offset.y < -4f)
                {
                    continue;
                }
                if (count < NearMost)
                {
                    _near[count] = other;
                    _nearSqr[count++] = sqr;
                    continue;
                }
                // More than eight that near (a jam): the nearest eight are kept.
                int furthest = 0;
                for (int k = 1; k < NearMost; k++)
                {
                    if (_nearSqr[k] > _nearSqr[furthest])
                    {
                        furthest = k;
                    }
                }
                if (sqr < _nearSqr[furthest])
                {
                    _near[furthest] = other;
                    _nearSqr[furthest] = sqr;
                }
            }
            for (int i = count; i < _nearCount; i++)
            {
                _near[i] = null;
            }
            _nearCount = count;
        }

        /// <summary>The traffic within 35 m of it at the last look, for whoever wants to sound a horn at it. Read at once; not kept.</summary>
        public VehicleComponent Near(int index) => index >= 0 && index < _nearCount ? _near[index] : null;
        public int NearCount => _nearCount;

        // The bend at the route point it is coming to, judged as it gets there: its real speed against what today's
        // road holds round a bend of that radius. A driver who plans within the grip there is never comes near it.
        private void Bend(VehicleComponent car, EmergencyDriver driver)
        {
            List<Vector3> path = _unit.Path;
            int next = driver.NextPoint;
            if (next < 1 || next >= path.Count - 1)
            {
                return;
            }
            Vector3 at = car.transform.position;
            Vector3 to = path[next] - at;
            to.y = 0f;
            if (to.sqrMagnitude > BendWithin * BendWithin)
            {
                return;
            }
            Vector3 before = path[next] - path[next - 1];
            Vector3 after = path[next + 1] - path[next];
            before.y = 0f;
            after.y = 0f;
            float legs = (before.magnitude + after.magnitude) * 0.5f;
            float bend = Vector3.Angle(before, after) * Mathf.Deg2Rad;
            if (legs < 1f || bend < 0.15f)
            {
                return;
            }
            float speed = driver.Speed;
            if (StreetRules.LosesIt(speed, legs / bend, Events.Weather.GripFactor))
            {
                // Not stopped dead in the bend: it goes straight on, and ends against whatever is there.
                _slideFrom = speed;
                _slideUntil = Time.time + SlideMost;
                driver.SeeSolids();
                driver.Slide();
            }
        }

        // ---------------------------------------------------------------- every other physics step

        private static readonly int HotFixedUpdate = HotSpots.Slot("Traffic: RecklessCar.FixedUpdate");
        private void FixedUpdate()
        {
            using var hotSpot = HotSpots.Time(HotFixedUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
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
            _skip = !_skip;
            if (_skip || Struck != Hit.None || !InUse)
            {
                return;
            }
            VehicleComponent car = _unit.Car;
            EmergencyDriver driver = _unit.Driver;
            if (car == null || car.rb == null || !driver.Active)
            {
                return;
            }
            Rigidbody body = car.rb;
            Vector3 velocity = body.velocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            _own = velocity;
            Vector3 origin = body.position;
            Quaternion toCar = Quaternion.Inverse(body.rotation);

            // The box another vehicle must reach into, in this car's own space: stretched towards where it is going by
            // what it covers before the next look, as CarStrikes stretches the player's.
            float reach = speed * Time.fixedDeltaTime * 2f;
            float front = _centre.z + _half.z + Touch + reach;
            float back = _centre.z - _half.z - Touch;

            for (int i = 0; i < _nearCount; i++)
            {
                VehicleComponent other = _near[i];
                if (other == null || other.rb == null || !other.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Transform theirs = other.transform;
                if (Meets(toCar, origin, theirs.position, theirs.forward, Mathf.Max(1.5f, other.length * 0.5f), Mathf.Max(0.8f, other.colliderWidth * 0.5f), front, back))
                {
                    Vector3 their = other.rb.velocity;
                    their.y = 0f;
                    _theirs = their;
                    Hold(Hit.Car, other, (velocity - their).magnitude);
                    return;
                }
            }

            CarStrikes strikes = CarStrikes.Instance;
            if (strikes != null && strikes.PlayerCar(out Rigidbody player, out Vector3 centre, out Vector3 half) && player != null)
            {
                Vector3 middle = player.position + player.rotation * new Vector3(0f, 0f, centre.z);
                if ((middle - origin).sqrMagnitude < NearWithin * NearWithin
                    && Meets(toCar, origin, middle, player.rotation * Vector3.forward, half.z, half.x, front, back))
                {
                    Vector3 their = player.velocity;
                    their.y = 0f;
                    Hold(Hit.Player, null, (velocity - their).magnitude);
                    return;
                }
            }

            // Something fixed a hand's width ahead of its nose while it is still moving: the driver's own look ahead
            // says so (the one query it has always made). A person is CarStrikes' business, not a crash.
            if (speed > SolidSpeed && driver.SolidRoom < SolidTouch)
            {
                Hold(Hit.Solid, null, speed);   // its nose has reached a wall, a lamp post or the like
            }
            else if (speed > SolidSpeed && driver.Room < SolidTouch && !driver.AheadIsPerson)
            {
                Rigidbody ahead = driver.Ahead;
                VehicleComponent vehicle = ahead != null ? ahead.GetComponent<VehicleComponent>() : null;
                if (vehicle != null && vehicle != car)
                {
                    Vector3 their = ahead.velocity;
                    their.y = 0f;
                    _theirs = their;
                    Hold(Hit.Car, vehicle, (velocity - their).magnitude);
                }
                else if (ahead != null && strikes != null && strikes.PlayerCar(out Rigidbody mine, out _, out _) && ahead == mine)
                {
                    Hold(Hit.Player, null, (velocity - mine.velocity).magnitude);
                }
                else
                {
                    Hold(Hit.Solid, null, speed);
                }
            }
        }

        // True if another vehicle's body reaches into this car's box. Five points along it are tried, with the
        // box grown by the other's half width on whichever sides that width lies as the two stand to each other: to
        // the sides for a car alongside or nose to tail, fore and aft for one that lies across. Two rotations, no physics.
        private bool Meets(Quaternion toCar, Vector3 origin, Vector3 middle, Vector3 forward, float halfLength, float halfWidth, float front, float back)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                return false;
            }
            Vector3 centre = toCar * (middle - origin);
            if (centre.y < -2.5f || centre.y > 2.5f)
            {
                return false;
            }
            Vector3 along = toCar * forward.normalized;
            float side = _half.x + Touch + halfWidth * Mathf.Abs(along.z);
            float grow = halfWidth * Mathf.Abs(along.x);
            // (Five points along it, so that none of them is further apart than this car is wide: a car lying across cannot straddle it unseen.)
            for (int step = -2; step <= 2; step++)
            {
                float x = centre.x + along.x * (halfLength * 0.5f * step);
                float z = centre.z + along.z * (halfLength * 0.5f * step);
                if (x > -side && x < side && z > back - grow && z < front + grow)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Told from outside that it has hit something (the game's own collision with the player's car): as if it had seen it itself.</summary>
        public void Report(Hit what, VehicleComponent other, float closing)
        {
            if (InUse && Struck == Hit.None)
            {
                Hold(what, other, closing);
            }
        }

        // It has hit something. The driver is stopped at once (the cars do not plough on through each other for a
        // frame); the director sees Struck in its own next frame and starts the scene.
        private void Hold(Hit what, VehicleComponent other, float closing)
        {
            Struck = what;
            Other = other;
            Closing = closing;
            Velocity = _own;
            OtherVelocity = other != null ? _theirs : Vector3.zero;
            _slideUntil = 0f;
            if (_unit.InUse && !_unit.IsLost)
            {
                _unit.Driver.Stop();
            }
        }

        private Vector3 _own;            // its velocity at the last physics step it looked at, and the other car's when they met
        private Vector3 _theirs;

        /// <summary>
        /// True if it can be handed back to traffic on the move: settled, at the traffic's pace, in its lane, with
        /// nothing in its look ahead and no light holding it. Then nothing is lost by the traffic system starting
        /// with an empty slate for it, and nothing shows as it changes hands.
        /// </summary>
        public bool CanRoll
        {
            get
            {
                if (!InUse || _unit.IsLost || !Settled || Struck != Hit.None || _slideUntil > 0f)
                {
                    return false;
                }
                EmergencyDriver driver = _unit.Driver;
                return driver.Active && !driver.OutOfLane && driver.HoldAt < 0 && driver.Room == float.MaxValue
                       && driver.Speed >= RollLeast && driver.Speed <= SettledPace + 0.5f;
            }
        }

        /// <summary>
        /// It has hit something, but no scene of its own is to come of that (it had just run somebody down, and that
        /// scene will take the car): it stays where it stopped, held still, and what it hit is forgotten.
        /// </summary>
        public void Stay()
        {
            Struck = Hit.None;
            Other = null;
            if (InUse && !_unit.IsLost)
            {
                _unit.Park();
            }
        }

        // ---------------------------------------------------------------- letting go

        /// <summary>Passes the car on as it stands to an accident scene (ResponseUnit.HandOver). Null if it was no longer ours.</summary>
        public VehicleComponent HandOver()
        {
            VehicleComponent car = _unit != null ? _unit.Car : null;
            Unwatch(car);
            VehicleComponent given = _unit != null ? _unit.HandOver() : null;
            Forget();
            return given;
        }

        /// <summary>
        /// Back to traffic as an ordinary car, or (remove) off the road. Safe in any state. Handed back, it is pointed
        /// at the road point it was driving to on its own route, not merely at the one nearest it: standing at a red
        /// light that is the stop line itself, whose flag the traffic system reads at once and holds the car by, so
        /// a car given back at a light waits for it like any other.
        /// </summary>
        /// <param name="rolling">It keeps the speed it has (ask CanRoll first).</param>
        public void Release(bool resumeTraffic, bool remove, bool rolling = false)
        {
            if (_unit == null)
            {
                return;
            }
            VehicleComponent car = _unit.Car;
            Waypoint next = null;
            if (_unit.InUse && !remove && resumeTraffic && car != null && !_unit.IsLost)
            {
                EmergencyDriver driver = _unit.Driver;
                int index = driver.HoldAt >= 0 ? driver.HoldAt : driver.NextPoint;
                next = index >= 0 && index < _route.Count ? _route[index] : null;
            }
            Unwatch(car);
            _unit.Release(resumeTraffic, remove, false, rolling);
            if (next != null && car != null && car.gameObject.activeInHierarchy && TrafficTools.Ready
                && !TrafficManager.Instance.IsVehicleIgnored(car.GetIndex()))
            {
                TrafficManager.Instance.SetVehicleTargetWaypoint(car, next);
            }
            Forget();
        }

        private static void Unwatch(VehicleComponent car)
        {
            CarStrikes strikes = CarStrikes.Instance;
            if (strikes != null && car != null && strikes.Watching(car))
            {
                strikes.Unwatch(car);
            }
        }

        private void Forget()
        {
            for (int i = 0; i < _near.Length; i++)
            {
                _near[i] = null;
            }
            _nearCount = 0;
            _route.Clear();
            Struck = Hit.None;
            Other = null;
            _slideUntil = 0f;
            enabled = false;
        }
    }
}
