using System;
using System.Collections.Generic;
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
    /// game's own parking room, which the traffic system holds its cars for by itself, made behind the loading screen. Nothing on a car or a lane point is
    /// ever written. Cost: nothing per frame; a few cars measured a look until all are known.
    /// </summary>
    internal sealed class DriverGame
    {
        private const string HaltTag = "AiVehicleHalt";   // the one tag a trigger must carry for a traffic car to stop for it (VehicleComponent.OnTriggerEnter)
        private const float LineDepth = 0.8f;             // metres a hold line's box runs along the lane
        private const float LineHeight = 1.4f;            // metres
        private const float ParkedAt = -500f;             // metres under the world: where a line is before its first use

        public static IModLogger Logger;

        /// <summary>The look's clock stopped and started again around the lines this writes (CityDrivers' own), so a look costs what it costs without them.</summary>
        public Action Quiet, Loud;

        // ---------------------------------------------------------------- the pool, measured once a car

        private object _pool;            // the traffic system the measurements belong to
        private Transform[] _at = new Transform[0];
        private float[] _middle = new float[0];       // metres from the car's origin forward to the middle of its body
        private float[] _halfLength = new float[0];
        private float[] _halfWidth = new float[0];
        private float[] _nose = new float[0];         // metres from its origin to its nose
        private float[] _sensor = new float[0];       // metres its sensor reaches past its nose
        private bool[] _known = new bool[0];
        private bool[] _wanted = new bool[0];     // seen in a look before it was measured: measured in a frame of its own
        private int _waiting;                     // how many are
        private float _waitingSince;              // when the first of them was seen
        private readonly Dictionary<int, int> _byKind = new Dictionary<int, int>(32);   // a car's kind (its prefab) and an upright car of it measured

        /// <summary>The minute's figures for its line: cars measured outside the looks, cars that took their kind's figures, the slowest measure.</summary>
        public int MeasuredOut, CopiedByKind;
        public long MeasureWorst;
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
            _wanted = new bool[cars];
            _waiting = 0;
            _byKind.Clear();
            _driving = traffic != null ? traffic.GetComponent<DrivingAI>() : null;   // (once a city: the component that drives every car)
            return true;
        }

        /// <summary>
        /// The car's transform once it is measured; null until then. An upright car of a kind already measured upright (the same
        /// prefab, so the same shape in its own space) takes that one's figures at once; any other is measured in a frame of its
        /// own (<see cref="MeasureWaiting"/>), never inside a look.
        /// </summary>
        public Transform At(int index, VehicleComponent car)
        {
            if (_known[index])
            {
                return _at[index];
            }
            int kind = Kind(car);
            if (kind != 0 && _byKind.TryGetValue(kind, out int like) && like < _known.Length && _known[like])
            {
                Transform own = car.transform;
                if (Mathf.Abs(own.up.y) >= 0.5f)   // (one first seen on its side is measured as it lies, as ever)
                {
                    _at[index] = own;
                    _middle[index] = _middle[like];
                    _halfLength[index] = _halfLength[like];
                    _halfWidth[index] = _halfWidth[like];
                    _nose[index] = _nose[like];
                    _sensor[index] = _sensor[like];
                    _known[index] = true;
                    CopiedByKind++;
                    return own;
                }
            }
            if (!_wanted[index])
            {
                if (_waiting == 0)
                {
                    _waitingSince = Time.time;
                }
                _wanted[index] = true;
                _waiting++;
            }
            return null;
        }

        /// <summary>True while a car seen in a look waits to be measured; and how long the first of them has waited.</summary>
        public bool Waiting => _waiting > 0;
        public float WaitedFor => _waiting > 0 ? Time.time - _waitingSince : 0f;

        /// <summary>
        /// One car seen in a look and not measured yet, measured now (outside any look; an upright one's figures kept for the
        /// others of its kind, if its colliders hold them whichever way it is turned). One a call: the caller does it in a frame
        /// with no look, or after a look if one waited too long.
        /// </summary>
        public void MeasureWaiting(List<VehicleComponent> pooled)
        {
            for (int i = 0; i < _wanted.Length; i++)
            {
                if (!_wanted[i])
                {
                    continue;
                }
                _wanted[i] = false;
                _waiting--;
                VehicleComponent car = i < pooled.Count ? pooled[i] : null;
                if (car == null || _known[i] || !car.isActiveAndEnabled || car.rb == null || car.GetIndex() != i)
                {
                    continue;   // back in the pool, or measured already: measured when it is next seen
                }
                TrafficTools.Measured();
                long began = System.Diagnostics.Stopwatch.GetTimestamp();
                bool shared = Measure(i, car);
                long took = System.Diagnostics.Stopwatch.GetTimestamp() - began;
                MeasuredOut++;
                if (took > MeasureWorst)
                {
                    MeasureWorst = took;
                }
                int kind = Kind(car);
                if (shared && kind != 0 && !_byKind.ContainsKey(kind))
                {
                    _byKind[kind] = i;   // (one on its side measures its width as its height, one of odd colliders as it is turned: theirs are their own)
                }
                if (_waiting > 0)
                {
                    _waitingSince = Time.time;
                }
                return;
            }
            _waiting = 0;
        }

        // A car's kind: the prefab the traffic system made it of; 0 for none known (then each is measured).
        private static int Kind(VehicleComponent car)
        {
            GameObject prefab = car.prefab;
            return prefab != null ? prefab.GetInstanceID() : 0;
        }

        // The car's figures in its own space, once: its body's middle, length and width, its nose and its sensor's reach. True
        // if they hold for every car of its kind standing upright: it stood upright, and its colliders were read in their own space.
        private bool Measure(int index, VehicleComponent car)
        {
            Transform body = car.transform;
            TrafficTools.Footprint(car, out Vector3 centre, out _, out float halfLength, out float halfWidth, out bool exact);
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
            return exact && Mathf.Abs(body.up.y) >= 0.5f;
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
        private bool _linesLayered;         // their layer set, as the first went out
        private bool _linesHidden;          // made behind the loading screen; and in how many ms (the self-check line)
        private double _linesMs;
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

        /// <summary>
        /// Behind the loading screen (ScenePrewarm): the two hold lines made where no frame is seen, switched off. Their layer is
        /// chosen when the first is put out, as it always was (the parking room of the car the player drives then). Made in the
        /// look that first wanted one they were 9 ms (the own-spike scan, 28 Oct 2026).
        /// </summary>
        public bool MakeAll(Transform parent)
        {
            MakeLines(parent, true);
            return true;
        }

        // The two, made once. False (and the lines are the mod's own braking alone) if the game refuses the tag.
        private bool MakeLines(Transform parent, bool hidden)
        {
            if (_linesMade)
            {
                return true;
            }
            if (_linesFailed)
            {
                return false;
            }
            long began = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                for (int i = 0; i < _line.Length; i++)
                {
                    var line = new GameObject("RealLifeOverhaul Hold Line " + (i + 1));
                    _line[i] = line;
                    line.transform.SetParent(parent, false);   // (a parent: some of the game's triggers ask whose child a collider is)
                    line.transform.position = new Vector3(0f, ParkedAt, 0f);
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
                _linesHidden = hidden;
                _linesMs = (System.Diagnostics.Stopwatch.GetTimestamp() - began) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                return true;
            }
            catch (Exception e)
            {
                LinesFailed(e.Message);
                return false;
            }
        }

        // The lines' layer, set once as the first goes out: the game's own parking room's, else "Vehicles". False if there is none.
        private bool Layered()
        {
            if (_linesLayered)
            {
                return true;
            }
            int layer = LineLayer();
            if (layer < 0)
            {
                LinesFailed("the game has no layer for the player's cars any more");
                return false;
            }
            for (int i = 0; i < _line.Length; i++)
            {
                _line[i].layer = layer;
            }
            _linesLayered = true;
            Quiet?.Invoke();
            Logger?.Info($"City drivers: {_line.Length} hold lines made on layer '{LayerMask.LayerToName(_lineLayer)}' (triggers tagged '{HaltTag}', as the game's own "
                         + "parking room is): made once, switched off until needed.");
            Logger?.Info(FormattableString.Invariant($"Traffic check (hold lines made out of play): made {(_linesHidden ? "behind the loading screen" : "in the look that first wanted one")} ")
                         + FormattableString.Invariant($"in {_linesMs:F2} ms (wanted: none made in play): ") + (_linesHidden ? "PASS" : "FAIL") + ".");
            Loud?.Invoke();
            return true;
        }

        private void LinesFailed(string why)
        {
            Logger?.Warn("City drivers: the hold lines could not be made (" + why + "); a car is held for the player by the mod's own braking alone.");
            DestroyLines();
            _linesFailed = true;
        }

        /// <summary>Puts a hold line across a lane: its middle on the road, the way the lane runs, as wide as the lane. False if lines cannot be used.</summary>
        public bool PlaceLine(int slot, Vector3 middle, Vector3 forward, float width, Transform parent)
        {
            if (!MakeLines(parent, false) || !Layered())
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
            _linesLayered = false;
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
