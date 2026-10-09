using Extensions;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Where the people of the living city may stand. Every place is found, never assumed:
    /// on the pavement (the navigation mesh the game's pedestrians use, at the height of the
    /// ground under it), clear of vehicles, street furniture, walls and other people, away from
    /// doors, and for work on a shopfront at a measured distance from a wall that really is
    /// there. All of it is asked when a site is being chosen (a handful of physics queries every
    /// few seconds at most), never per frame, and none of it allocates.
    /// </summary>
    public static class CitySpots
    {
        private const float GroundProbe = 2.5f;
        private const float GroundTolerance = 0.45f;   // a walkable point further than this from the ground is a ledge, not the street
        private const float BodyBottom = 0.35f;        // the clearance test starts this far up, so kerbs and the ground do not count
        private const float BodyTop = 1.75f;
        private const float DoorKeepOut = 2.4f;        // nobody works this close to something the player can use
        private const float WallReach = 9f;            // how far from a point of pavement a shopfront is looked for: a wide pavement is six metres deep
        private const int WallRays = 12;
        private const float WallAgree = 0.12f;         // the probes at chest and head height and to either side must agree to within this
        private const float PlinthMost = 0.3f;         // below the waist the wall may stand this much proud (a plinth, a sill) and no more
        private const int RingAttempts = 8;

        /// <summary>Why places were refused since the counts were last cleared: for saying in the log why a test found no room.</summary>
        public static int OffPavement, Blocked, NearDoor, NoWall, Uneven;

        private static NavMeshQueryFilter _pavement;
        private static NavMeshPath _path;   // one, reused: asking for a path allocates nothing after the first
        private static int _groundMask;
        private static int _solidMask;
        private static int _doorMask;
        private static int _wallMask;
        private static bool _ready;
        private static bool _wallLogged;   // which layer shopfronts turned out to be on has been said this session

        private static void Setup()
        {
            if (_ready)
            {
                return;
            }
            _ready = true;
            _pavement = new NavMeshQueryFilter { agentTypeID = NavMeshHelper.NpcAgentTypeId, areaMask = NavMesh.AllAreas & ~NavMeshHelper.SwimmingAreaMask };
            _groundMask = LayerMask.GetMask("Roads", "Ground", "GroundUnplacable");
            _doorMask = LayerMask.GetMask("InteractiveItems", "InteractiveItemsOutlined");
            _wallMask = LayerMask.GetMask("Buildings", "BuildingsOutlined", "BuildingWalls");
            // Everything a person could be standing in: all layers but the ground itself and the ones that are only zones.
            _solidMask = ~(_groundMask | LayerMask.GetMask("UI", "Ignore Raycast", "AutoParkSpots", "ParkingArea", "TransparentFX", "Water"));
        }

        /// <summary>The nearest point of pavement to a place, at street level. False if there is none within the radius.</summary>
        public static bool Pavement(Vector3 near, float radius, out Vector3 spot)
        {
            Setup();
            bool grounded = Ground(near, out float ground);
            near.y = ground;
            if (NavMesh.SamplePosition(near, out NavMeshHit hit, radius, _pavement) && (!grounded || Mathf.Abs(hit.position.y - ground) < GroundTolerance))
            {
                spot = hit.position;
                return true;
            }
            spot = default;
            return false;
        }

        /// <summary>True if a place is pavement exactly there (within a step), so that what is put there stands on it.</summary>
        public static bool OnPavement(Vector3 place, out Vector3 spot)
        {
            if (Pavement(place, 0.35f, out spot) && Flat(spot - place) < 0.35f * 0.35f)
            {
                return true;
            }
            OffPavement++;
            return false;
        }

        /// <summary>The height of the street under a point (road, pavement or open ground). False where there is none.</summary>
        public static bool Ground(Vector3 near, out float height)
        {
            Setup();
            if (_groundMask != 0 && Physics.Raycast(near + Vector3.up * GroundProbe, Vector3.down, out RaycastHit hit, GroundProbe * 3f, _groundMask, QueryTriggerInteraction.Ignore))
            {
                height = hit.point.y;
                return true;
            }
            height = near.y;
            return false;
        }

        /// <summary>
        /// True if somebody can stand at a point with this much room round them: no vehicle, wall,
        /// street furniture or person within the radius, and nothing the player can use (a door, a
        /// bench, a machine) close by.
        /// </summary>
        public static bool Clear(Vector3 spot, float radius)
        {
            Setup();
            return Free(spot, radius, _solidMask);
        }

        /// <summary>The same without counting a refusal: for places that are not part of what a test reports on.</summary>
        public static bool ClearQuietly(Vector3 spot, float radius)
        {
            int blocked = Blocked, door = NearDoor;
            bool clear = Clear(spot, radius);
            Blocked = blocked;
            NearDoor = door;
            return clear;
        }

        /// <summary>True if somebody on foot can walk the whole way from one point of pavement to another.</summary>
        public static bool Reachable(Vector3 from, Vector3 to)
        {
            Setup();
            if (_path == null)
            {
                _path = new NavMeshPath();
            }
            return NavMesh.CalculatePath(from, to, _pavement, _path) && _path.status == NavMeshPathStatus.PathComplete;
        }

        /// <summary>A point on the pavement at a place, with room round it. The usual question an activity asks for each of its people and props.</summary>
        public static bool Stand(Vector3 place, float room, out Vector3 spot)
        {
            return OnPavement(place, out spot) && Clear(spot, room);
        }

        /// <summary>
        /// A random point of pavement between two distances from a centre. With unseen set, only
        /// points the camera is not looking at will do.
        /// </summary>
        public static bool PavementRing(Vector3 centre, float minDistance, float maxDistance, bool unseen, out Vector3 spot)
        {
            float low = minDistance * 0.8f;
            float high = maxDistance * 1.2f;
            for (int i = 0; i < RingAttempts; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float distance = Random.Range(minDistance, maxDistance);
                if (!Pavement(centre + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance), 6f, out Vector3 found))
                {
                    continue;
                }
                float sqr = Flat(found - centre);
                if (sqr < low * low || sqr > high * high || (unseen && InView(found)))
                {
                    continue;
                }
                spot = found;
                return true;
            }
            spot = default;
            return false;
        }

        /// <summary>
        /// The direction a pavement runs in at a point: along its nearest edge (the kerb or the
        /// building line). False where there is no edge near enough to tell.
        /// </summary>
        public static bool Along(Vector3 spot, out Vector3 direction)
        {
            Setup();
            if (NavMesh.FindClosestEdge(spot, out NavMeshHit edge, _pavement) && edge.distance < 6f)
            {
                Vector3 normal = edge.normal;
                normal.y = 0f;
                if (normal.sqrMagnitude > 0.01f)
                {
                    direction = Vector3.Cross(Vector3.up, normal.normalized);
                    return true;
                }
            }
            direction = default;
            return false;
        }

        /// <summary>
        /// A shopfront to work on: the wall of a building that can be seen from a point of pavement
        /// (twelve rays round it, as far as a wide pavement is deep), upright, with pavement to
        /// stand on a given distance in front of it. The wall is then measured square on from that
        /// standing place: flat from waist to head and across the width of a person (a plinth or
        /// sill below the waist may stand a little proud, as real shopfronts have). Returns where
        /// to stand and the wall's normal (pointing out of the wall, towards whoever stands there).
        /// </summary>
        public static bool Shopfront(Vector3 near, float distance, float room, out Vector3 stand, out Vector3 outward)
        {
            Setup();
            stand = default;
            outward = default;
            float start = Random.value * 360f;
            bool seenWall = false;
            for (int i = 0; i < WallRays; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, start + i * (360f / WallRays), 0f) * Vector3.forward;
                if (!Wall(near, direction, 1.2f, WallReach, out RaycastHit seen))
                {
                    continue;
                }
                seenWall = true;
                Vector3 normal = seen.normal;
                if (Mathf.Abs(normal.y) > 0.2f)
                {
                    continue;   // not upright
                }
                normal.y = 0f;
                normal.Normalize();
                Vector3 wanted = seen.point + normal * distance;
                wanted.y = near.y;
                if (!OnPavement(wanted, out Vector3 found) || !FlatWall(found, normal, distance, out float gap, out RaycastHit face))
                {
                    continue;
                }
                // Measured from where the feet really are (the mesh may have moved them a little): one
                // correction, and then the wall must be the asked distance away to within five centimetres.
                if (Mathf.Abs(gap - distance) > 0.03f
                    && (!OnPavement(found + normal * (distance - gap), out found) || !FlatWall(found, normal, distance, out gap, out face) || Mathf.Abs(gap - distance) > 0.05f))
                {
                    continue;
                }
                if (!ClearBesideWall(found, room))
                {
                    continue;
                }
                if (!_wallLogged && face.collider != null)
                {
                    _wallLogged = true;
                    CityLifeDirector.Logger?.Info("City life: shopfronts are found on layer '" + LayerMask.LayerToName(face.collider.gameObject.layer) + "' (first: "
                                                  + face.collider.GetType().Name + " '" + face.collider.name + "', " + gap.ToString("F2") + " m from where the worker stands).");
                }
                stand = found;
                outward = normal;
                return true;
            }
            if (seenWall)
            {
                Uneven++;
            }
            else
            {
                NoWall++;
            }
            return false;
        }

        // The wall square on from a standing place: there at chest height, the same at head height and
        // a forearm to either side, and nothing below the waist that stands further out than a plinth does.
        private static bool FlatWall(Vector3 from, Vector3 normal, float distance, out float gap, out RaycastHit face)
        {
            gap = 0f;
            float reach = distance + 1f;
            if (!Wall(from, -normal, 1.2f, reach, out face) || Vector3.Dot(face.normal, normal) < 0.9f)
            {
                return false;
            }
            gap = face.distance;
            Vector3 across = Vector3.Cross(Vector3.up, normal) * 0.3f;
            return Wall(from, -normal, 1.65f, reach, out RaycastHit high) && Mathf.Abs(high.distance - gap) <= WallAgree
                   && Wall(from + across, -normal, 1.2f, reach, out RaycastHit left) && Mathf.Abs(left.distance - gap) <= WallAgree
                   && Wall(from - across, -normal, 1.2f, reach, out RaycastHit right) && Mathf.Abs(right.distance - gap) <= WallAgree
                   && Wall(from, -normal, 0.45f, reach, out RaycastHit low) && low.distance >= gap - PlinthMost && low.distance <= gap + 0.5f;
        }

        /// <summary>True if the camera is looking roughly towards a point that is near enough to notice someone appearing.</summary>
        public static bool InView(Vector3 point)
        {
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                return false;
            }
            Transform eye = camera.transform;
            Vector3 offset = point - eye.position;
            float distance = offset.magnitude;
            return distance < 110f && distance > 0.01f && Vector3.Dot(offset, eye.forward) > 0.35f * distance;
        }

        public static float Flat(Vector3 offset)
        {
            return offset.x * offset.x + offset.z * offset.z;
        }

        private static bool Wall(Vector3 from, Vector3 direction, float height, float reach, out RaycastHit hit)
        {
            return Physics.Raycast(from + Vector3.up * height, direction, out hit, reach, _wallMask, QueryTriggerInteraction.Ignore);
        }

        // Standing at arm's length from a wall: the wall itself must not count against the place.
        private static bool ClearBesideWall(Vector3 spot, float room)
        {
            return Free(spot, room, _solidMask & ~_wallMask);
        }

        private static bool Free(Vector3 spot, float radius, int solid)
        {
            if (Physics.CheckCapsule(spot + Vector3.up * (BodyBottom + radius), spot + Vector3.up * BodyTop, radius, solid, QueryTriggerInteraction.Ignore))
            {
                Blocked++;
                return false;
            }
            if (Physics.CheckSphere(spot + Vector3.up, DoorKeepOut, _doorMask, QueryTriggerInteraction.Collide))
            {
                NearDoor++;
                return false;
            }
            return true;
        }
    }
}
