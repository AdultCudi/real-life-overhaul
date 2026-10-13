using BAModAPI;
using Cinemachine;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// GTA-style chase camera for cars. The game's vehicle cameras are scene-authored
    /// Cinemachine rigs with no script to steer, so this adds its own virtual camera and
    /// raises its priority above the game's while the normal outdoor driving camera is live.
    /// The look-back camera, garages and parking keep the game's own cameras.
    /// While another author's mod drives the camera (<see cref="CameraYield"/>) this one is let go.
    /// </summary>
    public class ChaseCamera : MonoBehaviour
    {
        private const int LivePriority = 100;
        private const int IdlePriority = -100;
        private const float TargetHeight = 1.3f;
        private const float RestPitch = 12f;
        private const float MinPitch = -35f;        // under 0 the view looks up
        private const float MaxPitch = 65f;
        private const float RecenterDelay = 1.2f;   // seconds without mouse input before swinging back behind the car
        private const float RecenterSpeed = 2f;     // m/s the car must be moving

        // The view may look up further than the camera can swing down: the camera stops over the road and the rest of
        // the tilt is a look upwards from where it stands (Orbit).
        private const float GroundClearance = 0.5f;   // metres between the camera and the road under it
        private const float LowOrbit = 10f;           // degrees: at this tilt or steeper (the resting one too) the camera is well over the road, which is then not looked at
        private const float FloorEase = 12f;          // per second, so that a kerb under the camera does not make it hop

        public static IModLogger Logger;

        /// <summary>True while the mouse is steering the chase camera (cursor locked).</summary>
        public static bool IsLooking { get; private set; }

        /// <summary>True while the chase camera is the one asked for (its priority is above the game's).</summary>
        public static bool Live { get; private set; }

        private CinemachineVirtualCamera _vcam;
        private bool _active;
        private bool _lockedByUs;
        private float _yaw;
        private float _pitch = RestPitch;
        private float _floor;          // the lowest the camera may swing round the car (degrees of tilt), eased
        private bool _hasFloor;        // false until the first frame after the camera comes on, which takes the road as it is
        private float _yawVelocity;
        private float _pitchVelocity;
        private float _distance;
        private int _distanceSetting = int.MinValue;
        private float _lastMouseTime;
        private Vector3 _lastPos;

        private static readonly int HotLateUpdate = HotSpots.Slot("Camera: ChaseCamera.LateUpdate");
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            var gm = InstanceBehavior<GameManager>.Instance;
            VehicleController vehicle = gm != null ? gm.selectedVehicle : null;
            bool drive = !CameraYield.StoodDown && Settings.CameraEnabled && Settings.CarCamera && vehicle != null
                         && !vehicle.vehicleType.spawnInPlayerObject
                         && gm.vehicleCamera != null && gm.vehicleCamera.Priority == 1;
            if (!drive)
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

            // Mouse steering of the car and mouse look can't share the mouse.
            bool look = Settings.MouseLook && !PlayerPrefSettings.VehicleMouseInput && !ThirdPersonCamera.CursorFreed
                        && Application.isFocused && !ThirdPersonCamera.IsUiOpen();
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

            // The camera swings round the car no lower than the road allows; a tilt past that turns the view up from
            // where the camera stands, as a head turns.
            Vector3 target = car.position + Vector3.up * TargetHeight;
            float yaw = _yaw + (ThirdPersonCamera.LookBehind ? 180f : 0f);
            Quaternion orbit = Quaternion.Euler(Orbit(target, yaw, dt), yaw, 0f);
            _vcam.transform.SetPositionAndRotation(target - orbit * Vector3.forward * _distance, Quaternion.Euler(_pitch, yaw, 0f));

            if (!Mathf.Approximately(_vcam.m_Lens.FieldOfView, Settings.CameraFov))   // written only when the option has changed
            {
                LensSettings lens = _vcam.m_Lens;
                lens.FieldOfView = Settings.CameraFov;
                _vcam.m_Lens = lens;
            }
        }

        // How high the camera swings round the car, in degrees of tilt: the tilt asked for, but never so low that the
        // camera comes nearer the road under it than GroundClearance. One ray at most, and only while the tilt is low.
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

            EmpireView.Claim(gm);   // Empire Motors' view on the same key kept off while the key is V (Camera/EmpireView): once a run
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
        }
    }
}
