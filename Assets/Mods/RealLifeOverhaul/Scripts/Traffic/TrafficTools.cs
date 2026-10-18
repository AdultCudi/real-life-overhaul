using System;
using System.Collections.Generic;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the accident scenes, the response units and the pursuits need from the game's traffic system: whether it runs,
    /// routes along the road graph, the nearest lane point, and cars taken and handed back. Called on events, never per frame,
    /// but for the cheap state checks.
    /// </summary>
    public static class TrafficTools
    {
        private static readonly HashSet<int> Visited = new HashSet<int>();
        private static int _vehicleMask;
        private static int _peopleMask;

        public static IModLogger Logger;

        /// <summary>True while the traffic runs here and its cars are this machine's to take: not indoors, not fast-forwarded, not a Going Public guest.</summary>
        public static bool Ready => Street && !GoingPublic.TrafficMirrored;

        /// <summary>True while the player is out on a street the game is running, whoever's the cars: never for taking or driving one.</summary>
        public static bool Street =>
            TrafficManager.HasInstance && TrafficManager.IsInitialized && !BuildingManager.IsInsideBuilding
            && TrafficManager.Instance.enabled;

        /// <summary>True while out on a street whose traffic the game has stopped for its fast forward: the road graph is there, nothing drives.</summary>
        public static bool Paused =>
            TrafficManager.HasInstance && TrafficManager.IsInitialized && !BuildingManager.IsInsideBuilding
            && !TrafficManager.Instance.enabled && !GoingPublic.TrafficMirrored;

        /// <summary>Layers that hold vehicles of any kind; 0 if the game's layer names ever change.</summary>
        public static int VehicleMask
        {
            get
            {
                if (_vehicleMask == 0)
                {
                    _vehicleMask = LayerMask.GetMask("AiVehicles", "ParkedVehicles", "PlayerVehicles", "Vehicles");
                }
                return _vehicleMask;
            }
        }

        /// <summary>The layer ordinary traffic (and every vehicle the mod drives) is on; 0 if the game's layer name ever changes.</summary>
        public static int TrafficMask
        {
            get
            {
                if (_trafficMask == 0)
                {
                    _trafficMask = LayerMask.GetMask("AiVehicles");
                }
                return _trafficMask;
            }
        }

        private static int _trafficMask;

        /// <summary>Layers that hold people with a collider: pedestrians, the player on foot, a body traffic brakes for.</summary>
        public static int PeopleMask
        {
            get
            {
                if (_peopleMask == 0)
                {
                    _peopleMask = LayerMask.GetMask("Human", "Player");
                }
                return _peopleMask;
            }
        }

        /// <summary>True for the game's ambulance: only ever a response vehicle, never in a crash or anyone's suspect.</summary>
        public static bool IsAmbulance(VehicleComponent car)
        {
            return car != null && car.prefab != null && car.prefab.name.IndexOf("ambulance", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>True while a car the mod took from the traffic system is still ours: the pool hands removed cars out again, no longer ignored.</summary>
        public static bool IsHeld(VehicleComponent car)
        {
            return car != null && car.gameObject.activeInHierarchy && TrafficManager.HasInstance
                   && TrafficManager.Instance.IsVehicleIgnored(car.GetIndex());
        }

        private static readonly List<Collider> BodyScratch = new List<Collider>(16);

        /// <summary>The ground a vehicle really covers, from its colliders: its body's middle, the way it points, half its length and width.</summary>
        public static void Footprint(VehicleComponent car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth)
        {
            Transform body = car.transform;
            forward = body.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = body.up;   // standing on its nose
                forward.y = 0f;
            }
            forward = forward.sqrMagnitude < 0.01f ? Vector3.forward : forward.normalized;
            centre = car.rb != null ? car.rb.worldCenterOfMass : body.position;
            halfLength = Mathf.Max(1.5f, car.length * 0.5f);
            halfWidth = Mathf.Max(0.8f, car.colliderWidth * 0.5f);

            car.GetComponentsInChildren(false, BodyScratch);
            bool any = false;
            var box = default(Bounds);
            for (int i = 0; i < BodyScratch.Count; i++)
            {
                Collider collider = BodyScratch[i];
                if (collider == null || collider.isTrigger || collider is WheelCollider)
                {
                    continue;
                }
                Bounds own;
                Transform at = collider.transform;
                if (collider is BoxCollider cube)
                {
                    own = new Bounds(cube.center, cube.size);
                }
                else if (collider is MeshCollider shape && shape.sharedMesh != null)
                {
                    own = shape.sharedMesh.bounds;
                }
                else
                {
                    own = collider.bounds;   // already in the world
                    at = null;
                }
                Vector3 min = own.min;
                Vector3 max = own.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3((corner & 1) == 0 ? min.x : max.x, (corner & 2) == 0 ? min.y : max.y, (corner & 4) == 0 ? min.z : max.z);
                    Vector3 local = body.InverseTransformPoint(at != null ? at.TransformPoint(point) : point);
                    if (any)
                    {
                        box.Encapsulate(local);
                    }
                    else
                    {
                        box = new Bounds(local, Vector3.zero);
                        any = true;
                    }
                }
            }
            BodyScratch.Clear();
            // On its side or its roof, what is across the road is the car's height or width as it lies.
            bool onSide = Mathf.Abs(body.up.y) < 0.5f;
            float length = box.extents.z;
            float width = onSide ? box.extents.y : box.extents.x;
            if (any && length > 1f && length < 8f && width > 0.5f && width < 2.5f)
            {
                centre = body.TransformPoint(box.center);
                halfLength = length;
                halfWidth = width;
            }
        }

        /// <summary>Where a vehicle's axles are along it, from its wheel meshes (the game's vehicles disagree where their origin is). False if unknown.</summary>
        /// <param name="renderers">The vehicle's renderers, as the caller has gathered them already.</param>
        public static bool Axles(VehicleComponent car, List<Renderer> renderers, out float rear, out float front)
        {
            rear = float.MaxValue;
            front = float.MinValue;
            Transform body = car.transform;
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer is MeshRenderer && renderer.name.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    float along = body.InverseTransformPoint(renderer.bounds.center).z;
                    rear = Mathf.Min(rear, along);
                    front = Mathf.Max(front, along);
                }
            }
            return front - rear > 1.5f && front - rear < 9f;
        }

        /// <summary>Walks the road graph backwards from a waypoint; the route runs from the far end to the anchor, so it can be driven.</summary>
        /// <returns>The length walked, in metres.</returns>
        public static float WalkBack(Waypoint anchor, float length, int maxPoints, List<Waypoint> route)
        {
            float walked = Walk(anchor, length, maxPoints, route, backwards: true);
            route.Reverse();
            return walked;
        }

        /// <summary>Walks the road graph forwards from a waypoint, anchor first, taking a random turn at each junction.</summary>
        /// <returns>The length walked, in metres.</returns>
        public static float WalkForward(Waypoint anchor, float length, int maxPoints, List<Waypoint> route)
        {
            return Walk(anchor, length, maxPoints, route, backwards: false);
        }

        private static float Walk(Waypoint anchor, float length, int maxPoints, List<Waypoint> route, bool backwards)
        {
            route.Clear();
            if (anchor == null || !TrafficManager.HasInstance)
            {
                return 0f;
            }
            TrafficManager traffic = TrafficManager.Instance;
            Visited.Clear();
            Visited.Add(anchor.listIndex);
            route.Add(anchor);

            Waypoint current = anchor;
            float walked = 0f;
            while (walked < length && route.Count < maxPoints)
            {
                List<int> links = backwards ? current.prev : current.neighbors;
                if (links == null || links.Count == 0)
                {
                    break;
                }
                int start = Random.Range(0, links.Count);
                Waypoint next = null;
                for (int i = 0; i < links.Count && next == null; i++)
                {
                    int index = links[(start + i) % links.Count];
                    if (Visited.Add(index))
                    {
                        next = traffic.GetWaypoint(index);
                    }
                }
                if (next == null)
                {
                    break;
                }
                walked += Vector3.Distance(current.position, next.position);
                route.Add(next);
                current = next;
            }
            return walked;
        }

        /// <summary>The nearest waypoint whose lane runs roughly the given way, else simply the nearest. Two questions to the lane grid.</summary>
        public static Waypoint ClosestWaypointAlong(Vector3 position, Vector3 direction, float maxDistance)
        {
            direction.y = 0f;
            Waypoint found = null;
            if (direction.sqrMagnitude > 1f)
            {
                _laneDirection = direction.normalized;
                found = ClosestWaypoint(position, maxDistance, RunsAlongLane);
            }
            return found ?? ClosestWaypoint(position, maxDistance);
        }

        private const float LaneCell = 20f;   // metres a cell of the lane grid is wide: a question within 40 m covers some 25 cells
        private static readonly List<int> NearScratch = new List<int>(512);
        private static Waypoint[] _laneGridOf;   // the lane points the grid was made over (the city's road data), null before
        private static PointGrid _laneGrid;
        private static bool _laneAsking;         // a question is under way (a filter asking again is answered by the traffic system itself)

        /// <summary>
        /// The lane point nearest a position, within maxDistance, that the filter accepts: exactly what the traffic system's
        /// GetClosestWaypoint answers, which goes through every lane point of the city (some 19,000) each time. Here only the
        /// points of a grid's cells round the position are gone through (<see cref="PointGrid"/>), in the traffic system's
        /// order and with its test, so the same point comes back and the filter sees the same points. Until the grid is made
        /// (<see cref="PrewarmLaneGrid"/>), for another city, or asked from inside a filter, the traffic system answers.
        /// </summary>
        public static Waypoint ClosestWaypoint(Vector3 position, float maxDistance = 50f, Predicate<Waypoint> filter = null)
        {
            if (!TrafficManager.HasInstance)
            {
                return null;
            }
            TrafficManager traffic = TrafficManager.Instance;
            Waypoint[] all = _laneGridOf;
            if (all != null && !_laneAsking && TrafficManager.IsInitialized && !SameLanes(traffic, all))
            {
                _laneGridOf = null;   // another city's lane points: the traffic system answers until the next grid is made
                _laneGrid = null;
                all = null;
            }
            if (all == null || _laneAsking || !TrafficManager.IsInitialized)
            {
                return traffic.GetClosestWaypoint(position, maxDistance, filter);
            }
            _laneAsking = true;
            try
            {
                _laneGrid.Candidates(position.x, position.z, Mathf.Abs(maxDistance), NearScratch);   // (the traffic system squares the distance, so its sign is nothing)
                // The traffic system's own loop (GleyTrafficSystem.DensityManager.GetClosestWaypoint), over the grid's points.
                float nearest = maxDistance * maxDistance;
                Waypoint result = null;
                for (int k = 0; k < NearScratch.Count; k++)
                {
                    Waypoint waypoint = all[NearScratch[k]];
                    float distance = Vector3.SqrMagnitude(position - waypoint.position);
                    if (distance >= nearest || (filter != null && !filter(waypoint)))
                    {
                        continue;
                    }
                    result = waypoint;
                    nearest = distance;
                }
                return result;
            }
            finally
            {
                NearScratch.Clear();
                _laneAsking = false;
            }
        }

        // The grid is over the traffic system's own points: its first and last lane points are the very objects.
        private static bool SameLanes(TrafficManager traffic, Waypoint[] all)
        {
            try
            {
                return all.Length > 0 && ReferenceEquals(traffic.GetWaypoint(0), all[0]) && ReferenceEquals(traffic.GetWaypoint(all.Length - 1), all[all.Length - 1]);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Behind the loading screen: the lane grid over this city's lane points, about half a millisecond. False until there is road data.</summary>
        public static bool PrewarmLaneGrid()
        {
            Waypoint[] all = RoadData.Points;
            if (all == null || all.Length == 0 || !TrafficManager.HasInstance || !TrafficManager.IsInitialized)
            {
                return false;
            }
            if (ReferenceEquals(all, _laneGridOf))
            {
                return true;
            }
            if (!SameLanes(TrafficManager.Instance, all))
            {
                _laneGridOf = null;
                _laneGrid = null;
                Logger?.Warn("Traffic: the road data the mod found is not the traffic system's own; the nearest lane point is asked of the traffic system itself.");
                return true;
            }
            var xs = new float[all.Length];
            var zs = new float[all.Length];
            for (int i = 0; i < all.Length; i++)
            {
                Vector3 at = all[i].position;
                xs[i] = at.x;
                zs[i] = at.z;
            }
            _laneGrid = new PointGrid(xs, zs, LaneCell);
            _laneGridOf = all;
            return true;
        }

        // Kept as a field with its argument beside it, so asking does not allocate a closure.
        private static readonly Predicate<Waypoint> RunsAlongLane = IsAlongLane;
        private static Vector3 _laneDirection;

        private static bool IsAlongLane(Waypoint waypoint)
        {
            if (waypoint.neighbors == null || waypoint.neighbors.Count == 0)
            {
                return false;
            }
            Waypoint next = TrafficManager.Instance.GetWaypoint(waypoint.neighbors[0]);
            if (next == null)
            {
                return false;
            }
            Vector3 lane = next.position - waypoint.position;
            lane.y = 0f;
            return Vector3.Dot(lane.normalized, _laneDirection) > 0.3f;
        }

        /// <summary>
        /// Hands a held car back to the traffic AI, pointed at the road ahead, and logs the state it went back in. What its holder
        /// left is put right first: not kinematic, no empty scripted route (a marker of ours that stops the game clearing it), and
        /// standing still; or, with carry, moving along its lane: the traffic system builds each step's velocity on the body's own
        /// (TrafficManager.FixedUpdate), so it drives on. Only for a car with clear road ahead: its action list is emptied.
        /// </summary>
        /// <param name="by">Who is letting it go, for the log.</param>
        public static void ReturnToTraffic(VehicleComponent car, string by = "a scene", Vector3 carry = default)
        {
            try
            {
                TrafficManager traffic = TrafficManager.Instance;
                Manager.SetVehicleIgnored(car.GetIndex(), false);
                Free(car);

                // The car was moved by hand, so point it at the road ahead of where it is now.
                Waypoint closest = ClosestWaypoint(car.transform.position, 40f);
                if (closest != null)
                {
                    Waypoint ahead = closest.neighbors != null && closest.neighbors.Count > 0
                        ? traffic.GetWaypoint(closest.neighbors[0])
                        : closest;
                    traffic.SetVehicleTargetWaypoint(car, ahead);
                }
                traffic.ForceSetVehicleActiveAction(car, SpecialDriveActionTypes.Forward);
                carry.y = 0f;
                if (carry.sqrMagnitude > 1f && car.rb != null)
                {
                    car.rb.velocity = carry;
                    Touch(car, "handed back to traffic on the move");
                    LogRelease(car, "handed back to traffic at " + (carry.magnitude * 3.6f).ToString("F0") + " km/h", by);
                    return;
                }
                Touch(car, "handed back to traffic");
                LogRelease(car, "handed back to traffic", by);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Takes a car off the road and back into the pool, in a state the pool can hand out again, and logs it.</summary>
        /// <param name="by">Who is taking it off, for the log.</param>
        public static void Remove(VehicleComponent car, string by = "a scene")
        {
            try
            {
                Manager.SetVehicleIgnored(car.GetIndex(), false);
                Free(car);   // the pool does not reset a body: a car put away kinematic would come back frozen
                Touch(car, "taken off the road");
                LogRelease(car, "taken off the road", by);
                Manager.RemoveVehicle(car.GetIndex());
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static readonly int HotLoad = HotSpots.Slot("Traffic: TrafficTools.Load");
        /// <summary>An idle car of that prefab put on the road at a lane point (TrafficManager.LoadVehicle); null with none idle, as the game would take one in use.</summary>
        public static VehicleComponent Load(GameObject prefab, Waypoint at)
        {
            using var hotSpot = HotSpots.Time(HotLoad);
            if (prefab == null || at == null || !TrafficManager.HasInstance)
            {
                return null;
            }
            if (WhoseCar.FreeSlot(Manager.GetVehicleList(), prefab, PrefabOf, Active) < 0)
            {
                return null;
            }
            return TrafficManager.Instance.LoadVehicle(prefab, at);
        }

        // (Made once, so that asking allocates nothing.)
        private static readonly Func<VehicleComponent, object> PrefabOf = car => car != null ? car.prefab : null;
        private static readonly Func<VehicleComponent, bool> Active = car => car == null || car.gameObject.activeInHierarchy;

        /// <summary>True for a traffic car spoken for, which the mod's scenes leave alone: a taxi, or a private driver's car at work.</summary>
        public static bool SpokenFor(VehicleComponent car)
        {
            return car != null && (car.GetComponent<TaxiController>() != null || car.GetComponent<PrivateDriverVehicle>() != null);
        }

        // What a car must not keep once nobody of ours holds it.
        private static void Free(VehicleComponent car)
        {
            if (car.presetPath != null && car.presetPath.Count == 0)
            {
                car.presetPath = null;
            }
            Rigidbody body = car.rb;
            if (body == null)
            {
                return;
            }
            if (body.isKinematic)
            {
                body.isKinematic = false;
            }
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private static void LogRelease(VehicleComponent car, string what, string by)
        {
            Logger?.Info("Traffic: '" + Name(car) + "' " + what + " (" + by + "): " + State(car) + ".");
        }

        /// <summary>Sounds a car's horn through the game's own AiCarHorn.Hail, given back its place in the world (the game plays it from nowhere).</summary>
        public static void Hoot(AiCarHorn horn)
        {
            if (horn == null)
            {
                return;
            }
            horn.Hail(false);
            AudioSource source = horn.GetComponentInChildren<AudioSource>();
            if (source != null)
            {
                source.spatialBlend = 1f;
            }
        }

        /// <summary>A vehicle's name for the log.</summary>
        public static string Name(VehicleComponent car)
        {
            return car == null ? "nothing" : car.prefab != null ? car.prefab.name : car.name;
        }

        /// <summary>Kinematic flag, ignored flag and scripted route of a vehicle, for a log line (builds a string: on a transition only).</summary>
        public static string State(VehicleComponent car)
        {
            Rigidbody body = car.rb;
            List<int> route = car.presetPath;
            return "kinematic=" + (body != null && body.isKinematic) + ", ignored by traffic="
                   + (TrafficManager.HasInstance && TrafficManager.Instance.IsVehicleIgnored(car.GetIndex()))
                   + ", scripted route=" + (route == null ? "none" : route.Count == 0 ? "empty (a marker of the mod's)" : route.Count + " point(s)");
        }

        // What the mod last did with each pooled vehicle, for TrafficWatch's report: constants, so two array writes.
        private static string[] _touch = new string[0];
        private static float[] _touchAt = new float[0];

        /// <summary>Notes what the mod has just done with a traffic vehicle. Call on transitions only, with a constant.</summary>
        public static void Touch(VehicleComponent car, string what)
        {
            int index = car != null ? car.GetIndex() : -1;
            if (index < 0)
            {
                return;
            }
            if (index >= _touch.Length)
            {
                int size = Mathf.Max(index + 1, TrafficManager.HasInstance ? Manager.GetVehicleList().Count : 0);
                Array.Resize(ref _touch, size);
                Array.Resize(ref _touchAt, size);
            }
            _touch[index] = what;
            _touchAt[index] = Time.time;
        }

        /// <summary>What the mod last did with this vehicle and how many seconds ago; null if it never touched it.</summary>
        public static string LastTouch(VehicleComponent car, out float secondsAgo)
        {
            int index = car != null ? car.GetIndex() : -1;
            if (index < 0 || index >= _touch.Length || _touch[index] == null)
            {
                secondsAgo = 0f;
                return null;
            }
            secondsAgo = Time.time - _touchAt[index];
            return _touch[index];
        }

        /// <summary>Stops the game deleting a car for standing still. Returns the check to switch back on, or null.</summary>
        public static AiCarRescueCheck SuspendRescue(VehicleComponent car)
        {
            var rescue = car.GetComponentInChildren<AiCarRescueCheck>();
            if (rescue == null || !rescue.enabled)
            {
                return null;
            }
            rescue.enabled = false;
            return rescue;
        }
    }
}
