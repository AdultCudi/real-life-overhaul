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
    /// Notices the player's car, or their bicycle, motorbike or scooter, running into people. The game fades pedestrians out beside
    /// vehicles and lets cars pass through them, and people standing still have no collider at
    /// all, so this cannot wait for a collision: while the player is driving faster than
    /// walking pace, every other physics step it compares the positions of the pedestrians the
    /// game has out (and the mod's own people) with the box of the car, stretched ahead by the
    /// distance the car covers before the next look. That is a few dozen subtractions, no
    /// physics query and no allocation, and none of it runs while parked or on foot.
    /// What happens to whoever is hit is PedestrianIncidents' business. A two-wheeler (the game's scooter, and the
    /// mod's bicycles and motorbikes on it) is looked at the same way with its own box, measured from the colliders on
    /// its own body when it is got on and in the four looks after (the mod dresses it within a second, and the rider sits
    /// among its children): whoever it runs into is knocked off their feet, never more (PedestrianIncidents.KnockedBelow), so that
    /// nobody is hurt or killed by one and the police have no part in it. It is not the player's car to the traffic
    /// that asks for that (PlayerCar).
    /// A few traffic cars can be watched in exactly the same way: the one reckless driver of
    /// Traffic/Reckless and the car somebody has stepped out in front of (Watch, until Unwatch),
    /// and the cars Traffic/Solid finds within a second of somebody in their way (Glance, which
    /// runs out by itself). Eight at the most, never the whole traffic. The game lets its own
    /// cars pass through people just as it does the player's, so for each of those few the same
    /// look is taken with its box. Whoever is inside it at 8 km/h or more is reported to
    /// PedestrianIncidents as hit by traffic, at the car's real speed, and the reckless drivers'
    /// director is told which car did it (RecklessDrivers.Struck: the one path for every car that
    /// has hit somebody). Slower than that it is a bump: they turn on the car and shout
    /// (HornReactions.Shout) and nobody falls. The same few dozen subtractions for each watched
    /// car, and only while one is being watched.
    /// </summary>
    public class CarStrikes : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ragdoll", "CarStrikes", typeof(CarStrikes));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const float MinSpeed = 1.4f;        // m/s, about 5 km/h: slower than this the car only nudges
        private const float PersonRadius = 0.3f;
        private const float LookupInterval = 0.5f;  // how often it asks which car the player is driving
        private const int WatchMost = 8;            // traffic cars watched at once
        private const float BumpEvery = 3f;         // seconds between two shouts at one car that keeps nudging somebody

        private static readonly List<Collider> ColliderScratch = new List<Collider>();

        public static IModLogger Logger;
        public static CarStrikes Instance { get; private set; }

        private VehicleController _car;
        private bool _twoWheeler;       // the player's vehicle is the game's scooter (or a bicycle or motorbike on it)
        private int _measureAgain;      // looks left in which a two-wheeler's box is measured again
        private const int MeasureLooks = 4;   // looks (two seconds): the bike is dressed within a second of being spawned, and mounted the frame it is chosen
        private Rigidbody _body;
        private Vector3 _centre;        // of the car's body, in the car's own space
        private Vector3 _half;          // half its width, height and length
        private PedestrianSpawner _spawner;
        private float _nextLookup;
        private bool _driving;
        private bool _skip;

        // The few traffic cars watched as well.
        private sealed class Watched
        {
            public VehicleComponent Car;    // null: free
            public Rigidbody Body;
            public Vector3 Centre;
            public Vector3 Half;
            public bool Kept;               // named by Watch: watched until Unwatch
            public float Until;             // named by Glance: watched until then
            public float NextBump;
        }

        private readonly Watched[] _watched = new Watched[WatchMost];
        private int _watching;              // how many of them are in use

        // Each pooled traffic car's box, measured the first time it is asked for (by its index in the pool).
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
                // (Taken off the road, handed out again by the pool, or its glance has run out: nobody's to watch.)
                if (slot.Car == null || slot.Body == null || !slot.Car.gameObject.activeInHierarchy || (!slot.Kept && now > slot.Until))
                {
                    Free(slot);
                    continue;
                }
                Sweep(slot.Body, slot.Centre, slot.Half, slot.Car, slot, now);
            }
        }

        // ---------------------------------------------------------------- traffic cars watched as well

        /// <summary>
        /// Watches a traffic car as the player's is watched, until Unwatch or until it leaves the road. If all
        /// eight places are taken by cars that were named this way, it is not watched (that has never been more
        /// than two: the reckless car and the car somebody stepped out in front of).
        /// </summary>
        public void Watch(VehicleComponent car)
        {
            Watched slot = Place(car);
            if (slot != null)
            {
                slot.Kept = true;
            }
        }

        /// <summary>
        /// Watches a traffic car for a moment: until <paramref name="until"/> (Time.time), unless it is named again
        /// before then. For the cars that are about to reach somebody; they need no Unwatch.
        /// </summary>
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

        /// <summary>Stops watching a car named by Watch (a glance at it, if there is one, runs out by itself).</summary>
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

        /// <summary>How many traffic cars are being watched now.</summary>
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
        /// A pooled traffic car's box in its own space: the middle of its body and half its width, height and
        /// length. Measured from its colliders the first time it is asked for and remembered by the car's place in
        /// the pool (the pool hands the same cars out again and again), so asking costs two array reads after that.
        /// </summary>
        public void Box(VehicleComponent car, out Vector3 centre, out Vector3 half)
        {
            int index = car.GetIndex();
            object pool = TrafficManager.HasInstance ? TrafficManager.Instance : null;
            if (!ReferenceEquals(pool, _boxPool) || index >= _boxKnown.Length)
            {
                // Another city's traffic, or the first car: the boxes are measured afresh.
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

        // ---------------------------------------------------------------- the look

        // One look for one vehicle: everybody inside its box, stretched ahead by what it covers before the next look.
        // traffic is null for the player's car, and the traffic car itself (with its place among the watched) otherwise.
        private void Sweep(Rigidbody body, Vector3 centre, Vector3 half, VehicleComponent traffic, Watched watched, float now)
        {
            Vector3 velocity = body.velocity;
            float sqrSpeed = velocity.x * velocity.x + velocity.z * velocity.z;
            bool player = (object)traffic == null;
            // (A traffic car is looked at from a slower pace than the player's: between that and 8 km/h it bumps people.)
            float least = player ? MinSpeed : SolidRules.BumpFrom;
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
            // On two wheels whoever is run into is knocked off their feet, never more: the strike is reckoned at no more than that speed.
            float hit = player && _twoWheeler && speed > PedestrianIncidents.KnockedBelow ? PedestrianIncidents.KnockedBelow : speed;
            Vector3 thrown = hit < speed ? velocity * (hit / speed) : velocity;
            _struckBefore = _down;
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
                        if (bump)
                        {
                            Bump(walker, null, traffic, watched, origin, speed, now);
                            continue;
                        }
                        Count(player ? incidents.StrikePedestrian(walker, thrown, hit, body) : incidents.StruckByTraffic(walker, velocity, speed, traffic), player);
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
                    if (bump)
                    {
                        Bump(null, figure, traffic, watched, origin, speed, now);
                        continue;
                    }
                    Count(player ? incidents.StrikeFigure(figure, thrown, hit, body) : incidents.StruckByTraffic(figure, velocity, speed, traffic), player);
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
                    && !actor.Limp && !bump)
                {
                    Count(player ? incidents.StrikeActor(actor, thrown, hit, body) : incidents.StruckByTraffic(actor, velocity, speed, traffic), player);
                }
            }
            if (!player && _down > _struckBefore)
            {
                // The one path for every traffic car that has hit somebody: its driver stops or runs (Traffic/Reckless).
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

        // A traffic car has come up against somebody at walking pace: they turn on it and shout, and nobody falls.
        // Said once every few seconds for a car that keeps nudging.
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
            VehicleController selected = gm != null ? gm.selectedVehicle : null;
            bool twoWheeler = selected is ScooterController;
            VehicleController car = selected is CarController || twoWheeler ? selected : null;
            _driving = car != null && PlayerHelper.IsUsingVehicle;
            if (car != _car)
            {
                _car = car;
                _twoWheeler = twoWheeler;
                _measureAgain = MeasureLooks;   // now, and after the bike is dressed and its rider seated
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

        // A two-wheeler's size: its own body's colliders that meet the world (not the rider's, who sits among its children,
        // nor a trigger), with a narrow default if none is found. On getting on and the four looks after: a walk of the bike and its rider.
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
                Transform from = box.transform;
                Vector3 min = box.center - box.size * 0.5f;
                Vector3 max = box.center + box.size * 0.5f;
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

        // A vehicle's size from its colliders, once per vehicle. Anything that does not look like a car gets a saloon's.
        // say: put it in the log (the player's car; a traffic car's is measured once for the whole session, quietly).
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
            if (say)
            {
                Logger?.Info(FormattableString.Invariant($"Pedestrian collisions: {vehicle.name} measured {half.x * 2f:F1} m wide, {half.z * 2f:F1} m long."));
            }
        }
    }
}
