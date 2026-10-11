using System;
using System.Collections.Generic;
using BAModAPI;
using GleyTrafficSystem;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the accident scene, the response units and the player pursuit all need from the
    /// game's traffic system: whether it is running, routes along the road graph, and handing
    /// cars back. Everything here is called on events (a dispatch, a release), never per frame,
    /// except the two cheap state checks.
    /// </summary>
    public static class TrafficTools
    {
        private static readonly HashSet<int> Visited = new HashSet<int>();
        private static int _vehicleMask;
        private static int _peopleMask;

        public static IModLogger Logger;

        /// <summary>
        /// True while the traffic system is simulating. It is switched off (and all ordinary
        /// traffic removed) inside buildings and while time is fast-forwarded.
        /// </summary>
        public static bool Ready =>
            TrafficManager.HasInstance && TrafficManager.IsInitialized && !BuildingManager.IsInsideBuilding
            && TrafficManager.Instance.enabled;

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

        /// <summary>
        /// True for the game's ambulance. It is only ever a response vehicle: never a car in a
        /// crash, a hit-and-run driver's car or anyone's suspect.
        /// </summary>
        public static bool IsAmbulance(VehicleComponent car)
        {
            return car != null && car.prefab != null && car.prefab.name.IndexOf("ambulance", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// True while a car this mod took from the traffic system is still ours. The pool hands
        /// cars out again after removing them, and a car that comes back is no longer ignored by
        /// the traffic AI, so a lost car must not be driven or moved any more.
        /// </summary>
        public static bool IsHeld(VehicleComponent car)
        {
            return car != null && car.gameObject.activeInHierarchy && TrafficManager.HasInstance
                   && TrafficManager.Instance.IsVehicleIgnored(car.GetIndex());
        }

        private static readonly List<Collider> BodyScratch = new List<Collider>(16);

        /// <summary>
        /// The ground a vehicle really covers, from its own colliders: the middle of its body in the
        /// world, the way it points along the ground, and half its length and width. A car's
        /// nominal length and its centre of mass are not that (a rear-engined car's middle is well
        /// ahead of its centre of mass), and a cordon or a cut in the navigation mesh placed by
        /// them leaves an end of the car sticking out. Falls back on the nominal size if the
        /// colliders say nothing sensible. For one-off use (a scene being set up), not per frame.
        /// </summary>
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

        /// <summary>
        /// Where a vehicle's axles are along its own forward axis, in its own space, read from its
        /// wheel meshes (the game's vehicles do not agree on where their origin is: the vans have it
        /// under the middle of the body, the trucks under the rear axle). For a modelled body that
        /// stands over a vehicle with wheels of its own: put its axles on these and its wheels turn
        /// about the points the vehicle really turns about. False if the wheels cannot be told.
        /// </summary>
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

        /// <summary>
        /// Walks the road graph backwards from a waypoint. The result runs from the far end to
        /// the anchor, so a vehicle put on route[0] can always drive it.
        /// </summary>
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

        /// <summary>
        /// The waypoint nearest a position whose lane runs roughly the given way, or simply the
        /// nearest one if no lane does (or no direction is given). Scans the whole road graph:
        /// for one-off use only.
        /// </summary>
        public static Waypoint ClosestWaypointAlong(Vector3 position, Vector3 direction, float maxDistance)
        {
            TrafficManager traffic = TrafficManager.Instance;
            direction.y = 0f;
            Waypoint found = null;
            if (direction.sqrMagnitude > 1f)
            {
                _laneDirection = direction.normalized;
                found = traffic.GetClosestWaypoint(position, maxDistance, RunsAlongLane);
            }
            return found ?? traffic.GetClosestWaypoint(position, maxDistance);
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
        /// Hands a held car back to the traffic AI, pointed at the road ahead of where it now is, and says
        /// in the log (one line) what state it went back in. Whatever its holder left on it is put right
        /// here first: a car goes back able to move (not kinematic), without an empty scripted route (only
        /// ever a marker of ours; with one the game never clears a stop or takes the car off the road) and
        /// standing still, so that the traffic system starts it from rest.
        /// It can also go back on the move (<paramref name="carry"/>). The traffic system keeps no speed of
        /// its own for a car: every physics step it reads the body's velocity and builds the next one on it
        /// (TrafficManager.FixedUpdate fills vehicleVelocity from the rigidbodies; DriveJob.Execute starts from
        /// its length), and it gives a car it has just put on the road a velocity itself (NewVehicleAdded). So
        /// a car handed back with a velocity along its lane simply drives on at that pace, and is slowed with
        /// ordinary braking if that is over its lane's limit. Only a car with clear road ahead may go back
        /// this way: the action list is emptied below, and with it anything its sensor knew.
        /// </summary>
        /// <param name="by">Who is letting it go, for the log.</param>
        /// <param name="carry">The velocity it keeps, flat along its lane; zero (the default) to go back standing.</param>
        public static void ReturnToTraffic(VehicleComponent car, string by = "a scene", Vector3 carry = default)
        {
            try
            {
                TrafficManager traffic = TrafficManager.Instance;
                Manager.SetVehicleIgnored(car.GetIndex(), false);
                Free(car);

                // The car was moved by hand, so point it at the road ahead of where it is now.
                Waypoint closest = traffic.GetClosestWaypoint(car.transform.position, 40f);
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

        /// <summary>
        /// Takes a car off the road and back into the pool (towed, or no longer needed), in a state the
        /// pool can hand out again, and says so in the log (one line).
        /// </summary>
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

        // What the mod last did with each pooled vehicle, by its index in the pool: for TrafficWatch's report.
        // The labels are constants, so remembering one costs two array writes.
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
