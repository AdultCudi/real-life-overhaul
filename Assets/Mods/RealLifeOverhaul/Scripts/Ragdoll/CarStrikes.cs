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
    /// Notices the player's car running into people. The game fades pedestrians out beside
    /// vehicles and lets cars pass through them, and people standing still have no collider at
    /// all, so this cannot wait for a collision: while the player is driving faster than
    /// walking pace, every other physics step it compares the positions of the pedestrians the
    /// game has out (and the mod's own people) with the box of the car, stretched ahead by the
    /// distance the car covers before the next look. That is a few dozen subtractions, no
    /// physics query and no allocation, and none of it runs while parked or on foot.
    /// What happens to whoever is hit is PedestrianIncidents' business.
    /// One traffic car can be watched in exactly the same way (Watch): the one reckless driver
    /// of Traffic/Reckless, or the car somebody has stepped out in front of. The game lets its
    /// own cars pass through people just as it does the player's, so for that one car the same
    /// look is taken with its box, and whoever is inside it is reported to PedestrianIncidents
    /// as hit by traffic, at the car's real speed. The same few dozen subtractions again, and
    /// only while a car is being watched.
    /// </summary>
    public class CarStrikes : MonoBehaviour
    {
        private const float MinSpeed = 1.4f;        // m/s, about 5 km/h: slower than this the car only nudges
        private const float PersonRadius = 0.3f;
        private const float LookupInterval = 0.5f;  // how often it asks which car the player is driving

        private static readonly List<Collider> ColliderScratch = new List<Collider>();

        public static IModLogger Logger;
        public static CarStrikes Instance { get; private set; }

        private CarController _car;
        private Rigidbody _body;
        private Vector3 _centre;        // of the car's body, in the car's own space
        private Vector3 _half;          // half its width, height and length
        private PedestrianSpawner _spawner;
        private float _nextLookup;
        private bool _driving;
        private bool _skip;

        // The one traffic car watched as well, if any.
        private VehicleComponent _watched;
        private Rigidbody _watchedBody;
        private Vector3 _watchedCentre;
        private Vector3 _watchedHalf;
        private int _watchedDown;       // people it has thrown down since they were last asked for

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void FixedUpdate()
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
                // Without this nobody is run over; better that than a fault every physics step.
                Logger?.Error(e);
                Logger?.Warn("Pedestrian collisions: switched off for this session after the error above.");
                _watched = null;
                _watchedBody = null;
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
                Sweep(_body, _centre, _half, null, now);
            }
            if ((object)_watched != null)
            {
                // (Taken off the road, or handed out again by the pool, it is nobody's to watch.)
                if (_watched == null || _watchedBody == null || !_watched.gameObject.activeInHierarchy)
                {
                    _watched = null;
                    _watchedBody = null;
                }
                else
                {
                    Sweep(_watchedBody, _watchedCentre, _watchedHalf, _watched, now);
                }
            }
        }

        // ---------------------------------------------------------------- a traffic car watched as well

        /// <summary>
        /// Watches one traffic car as the player's is watched, until Unwatch or until it leaves the road. Its
        /// size is measured once, here. One car at a time: a second call replaces the first.
        /// </summary>
        public void Watch(VehicleComponent car)
        {
            if (car == null || car.rb == null)
            {
                Unwatch();
                return;
            }
            _watched = car;
            _watchedBody = car.rb;
            _watchedDown = 0;
            Measure(car, _watchedBody, out _watchedCentre, out _watchedHalf);
        }

        /// <summary>Stops watching (that car, or whichever it is).</summary>
        public void Unwatch(VehicleComponent car = null)
        {
            if (car == null || car == _watched)
            {
                _watched = null;
                _watchedBody = null;
            }
        }

        public bool Watching(VehicleComponent car) => car != null && car == _watched;

        /// <summary>How many people the watched car has thrown down since this was last asked.</summary>
        public int TakeWatchedDown()
        {
            int down = _watchedDown;
            _watchedDown = 0;
            return down;
        }

        /// <summary>The box of the watched car in its own space (as measured when it was named). False if none is watched.</summary>
        public bool WatchedBox(out Vector3 centre, out Vector3 half)
        {
            centre = _watchedCentre;
            half = _watchedHalf;
            return (object)_watched != null;
        }

        /// <summary>The car the player is driving and its box in its own space. False on foot or parked up.</summary>
        public bool PlayerCar(out Rigidbody body, out Vector3 centre, out Vector3 half)
        {
            body = _body;
            centre = _centre;
            half = _half;
            return _driving && _body != null;
        }

        // ---------------------------------------------------------------- the look

        // One look for one vehicle: everybody inside its box, stretched ahead by what it covers before the next look.
        // traffic is null for the player's car, and the traffic car itself otherwise.
        private void Sweep(Rigidbody body, Vector3 centre, Vector3 half, VehicleComponent traffic, float now)
        {
            Vector3 velocity = body.velocity;
            float sqrSpeed = velocity.x * velocity.x + velocity.z * velocity.z;
            if (sqrSpeed < MinSpeed * MinSpeed)
            {
                return;
            }
            PedestrianIncidents incidents = PedestrianIncidents.Instance;
            if (incidents == null)
            {
                return;
            }
            bool player = (object)traffic == null;

            float speed = Mathf.Sqrt(sqrSpeed);
            Vector3 origin = body.position;
            Quaternion toCar = Quaternion.Inverse(body.rotation);
            float along = (toCar * velocity).z;
            float reach = speed * Time.fixedDeltaTime * 2f;

            // The box people must be inside, in the car's own space. Only the end the car is moving
            // towards counts: someone it has just passed, or is backing away from, is not hit.
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
                // (The player may never have driven this session: the game's list is asked for here too.)
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
                    // Switched off: gone, or already in our hands. The one exception is somebody the mod itself has sent
                    // across the road in front of the watched car: that is exactly who it may hit.
                    if (walker == null || (!walker.enabled && (player || !Jaywalker.Holds(walker))))
                    {
                        continue;
                    }
                    Vector3 local = toCar * (walker.transform.position - origin);
                    if (local.x > -side && local.x < side && local.z > back && local.z < front && local.y > -1.5f && local.y < 2f)
                    {
                        Count(player ? incidents.StrikePedestrian(walker, velocity, speed, body) : incidents.StruckByTraffic(walker, velocity, speed, traffic), player);
                    }
                }
            }

            // The people who stand about (groups at a hangout spot, performers): not in the walkers' list, and
            // without colliders. Everyone inside the sweep in this look is struck, not the first one found.
            List<BaseHuman> standing = StreetPeople.Standing(Logger);
            for (int i = standing.Count - 1; i >= 0; i--)
            {
                BaseHuman figure = standing[i];
                if (figure == null || !figure.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Vector3 local = toCar * (figure.transform.position - origin);
                if (local.x > -side && local.x < side && local.z > back && local.z < front && local.y > -1.5f && local.y < 2f)
                {
                    Count(player ? incidents.StrikeFigure(figure, velocity, speed, body) : incidents.StruckByTraffic(figure, velocity, speed, traffic), player);
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
                Vector3 local = toCar * (actor.Position - origin);
                if (local.x > -side && local.x < side && local.z > back && local.z < front && local.y > -1.5f && local.y < 2f
                    && !actor.Limp)
                {
                    Count(player ? incidents.StrikeActor(actor, velocity, speed, body) : incidents.StruckByTraffic(actor, velocity, speed, traffic), player);
                }
            }
            if (_swept > 0)
            {
                Report(speed, now, player);
            }
        }

        // ---------------------------------------------------------------- one line per batch

        private int _swept, _down, _seated, _noRagdoll, _failed;
        private float _nextRefusalLine;

        private void Count(PedestrianIncidents.Strike result, bool player)
        {
            switch (result)
            {
                case PedestrianIncidents.Strike.Down: _swept++; _down++; break;
                case PedestrianIncidents.Strike.Seated: _swept++; _seated++; break;
                case PedestrianIncidents.Strike.NoRagdoll: _swept++; _noRagdoll++; break;
                case PedestrianIncidents.Strike.Failed: _swept++; _failed++; break;
            }
            if (!player && result == PedestrianIncidents.Strike.Down)
            {
                _watchedDown++;
            }
        }

        // What one look found: said whenever somebody went down; a look that only refused (the car is still
        // over someone who cannot be thrown) is said at most every few seconds.
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

        // Twice a second: which car, if any, is the player driving, and where does the game keep its pedestrians.
        private void Lookup()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            CarController car = gm != null ? gm.selectedVehicle as CarController : null;
            _driving = car != null && PlayerHelper.IsUsingVehicle;
            if (car != _car)
            {
                _car = car;
                _body = car != null ? car.GetComponent<Rigidbody>() : null;
                if (_body != null)
                {
                    Measure(car, _body, out _centre, out _half);
                }
            }
            if (_spawner == null && _driving)
            {
                var city = InstanceBehavior<CityManager>.Instance;
                _spawner = city != null ? city.pedestrianSpawner : null;
            }
        }

        // A vehicle's size from its colliders, once per vehicle. Anything that does not look like a car gets a saloon's.
        private static void Measure(Component vehicle, Rigidbody body, out Vector3 centre, out Vector3 half)
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
                Transform from = collider.transform;
                Vector3 min = own.min;
                Vector3 max = own.max;
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
            Logger?.Info($"Pedestrian collisions: {vehicle.name} measured {half.x * 2f:F1} m wide, {half.z * 2f:F1} m long.");
        }
    }
}
