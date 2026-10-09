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
    /// </summary>
    public class ChaseCamera : MonoBehaviour
    {
        private const int LivePriority = 100;
        private const int IdlePriority = -100;
        private const float TargetHeight = 1.3f;
        private const float RestPitch = 12f;
        private const float MinPitch = -4f;
        private const float MaxPitch = 65f;
        private const float RecenterDelay = 1.2f;   // seconds without mouse input before swinging back behind the car
        private const float RecenterSpeed = 2f;     // m/s the car must be moving

        public static IModLogger Logger;

        /// <summary>True while the mouse is steering the chase camera (cursor locked).</summary>
        public static bool IsLooking { get; private set; }

        private CinemachineVirtualCamera _vcam;
        private bool _active;
        private bool _lockedByUs;
        private float _yaw;
        private float _pitch = RestPitch;
        private float _yawVelocity;
        private float _pitchVelocity;
        private float _distance;
        private int _distanceSetting = int.MinValue;
        private float _lastMouseTime;
        private Vector3 _lastPos;

        private void LateUpdate()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            VehicleController vehicle = gm != null ? gm.selectedVehicle : null;
            bool drive = Settings.CameraEnabled && Settings.CarCamera && vehicle != null
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

            Vector3 target = car.position + Vector3.up * TargetHeight;
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw + (ThirdPersonCamera.LookBehind ? 180f : 0f), 0f);
            _vcam.transform.SetPositionAndRotation(target - rotation * Vector3.forward * _distance, rotation);

            if (!Mathf.Approximately(_vcam.m_Lens.FieldOfView, Settings.CameraFov))   // written only when the option has changed
            {
                LensSettings lens = _vcam.m_Lens;
                lens.FieldOfView = Settings.CameraFov;
                _vcam.m_Lens = lens;
            }
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
                    Logger?.Info($"Camera: vehicle vanilla fov={vanilla.m_Lens.FieldOfView} far={vanilla.m_Lens.FarClipPlane}");
                }
            }

            _yaw = vehicle.transform.eulerAngles.y;
            _pitch = RestPitch;
            _lastPos = vehicle.transform.position;
            _lastMouseTime = 0f;
            _vcam.Priority = LivePriority;
            _active = true;
            Logger?.Info("Camera: chase camera on.");
        }

        private void Deactivate()
        {
            if (!_active)
            {
                return;
            }
            _active = false;
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
        }
    }
}
