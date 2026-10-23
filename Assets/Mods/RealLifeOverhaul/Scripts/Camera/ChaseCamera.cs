using BAModAPI;
using Cinemachine;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A chase camera for cars: a virtual camera of its own above the game's in priority while <see cref="CameraYield"/>
    /// orders it, as the game's vehicle cameras have nothing to steer. Per frame: the orders read; while live, at most one
    /// ray for the road under it and a few vector operations.
    /// </summary>
    public class ChaseCamera : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("camera", "ChaseCamera", typeof(ChaseCamera));

        private const int LivePriority = 100;       // over the game's cameras
        private const int IdlePriority = -100;
        private const float TargetHeight = 1.3f;    // metres above the car's origin
        private const float RestPitch = 12f;        // degrees
        private const float MinPitch = -35f;        // degrees; under 0 the view looks up
        private const float MaxPitch = 65f;         // degrees
        private const float RecenterDelay = 1.2f;   // seconds without mouse input before swinging back behind the car
        private const float RecenterSpeed = 2f;     // m/s the car must be moving

        // The camera stops over the road; the rest of a low tilt is a look upwards from where it stands (Orbit).
        private const float GroundClearance = 0.5f;   // metres between the camera and the road under it
        private const float LowOrbit = 10f;           // degrees: at this tilt or steeper the camera is well over the road, not looked at
        private const float FloorEase = 12f;          // per second, so that a kerb under the camera does not make it hop

        public static IModLogger Logger;

        /// <summary>True while the mouse is steering the chase camera (cursor locked).</summary>
        public static bool IsLooking { get; private set; }

        /// <summary>True while the chase camera is the one asked for.</summary>
        public static bool Live { get; private set; }

        private CinemachineVirtualCamera _vcam;
        private bool _active;
        private bool _lockedByUs;
        private float _yaw;
        private float _pitch = RestPitch;
        private float _floor;          // the lowest the camera may swing round the car (degrees of tilt), eased
        private bool _hasFloor;        // false until the first frame after the camera comes on
        private float _yawVelocity;
        private float _pitchVelocity;
        private float _distance;
        private int _distanceSetting = int.MinValue;
        private float _lastMouseTime;
        private Vector3 _lastPos;

        /// <summary>True while the chase camera is live, in the blend out of it too.</summary>
        public static bool InPicture { get; private set; }

        private static ChaseCamera _instance;   // for the feature's clean-up

        // Empire Motors' view on V is looked for behind the loading screen, not the first time this camera comes on.
        private void Awake()
        {
            _instance = this;
            ScenePrewarm.Add("camera: Empire Motors' view from the driver's seat being found", EmpireView.Prewarm);
        }

        private static readonly int HotLateUpdate = HotSpots.Slot("Camera: ChaseCamera.LateUpdate");
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults
                LetGo();
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnLateUpdate()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            VehicleController vehicle = gm != null ? gm.selectedVehicle : null;
            InPicture = _vcam != null && (_active || InPicture) && CinemachineCore.Instance.IsLive(_vcam);
            CameraOrders orders = CameraYield.Orders;
            BridgeHold.Hold(orders.HoldBridges);        // the on-foot camera holds them too: whichever runs first does it
            if (!orders.Chase || vehicle == null)
            {
                Deactivate();
                return;
            }

            if (!_active)
            {
                Activate(gm, vehicle);
            }
            if (_distanceSetting != Settings.CarCameraDistance)
            {
                _distanceSetting = Settings.CarCameraDistance;
                _distance = Settings.CarCameraDistance;
            }

            // Mouse steering of the car and mouse look cannot share the mouse.
            bool look = orders.ChaseLook;
            SetLooking(look);

            float dt = Time.unscaledDeltaTime;
            Transform car = vehicle.transform;
            if (look)
            {
                var delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
                if (delta != Vector2.zero)
                {
                    float degreesPerUnit = Settings.MouseSensitivity * 0.25f;
                    _yaw += delta.x * degreesPerUnit;
                    _pitch = Mathf.Clamp(_pitch - delta.y * degreesPerUnit, MinPitch, MaxPitch);
                    _lastMouseTime = Time.unscaledTime;
                    _yawVelocity = 0f;
                    _pitchVelocity = 0f;
                }
                _distance = Mathf.Clamp(_distance - Input.mouseScrollDelta.y * 0.6f, 3f, 25f);
            }

            Vector3 moved = car.position - _lastPos;
            moved.y = 0f;
            _lastPos = car.position;
            bool moving = dt > 0f && moved.magnitude / dt > RecenterSpeed;
            if (moving && Time.unscaledTime - _lastMouseTime > RecenterDelay)
            {
                _yaw = Mathf.SmoothDampAngle(_yaw, car.eulerAngles.y, ref _yawVelocity, 0.35f, Mathf.Infinity, dt);
                _pitch = Mathf.SmoothDamp(_pitch, RestPitch, ref _pitchVelocity, 0.6f, Mathf.Infinity, dt);
            }

            // The camera swings round the car no lower than the road allows; a tilt past that turns the view up, as a head turns.
            Vector3 target = car.position + Vector3.up * TargetHeight;
            float yaw = _yaw + (ThirdPersonCamera.LookBehind ? 180f : 0f);
            Quaternion orbit = Quaternion.Euler(Orbit(target, yaw, dt), yaw, 0f);
            _vcam.transform.SetPositionAndRotation(target - orbit * Vector3.forward * _distance, Quaternion.Euler(_pitch, yaw, 0f));

            if (!Mathf.Approximately(_vcam.m_Lens.FieldOfView, Settings.CameraFov))
            {
                LensSettings lens = _vcam.m_Lens;
                lens.FieldOfView = Settings.CameraFov;
                _vcam.m_Lens = lens;
            }
        }

        // The tilt the camera swings at, never nearer the road than GroundClearance; one ray at most, only while low.
        private float Orbit(Vector3 target, float yaw, float dt)
        {
            float above = TargetHeight;   // as on a level road
            if (_pitch < LowOrbit)
            {
                Vector3 spot = target - Quaternion.Euler(Mathf.Max(_pitch, _floor), yaw, 0f) * Vector3.forward * _distance;
                spot.y = target.y;   // asked at the target's height: the ray starts 2.5 m over it
                if (CitySpots.Ground(spot, out float ground))
                {
                    above = target.y - ground;
                }
            }
            float floor = ThirdPersonCamera.LowestOrbit(above, GroundClearance, _distance);
            _floor = _hasFloor ? Mathf.Lerp(_floor, floor, 1f - Mathf.Exp(-FloorEase * dt)) : floor;
            _hasFloor = true;
            return Mathf.Max(_pitch, _floor);
        }

        private void Activate(GameManager gm, VehicleController vehicle)
        {
            if (_vcam == null)
            {
                var go = new GameObject("RealLifeOverhaul Chase Camera");
                go.transform.SetParent(transform, false);
                _vcam = go.AddComponent<CinemachineVirtualCamera>();
                if (gm.vehicleCamera is CinemachineVirtualCamera vanilla)
                {
                    _vcam.m_Lens = vanilla.m_Lens;   // keep the game's clip planes
                    Logger?.Info($"Camera: vehicle vanilla fov={vanilla.m_Lens.FieldOfView} near={vanilla.m_Lens.NearClipPlane} far={vanilla.m_Lens.FarClipPlane}");
                }
            }

            EmpireView.Claim(gm);   // Empire Motors' view on the same key kept off while the key is V
            _yaw = vehicle.transform.eulerAngles.y;
            _pitch = RestPitch;
            _hasFloor = false;
            _lastPos = vehicle.transform.position;
            _lastMouseTime = 0f;
            _vcam.Priority = LivePriority;
            _active = true;
            Live = true;
            Logger?.Info("Camera: chase camera on.");
        }

        private void Deactivate()
        {
            if (!_active)
            {
                return;
            }
            _active = false;
            Live = false;
            _vcam.Priority = IdlePriority;
            SetLooking(false);
            Logger?.Info("Camera: chase camera off.");
        }

        private void SetLooking(bool look)
        {
            IsLooking = look;
            if (look)
            {
                if (Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                }
                _lockedByUs = true;
            }
            else if (_lockedByUs)
            {
                Cursor.lockState = CursorLockMode.None;
                _lockedByUs = false;
            }
        }

        private void OnDestroy()
        {
            SetLooking(false);
            Live = false;
            InPicture = false;
            BridgeHold.Release();   // the city unloading
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>Lets the chase camera go, frees the pointer and gives the bridge back: the feature's clean-up. Safe at any time.</summary>
        public static void LetGo()
        {
            if (_instance != null)
            {
                _instance.Deactivate();
            }
            InPicture = false;
            BridgeHold.Release();
        }
    }
}
