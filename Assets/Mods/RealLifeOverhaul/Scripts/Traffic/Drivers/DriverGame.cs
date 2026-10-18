using System;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Everything the city's drivers ask of the game: each pooled car measured once (its body, its nose, how far its sensor
    /// reaches past it), the lane point a car is driving to and the way its indicator says it turns, the player's car with the
    /// game's own parking room behind it, and the hold lines: two trigger boxes tagged "AiVehicleHalt" on the layer of the
    /// game's own parking room, which the traffic system holds its cars for by itself. Nothing on a car or a lane point is
    /// ever written. Cost: nothing per frame; a few cars measured a look until all are known.
    /// </summary>
    internal sealed class DriverGame
    {
        private const string HaltTag = "AiVehicleHalt";   // the one tag a trigger must carry for a traffic car to stop for it (VehicleComponent.OnTriggerEnter)
        private const int MeasuredAtOnce = 3;             // cars measured for the first time in one look: the rest wait for the next
        private const float LineDepth = 0.8f;             // metres a hold line's box runs along the lane
        private const float LineHeight = 1.4f;            // metres
        private const float ParkedAt = -500f;             // metres under the world: where a line is before its first use

        public static IModLogger Logger;

        // ---------------------------------------------------------------- the pool, measured once a car

        private object _pool;            // the traffic system the measurements belong to
        private Transform[] _at = new Transform[0];
        private float[] _middle = new float[0];       // metres from the car's origin forward to the middle of its body
        private float[] _halfLength = new float[0];
        private float[] _halfWidth = new float[0];
        private float[] _nose = new float[0];         // metres from its origin to its nose
        private float[] _sensor = new float[0];       // metres its sensor reaches past its nose
        private bool[] _known = new bool[0];
        private int _budget;
        private DrivingAI _driving;      // the traffic system's own drivers, for their indicators

        /// <summary>Makes room for the traffic system's cars, forgetting what was measured if it is another city's; true if it was.</summary>
        public bool Pool(TrafficManager traffic, int cars)
        {
            if (ReferenceEquals(traffic, _pool) && cars <= _known.Length)
            {
                return false;
            }
            _pool = traffic;
            _at = new Transform[cars];
            _middle = new float[cars];
            _halfLength = new float[cars];
            _halfWidth = new float[cars];
            _nose = new float[cars];
            _sensor = new float[cars];
            _known = new bool[cars];
            _driving = traffic != null ? traffic.GetComponent<DrivingAI>() : null;   // (once a city: the component that drives every car)
            return true;
        }

        /// <summary>A new look begins: a few more cars may be measured in it.</summary>
        public void BeginLook()
        {
            _budget = MeasuredAtOnce;
        }

        /// <summary>The car's transform, measured the first time it is seen; null if not measured yet and this look's budget is spent.</summary>
        public Transform At(int index, VehicleComponent car)
        {
            if (_known[index])
            {
                return _at[index];
            }
            if (_budget <= 0)
            {
                return null;
            }
            _budget--;
            Transform body = car.transform;
            TrafficTools.Footprint(car, out Vector3 centre, out _, out float halfLength, out float halfWidth);
            float middle = body.InverseTransformPoint(centre).z;
            _at[index] = body;
            _middle[index] = middle;
            _halfLength[index] = halfLength;
            _halfWidth[index] = halfWidth;
            _nose[index] = middle + halfLength;
            Transform trigger = car.frontTrigger;
            BoxCollider box = trigger != null ? trigger.GetComponentInChildren<BoxCollider>(true) : null;
            float reach = float.NaN;
            if (box != null)
            {
                // Measured in the sensor's own space: it is turned with the wheels, and its reach is not to depend on that.
                float axle = body.InverseTransformPoint(trigger.position).z;
                reach = axle + trigger.InverseTransformPoint(box.transform.TransformPoint(box.center + new Vector3(0f, 0f, box.size.z * 0.5f))).z;
            }
            _sensor[index] = DriverRules.Sensor(float.IsNaN(reach) ? -1f : reach - _nose[index]);
            _known[index] = true;
            return body;
        }

        public float Middle(int index) => _middle[index];
        public float HalfLength(int index) => _halfLength[index];
        public float HalfWidth(int index) => _halfWidth[index];
        public float Nose(int index) => _nose[index];
        public float SensorPast(int index) => _sensor[index];

        /// <summary>The lane point the traffic system is driving a car to; null if none.</summary>
        public static Waypoint Target(TrafficManager traffic, int index)
        {
            return traffic.GetTargetWaypoint(index);
        }

        /// <summary>Which way a car's indicator says it turns next: -1 left, 1 right, 0 neither. The traffic system plans a turn some 36 m ahead.</summary>
        public int Turning(int index)
        {
            if (_driving == null)
            {
                return 0;
            }
            BlinkType blink = _driving.GetBlinkType(index);
            return blink == BlinkType.BlinkLeft ? -1 : blink == BlinkType.BlinkRight ? 1 : 0;
        }

        /// <summary>True while the traffic system brakes or backs the car itself: its own stop is the one that counts then.</summary>
        public static bool GameStops(SpecialDriveActionTypes action)
        {
            return action == SpecialDriveActionTypes.StopInDistance || action == SpecialDriveActionTypes.StopNow || action == SpecialDriveActionTypes.TempStop
                   || action == SpecialDriveActionTypes.NoWaypoint || action == SpecialDriveActionTypes.Reverse || action == SpecialDriveActionTypes.AvoidReverse
                   || action == SpecialDriveActionTypes.AvoidForward || action == SpecialDriveActionTypes.Destroyed;
        }

        // ---------------------------------------------------------------- the player's car

        private VehicleController _vehicle;        // the car the player was last seen driving
        private Rigidbody _vehicleBody;
        private VehicleParkingHelper _parking;
        private BoxCollider _room;                 // the game's parking room behind it (its parallelParkingCollider)
        private float _vehicleMiddle, _vehicleHalfLength, _vehicleHalfWidth;

        /// <summary>The player's car while the player drives it; null on foot. Looked up once a car.</summary>
        public VehicleController PlayerCar(out Rigidbody body)
        {
            body = null;
            GameManager game = InstanceBehavior<GameManager>.Instance;
            VehicleController vehicle = game != null && PlayerHelper.IsUsingVehicle ? game.selectedVehicle : null;
            if (vehicle == null || !vehicle.controlledByPlayer || vehicle.vehicleCollider == null)
            {
                return null;
            }
            if (vehicle != _vehicle)
            {
                _vehicle = vehicle;
                _vehicleBody = vehicle.GetComponent<Rigidbody>();   // looked up once per vehicle
                _parking = vehicle.GetComponentInChildren<VehicleParkingHelper>(true);
                _room = _parking != null ? _parking.parallelParkingCollider as BoxCollider : null;
                MeasurePlayerCar(vehicle);
            }
            body = _vehicleBody;
            return body != null ? vehicle : null;
        }

        // Its body from the vehicle's own collider, in its own space: the game's cars disagree where their origin is.
        private void MeasurePlayerCar(VehicleController vehicle)
        {
            Transform frame = vehicle.transform;
            Collider shape = vehicle.vehicleCollider;
            Bounds local;
            Transform at = shape.transform;
            if (shape is BoxCollider cube)
            {
                local = new Bounds(cube.center, cube.size);
            }
            else if (shape is MeshCollider mesh && mesh.sharedMesh != null)
            {
                local = mesh.sharedMesh.bounds;
            }
            else
            {
                local = shape.bounds;
                at = null;
            }
            Vector3 front = frame.InverseTransformPoint(at != null ? at.TransformPoint(local.center + new Vector3(0f, 0f, local.extents.z)) : local.center + frame.forward * local.extents.z);
            Vector3 back = frame.InverseTransformPoint(at != null ? at.TransformPoint(local.center - new Vector3(0f, 0f, local.extents.z)) : local.center - frame.forward * local.extents.z);
            Vector3 side = at != null ? at.TransformVector(new Vector3(local.extents.x, 0f, 0f)) : new Vector3(local.extents.x, 0f, 0f);
            _vehicleMiddle = (front.z + back.z) * 0.5f;
            _vehicleHalfLength = Mathf.Clamp(Mathf.Abs(front.z - back.z) * 0.5f, 1f, 7f);
            _vehicleHalfWidth = Mathf.Clamp(side.magnitude, 0.6f, 1.6f);
        }

        public float PlayerMiddle => _vehicleMiddle;
        public float PlayerHalfLength => _vehicleHalfLength;
        public float PlayerHalfWidth => _vehicleHalfWidth;

        /// <summary>
        /// True while the game holds its own parking room behind the player's car (a free space was found and the car is
        /// slow), with that room's middle, way, half length and half width.
        /// </summary>
        public bool ParkingRoom(out Vector3 middle, out Vector3 forward, out float halfLength, out float halfWidth)
        {
            middle = default;
            forward = default;
            halfLength = 0f;
            halfWidth = 0f;
            if (_room == null || !_room.enabled || !_room.gameObject.activeInHierarchy)
            {
                return false;
            }
            Transform room = _room.transform;
            middle = room.TransformPoint(_room.center);
            forward = room.forward;
            Vector3 scale = room.lossyScale;
            halfLength = Mathf.Abs(_room.size.z * scale.z) * 0.5f;
            halfWidth = Mathf.Abs(_room.size.x * scale.x) * 0.5f;
            return halfLength > 0.5f;
        }

        /// <summary>True if the game has found a free space for the player's car to park in (its auto-park offer).</summary>
        public bool SpaceOffered => _parking != null && _parking.availableAutoParkSpot != null;

        // ---------------------------------------------------------------- the hold lines

        private readonly GameObject[] _line = new GameObject[2];
        private readonly BoxCollider[] _lineBox = new BoxCollider[2];
        private readonly bool[] _lineOut = new bool[2];
        private bool _linesMade;
        private bool _linesFailed;
        private int _lineLayer = -1;

        /// <summary>True if the hold lines can be used: made, or makeable, on a layer the cars stop for.</summary>
        public bool LinesUsable => !_linesFailed;

        /// <summary>The layer the hold lines go on: the game's own parking room's, else "Vehicles" (what the cars sense: the player's cars).</summary>
        private int LineLayer()
        {
            if (_lineLayer >= 0)
            {
                return _lineLayer;
            }
            if (_room != null)
            {
                _lineLayer = _room.gameObject.layer;
            }
            else
            {
                _lineLayer = LayerMask.NameToLayer("Vehicles");
            }
            return _lineLayer;
        }

        // The two, made once. False (and the lines are the mod's own braking alone) if the game refuses the tag or has no such layer.
        private bool MakeLines(Transform parent)
        {
            if (_linesMade)
            {
                return true;
            }
            if (_linesFailed)
            {
                return false;
            }
            try
            {
                int layer = LineLayer();
                if (layer < 0)
                {
                    throw new InvalidOperationException("the game has no layer for the player's cars any more");
                }
                for (int i = 0; i < _line.Length; i++)
                {
                    var line = new GameObject("RealLifeOverhaul Hold Line " + (i + 1));
                    _line[i] = line;
                    line.transform.SetParent(parent, false);   // (a parent: some of the game's triggers ask whose child a collider is)
                    line.transform.position = new Vector3(0f, ParkedAt, 0f);
                    line.layer = layer;
                    line.tag = HaltTag;   // throws if the game has no such tag any more
                    Rigidbody body = line.AddComponent<Rigidbody>();
                    body.isKinematic = true;
                    body.useGravity = false;
                    BoxCollider box = line.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.center = new Vector3(0f, LineHeight * 0.5f, 0f);
                    box.size = new Vector3(2.4f, LineHeight, LineDepth);
                    _lineBox[i] = box;
                    line.SetActive(false);
                }
                _linesMade = true;
                Logger?.Info($"City drivers: {_line.Length} hold lines made on layer '{LayerMask.LayerToName(_lineLayer)}' (triggers tagged '{HaltTag}', as the game's own "
                             + "parking room is): made once, switched off until needed.");
                return true;
            }
            catch (Exception e)
            {
                Logger?.Warn("City drivers: the hold lines could not be made (" + e.Message + "); a car is held for the player by the mod's own braking alone.");
                DestroyLines();
                _linesFailed = true;
                return false;
            }
        }

        /// <summary>Puts a hold line across a lane: its middle on the road, the way the lane runs, as wide as the lane. False if lines cannot be used.</summary>
        public bool PlaceLine(int slot, Vector3 middle, Vector3 forward, float width, Transform parent)
        {
            if (!MakeLines(parent))
            {
                return false;
            }
            Transform line = _line[slot].transform;
            forward.y = 0f;
            line.SetPositionAndRotation(middle, forward.sqrMagnitude > 0.01f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity);
            _lineBox[slot].size = new Vector3(width, LineHeight, LineDepth);
            if (!_lineOut[slot])
            {
                _lineOut[slot] = true;
                _line[slot].SetActive(true);
            }
            return true;
        }

        /// <summary>Takes a hold line off the road: the traffic system is told it has gone (its cars drive on), and it is switched off.</summary>
        public void ParkLine(int slot)
        {
            if (!_linesMade || !_lineOut[slot])
            {
                return;
            }
            _lineOut[slot] = false;
            if (_line[slot] == null)
            {
                return;   // destroyed with the mod's root object
            }
            if (TrafficManager.HasInstance && TrafficManager.IsInitialized)
            {
                Manager.TriggerColliderRemovedEvent(_lineBox[slot]);
            }
            _line[slot].SetActive(false);
        }

        /// <summary>True while that hold line stands on the road.</summary>
        public bool LineOut(int slot) => _linesMade && _lineOut[slot];

        /// <summary>The mod is closing: the lines go with it, the traffic told first.</summary>
        public void DestroyLines()
        {
            for (int i = 0; i < _line.Length; i++)
            {
                if (_line[i] != null)
                {
                    if (_lineOut[i] && TrafficManager.HasInstance && TrafficManager.IsInitialized && _lineBox[i] != null)
                    {
                        Manager.TriggerColliderRemovedEvent(_lineBox[i]);
                    }
                    Object.Destroy(_line[i]);
                }
                _line[i] = null;
                _lineBox[i] = null;
                _lineOut[i] = false;
            }
            _linesMade = false;
        }

        // ---------------------------------------------------------------- lanes

        /// <summary>The lane point a vehicle drives along here: the nearest whose lane runs its way, within a few metres. Two questions to the lane grid.</summary>
        public static Waypoint LaneAt(Vector3 position, Vector3 forward)
        {
            return TrafficTools.ClosestWaypointAlong(position, forward * 2f, 7f);
        }

        /// <summary>The way a lane runs at a point, flat and of unit length; zero at a point that leads nowhere.</summary>
        public static Vector3 LaneWay(TrafficManager traffic, Waypoint point)
        {
            Vector3 way = Vector3.zero;
            if (point.neighbors != null && point.neighbors.Count > 0)
            {
                Waypoint next = traffic.GetWaypoint(point.neighbors[0]);
                way = next != null ? next.position - point.position : Vector3.zero;
            }
            else if (point.prev != null && point.prev.Count > 0)
            {
                Waypoint before = traffic.GetWaypoint(point.prev[0]);
                way = before != null ? point.position - before.position : Vector3.zero;
            }
            way.y = 0f;
            return way.sqrMagnitude < 0.01f ? Vector3.zero : way.normalized;
        }

        /// <summary>Lanes running the same way on each side of a lane point (its other lanes), for whether it is a kerb-side lane.</summary>
        public static void SideLanes(TrafficManager traffic, Waypoint point, Vector3 way, out int left, out int right)
        {
            left = 0;
            right = 0;
            if (point.otherLanes == null)
            {
                return;
            }
            Vector3 toRight = Vector3.Cross(Vector3.up, way);
            for (int i = 0; i < point.otherLanes.Count; i++)
            {
                Waypoint other = traffic.GetWaypoint(point.otherLanes[i]);
                if (other == null)
                {
                    continue;
                }
                float side = Vector3.Dot(other.position - point.position, toRight);
                if (side > 0.5f)
                {
                    right++;
                }
                else if (side < -0.5f)
                {
                    left++;
                }
            }
        }

        /// <summary>The lane point at least so many metres back along a lane from a point (behind it), while it has one way in; null if it forks or ends first.</summary>
        public static Waypoint PointBack(TrafficManager traffic, Waypoint from, float metres)
        {
            Waypoint point = from;
            float walked = 0f;
            for (int guard = 0; guard < 40 && walked < metres; guard++)
            {
                if (point.prev == null || point.prev.Count != 1)
                {
                    return null;
                }
                Waypoint before = traffic.GetWaypoint(point.prev[0]);
                if (before == null || before.temporaryDisabled)
                {
                    return null;
                }
                walked += Vector3.Distance(point.position, before.position);
                point = before;
            }
            return walked >= metres ? point : null;
        }

        /// <summary>
        /// The place on a lane so many metres back from a point along it (behind it), walking back while the lane has one way
        /// in; false if it forks, ends or is closed first.
        /// </summary>
        public static bool BackAlong(TrafficManager traffic, Waypoint from, Vector3 start, float metres, out Vector3 place, out Vector3 way)
        {
            place = start;
            way = LaneWay(traffic, from);
            Waypoint point = from;
            Vector3 at = start;
            float left = metres;
            for (int guard = 0; guard < 12 && left > 0f; guard++)
            {
                if (point.prev == null || point.prev.Count != 1)
                {
                    return false;
                }
                Waypoint before = traffic.GetWaypoint(point.prev[0]);
                if (before == null || before.temporaryDisabled)
                {
                    return false;
                }
                Vector3 leg = before.position - at;
                leg.y = 0f;
                float length = leg.magnitude;
                if (length >= left)
                {
                    place = at + leg * (left / Mathf.Max(0.01f, length));
                    place.y = Mathf.Lerp(at.y, before.position.y, left / Mathf.Max(0.01f, length));
                    way = length > 0.01f ? -leg / length : way;
                    return true;
                }
                left -= length;
                at = before.position;
                point = before;
                way = length > 0.01f ? -leg / length : way;
            }
            place = at;
            return left <= 0f;
        }
    }
}
