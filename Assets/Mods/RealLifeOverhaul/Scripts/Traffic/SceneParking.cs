using System.Collections.Generic;
using GleyTrafficSystem;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Where a vehicle may stand: the free-space tests for wrecks, units and the roadside, and the point on a unit's own route
    /// where it pulls up (in line with its lane, the nearest free one to the scene). Physics box tests, on events only.
    /// </summary>
    public static class SceneParking
    {
        private const float SearchBack = 45f;   // metres back along the route a free place is looked for

        private static readonly Collider[] Overlaps = new Collider[16];
        private static int _solidMask;

        // Everything a parked vehicle must not stand in: vehicles of every kind and street furniture.
        private static int SolidMask
        {
            get
            {
                if (_solidMask == 0)
                {
                    _solidMask = TrafficTools.VehicleMask | LayerMask.GetMask("StreetProps");
                }
                return _solidMask;
            }
        }

        /// <summary>True if the car would overlap nothing in that pose, apart from itself and the bodies named.</summary>
        /// <param name="except">Bodies that do not count (the other cars of the same crash), or null.</param>
        /// <param name="margin">Extra room wanted all round, in metres.</param>
        public static bool IsFree(VehicleComponent car, Vector3 position, Quaternion rotation, HashSet<Rigidbody> except, float margin = 0f)
        {
            return Touching(car, position, rotation, except, margin) == null;
        }

        /// <summary>What the car would overlap in that pose, apart from itself and the bodies named; null if nothing. For a check and its log line.</summary>
        public static Collider Touching(VehicleComponent car, Vector3 position, Quaternion rotation, HashSet<Rigidbody> except, float margin = 0f)
        {
            int mask = SolidMask;
            if (mask == 0)
            {
                return null;
            }
            var half = new Vector3(Mathf.Max(0.8f, car.colliderWidth * 0.5f) + margin, 0.5f, Mathf.Max(1.5f, car.length * 0.5f) + margin);
            int count = Physics.OverlapBoxNonAlloc(position + Vector3.up * 0.9f, half, Overlaps, rotation, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Rigidbody body = Overlaps[i].attachedRigidbody;
                if (body == car.rb && body != null)
                {
                    continue;
                }
                if (body == null || except == null || !except.Contains(body))
                {
                    return Overlaps[i];
                }
            }
            return null;
        }

        private static int _everyMask;

        /// <summary>Every layer something solid can be on: vehicles, street furniture, buildings with their steps and railings, the default layer.</summary>
        public static int EveryMask
        {
            get
            {
                if (_everyMask == 0)
                {
                    _everyMask = TrafficTools.VehicleMask
                                 | LayerMask.GetMask("Default", "StreetProps", "Buildings", "BuildingWalls", "BuildingsOutlined", "InteractiveItems", "InteractiveItemsOutlined");
                }
                return _everyMask;
            }
        }

        /// <summary>Only what stands on the road and beside it (vehicles, street furniture): for a crew with no way clear of buildings' colliders.</summary>
        public static int RoadsideMask => SolidMask;

        private static int _streetMask;

        /// <summary>What stands in the street for people on foot and what they wheel: vehicles, street furniture and buildings, not the default layer.</summary>
        public static int StreetMask
        {
            get
            {
                if (_streetMask == 0)
                {
                    _streetMask = TrafficTools.VehicleMask
                                  | LayerMask.GetMask("StreetProps", "Buildings", "BuildingWalls", "BuildingsOutlined", "InteractiveItems", "InteractiveItemsOutlined");
                }
                return _streetMask;
            }
        }

        /// <summary>What a response vehicle would stand in by its whole body and a margin, against every solid layer (moving traffic and itself excepted).</summary>
        public static Collider Standing(VehicleComponent car, Vector3 position, Vector3 forward, float margin)
        {
            int mask = EveryMask;
            forward.y = 0f;
            if (mask == 0 || car == null || forward.sqrMagnitude < 0.01f)
            {
                return null;
            }
            TrafficTools.Footprint(car, out _, out _, out float halfLength, out float halfWidth);
            if (StreetSpots.Ground(position, out float ground))
            {
                position.y = ground;
            }
            float top = Mathf.Clamp(car.coliderHeight, 1.4f, 3.4f);
            var half = new Vector3(halfWidth + margin, (top - BoxFrom) * 0.5f, halfLength + margin);
            int count = Physics.OverlapBoxNonAlloc(position + Vector3.up * (BoxFrom + half.y), half, Overlaps, Quaternion.LookRotation(forward.normalized, Vector3.up),
                mask, QueryTriggerInteraction.Ignore);
            Collider found = null;
            for (int i = 0; i < count; i++)
            {
                Rigidbody body = Overlaps[i].attachedRigidbody;
                bool own = body != null && body == car.rb;
                bool passing = body != null && !body.isKinematic && ((1 << Overlaps[i].gameObject.layer) & TrafficTools.TrafficMask) != 0;
                if (found == null && !own && !passing)
                {
                    found = Overlaps[i];
                }
                Overlaps[i] = null;
            }
            return found;
        }

        private const float BoxFrom = 0.35f;   // metres above the road a whole-body box starts: over the kerb, under every bumper

        /// <summary>True if a place in a lane lies on a crossing: the pedestrians' mesh runs under the vehicle's nose, middle or tail.</summary>
        public static bool OnCrossing(Vector3 position, Vector3 forward, float halfLength)
        {
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.01f ? Vector3.forward : forward.normalized;
            for (int i = -1; i <= 1; i++)
            {
                Vector3 point = position + forward * (i * halfLength);
                if (StreetSpots.Ground(point, out float ground))
                {
                    point.y = ground;
                }
                if (NavMesh.SamplePosition(point, out _, 0.3f, StreetSpots.Pavement))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>What stands in a strip of road between two points, so wide each side, kerb to head height; null if clear. One body may be left out.</summary>
        /// <param name="mask">The layers that count; 0 for everything that stands in the street (StreetMask).</param>
        public static Collider InStrip(Vector3 from, Vector3 to, float halfWidth, Rigidbody except = null, int mask = 0)
        {
            mask = mask != 0 ? mask : StreetMask;
            Vector3 run = to - from;
            run.y = 0f;
            float length = run.magnitude;
            if (mask == 0 || length < 0.05f)
            {
                return null;
            }
            Vector3 middle = (from + to) * 0.5f;
            if (StreetSpots.Ground(middle, out float ground))
            {
                middle.y = ground;
            }
            int count = Physics.OverlapBoxNonAlloc(middle + Vector3.up * (BoxFrom + 0.75f), new Vector3(halfWidth, 0.75f, length * 0.5f), Overlaps,
                Quaternion.LookRotation(run / length, Vector3.up), mask, QueryTriggerInteraction.Ignore);
            Collider found = null;
            for (int i = 0; i < count; i++)
            {
                Rigidbody body = Overlaps[i].attachedRigidbody;
                if (found == null && (except == null || body != except))
                {
                    found = Overlaps[i];
                }
                Overlaps[i] = null;
            }
            return found;
        }

        /// <summary>True if a box that size, standing on the ground at that place, would overlap no vehicle and no street furniture.</summary>
        /// <param name="margin">Extra room wanted all round, in metres.</param>
        public static bool IsFree(Vector3 position, Quaternion rotation, float halfWidth, float halfLength, float margin)
        {
            int mask = SolidMask;
            return mask == 0 || !Physics.CheckBox(position + Vector3.up * 0.9f, new Vector3(halfWidth + margin, 0.5f, halfLength + margin), rotation, mask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>True if something that will not move (a parked vehicle, street furniture) stands in a box that size on the ground there.</summary>
        public static bool HasParked(Vector3 position, Quaternion rotation, float halfWidth, float halfLength)
        {
            if (_parkedMask == 0)
            {
                _parkedMask = LayerMask.GetMask("ParkedVehicles", "StreetProps");
            }
            return _parkedMask != 0 && Physics.CheckBox(position + Vector3.up * 0.9f, new Vector3(halfWidth, 0.5f, halfLength), rotation, _parkedMask, QueryTriggerInteraction.Ignore);
        }

        private static int _parkedMask;

        /// <summary>How far a car stopped at the roadside can pull in to its right over clear carriageway; 0 to stop in its lane.</summary>
        /// <param name="follower">The vehicle behind it, which does not count as being in the way; or null.</param>
        public static float KerbRoom(VehicleComponent car, Rigidbody follower)
        {
            if (car == null || SolidMask == 0 || !TrafficManager.HasInstance)
            {
                return 0f;
            }
            Transform body = car.transform;
            Vector3 forward = Flat(body.forward);
            if (forward.sqrMagnitude < 0.01f)
            {
                return 0f;
            }
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 at = body.position;
            float halfWidth = Mathf.Max(0.8f, car.colliderWidth * 0.5f);
            // A lane running beside it on that side is somebody's lane, not the roadside.
            if (TrafficTools.ClosestWaypoint(at + forward * 6f + right * 3.3f, 1.7f) != null)
            {
                return 0f;
            }
            Quaternion facing = Quaternion.LookRotation(forward, Vector3.up);
            for (float over = 2.2f; over > 1.15f; over -= 0.5f)
            {
                bool road = true;
                for (int i = 0; i < 3 && road; i++)
                {
                    Vector3 edge = at + forward * (-10f + i * 13f) + right * (over + halfWidth);
                    road = StreetSpots.OnRoad(edge, 0.3f, out Vector3 ground) && !NavMesh.SamplePosition(ground, out _, 0.25f, StreetSpots.Pavement);
                }
                if (!road)
                {
                    continue;
                }
                int count = Physics.OverlapBoxNonAlloc(at + forward * 3f + right * over + Vector3.up * 0.9f, new Vector3(halfWidth + 0.2f, 0.5f, 15f), Overlaps,
                    facing, SolidMask, QueryTriggerInteraction.Ignore);
                bool free = true;
                for (int i = 0; i < count; i++)
                {
                    Rigidbody hit = Overlaps[i].attachedRigidbody;
                    free &= hit != null && (hit == car.rb || hit == follower);
                }
                if (free)
                {
                    return over;
                }
            }
            return 0f;
        }

        /// <summary>The point on a unit's route where it pulls up: about back metres short of the scene, or the first free one further back; -1 if none.</summary>
        /// <param name="from">The first point still ahead of it.</param>
        public static int Find(VehicleComponent car, List<Vector3> route, int from, Vector3 scene, float back)
        {
            if (route.Count < 2 || from >= route.Count)
            {
                return -1;
            }
            // The point of the route nearest the scene: the route may run on past it.
            int nearest = from;
            float best = float.MaxValue;
            for (int i = from; i < route.Count; i++)
            {
                float sqr = Flat(route[i] - scene).sqrMagnitude;
                if (sqr < best)
                {
                    best = sqr;
                    nearest = i;
                }
            }

            float walked = 0f;
            for (int i = nearest; i > from; i--)
            {
                walked += Flat(route[i] - route[i - 1]).magnitude;
                if (walked < back)
                {
                    continue;
                }
                if (walked > back + SearchBack)
                {
                    break;
                }
                Vector3 along = Flat(route[i] - route[i - 1]);
                if (along.sqrMagnitude > 0.01f && IsFree(car, route[i - 1], Quaternion.LookRotation(along), null, 0.3f))
                {
                    return i - 1;
                }
            }
            return -1;
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }
    }
}
