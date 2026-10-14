using Streets;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>A place on the pavement in front of a building's door, with the building it belongs to.</summary>
    public struct DoorSpot
    {
        public Vector3 Front;                      // where someone stands: on the pavement's navigation mesh, clear of vehicles
        public Vector3 Door;                       // the door itself, to face
        public CityBuildingController Building;
        public string Name;                        // the address as the game writes it
    }

    /// <summary>
    /// Where jobs happen: door fronts picked from the city's own buildings, and the ground
    /// beside the player's car where a passenger gets in or out. Every answer is a point on the
    /// navigation mesh that has been tested for vehicles, or "no". Asked when a job needs a new
    /// place (a fare, a round), never on a timer.
    /// </summary>
    public static class JobSpots
    {
        private const int Picks = 48;               // random buildings looked at before giving up
        private const float PersonRadius = 0.35f;

        private static readonly Collider[] Touching = new Collider[8];

        /// <summary>
        /// A random building's door front between two distances from a point. With a lane
        /// distance given, only doors with a traffic lane that near (a car can pull up by them).
        /// False if none was found: the caller tries again later or says so.
        /// </summary>
        /// <param name="only">Buildings that will do (a shop that is open, say), or null for any. Pass a delegate kept in a field, not a new lambda.</param>
        public static bool RandomDoor(Vector3 around, float minDistance, float maxDistance, CityBuildingController not, float laneWithin, out DoorSpot spot,
            System.Predicate<CityBuildingController> only = null, int picks = Picks)
        {
            spot = default;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] buildings = city != null ? city.cityBuildingControllers : null;
            if (buildings == null || buildings.Length == 0)
            {
                return false;
            }
            bool lanes = laneWithin > 0f && RoadMap.Ensure();
            float minSqr = minDistance * minDistance;
            float maxSqr = maxDistance * maxDistance;
            for (int i = 0; i < picks; i++)
            {
                CityBuildingController building = buildings[Random.Range(0, buildings.Length)];
                if (building == null || building == not || building.building == null || building.entranceDoors == null
                    || building.entranceDoors.Length == 0 || GameMembers.Fenced(building.building))
                {
                    continue;
                }
                BuildingEntranceDoorRef(building, out Transform door);
                if (door == null)
                {
                    continue;
                }
                Vector3 offset = door.position - around;
                offset.y = 0f;
                float sqr = offset.sqrMagnitude;
                if (sqr < minSqr || sqr > maxSqr || Mathf.Abs(door.position.y - around.y) > 6f)
                {
                    continue;
                }
                if ((only != null && !only(building)) || !Front(building, out Vector3 front))
                {
                    continue;
                }
                if (lanes && (!RoadMap.NearestLane(front, out float lane, out _, out _) || lane > laneWithin))
                {
                    continue;
                }
                spot.Front = front;
                spot.Door = door.position;
                spot.Building = building;
                spot.Name = building.building.Address.ToFormattedString();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Where someone stands in front of a building's door: the pavement nearest the door,
        /// a step out from the wall, at street level and not inside a parked vehicle.
        /// </summary>
        public static bool Front(CityBuildingController building, out Vector3 front)
        {
            front = default;
            BuildingEntranceDoorRef(building, out Transform door);
            if (door == null || !StreetSpots.OnPavement(door.position, 6f, out front))
            {
                return false;
            }
            Vector3 outward = front - door.position;
            outward.y = 0f;
            if (outward.sqrMagnitude > 0.04f && StreetSpots.OnPavement(front + outward.normalized * 0.7f, 1.2f, out Vector3 further))
            {
                front = further;
            }
            return StreetSpots.OutOfVehicles(ref front);
        }

        private static void BuildingEntranceDoorRef(CityBuildingController building, out Transform door)
        {
            door = null;
            if (building == null || building.entranceDoors == null)
            {
                return;
            }
            for (int i = 0; i < building.entranceDoors.Length; i++)
            {
                if (building.entranceDoors[i] != null && building.entranceDoors[i].doorTransform != null)
                {
                    door = building.entranceDoors[i].doorTransform;
                    return;
                }
            }
        }

        /// <summary>
        /// The player's car as a box on the ground: its middle, the way it points, half its
        /// length and width, from its own collider. Sensible defaults if the collider says
        /// nothing useful.
        /// </summary>
        public static void CarFrame(VehicleController car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth)
        {
            Transform body = car.transform;
            forward = body.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.01f ? Vector3.forward : forward.normalized;
            centre = body.position;
            halfLength = 2.2f;
            halfWidth = 0.95f;
            if (car.vehicleCollider is BoxCollider box)
            {
                Vector3 size = Vector3.Scale(box.size, box.transform.lossyScale);
                if (size.z > 2f && size.z < 12f && size.x > 1f && size.x < 3.5f)
                {
                    centre = box.transform.TransformPoint(box.center);
                    halfLength = size.z * 0.5f;
                    halfWidth = size.x * 0.5f;
                }
            }
            else if (car.vehicleCollider != null)
            {
                // Any other shape: its box in the world is right while the car stands square to the axes, and near enough otherwise for a door.
                Bounds bounds = car.vehicleCollider.bounds;
                centre = bounds.center;
                float across = Mathf.Min(bounds.size.x, bounds.size.z);
                if (across > 1f && across < 3.5f)
                {
                    halfWidth = across * 0.5f;
                }
            }
        }

        /// <summary>
        /// The ground beside a rear door of the player's car, on the side nearer a point (the
        /// kerb the passenger is on, the door they are going to), or on the other side if that
        /// one is blocked. On the navigation mesh, at street level, with room for a person
        /// between the car and whatever stands next to it. False if neither side has room.
        /// </summary>
        public static bool BesideCar(VehicleController car, Vector3 towards, out Vector3 spot)
        {
            CarFrame(car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float side = Vector3.Dot(towards - centre, right) >= 0f ? StreetSpots.Right : StreetSpots.Left;
            Vector3 door = centre - forward * Mathf.Min(0.5f, halfLength * 0.25f);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (StreetSpots.Beside(door, forward, halfWidth, side, 0.6f, out spot) && Clear(spot, car.transform)
                    && Mathf.Abs(Vector3.Dot(spot - centre, right)) > halfWidth + 0.3f)
                {
                    return true;
                }
                side = -side;
            }
            spot = default;
            return false;
        }

        private const float SeatOut = 0.45f;     // of the car's half width: where the middle of a rear seat is taken to be (the game's cars say nothing of their seats)
        private const float DoorStand = 0.75f;   // metres from the middle of the seat to the feet: what job_car_enter and job_car_exit are made to (CityAssets/README.md)
        private const float DoorClear = 0.2f;    // ...and never nearer the car's side than this

        /// <summary>
        /// Where somebody stands to get into (or comes out of) a rear door in the clips made for it, on the side
        /// of the car a point beside it is on: on the line square to the car through the seat, the feet 0.75 m out
        /// from the middle of the seat, facing the car. The seat is an estimate (the game's vehicles carry no seat
        /// or door positions): as far back as the rear door <see cref="BesideCar"/> uses, 0.45 of the half width out.
        /// Not tested for room: use it only on the side BesideCar found clear, which is the same ground a step nearer.
        /// </summary>
        /// <param name="inwards">The way they face: square to the car, towards it.</param>
        /// <param name="rightSide">True for a door on the car's right-hand side (job_car_enter / job_car_exit), false for its left (the _l clips).</param>
        public static void AtDoor(VehicleController car, Vector3 beside, out Vector3 stand, out Vector3 inwards, out bool rightSide)
        {
            CarFrame(car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float side = Vector3.Dot(beside - centre, right) >= 0f ? 1f : -1f;
            float across = Mathf.Max(halfWidth * SeatOut + DoorStand, halfWidth + DoorClear);
            stand = centre - forward * Mathf.Min(0.5f, halfLength * 0.25f) + right * (side * across);
            stand.y = beside.y;
            inwards = right * -side;
            rightSide = side > 0f;
        }

        /// <summary>True if a person standing at a point touches no vehicle other than the given one.</summary>
        public static bool Clear(Vector3 spot, Transform except)
        {
            int mask = TrafficTools.VehicleMask;
            if (mask == 0)
            {
                return true;
            }
            int count = Physics.OverlapSphereNonAlloc(spot + Vector3.up * 0.9f, PersonRadius, Touching, mask, QueryTriggerInteraction.Ignore);
            bool clear = true;
            for (int i = 0; i < count; i++)
            {
                if (clear && Touching[i] != null && (except == null || !Touching[i].transform.IsChildOf(except)))
                {
                    clear = false;
                }
                Touching[i] = null;
            }
            return clear;
        }

        /// <summary>Distance between two points along the ground.</summary>
        public static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
