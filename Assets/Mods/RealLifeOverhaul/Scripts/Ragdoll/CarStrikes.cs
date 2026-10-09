using System;
using System.Collections.Generic;
using BAModAPI;
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
    /// </summary>
    public class CarStrikes : MonoBehaviour
    {
        private const float MinSpeed = 1.4f;        // m/s, about 5 km/h: slower than this the car only nudges
        private const float PersonRadius = 0.3f;
        private const float LookupInterval = 0.5f;  // how often it asks which car the player is driving

        private static readonly List<Collider> ColliderScratch = new List<Collider>();

        public static IModLogger Logger;

        private CarController _car;
        private Rigidbody _body;
        private Vector3 _centre;        // of the car's body, in the car's own space
        private Vector3 _half;          // half its width, height and length
        private PedestrianSpawner _spawner;
        private float _nextLookup;
        private bool _driving;
        private bool _skip;

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
            if (!_driving || _body == null)
            {
                return;
            }
            Vector3 velocity = _body.velocity;
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

            float speed = Mathf.Sqrt(sqrSpeed);
            Vector3 origin = _body.position;
            Quaternion toCar = Quaternion.Inverse(_body.rotation);
            float along = (toCar * velocity).z;
            float reach = speed * Time.fixedDeltaTime * 2f;

            // The box people must be inside, in the car's own space. Only the end the car is moving
            // towards counts: someone it has just passed, or is backing away from, is not hit.
            float side = _half.x + PersonRadius;
            float front = _centre.z + _half.z + PersonRadius;
            float back = _centre.z - _half.z - PersonRadius;
            if (along >= 0f)
            {
                front += reach;
                back = _centre.z;
            }
            else
            {
                back -= reach;
                front = _centre.z;
            }

            List<Pedestrian> walkers = _spawner != null ? _spawner.activePedestrians : null;
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
                    if (walker == null || !walker.enabled)
                    {
                        continue;   // gone, or already in our hands
                    }
                    Vector3 local = toCar * (walker.transform.position - origin);
                    if (local.x > -side && local.x < side && local.z > back && local.z < front && local.y > -1.5f && local.y < 2f)
                    {
                        Count(incidents.StrikePedestrian(walker, velocity, speed, _body));
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
                    Count(incidents.StrikeFigure(figure, velocity, speed, _body));
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
                    Count(incidents.StrikeActor(actor, velocity, speed, _body));
                }
            }
            if (_swept > 0)
            {
                Report(speed, now);
            }
        }

        // ---------------------------------------------------------------- one line per batch

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

        // What one look found: said whenever somebody went down; a look that only refused (the car is still
        // over someone who cannot be thrown) is said at most every few seconds.
        private void Report(float speed, float now)
        {
            int refused = _seated + _noRagdoll + _failed;
            if (_down > 0 || now >= _nextRefusalLine)
            {
                if (refused > 0)
                {
                    _nextRefusalLine = now + 4f;
                }
                Logger?.Info($"Car strike at {speed * 3.6f:F0} km/h: {_swept} in the sweep, {_down} thrown down, {refused} refused"
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
                    Measure(car);
                }
            }
            if (_spawner == null && _driving)
            {
                var city = InstanceBehavior<CityManager>.Instance;
                _spawner = city != null ? city.pedestrianSpawner : null;
            }
        }

        // The car's size from its colliders, once per car. Anything that does not look like a car gets a saloon's.
        private void Measure(CarController car)
        {
            Transform root = _body.transform;
            var bounds = new Bounds();
            bool any = false;
            car.GetComponentsInChildren(false, ColliderScratch);
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
                _centre = bounds.center;
                _half = bounds.extents;
            }
            else
            {
                _centre = new Vector3(0f, 0.8f, 0f);
                _half = new Vector3(0.95f, 0.8f, 2.2f);
            }
            Logger?.Info($"Pedestrian collisions: {car.name} measured {_half.x * 2f:F1} m wide, {_half.z * 2f:F1} m long.");
        }
    }
}
