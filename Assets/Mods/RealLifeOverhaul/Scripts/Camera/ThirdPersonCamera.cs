using System.Reflection;
using BAModAPI;
using BlueprintsUI;
using CameraControllers;
using Cinemachine;
using PlayerActivity;
using UI;
using UI.InteriorDesigner;
using UI.Load;
using UI.MiniMenu;
using UI.Purchase;
using UI.PurchaseVehicle;
using UI.Smartphone;
using UI.Smartphone.Apps.Feedback;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Turns the on-foot camera into a GTA-style third-person camera by driving the
    /// public fields of the game's own PedestrianCam (offset, distance, angle), so the
    /// game keeps doing the positioning and zooming itself. With mouse look on, the
    /// cursor is locked and the mouse orbits the camera; the game's own hover/click
    /// raycast then runs from the screen centre, which acts as a crosshair.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        private const float MinMoveSpeed = 0.6f;   // m/s before the camera starts swinging behind
        private const float MinDistance = 2.5f;
        private const float MinPitch = 4f;
        private const float MaxPitch = 80f;

        public static IModLogger Logger;

        /// <summary>True while the mouse is steering the camera (cursor locked).</summary>
        public static bool IsLooking { get; private set; }

        /// <summary>True after the player pressed the free-cursor key (Left Alt unless rebound) to get the cursor back.</summary>
        public static bool CursorFreed { get; private set; }

        /// <summary>True while the look-behind key (the right mouse button unless rebound) is held to look behind the car.</summary>
        public static bool LookBehind { get; private set; }

        /// <summary>
        /// While set, the camera watches this instead of the spot the player stands on: the body
        /// of a player who has been knocked down, which lies somewhere other than their root.
        /// </summary>
        public static Transform Focus { get; set; }

        private const float LookBehindHoldTime = 0.2f;   // a shorter right click stays the game's own right-click action

        // The game flags a right-button drag here to stop the release from also firing a right-click action.
        private static readonly FieldInfo RightButtonHeldField =
            typeof(MouseController).GetField("RightButtonHeld", BindingFlags.NonPublic | BindingFlags.Static);

        private float _rightHeldFor;

        private sealed class CamState
        {
            public PedestrianCam Cam;
            public CinemachineVirtualCameraBase VCam;
            public Vector3 Offset;
            public Vector2 MinMaxDistance;
            public float Distance;
            public bool Applied;
            public int AppliedDistance;
            public CinemachineVirtualCamera Lens;   // null if the camera is not a plain virtual camera
            public Transform Follow;
            public Transform LookAt;
            public float Fov;
            public Transform Target;                // our own orbit/look-at point at the player's upper body
            public bool Indoor;
            public float DistanceSpeed;
            public float Desired;                   // distance the player asked for, before walls pull the camera in
            public float LastWritten;
        }

        private const float IndoorMinDistance = 1f;
        private const float CollisionRadius = 0.25f;
        private static readonly RaycastHit[] CollisionHits = new RaycastHit[24];

        private CamState _outdoor;
        private CamState _indoor;
        private Vector3 _lastPlayerPos;
        private bool _hasLastPos;
        private float _pitch;
        private int _pitchSetting = int.MinValue;
        private bool _lockedByUs;
        private bool _loggedMouseDelta;

        private void Update()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null || gm.playerController == null)
            {
                SetLooking(false);
                return;
            }

            if (_outdoor == null)
            {
                _outdoor = Capture(gm.pedestrianCamera, "outdoor");
                _indoor = Capture(gm.indoorCamera, "indoor");
                if (_indoor != null)
                {
                    _indoor.Indoor = true;
                }
            }

            // The camera key (V unless rebound) switches between the third-person cameras and the game's normal camera.
            if (Keys.Down(KeyAction.CameraToggle) && !IsUiOpen())
            {
                Settings.ToggleCamera();
                Logger?.Info($"Camera: third person {(Settings.CameraEnabled ? "on" : "off")} ({Keys.Label(KeyAction.CameraToggle)} key).");
            }

            if (_pitchSetting != Settings.CameraPitch)
            {
                _pitchSetting = Settings.CameraPitch;
                _pitch = Settings.CameraPitch;
            }

            Transform player = gm.playerController.transform;
            Apply(_outdoor, Settings.CameraEnabled, player);
            Apply(_indoor, Settings.CameraEnabled && Settings.CameraIndoors, player);

            if (Keys.Down(KeyAction.FreeCursor))
            {
                CursorFreed = !CursorFreed;
            }

            // The pedestrian cameras are also used on scooters and hand trucks, so those get mouse look too.
            CamState active = ActiveState();
            bool look = Settings.CameraEnabled && Settings.MouseLook && active != null && !CursorFreed
                        && Application.isFocused && !IsUiOpen();
            SetLooking(look);
            UpdateLookBehind();

            if (look)
            {
                MouseLook(active);
            }
            else
            {
                FollowPlayer(gm);
            }
            TrackPlayer(gm);
        }

        private void OnDestroy()
        {
            SetLooking(false);
            Apply(_outdoor, false, null);
            Apply(_indoor, false, null);
            DestroyTarget(_outdoor);
            DestroyTarget(_indoor);

        }

        /// <summary>Hold the look-behind key (the right mouse button unless rebound) while driving to look behind the car.</summary>
        private void UpdateLookBehind()
        {
            bool held = ChaseCamera.IsLooking && Keys.Held(KeyAction.LookBehind);
            _rightHeldFor = held ? _rightHeldFor + Time.unscaledDeltaTime : 0f;
            LookBehind = _rightHeldFor > LookBehindHoldTime;
            if (LookBehind && Keys.Key(KeyAction.LookBehind) == KeyCode.Mouse1)
            {
                RightButtonHeldField?.SetValue(null, true);   // only the right button has a click of the game's to swallow
            }
        }

        private static void DestroyTarget(CamState state)
        {
            if (state != null && state.Target != null)
            {
                Destroy(state.Target.gameObject);
            }
        }


        /// <summary>Any screen that needs the mouse cursor. Left Alt covers the ones not listed here.</summary>
        public static bool IsUiOpen()
        {
            // The item/vehicle panel is deliberately not counted: it is on screen the whole time the player
            // drives or carries something, and it works by keys, so it must not switch mouse look or the HUD off.
            return Leisure.PhotoMode.Active || FullMenu.IsOpen || CityMap.IsOpen || MiniMenu.IsOpen || BuildingPreview.isPreviewing
                   || HudConfirm.isOpen || Feedback.IsOpen || PlayerActivityUI.IsPanelOpen
                   || PurchaseUI.IsPanelOpen || PurchaseVehicleUI.IsPanelOpen || InteriorDesignerUI.IsOpen
                   || BlueprintsPanel.IsOpen || AcknowledgeWarning.isOpen
                   || CustomColorPicker.isOpen || LoadScene.isLoading || TravelUi.PanelOpen
                   || GarageUi.NeedsCursor   // the custom shop, "My vehicles", a two-wheeler stand's panel
                   || BigAmbitions.PlacementSystem.PlacementSystem.IsInPlacementMode
                   || GameManager.ShouldBlockKeyboardShortcuts()
                   || Law.CustodyPanel.IsOpen   // the holding cell has buttons
                   || Meta.MetaUi.AnyOpen        // the guide, the welcome panel, confirmations: all worked with the mouse
                   || JusticePanel.IsOpen;       // bail, hearings, the Legal file
        }

        private void SetLooking(bool look)
        {
            if (look != IsLooking)
            {
                Logger?.Info($"Camera: mouse look {(look ? "on" : "off")} (uiOpen={IsUiOpen()} cursorFreed={CursorFreed} focused={Application.isFocused}).");
            }
            IsLooking = look;
            if (look || Hud.Exists)
            {
                Hud.Instance.SetCrosshair(look);
            }
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

        private CamState ActiveState()
        {
            if (_outdoor != null && _outdoor.Applied && _outdoor.VCam.Priority == 1)
            {
                return _outdoor;
            }
            if (_indoor != null && _indoor.Applied && _indoor.VCam.Priority == 1)
            {
                return _indoor;
            }
            return null;
        }

        private void MouseLook(CamState state)
        {
            // Same legacy axes the game's own free-look camera reads.
            var delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            float degreesPerUnit = Settings.MouseSensitivity * 0.25f;
            state.Cam.angle += delta.x * degreesPerUnit;
            _pitch = Mathf.Clamp(_pitch - delta.y * degreesPerUnit, MinPitch, MaxPitch);

            if (!_loggedMouseDelta && delta != Vector2.zero)
            {
                _loggedMouseDelta = true;
                Logger?.Info($"Camera: first mouse look input {delta}.");
            }
        }

        private static CamState Capture(CinemachineVirtualCameraBase vcam, string label)
        {
            var cam = vcam != null ? vcam.GetComponent<PedestrianCam>() : null;
            if (cam == null)
            {
                Logger?.Warn($"Camera: no PedestrianCam on the {label} camera, leaving it alone.");
                return null;
            }

            var lens = vcam as CinemachineVirtualCamera;
            float fov = lens != null ? lens.m_Lens.FieldOfView : 0f;
            Logger?.Info($"Camera: {label} vanilla offset={cam.offset} minMax={cam.minMaxDistance} distance={cam.distance} "
                         + $"fov={fov} follow={(vcam.Follow != null ? vcam.Follow.name : "none")} lookAt={(vcam.LookAt != null ? vcam.LookAt.name : "none")}");
            return new CamState
            {
                Cam = cam,
                VCam = vcam,
                Lens = lens,
                Follow = vcam.Follow,
                LookAt = vcam.LookAt,
                Fov = fov,
                DistanceSpeed = cam.distanceSpeed,
                Offset = cam.offset,
                MinMaxDistance = cam.minMaxDistance,
                Distance = cam.distance
            };
        }

        private void Apply(CamState state, bool enabled, Transform player)
        {
            if (state == null || state.Cam == null)
            {
                return;
            }

            if (!enabled)
            {
                if (state.Applied)
                {
                    state.Cam.offset = state.Offset;
                    state.Cam.minMaxDistance = state.MinMaxDistance;
                    state.Cam.distance = state.Distance;
                    state.Cam.distanceSpeed = state.DistanceSpeed;
                    state.VCam.Follow = state.Follow;
                    state.VCam.LookAt = state.LookAt;
                    SetFov(state, state.Fov);
                    state.Applied = false;
                }
                return;
            }

            // The vanilla camera is a long-lens top-down view aimed at the player's feet. Up close that
            // only frames the legs, so orbit and aim at the upper body instead, with a wider lens.
            if (state.Follow != null && player != null)
            {
                if (state.Target == null)
                {
                    state.Target = new GameObject("RealLifeOverhaul Camera Target").transform;
                    state.Target.SetParent(state.Follow, false);
                }
                // Parented to the vanilla follow point so it moves with the player without a frame of lag.
                Vector3 follow = Focus != null ? Focus.position : state.Follow.position;
                state.Target.position = new Vector3(follow.x, player.position.y + Settings.CameraHeight / 10f, follow.z);
                state.VCam.Follow = state.Target;
                if (state.LookAt != null)
                {
                    state.VCam.LookAt = state.Target;
                }
            }
            SetFov(state, Settings.CameraFov);

            // Keep the vanilla compass direction of the offset, only change how steeply it looks down.
            var flat = new Vector3(state.Offset.x, 0f, state.Offset.z);
            flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.back;
            float pitch = _pitch * Mathf.Deg2Rad;
            state.Cam.offset = flat * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch);
            float min = state.Indoor ? IndoorMinDistance : MinDistance;
            float max = Mathf.Max(state.MinMaxDistance.y, min + 1f);
            state.Cam.minMaxDistance = new Vector2(min, max);

            if (!state.Applied || state.AppliedDistance != Settings.CameraDistance)
            {
                state.Desired = Mathf.Clamp(Settings.CameraDistance, min, max);
                state.AppliedDistance = Settings.CameraDistance;
            }
            else
            {
                // Whatever the game's scroll-wheel zoom changed since our last write is the player's doing.
                state.Desired = Mathf.Clamp(state.Desired + (state.Cam.distance - state.LastWritten), min, max);
            }

            // Interiors are built for the top-down view, so indoors the camera must stay in front of walls.
            // (Only while that camera is the one being looked through: out of doors the indoor camera is not live, and a sphere cast a frame for it is wasted.)
            float distance = state.Indoor && CinemachineCore.Instance.IsLive(state.VCam) ? Collide(state, player, state.Desired) : state.Desired;
            state.Cam.distance = distance;
            state.LastWritten = distance;
            state.Cam.distanceSpeed = state.Indoor ? 14f : state.DistanceSpeed;
            state.Applied = true;
        }

        private static float Collide(CamState state, Transform player, float desired)
        {
            if (state.Target == null || player == null)
            {
                return desired;
            }
            Vector3 origin = state.Target.position;
            Vector3 toCamera = state.Cam.transform.position - origin;
            if (toCamera.sqrMagnitude < 0.01f)
            {
                return desired;
            }

            int count = Physics.SphereCastNonAlloc(origin, CollisionRadius, toCamera.normalized, CollisionHits, desired,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = desired;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = CollisionHits[i];
                // Distance 0 means the cast started inside the collider; the player's own colliders don't count either.
                if (hit.distance <= 0f || hit.collider.transform.IsChildOf(player))
                {
                    continue;
                }
                nearest = Mathf.Min(nearest, hit.distance);
            }
            return Mathf.Max(IndoorMinDistance, nearest - 0.15f);
        }

        private static void SetFov(CamState state, float fov)
        {
            if (state.Lens == null || fov <= 0f || Mathf.Approximately(state.Lens.m_Lens.FieldOfView, fov))
            {
                return;
            }
            LensSettings lens = state.Lens.m_Lens;
            lens.FieldOfView = fov;
            state.Lens.m_Lens = lens;
        }

        private void TrackPlayer(GameManager gm)
        {
            _lastPlayerPos = gm.playerController.transform.position;
            _hasLastPos = true;
        }

        /// <summary>Without mouse look: swing the camera behind the player as they walk.</summary>
        private void FollowPlayer(GameManager gm)
        {
            float dt = Time.unscaledDeltaTime;
            if (!_hasLastPos || dt <= 0f || Settings.CameraFollow <= 0 || Settings.MouseLook)
            {
                return;
            }

            // Right mouse button is the game's manual camera rotation; never fight it.
            if (Input.GetMouseButton(1))
            {
                return;
            }

            Vector3 delta = gm.playerController.transform.position - _lastPlayerPos;
            delta.y = 0f;
            if (delta.magnitude / dt < MinMoveSpeed)
            {
                return;
            }

            float heading = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float blend = 1f - Mathf.Exp(-Settings.CameraFollow * 0.3f * dt);
            Steer(_outdoor, heading, blend);
            Steer(_indoor, heading, blend);
        }

        private static void Steer(CamState state, float heading, float blend)
        {
            if (state == null || !state.Applied || state.VCam.Priority != 1)
            {
                return;
            }

            // PedestrianCam places the camera at offset rotated by `angle` around the player,
            // so the camera sits behind the player when offsetYaw + angle == heading + 180.
            Vector3 offset = state.Cam.offset;
            float offsetYaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            float target = heading + 180f - offsetYaw;
            state.Cam.angle = Mathf.LerpAngle(state.Cam.angle, target, blend);
        }
    }
}
