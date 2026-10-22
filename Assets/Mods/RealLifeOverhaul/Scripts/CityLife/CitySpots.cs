using System.Globalization;
using Extensions;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Where the living city's people may stand, always found and never assumed: on the pedestrians' navigation mesh at
    /// ground height, clear of vehicles, street furniture, walls and people, away from doors, and for shopfront work at a
    /// measured distance from a real wall. Asked only while a site is chosen (a few physics queries every few seconds), but
    /// for <see cref="Ground"/>, which the cameras ask once a frame while they swing low (one ray). Nothing allocates.
    /// </summary>
    public static class CitySpots
    {
        private const float GroundProbe = 2.5f;   // metres
        private const float GroundTolerance = 0.45f;   // metres: a walkable point further from the ground is a ledge, not the street
        private const float BodyBottom = 0.35f;        // metres up the clearance test starts, so kerbs and the ground do not count
        private const float BodyTop = 1.75f;   // metres
        private const float DoorKeepOut = 2.4f;        // metres: nobody works this close to something the player can use
        private const float WallReach = 9f;            // metres a shopfront is looked for from the pavement: a wide pavement is six deep
        private const int WallRays = 12;
        private const float WallAgree = 0.12f;         // metres the probes at chest and head height and to either side must agree within
        private const float PlinthMost = 0.3f;         // metres a plinth or sill may stand proud below the waist
        private const int RingAttempts = 8;

        // Metres: the table, stools and approaches with a 0.35 m pavement snap and 0.3 m of room for a person; the width also
        // covers an onlooker 1.3 m to either side.
        private static readonly Vector3 ChessHalf = new Vector3(2f, 1.5f, 1.85f);
        private static readonly Collider[] CrossingOverlaps = new Collider[64];   // one buffer: crossing tests run one at a time, on the main thread

        /// <summary>Why places were refused since the counts were last cleared, for the log to say why a test found no room.</summary>
        public static int OffPavement, Blocked, NearDoor, NoWall, Uneven;

        private static NavMeshQueryFilter _pavement;
        private static NavMeshPath _path;   // one, reused: asking for a path allocates nothing after the first
        private static int _groundMask;
        private static int _solidMask;
        private static int _doorMask;
        private static int _wallMask;
        private static int _crosswalkLayer;   // the game's marked crossings' triggers (CrosswalkTrigger); -1 matches nothing
        private static bool _ready;
        private static bool _wallLogged;   // the layer shopfronts are on has been logged this session

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
            _crosswalkLayer = LayerMask.NameToLayer("Crosswalk");
            // Everything a person could stand in: all layers but the ground and the zones.
            _solidMask = ~(_groundMask | LayerMask.GetMask("UI", "Ignore Raycast", "AutoParkSpots", "ParkingArea", "TransparentFX", "Water"));
        }

        /// <summary>The nearest point of pavement to a place, at street level; false if there is none within the radius.</summary>
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

        /// <summary>Whether a place is pavement exactly there, within a step, so what is put there stands on it.</summary>
        public static bool OnPavement(Vector3 place, out Vector3 spot)
        {
            if (Pavement(place, 0.35f, out spot) && Flat(spot - place) < 0.35f * 0.35f)
            {
                return true;
            }
            OffPavement++;
            return false;
        }

        /// <summary>The height of the street under a point (road, pavement or open ground); false where there is none.</summary>
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

        /// <summary>Whether somebody can stand here with this much room: nothing solid within the radius and nothing usable close by.</summary>
        public static bool Clear(Vector3 spot, float radius)
        {
            Setup();
            return Free(spot, radius, _solidMask);
        }

        /// <summary>The same without counting a refusal, for places a test does not report on.</summary>
        public static bool ClearQuietly(Vector3 spot, float radius)
        {
            int blocked = Blocked, door = NearDoor;
            bool clear = Clear(spot, radius);
            Blocked = blocked;
            NearDoor = door;
            return clear;
        }

        public static bool Reachable(Vector3 from, Vector3 to)
        {
            Setup();
            if (_path == null)
            {
                _path = new NavMeshPath();
            }
            return NavMesh.CalculatePath(from, to, _pavement, _path) && _path.status == NavMeshPathStatus.PathComplete;
        }

        private static readonly Vector3[] Corners = new Vector3[32];   // a route's corners, read into this one buffer

        /// <summary>The same, with the walk's length in metres (-1 with no whole route); a route of more corners than the buffer is measured straight on.</summary>
        public static bool Reachable(Vector3 from, Vector3 to, out float length)
        {
            length = -1f;
            if (!Reachable(from, to))
            {
                return false;
            }
            int count = _path.GetCornersNonAlloc(Corners);
            float sum = 0f;
            Vector3 last = from;
            for (int i = 0; i < count; i++)
            {
                sum += Vector3.Distance(last, Corners[i]);
                last = Corners[i];
            }
            length = sum + Vector3.Distance(last, to);
            return true;
        }

        /// <summary>A point on the pavement at a place with room round it: what an activity asks for each person and prop.</summary>
        public static bool Stand(Vector3 place, float room, out Vector3 spot)
        {
            return OnPavement(place, out spot) && Clear(spot, room);
        }

        /// <summary>
        /// Whether a turned box misses every marked crossing, a trigger tagged "Crosswalk" on the Crosswalk layer (either counts);
        /// a full buffer (<paramref name="full"/>) refuses too, in case one left out is a crossing. One query, as a place is chosen.
        /// </summary>
        public static bool ClearOfCrossings(Vector3 centre, Vector3 halfExtents, Quaternion rotation, out bool full)
        {
            Setup();
            int count = Physics.OverlapBoxNonAlloc(centre, halfExtents, CrossingOverlaps, rotation, Physics.AllLayers, QueryTriggerInteraction.Collide);
            full = count >= CrossingOverlaps.Length;
            bool crossing = false;
            for (int i = 0; i < count; i++)
            {
                Collider hit = CrossingOverlaps[i];
                if (hit != null && (hit.gameObject.layer == _crosswalkLayer || hit.CompareTag("Crosswalk")))
                {
                    crossing = true;
                }
                CrossingOverlaps[i] = null;   // no collider is held past the query
            }
            return !crossing && !full;
        }

        /// <summary>Whether the whole chess setup, its approaches and onlookers included, misses crossings.</summary>
        public static bool ChessClear(Vector3 table, Vector3 along)
        {
            bool clear = ClearOfCrossings(table + Vector3.up * 0.5f, ChessHalf, Quaternion.LookRotation(along), out bool full);
            if (Meta.Dev.Verbose)
            {
                CityLifeDirector.Logger?.Info(clear ? "Chess check (crossing clearance): no crossing under the table, stools, approaches or onlookers: PASS"
                    : full ? "Chess check (crossing clearance): more colliders than the query holds, the place refused in case one is a crossing: PASS"
                    : "Chess check (crossing clearance): a crossing under the table, stools, approaches or onlookers, the place refused: PASS");
            }
            return clear;
        }

        /// <summary>A random point of pavement between two distances from a centre; with unseen, only one out of view.</summary>
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

        /// <summary>The direction the pavement runs at a point, along its nearest edge; false with no edge near enough.</summary>
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

        /// <summary>A shopfront to work at: an upright wall seen from the pavement with pavement a given distance in front, flat from waist to head and a person wide.</summary>
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
                // Measured from where the feet really are (the mesh may have moved them): one correction, and then the wall must be
                // the asked distance away to within five centimetres.
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
                                                  + face.collider.GetType().Name + " '" + face.collider.name + "', " + gap.ToString("F2", CultureInfo.InvariantCulture)
                                                  + " m from where the worker stands).");
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

        // The wall square on from a standing place: there at chest height, the same at head height and a forearm to either
        // side, and nothing below the waist further out than a plinth.
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

        /// <summary>Whether the camera is looking roughly towards a point near enough to notice someone appearing.</summary>
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

        /// <summary>The square of a flat distance (x and z): compare it with a reach squared, never with metres.</summary>
        public static float Flat(Vector3 offset)
        {
            return offset.x * offset.x + offset.z * offset.z;
        }

        private static bool Wall(Vector3 from, Vector3 direction, float height, float reach, out RaycastHit hit)
        {
            return Physics.Raycast(from + Vector3.up * height, direction, out hit, reach, _wallMask, QueryTriggerInteraction.Ignore);
        }

        // At arm's length from a wall, the wall itself must not count against the place.
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
