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

    /// <summary>Where jobs happen: door fronts, and the ground beside the player's car, each a point on the navigation mesh tested for vehicles.</summary>
    public static class JobSpots
    {
        private const int Picks = 48;               // random buildings looked at before giving up
        private const float PersonRadius = 0.35f;   // metres

        private static readonly Collider[] Touching = new Collider[8];

        /// <summary>A random building's door front between two distances; with <paramref name="laneWithin"/>, only one with a lane that near.</summary>
        /// <param name="only">Buildings that will do, or null for any: a delegate kept in a field, not a new lambda.</param>
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

        /// <summary>Where someone stands in front of a building's door: the pavement nearest it, a step out from the wall, clear of parked vehicles.</summary>
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

        /// <summary>The first entrance door of a building that has a transform: where it is, for those who stand at it. False if it has none.</summary>
        public static bool DoorOf(CityBuildingController building, out Vector3 door)
        {
            BuildingEntranceDoorRef(building, out Transform t);
            door = t != null ? t.position : default;
            return t != null;
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

        /// <summary>The player's car as a box on the ground, from its own collider: middle, heading, half length and half width.</summary>
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
                // Any other shape: its world box is right while the car is square to the axes, and near enough for a door otherwise.
                Bounds bounds = car.vehicleCollider.bounds;
                centre = bounds.center;
                float across = Mathf.Min(bounds.size.x, bounds.size.z);
                if (across > 1f && across < 3.5f)
                {
                    halfWidth = across * 0.5f;
                }
            }
        }

        /// <summary>The ground beside a rear door of the player's car, nearer side first, with room for a person; false if neither side has room.</summary>
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

        private const float SeatOut = 0.45f;     // of the car's half width: the middle of a rear seat (the game's cars say nothing of seats)
        private const float DoorStand = 0.75f;   // metres from the middle of the seat to the feet, as job_car_enter and job_car_exit are made
        private const float DoorClear = 0.2f;    // ...and never nearer the car's side than this

        /// <summary>Where somebody stands for the door clips, on the side <paramref name="beside"/> is on, facing the car; only for a side <see cref="BesideCar"/> found clear.</summary>
        /// <param name="rightSide">True for a door on the car's right (job_car_enter, job_car_exit), false for its left (the _l clips).</param>
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
