using GleyTrafficSystem;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Where people can stand: beside a car's doors, behind it, and on stretches of pavement
    /// around a point. Every answer is a point on the navigation mesh, or "no". All of this is
    /// asked when something happens (a car parks, a scene starts), never on a timer.
    /// </summary>
    public static class StreetSpots
    {
        public const float Left = -1f;    // the driver's side
        public const float Right = 1f;

        private const float SampleRadius = 2.5f;
        private const int RingAttempts = 8;

        private const float GroundProbe = 2.5f;       // metres above a point the ground ray starts
        private const float GroundTolerance = 0.45f;  // a walkable point further than this from the ground is a ledge, not the street

        private static NavMeshQueryFilter _pavement;
        private static bool _pavementReady;
        private static int _groundMask;
        private static bool _groundReady;

        /// <summary>Pavements and paths the game's own pedestrians use, without the water people can swim in.</summary>
        public static NavMeshQueryFilter Pavement
        {
            get
            {
                if (!_pavementReady)
                {
                    _pavementReady = true;
                    _pavement = new NavMeshQueryFilter
                    {
                        agentTypeID = Extensions.NavMeshHelper.NpcAgentTypeId,
                        areaMask = NavMesh.AllAreas & ~Extensions.NavMeshHelper.SwimmingAreaMask
                    };
                }
                return _pavement;
            }
        }

        /// <summary>A point on the road surface beside a car's door. False if the car is gone or there is no ground there.</summary>
        /// <param name="side">Left (the driver's door) or Right.</param>
        /// <param name="clearance">Metres between the car's side and the person.</param>
        public static bool BesideCar(VehicleComponent car, float side, float clearance, out Vector3 spot)
        {
            if (car == null)
            {
                spot = default;
                return false;
            }
            Frame(car, out Vector3 centre, out Vector3 forward);
            return Beside(centre, forward, Mathf.Max(0.8f, car.colliderWidth * 0.5f), side, clearance, out spot);
        }

        /// <summary>The same for any vehicle known only by where it is and which way it points (the player's car).</summary>
        public static bool Beside(Vector3 centre, Vector3 forward, float halfWidth, float side, float clearance, out Vector3 spot)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.forward;
            }
            Vector3 right = Vector3.Cross(Vector3.up, forward.normalized);
            return OnRoad(centre + right * (side * (halfWidth + clearance)), SampleRadius, out spot);
        }

        /// <summary>A point on the road just behind a vehicle: where an ambulance crew gets out.</summary>
        public static bool BehindCar(VehicleComponent car, float clearance, out Vector3 spot)
        {
            if (car == null)
            {
                spot = default;
                return false;
            }
            Frame(car, out Vector3 centre, out Vector3 forward);
            return OnRoad(centre - forward * (Mathf.Max(1.5f, car.length * 0.5f) + clearance), SampleRadius, out spot);
        }

        /// <summary>
        /// Where somebody stands to get into a rear door in the clips made for it (job_car_enter, cuffed_car_enter and
        /// their _l twins: CityAssets/README.md), on one side of a traffic vehicle: on the line square to the car
        /// through the seat, the feet 0.75 m out from the middle of the seat, facing the car. The seat is an estimate,
        /// the one Jobs/JobSpots.AtDoor makes for the player's car (the game's vehicles carry no seat or door
        /// positions): a little behind the middle, 0.45 of the half width out. False if that is not ground anyone
        /// can stand on.
        /// </summary>
        /// <param name="inwards">The way they face: square to the car, towards it.</param>
        public static bool AtRearDoor(VehicleComponent car, float side, out Vector3 stand, out Vector3 inwards)
        {
            stand = default;
            inwards = default;
            if (car == null)
            {
                return false;
            }
            Frame(car, out Vector3 centre, out Vector3 forward);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float halfWidth = Mathf.Max(0.8f, car.colliderWidth * 0.5f);
            float halfLength = Mathf.Max(1.5f, car.length * 0.5f);
            float across = Mathf.Max(halfWidth * 0.45f + 0.75f, halfWidth + 0.2f);
            Vector3 at = centre - forward * Mathf.Min(0.5f, halfLength * 0.25f) + right * (side * across);
            inwards = right * -side;
            if (!OnRoad(at, 0.3f, out Vector3 ground))
            {
                return false;
            }
            stand = new Vector3(at.x, ground.y, at.z);
            return true;
        }

        // A car's middle and its heading along the ground; a car on its roof or side still has both.
        private static void Frame(VehicleComponent car, out Vector3 centre, out Vector3 forward)
        {
            Transform body = car.transform;
            centre = car.rb != null ? car.rb.worldCenterOfMass : body.position;
            forward = body.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = body.up;   // standing on its nose
                forward.y = 0f;
            }
            forward = forward.sqrMagnitude < 0.01f ? Vector3.forward : forward.normalized;
        }

        /// <summary>The nearest point anyone on foot can reach, road surface included.</summary>
        public static bool OnRoad(Vector3 near, float radius, out Vector3 spot)
        {
            return OnStreet(near, radius, StreetActor.RoadFilter, out spot);
        }

        /// <summary>The nearest point on a pavement.</summary>
        public static bool OnPavement(Vector3 near, float radius, out Vector3 spot)
        {
            return OnStreet(near, radius, Pavement, out spot);
        }

        /// <summary>The nearest point of the ground a given navigation agent may walk (its own type of agent and its areas).</summary>
        public static bool OnWalk(Vector3 near, float radius, NavMeshQueryFilter filter, out Vector3 spot)
        {
            return OnStreet(near, radius, filter, out spot);
        }

        /// <summary>
        /// The height of the street surface under a point: road, pavement or open ground, found
        /// with one ray and never the roof of a car. False (and the point's own height) where
        /// there is none.
        /// </summary>
        public static bool Ground(Vector3 near, out float height)
        {
            if (!_groundReady)
            {
                _groundReady = true;
                _groundMask = LayerMask.GetMask("Roads", "Ground", "GroundUnplacable");
            }
            if (_groundMask != 0 && Physics.Raycast(near + Vector3.up * GroundProbe, Vector3.down, out RaycastHit hit, GroundProbe * 3f,
                    _groundMask, QueryTriggerInteraction.Ignore))
            {
                height = hit.point.y;
                return true;
            }
            height = near.y;
            return false;
        }

        // The nearest walkable point at street level. Points are often asked for at the height of a
        // car's middle, where the nearest piece of navigation mesh need not be the ground beside it:
        // anything that is not at the height of the street underneath is refused.
        private static bool OnStreet(Vector3 near, float radius, NavMeshQueryFilter filter, out Vector3 spot)
        {
            bool grounded = Ground(near, out float ground);
            near.y = ground;
            if (NavMesh.SamplePosition(near, out NavMeshHit hit, radius, filter)
                && (!grounded || Mathf.Abs(hit.position.y - ground) < GroundTolerance))
            {
                spot = hit.position;
                return true;
            }
            // Something nearer was a ledge: only the mesh right at the ground will do.
            if (grounded && NavMesh.SamplePosition(near, out hit, GroundTolerance, filter))
            {
                spot = hit.position;
                return true;
            }
            spot = default;
            return false;
        }

        private static readonly Collider[] Solid = new Collider[4];
        private static int _vehicleMask;

        /// <summary>
        /// Moves a place to stand on the road out of any vehicle it is in, half a metre at a time
        /// straight away from that vehicle's middle, staying on the street. The navigation mesh
        /// already keeps people out of the vehicles that have been cut out of it; this is for the
        /// ones that have not (parked cars, a car whose cut is still being made). False if no clear
        /// place was found within two metres; the place is then left where it was.
        /// Asked when someone is given a place, never on a timer.
        /// </summary>
        public static bool OutOfVehicles(ref Vector3 spot)
        {
            if (_vehicleMask == 0)
            {
                _vehicleMask = TrafficTools.VehicleMask;
                if (_vehicleMask == 0)
                {
                    return true;
                }
            }
            Vector3 at = spot;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                int count = Physics.OverlapSphereNonAlloc(at + Vector3.up * 0.9f, 0.4f, Solid, _vehicleMask, QueryTriggerInteraction.Ignore);
                if (count == 0)
                {
                    spot = at;
                    return true;
                }
                Vector3 away = at - Solid[0].bounds.center;
                for (int i = 0; i < count; i++)
                {
                    Solid[i] = null;
                }
                away.y = 0f;
                away = away.sqrMagnitude < 0.01f ? Vector3.right : away.normalized;
                if (!OnRoad(at + away * 0.5f, 0.6f, out at))
                {
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// A random point on a pavement between two distances from a centre. With unseen set,
        /// the first tries only accept points the camera is not looking at.
        /// </summary>
        public static bool PavementAround(Vector3 centre, float minDistance, float maxDistance, bool unseen, out Vector3 spot)
        {
            float low = minDistance * 0.8f;
            float high = maxDistance * 1.2f;
            for (int i = 0; i < RingAttempts; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float distance = Random.Range(minDistance, maxDistance);
                Vector3 probe = centre + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
                if (!OnPavement(probe, 6f, out Vector3 found))
                {
                    continue;
                }
                // The nearest pavement may be well off the ring.
                float sqr = (found - centre).sqrMagnitude;
                if (sqr < low * low || sqr > high * high)
                {
                    continue;
                }
                if (unseen && i < RingAttempts - 3 && IsInView(found))
                {
                    continue;
                }
                spot = found;
                return true;
            }
            spot = default;
            return false;
        }

        /// <summary>A point about a distance away from one place, on the far side from another: where someone walks off to.</summary>
        public static bool AwayFrom(Vector3 from, Vector3 avoid, float distance, bool onRoad, out Vector3 spot)
        {
            Vector3 direction = from - avoid;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                float angle = Random.value * Mathf.PI * 2f;
                direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }
            direction.Normalize();

            // Straight away, then a little to either side: a pavement rarely runs exactly where it is wanted.
            for (int i = 0; i < 3; i++)
            {
                Vector3 heading = i == 0 ? direction : Quaternion.Euler(0f, i == 1 ? 40f : -40f, 0f) * direction;
                Vector3 probe = from + heading * distance;
                bool found = onRoad ? OnRoad(probe, 8f, out spot) : OnPavement(probe, 8f, out spot);
                if (found && (spot - from).sqrMagnitude > distance * distance * 0.25f)
                {
                    return true;
                }
            }
            if (!onRoad)
            {
                return PavementAround(from, distance * 0.7f, distance * 1.2f, false, out spot);
            }
            spot = default;
            return false;
        }

        /// <summary>True if the camera is looking roughly towards a point that is near enough to notice someone appearing.</summary>
        public static bool IsInView(Vector3 point)
        {
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                return false;
            }
            Transform eye = camera.transform;
            Vector3 offset = point - eye.position;
            float distance = offset.magnitude;
            return distance < 95f && distance > 0.01f && Vector3.Dot(offset, eye.forward) > 0.45f * distance;
        }
    }
}
