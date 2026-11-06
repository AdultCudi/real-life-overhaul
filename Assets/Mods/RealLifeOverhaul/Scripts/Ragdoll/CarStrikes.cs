using System;
using System.Collections.Generic;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using Streets.Pedestrians;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Notices the player's vehicle, and up to eight watched traffic cars, running into people. The game lets cars pass
    /// through pedestrians and people standing about have no collider, so this cannot wait for a collision: every other
    /// physics step each vehicle's box, stretched by what it covers before the next look, is compared with where people
    /// are. What a hit does is <see cref="PedestrianIncidents"/>' business; a two-wheeler only knocks people down, and a
    /// traffic car under 8 km/h only bumps them (<see cref="HornReactions.Shout"/>).
    /// Cost: a few dozen subtractions per vehicle per look, no physics query and no allocation; parked or on foot with
    /// nothing watched, a lookup twice a second.
    /// </summary>
    public class CarStrikes : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ragdoll", "CarStrikes", typeof(CarStrikes));

        private const float MinSpeed = 1.4f;        // m/s, about 5 km/h: slower than this the car only nudges
        private const float PersonRadius = 0.3f;    // metres
        private const float LookupInterval = 0.5f;  // seconds between asking which car the player is driving
        private const int WatchMost = 8;            // traffic cars watched at once
        private const float BumpEvery = 3f;         // seconds between two shouts at one car that keeps nudging somebody

        private static readonly List<Collider> ColliderScratch = new List<Collider>();

        public static IModLogger Logger;
        public static CarStrikes Instance { get; private set; }

        private VehicleController _car;
        private bool _twoWheeler;       // the game's scooter, or a bicycle or motorbike on it
        private int _measureAgain;      // looks left in which a two-wheeler's box is measured again
        private const int MeasureLooks = 4;   // lookups (two seconds): the mod dresses a bike within a second and seats its rider among its children
        private Rigidbody _body;
        private Vector3 _centre;        // of the car's body, in the car's own space
        private Vector3 _half;          // half its width, height and length
        private PedestrianSpawner _spawner;
        private float _nextLookup;
        private bool _driving;
        private bool _skip;

        private sealed class Watched
        {
            public VehicleComponent Car;    // null: free
            public Rigidbody Body;
            public Vector3 Centre;
            public Vector3 Half;
            public bool Kept;               // named by Watch: watched until Unwatch
            public float Until;             // named by Glance: watched until then (Time.time)
            public float NextBump;
        }

        private readonly Watched[] _watched = new Watched[WatchMost];
        private int _watching;              // places in use

        // Each pooled traffic car's box by its index in the pool, measured when first asked for.
        private Vector3[] _boxCentre = new Vector3[0];
        private Vector3[] _boxHalf = new Vector3[0];
        private bool[] _boxKnown = new bool[0];
        private object _boxPool;            // the traffic system the boxes were measured in

        private void Awake()
        {
            Instance = this;
            for (int i = 0; i < _watched.Length; i++)
            {
                _watched[i] = new Watched();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotFixedUpdate = HotSpots.Slot("Ragdoll: CarStrikes.FixedUpdate");

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
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnFixedUpdate()
        {
            _skip = !_skip;
            if (_skip || !Settings.Ragdolls)
            {
                return;
            }
            try
            {
                Sense();
            }
            catch (Exception e)
            {
                // Nobody is run over from now on: better than a fault every physics step.
                Logger?.Error(e);
                Logger?.Warn("Pedestrian collisions: switched off for this session after the error above.");
                Forget();
                enabled = false;
            }
        }

        private void Sense()
        {
            float now = Time.time;
            if (now >= _nextLookup)
            {
                _nextLookup = now + LookupInterval;
                Lookup();
            }
            if (_driving && _body != null)
            {
                Sweep(_body, _centre, _half, null, null, now);
            }
            for (int i = 0; i < _watched.Length && _watching > 0; i++)
            {
                Watched slot = _watched[i];
                if ((object)slot.Car == null)
                {
                    continue;
                }
                // Taken off the road, handed out again by the pool, or its glance has run out.
                if (slot.Car == null || slot.Body == null || !slot.Car.gameObject.activeInHierarchy || (!slot.Kept && now > slot.Until))
                {
                    Free(slot);
                    continue;
                }
                Sweep(slot.Body, slot.Centre, slot.Half, slot.Car, slot, now);
            }
        }

        /// <summary>Watches a traffic car until Unwatch or until it leaves the road; not if all eight places are kept by Watch.</summary>
        public void Watch(VehicleComponent car)
        {
            Watched slot = Place(car);
            if (slot != null)
            {
                slot.Kept = true;
            }
        }

        /// <summary>Watches a traffic car about to reach somebody until <paramref name="until"/> (Time.time); it needs no Unwatch.</summary>
        public void Glance(VehicleComponent car, float until)
        {
            Watched slot = Place(car);
            if (slot != null && until > slot.Until)
            {
                slot.Until = until;
            }
        }

        // The car's place among the watched: the one it has, or a free one, or one whose glance is the oldest.
        private Watched Place(VehicleComponent car)
        {
            if (car == null || car.rb == null || !enabled)
            {
                return null;
            }
            Watched free = null;
            Watched oldest = null;
            for (int i = 0; i < _watched.Length; i++)
            {
                Watched slot = _watched[i];
                if ((object)slot.Car == car)
                {
                    return slot;
                }
                if ((object)slot.Car == null)
                {
                    free = free ?? slot;
                }
                else if (!slot.Kept && (oldest == null || slot.Until < oldest.Until))
                {
                    oldest = slot;
                }
            }
            Watched take = free ?? oldest;
            if (take == null)
            {
                return null;
            }
            if ((object)take.Car == null)
            {
                _watching++;
            }
            take.Car = car;
            take.Body = car.rb;
            take.Kept = false;
            take.Until = 0f;
            take.NextBump = 0f;
            Box(car, out take.Centre, out take.Half);
            return take;
        }

        /// <summary>Stops watching a car named by Watch; a glance at it runs out by itself.</summary>
        public void Unwatch(VehicleComponent car)
        {
            for (int i = 0; i < _watched.Length; i++)
            {
                Watched slot = _watched[i];
                if ((object)slot.Car != null && (object)slot.Car == car)
                {
                    slot.Kept = false;
                    if (Time.time > slot.Until)
                    {
                        Free(slot);
                    }
                }
            }
        }

        public bool Watching(VehicleComponent car)
        {
            for (int i = 0; i < _watched.Length && car != null; i++)
            {
                if ((object)_watched[i].Car == car)
                {
                    return true;
                }
            }
            return false;
        }

        public int WatchedCount => _watching;

        private void Free(Watched slot)
        {
            slot.Car = null;
            slot.Body = null;
            slot.Kept = false;
            slot.Until = 0f;
            _watching = Mathf.Max(0, _watching - 1);
        }

        private void Forget()
        {
            for (int i = 0; i < _watched.Length; i++)
            {
                _watched[i].Car = null;
                _watched[i].Body = null;
                _watched[i].Kept = false;
            }
            _watching = 0;
        }

        /// <summary>
        /// A pooled traffic car's box in its own space: its middle and half its width, height and length. Measured once per
        /// place in the pool; two array reads after that.
        /// </summary>
        public void Box(VehicleComponent car, out Vector3 centre, out Vector3 half)
        {
            int index = car.GetIndex();
            object pool = TrafficManager.HasInstance ? TrafficManager.Instance : null;
            if (!ReferenceEquals(pool, _boxPool) || index >= _boxKnown.Length)
            {
                // Another city's traffic, or the first car: measured afresh.
                int size = Mathf.Max(index + 1, pool != null ? Manager.GetVehicleList().Count : 0);
                _boxCentre = new Vector3[size];
                _boxHalf = new Vector3[size];
                _boxKnown = new bool[size];
                _boxPool = pool;
            }
            if (index < 0)
            {
                Measure(car, car.rb, out centre, out half, false);
                return;
            }
            if (!_boxKnown[index])
            {
                Measure(car, car.rb, out _boxCentre[index], out _boxHalf[index], false);
                _boxKnown[index] = true;
            }
            centre = _boxCentre[index];
            half = _boxHalf[index];
        }

        /// <summary>The car the player is driving and its box in its own space. False on foot, parked up, or on two wheels.</summary>
        public bool PlayerCar(out Rigidbody body, out Vector3 centre, out Vector3 half)
        {
            body = _body;
            centre = _centre;
            half = _half;
            return _driving && _body != null && !_twoWheeler;
        }

        // One look for one vehicle. traffic is null for the player's vehicle; otherwise the traffic car and its place.
        private void Sweep(Rigidbody body, Vector3 centre, Vector3 half, VehicleComponent traffic, Watched watched, float now)
        {
            Vector3 velocity = body.velocity;
            float sqrSpeed = velocity.x * velocity.x + velocity.z * velocity.z;
            bool player = (object)traffic == null;
            float least = player ? MinSpeed : SolidRules.BumpFrom;   // a traffic car bumps people from a slower pace
            if (sqrSpeed < least * least)
            {
                return;
            }
            PedestrianIncidents incidents = PedestrianIncidents.Instance;
            if (incidents == null)
            {
                return;
            }

            float speed = Mathf.Sqrt(sqrSpeed);
            bool bump = !player && SolidRules.Touch(speed) == SolidRules.Contact.Bump;
            // On two wheels the strike is reckoned at no more than knocks people off their feet.
            float hit = player && _twoWheeler && speed > PedestrianIncidents.KnockedBelow ? PedestrianIncidents.KnockedBelow : speed;
            Vector3 thrown = hit < speed ? velocity * (hit / speed) : velocity;
            _struckBefore = _down;
            Vector3 origin = body.position;
            Quaternion toCar = Quaternion.Inverse(body.rotation);
            float along = (toCar * velocity).z;
            float reach = speed * Time.fixedDeltaTime * 2f;

            // The box in the car's own space. Only the end it is moving towards counts: someone it has passed, or is
            // backing away from, is not hit.
            float side = half.x + PersonRadius;
            float front = centre.z + half.z + PersonRadius;
            float back = centre.z - half.z - PersonRadius;
            if (along >= 0f)
            {
                front += reach;
                back = centre.z;
            }
            else
            {
                back -= reach;
                front = centre.z;
            }

            List<Pedestrian> walkers = _spawner != null ? _spawner.activePedestrians : null;
            if (walkers == null && !player)
            {
                // The player may not have driven this session.
                var city = InstanceBehavior<CityManager>.Instance;
                _spawner = city != null ? city.pedestrianSpawner : null;
                walkers = _spawner != null ? _spawner.activePedestrians : null;
            }
            if (walkers != null)
            {
                // Backwards: a hit that fails can end with the pedestrian leaving this list.
                for (int i = walkers.Count - 1; i >= 0; i--)
                {
                    if (i >= walkers.Count)
                    {
                        continue;
                    }
                    Pedestrian walker = walkers[i];
                    // Switched off: gone, or already ours; except someone the mod sent across in front of a watched car.
                    if (walker == null || (!walker.enabled && (player || !Jaywalker.Holds(walker))))
                    {
                        continue;
                    }
                    if (Inside(toCar * (walker.transform.position - origin), side, back, front))
                    {
                        if (bump)
                        {
                            Bump(walker, null, traffic, watched, origin, speed, now);
                            continue;
                        }
                        Count(player ? incidents.StrikePedestrian(walker, thrown, hit, body) : incidents.StruckByTraffic(walker, velocity, speed, traffic));
                    }
                }
            }

            // The people who stand about (groups at a hangout spot, performers): everyone inside is struck, not only the first.
            List<BaseHuman> standing = StreetPeople.Standing(Logger);
            for (int i = standing.Count - 1; i >= 0; i--)
            {
                BaseHuman figure = standing[i];
                if (figure == null || !figure.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (Inside(toCar * (figure.transform.position - origin), side, back, front))
                {
                    if (bump)
                    {
                        Bump(null, figure, traffic, watched, origin, speed, now);
                        continue;
                    }
                    Count(player ? incidents.StrikeFigure(figure, thrown, hit, body) : incidents.StruckByTraffic(figure, velocity, speed, traffic));
                }
            }

            ActorPool pool = ActorPool.Instance;
            for (int i = pool != null ? pool.ActorCount - 1 : -1; i >= 0; i--)
            {
                StreetActor actor = pool.ActorAt(i);
                if (!actor.InUse || !actor.IsValid)
                {
                    continue;
                }
                if (Inside(toCar * (actor.Position - origin), side, back, front) && !actor.Limp && !bump)
                {
                    Count(player ? incidents.StrikeActor(actor, thrown, hit, body) : incidents.StruckByTraffic(actor, velocity, speed, traffic));
                }
            }
            if (!player && _down > _struckBefore)
            {
                // The one path for every traffic car that has hit somebody: its driver stops or runs.
                RecklessDrivers drivers = RecklessDrivers.Instance;
                if (drivers != null)
                {
                    drivers.Struck(traffic, speed);
                }
            }
            if (_swept > 0)
            {
                Report(speed, now, player);
            }
        }

        private int _struckBefore;

        // Inside a vehicle's box, given in its own space.
        private static bool Inside(Vector3 local, float side, float back, float front)
        {
            return local.x > -side && local.x < side && local.z > back && local.z < front && local.y > -1.5f && local.y < 2f;
        }

        // A traffic car against somebody at walking pace: they turn on it and shout, once every few seconds per car.
        private void Bump(Pedestrian walker, BaseHuman figure, VehicleComponent car, Watched watched, Vector3 origin, float speed, float now)
        {
            if (watched == null || now < watched.NextBump)
            {
                return;
            }
            watched.NextBump = now + BumpEvery;
            HornReactions reactions = HornReactions.Instance;
            bool shouted = reactions != null && reactions.Shout(walker, figure, origin);
            Logger?.Info($"Traffic check (bump): '{TrafficTools.Name(car)}' touched somebody at {speed * 3.6f:F0} km/h, under {SolidRules.StrikeFrom * 3.6f:F0}: "
                         + (shouted ? "they turn on it and shout, nobody falls: PASS" : "nobody falls (they could not be made to react): PASS"));
        }

        private int _swept, _down, _seated, _noRagdoll, _failed;
        private float _nextRefusalLine;

        private void Count(PedestrianIncidents.Strike result)
        {
            switch (result)
            {
                case PedestrianIncidents.Strike.Down: _swept++; _down++; break;
                case PedestrianIncidents.Strike.Seated: _swept++; _seated++; break;
                case PedestrianIncidents.Strike.NoRagdoll: _swept++; _noRagdoll++; break;
                case PedestrianIncidents.Strike.Failed: _swept++; _failed++; break;
            }
        }

        // What one look found: said whenever somebody went down, and a look that only refused at most every few seconds.
        private void Report(float speed, float now, bool player)
        {
            int refused = _seated + _noRagdoll + _failed;
            if (_down > 0 || now >= _nextRefusalLine)
            {
                if (refused > 0)
                {
                    _nextRefusalLine = now + 4f;
                }
                Logger?.Info($"Car strike{(player ? string.Empty : " by a traffic car")} at {speed * 3.6f:F0} km/h: {_swept} in the sweep, {_down} thrown down, {refused} refused"
                             + (refused > 0 ? $" (seated {_seated}, no ragdoll free or not drawn {_noRagdoll}, could not be taken {_failed})" : string.Empty) + ".");
            }
            _swept = _down = _seated = _noRagdoll = _failed = 0;
        }

        // Twice a second: which vehicle the player is driving, and where the game keeps its pedestrians.
        private void Lookup()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            VehicleController selected = gm != null ? gm.selectedVehicle : null;
            bool twoWheeler = selected is ScooterController;
            VehicleController car = selected is CarController || twoWheeler ? selected : null;
            _driving = car != null && PlayerHelper.IsUsingVehicle;
            if (car != _car)
            {
                _car = car;
                _twoWheeler = twoWheeler;
                _measureAgain = MeasureLooks;
                _body = car != null ? car.GetComponent<Rigidbody>() : null;
                if (_body != null && !twoWheeler)
                {
                    Measure(car, _body, out _centre, out _half, true);
                }
            }
            if (_twoWheeler && _driving && _body != null && _measureAgain > 0)
            {
                _measureAgain--;
                MeasureTwoWheeler(_car, _body, out _centre, out _half);
            }
            if (_spawner == null && _driving)
            {
                var city = InstanceBehavior<CityManager>.Instance;
                _spawner = city != null ? city.pedestrianSpawner : null;
            }
        }

        // A two-wheeler's size from its own body's solid boxes (not the rider's, who sits among its children), or a narrow
        // default. A walk of the bike and its rider, on getting on and the four lookups after.
        private static void MeasureTwoWheeler(Component vehicle, Rigidbody body, out Vector3 centre, out Vector3 half)
        {
            Transform root = body.transform;
            var bounds = new Bounds();
            bool any = false;
            vehicle.GetComponentsInChildren(false, ColliderScratch);
            for (int i = 0; i < ColliderScratch.Count; i++)
            {
                if (!(ColliderScratch[i] is BoxCollider box) || box.isTrigger || !box.enabled || box.attachedRigidbody != body)
                {
                    continue;
                }
                Encapsulate(ref bounds, ref any, root, box.transform, box.center - box.size * 0.5f, box.center + box.size * 0.5f);
            }
            ColliderScratch.Clear();
            if (any && bounds.size.z > 0.8f && bounds.size.x > 0.2f)
            {
                centre = bounds.center;
                half = bounds.extents;
            }
            else
            {
                centre = new Vector3(0f, 0.7f, 0.05f);
                half = new Vector3(0.3f, 0.7f, 0.95f);
            }
        }

        // A vehicle's size from its colliders, once per vehicle; anything that does not look like a car gets a saloon's.
        // say: log it (the player's car; traffic cars are measured quietly).
        private static void Measure(Component vehicle, Rigidbody body, out Vector3 centre, out Vector3 half, bool say)
        {
            Transform root = body.transform;
            var bounds = new Bounds();
            bool any = false;
            vehicle.GetComponentsInChildren(false, ColliderScratch);
            for (int i = 0; i < ColliderScratch.Count; i++)
            {
                Collider collider = ColliderScratch[i];
                Bounds own;
                if (collider.isTrigger)
                {
                    continue;
                }
                if (collider is BoxCollider box)
                {
                    own = new Bounds(box.center, box.size);
                }
                else if (collider is MeshCollider mesh && mesh.sharedMesh != null)
                {
                    own = mesh.sharedMesh.bounds;
                }
                else
                {
                    continue;   // wheels and odd shapes
                }
                Encapsulate(ref bounds, ref any, root, collider.transform, own.min, own.max);
            }
            ColliderScratch.Clear();

            Vector3 size = bounds.size;
            if (any && size.x > 1.2f && size.x < 3.2f && size.z > 2.2f && size.z < 9f)
            {
                centre = bounds.center;
                half = bounds.extents;
            }
            else
            {
                centre = new Vector3(0f, 0.8f, 0f);
                half = new Vector3(0.95f, 0.8f, 2.2f);
            }
            if (say)
            {
                Logger?.Info(FormattableString.Invariant($"Pedestrian collisions: {vehicle.name} measured {half.x * 2f:F1} m wide, {half.z * 2f:F1} m long."));
            }
        }

        // Grows bounds in root's space by the eight corners of a box given in from's space.
        private static void Encapsulate(ref Bounds bounds, ref bool any, Transform root, Transform from, Vector3 min, Vector3 max)
        {
            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3((corner & 1) == 0 ? min.x : max.x, (corner & 2) == 0 ? min.y : max.y, (corner & 4) == 0 ? min.z : max.z);
                Vector3 local = root.InverseTransformPoint(from.TransformPoint(point));
                if (any)
                {
                    bounds.Encapsulate(local);
                }
                else
                {
                    bounds = new Bounds(local, Vector3.zero);
                    any = true;
                }
            }
        }
    }
}
