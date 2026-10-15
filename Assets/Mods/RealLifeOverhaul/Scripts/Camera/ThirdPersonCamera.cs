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
    ///
    /// While another author's mod drives the camera (<see cref="CameraYield"/>) none of this is done: the game's
    /// cameras have their own figures back and the pointer is free, until that mod stops.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("camera", "ThirdPersonCamera", typeof(ThirdPersonCamera));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const float MinMoveSpeed = 0.6f;   // m/s before the camera starts swinging behind
        private const float MinDistance = 2.5f;
        private const float MinPitch = -60f;       // out of doors; under 0 the view looks up, to the tops of the buildings
        private const float MaxPitch = 80f;

        public static IModLogger Logger;

        /// <summary>True while the mouse is steering the camera (cursor locked).</summary>
        public static bool IsLooking { get; private set; }

        /// <summary>True after the player pressed the free-cursor key (Left Alt unless rebound) to get the cursor back.</summary>
        public static bool CursorFreed { get; private set; }

        /// <summary>True while the look-behind key (the right mouse button unless rebound) is held to look behind the car.</summary>
        public static bool LookBehind { get; private set; }

        /// <summary>True while one of the game's on-foot cameras carries this mod's figures (its offset, distance and lens).</summary>
        public static bool Steering { get; private set; }

        /// <summary>
        /// While set, the camera watches this instead of the spot the player stands on: the body
        /// of a player who has been knocked down, which lies somewhere other than their root.
        /// </summary>
        public static Transform Focus { get; set; }

        /// <summary>
        /// While set, the player rides inside it (the mod's bus: Transport/BusRider): out of doors the camera keeps within
        /// its sides and ends, coming in closer as a wall would have it, so that it looks at the player inside and not at the
        /// outside of the bus. Its top is open: swung up over it the camera rises out, and the bus's roof is not drawn then.
        /// One box test a frame while set, nothing otherwise.
        /// </summary>
        public static Transform Inside { get; private set; }

        private static Vector3 _insideCentre;
        private static Vector3 _insideHalf;

        /// <summary>Keeps the out-of-doors camera inside a box of a frame (its middle and half sizes in that frame's own space; the top is open).</summary>
        public static void Confine(Transform frame, Vector3 centre, Vector3 half)
        {
            Inside = frame;
            _insideCentre = centre;
            _insideHalf = half;
        }

        /// <summary>Lets the camera go again, if it was this frame that held it.</summary>
        public static void Unconfine(Transform frame)
        {
            if (Inside == frame || Inside == null)
            {
                Inside = null;
            }
        }

        private const float LookBehindHoldTime = 0.2f;   // a shorter right click stays the game's own right-click action

        // The game flags a right-button drag here to stop the release from also firing a right-click action.
        private static readonly FieldInfo RightButtonHeldField = GameContract.RightButtonHeld.Field;   // (resolved once: Core/GameContract)

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
            public Transform Aim;                   // what the camera looks at: the target, or above it while the view looks up past the camera's lowest swing
            public float Floor;                     // out of doors, the lowest the camera may swing (degrees of tilt, under 0 below the target), eased
            public bool Indoor;
            public float DistanceSpeed;
            public float Desired;                   // distance the player asked for, before walls pull the camera in
            public float LastWritten;
        }

        private const float IndoorMinDistance = 1f;
        private const float IndoorMinPitch = 4f;   // interiors are built to be seen from above, and looking up there shows no ceiling
        private const float CollisionRadius = 0.25f;
        private static readonly RaycastHit[] CollisionHits = new RaycastHit[24];

        // Out of doors the view may look up further than the camera can swing down: the camera stops over the ground
        // and the rest of the tilt is a look upwards from where it stands (Orbit).
        private const float GroundClearance = 0.45f;   // metres between the camera and the ground under it
        private const float LowOrbit = 15f;            // degrees: at this tilt or steeper the camera is well over the ground, which is then not looked at
        private const float FloorEase = 12f;           // per second, so that a kerb under the camera does not make it hop
        private const float SinDeepest = 0.42261826f;  // sin 25 degrees: the deepest the camera swings under its target, over a drop

        private CamState _outdoor;
        private CamState _indoor;
        private Vector3 _lastPlayerPos;
        private bool _hasLastPos;
        private float _pitch;
        private int _pitchSetting = int.MinValue;
        private bool _lockedByUs;
        private bool _loggedMouseDelta;

        private void Awake()
        {
            CameraYield.Reset();   // a new city: nothing has been seen of another mod's camera in it yet
        }

        private static readonly int HotUpdate = HotSpots.Slot("Camera: ThirdPersonCamera.Update");
        // The frame's work is a method of its own, so that a game lacking something it names fails there, inside the try
        // (Core/GameMembers): said once, the game's cameras given back, and the mod's camera off for the session.
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e) when (Guard.StandsDown(e, "the third-person camera"))
            {
                enabled = false;
                try
                {
                    SetLooking(false);
                    Apply(_outdoor, false, null);
                    Apply(_indoor, false, null);
                    Steering = false;
                }
                catch (System.Exception second)
                {
                    Logger?.Error(second);
                }
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null || gm.playerController == null)
            {
                SetLooking(false);
                return;
            }
            CameraYield.Observe();
            bool mine = !CameraYield.StoodDown;   // false: another mod has the camera, and nothing below touches it

            if (_outdoor == null)
            {
                _outdoor = Capture(gm.pedestrianCamera, "outdoor");
                _indoor = Capture(gm.indoorCamera, "indoor");
                if (_indoor != null)
                {
                    _indoor.Indoor = true;
                }
            }

            // The camera key (V unless rebound) switches between the third-person cameras and the game's normal camera,
            // on foot and in a car. Empire Motors' view from the driver's seat acts on V too: while the key is V that view
            // is kept off, so a press in a car does one thing (Camera/EmpireView).
            if (Keys.Down(KeyAction.CameraToggle) && !IsUiOpen())
            {
                if (gm.selectedVehicle != null && gm.selectedVehicle.controlledByPlayer)
                {
                    EmpireView.Claim(gm);
                }
                if (mine)
                {
                    Settings.ToggleCamera();
                    Logger?.Info($"Camera: third person {(Settings.CameraEnabled ? "on" : "off")} ({Keys.Label(KeyAction.CameraToggle)} key).");
                }
                else
                {
                    CameraYield.Tell();   // the key does nothing now: the notice says why
                }
            }

            if (_pitchSetting != Settings.CameraPitch)
            {
                _pitchSetting = Settings.CameraPitch;
                _pitch = Settings.CameraPitch;
            }

            Transform player = gm.playerController.transform;
            Apply(_outdoor, mine && Settings.CameraEnabled, player);
            Apply(_indoor, mine && Settings.CameraEnabled && Settings.CameraIndoors, player);
            Steering = (_outdoor != null && _outdoor.Applied) || (_indoor != null && _indoor.Applied);

            if (Keys.Down(KeyAction.FreeCursor))
            {
                CursorFreed = !CursorFreed;
            }

            // The pedestrian cameras are also used on scooters and hand trucks, so those get mouse look too.
            CamState active = ActiveState();
            bool look = mine && Settings.CameraEnabled && Settings.MouseLook && active != null && !CursorFreed
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
            Steering = false;
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
                Destroy(state.Target.gameObject);   // the aim point goes with it: it is the target's child
            }
        }


        /// <summary>Any screen that needs the mouse cursor. Left Alt covers the ones not listed here.</summary>
        public static bool IsUiOpen()
        {
            // Photo mode and the phone's camera (Photos/CameraMode) are the mod's own screens over the world.
            return Leisure.PhotoMode.Active || Photos.CameraMode.Active || AnyWindow(true);
        }

        /// <summary>
        /// A window of the game's or the mod's that needs the mouse cursor, the mod's own cameras left out (the phone's camera
        /// asks it while it is up). <paramref name="menuCounts"/>: whether the phone's full menu counts.
        /// </summary>
        public static bool AnyWindow(bool menuCounts)
        {
            // The item/vehicle panel is deliberately not counted: it is on screen the whole time the player
            // drives or carries something, and it works by keys, so it must not switch mouse look or the HUD off.
            return (menuCounts && FullMenu.IsOpen) || CityMap.IsOpen || MiniMenu.IsOpen || BuildingPreview.isPreviewing
                   || HudConfirm.isOpen || Feedback.IsOpen || PlayerActivityUI.IsPanelOpen
                   || PurchaseUI.IsPanelOpen || PurchaseVehicleUI.IsPanelOpen || InteriorDesignerUI.IsOpen
                   || BlueprintsPanel.IsOpen || AcknowledgeWarning.isOpen
                   || CustomColorPicker.isOpen || LoadScene.isLoading
                   || BigAmbitions.PlacementSystem.PlacementSystem.IsInPlacementMode
                   || GameManager.ShouldBlockKeyboardShortcuts()

                   || Meta.MetaUi.AnyOpen        // the options page's messages and questions, worked with the mouse
                   || Economy.Dealership.ShowroomView.Active   // the Car Showroom's camera, and choosing a bay: the game's own camera, worked with the mouse
                   || JusticePanel.IsOpen        // bail, hearings, the Legal file
                   || GameUi.TravelList.AnyOpen; // the game's own list window, worked with the mouse: bus stops, the cooking list, the custom shop, "My vehicles", a two-wheeler stand
        }

        private void SetLooking(bool look)
        {
            if (look != IsLooking)
            {
                Logger?.Info($"Camera: mouse look {(look ? "on" : "off")} (uiOpen={IsUiOpen()} cursorFreed={CursorFreed} focused={Application.isFocused}).");
            }
            IsLooking = look;
            if (look || CrosshairDot.Exists)
            {
                CrosshairDot.Set(look);
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
            _pitch = Mathf.Clamp(_pitch - delta.y * degreesPerUnit, state.Indoor ? IndoorMinPitch : MinPitch, MaxPitch);

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
                         + $"fov={fov} near={(lens != null ? lens.m_Lens.NearClipPlane : 0f)} far={(lens != null ? lens.m_Lens.FarClipPlane : 0f)} "
                         + $"follow={(vcam.Follow != null ? vcam.Follow.name : "none")} lookAt={(vcam.LookAt != null ? vcam.LookAt.name : "none")}");
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
                if (state.Aim == null)
                {
                    state.Aim = new GameObject("RealLifeOverhaul Camera Aim").transform;
                    state.Aim.SetParent(state.Target, false);
                }
                // Parented to the vanilla follow point so it moves with the player without a frame of lag.
                Vector3 follow = Focus != null ? Focus.position : state.Follow.position;
                state.Target.position = new Vector3(follow.x, player.position.y + Settings.CameraHeight / 10f, follow.z);
                state.VCam.Follow = state.Target;
                if (state.LookAt != null)
                {
                    state.VCam.LookAt = state.Aim;
                }
            }
            SetFov(state, Settings.CameraFov);

            // Keep the vanilla compass direction of the offset, only change how high the camera swings round.
            var flat = new Vector3(state.Offset.x, 0f, state.Offset.z);
            flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.back;
            float pitch = Orbit(state, flat) * Mathf.Deg2Rad;
            state.Cam.offset = flat * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch);
            bool confined = !state.Indoor && Inside != null && state.Target != null;
            float min = state.Indoor || confined ? IndoorMinDistance : MinDistance;
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
            float distance = state.Indoor && CinemachineCore.Instance.IsLive(state.VCam) ? Collide(state, player, state.Desired)
                : confined && CinemachineCore.Instance.IsLive(state.VCam) ? Confined(state, state.Desired) : state.Desired;
            state.Cam.distance = distance;
            state.LastWritten = distance;
            state.Cam.distanceSpeed = state.Indoor || confined ? 14f : state.DistanceSpeed;
            state.Applied = true;
        }

        /// <summary>
        /// How high the camera swings round its target, in degrees of tilt as the mouse sets it, and what it looks at.
        /// Indoors that is the tilt, never under <see cref="IndoorMinPitch"/>. Out of doors the view may look up to the
        /// skyline, but the camera itself stops <see cref="GroundClearance"/> over the ground under it; past that it aims
        /// at a point above the target instead, as high as makes the view's tilt the one asked for. One ray at most, and
        /// only while the camera may swing low enough for the ground to matter.
        /// </summary>
        private float Orbit(CamState state, Vector3 flat)
        {
            if (state.Indoor || state.Target == null || state.Aim == null)
            {
                return Mathf.Max(_pitch, IndoorMinPitch);   // (with no target of our own there is nothing to aim above either)
            }

            Vector3 target = state.Target.position;
            // The game eases the camera to its distance, so the distance that counts is where it is now.
            float distance = Mathf.Max(Vector3.Distance(state.Cam.transform.position, target), 0.5f);

            // The target's height over the ground under where the camera is going, for the camera being looked through only.
            float above = Settings.CameraHeight / 10f;   // as on level ground
            if (_pitch < LowOrbit && CinemachineCore.Instance.IsLive(state.VCam))
            {
                float planned = Mathf.Max(_pitch, state.Floor) * Mathf.Deg2Rad;
                Vector3 spot = target + Quaternion.AngleAxis(state.Cam.angle, Vector3.up) * flat * (distance * Mathf.Cos(planned));
                if (CitySpots.Ground(spot, out float ground))   // asked at the target's height: the ray starts 2.5 m over it
                {
                    above = target.y - ground;
                }
            }
            float floor = LowestOrbit(above, GroundClearance, distance);
            state.Floor = state.Applied ? Mathf.Lerp(state.Floor, floor, 1f - Mathf.Exp(-FloorEase * Time.unscaledDeltaTime)) : floor;
            float orbit = Mathf.Max(_pitch, state.Floor);

            // Nothing above the target while the camera can swing as far as the tilt, so nothing jumps where one gives way to the other.
            float raise = 0f;
            if (_pitch < orbit)
            {
                float swing = orbit * Mathf.Deg2Rad;
                raise = distance * (Mathf.Cos(swing) * Mathf.Tan(-_pitch * Mathf.Deg2Rad) + Mathf.Sin(swing));
            }
            state.Aim.position = target + Vector3.up * raise;   // straight up in the world, whatever the player's own lean
            return orbit;
        }

        /// <summary>
        /// The lowest a camera may swing round its target and stay a clearance over the ground under it, in degrees of
        /// tilt (under 0: below the target, looking up at it). <paramref name="above"/> is the target's height over that
        /// ground and <paramref name="distance"/> the camera's from the target. Never deeper than 25 degrees under the
        /// target, over a drop; ground that rises behind lifts it to 30 degrees at most. The chase camera's too.
        /// </summary>
        public static float LowestOrbit(float above, float clearance, float distance)
        {
            return -Mathf.Asin(Mathf.Clamp((above - clearance) / distance, -0.5f, SinDeepest)) * Mathf.Rad2Deg;
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

        /// <summary>
        /// How far the camera may be from its target and stay inside <see cref="Inside"/>'s sides and ends (its top is open):
        /// where the line from the target to the camera leaves the box, a little short of it. The desired distance if the
        /// target is not in the box.
        /// </summary>
        private static float Confined(CamState state, float desired)
        {
            Transform frame = Inside;
            if (frame == null)
            {
                return desired;
            }
            Vector3 origin = state.Target.position;
            Vector3 toCamera = state.Cam.transform.position - origin;
            if (toCamera.sqrMagnitude < 0.01f)
            {
                return desired;
            }
            Vector3 o = frame.InverseTransformPoint(origin) - _insideCentre;
            Vector3 d = frame.InverseTransformDirection(toCamera.normalized);
            Vector3 h = _insideHalf;
            if (Mathf.Abs(o.x) > h.x || Mathf.Abs(o.z) > h.z || o.y < -h.y)
            {
                return desired;
            }
            float exit = desired;
            if (Mathf.Abs(d.x) > 1e-4f)
            {
                exit = Mathf.Min(exit, ((d.x > 0f ? h.x : -h.x) - o.x) / d.x);
            }
            if (Mathf.Abs(d.z) > 1e-4f)
            {
                exit = Mathf.Min(exit, ((d.z > 0f ? h.z : -h.z) - o.z) / d.z);
            }
            if (d.y < -1e-4f)
            {
                exit = Mathf.Min(exit, (-h.y - o.y) / d.y);
            }
            return Mathf.Clamp(exit - 0.2f, IndoorMinDistance, desired);
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
