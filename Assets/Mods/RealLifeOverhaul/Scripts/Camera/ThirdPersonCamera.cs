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
    /// A third-person camera on foot, made by driving the public fields of the game's own PedestrianCam (offset, distance,
    /// angle), so the game keeps doing the positioning and zooming. With mouse look the pointer is locked and the mouse
    /// orbits the camera; the game's hover and click ray then runs from the screen's centre, a crosshair.
    ///
    /// It follows <see cref="CameraYield.Orders"/>: for any camera of the game's not of ordinary play, the figures are given
    /// back exactly as taken (<see cref="FigureHandover"/>) once that camera has left the picture, and taken again when play
    /// is back, the player's distance kept across it. The placement camera copies the indoor camera's distance as it opens:
    /// what it copied of this mod's is put back to the game's own.
    ///
    /// Per frame: the orders read, the figures written to the cameras that carry them and, while one is looked through,
    /// at most one ray for the ground under it out of doors, or one sphere cast indoors.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("camera", "ThirdPersonCamera", typeof(ThirdPersonCamera));

        private const float MinMoveSpeed = 0.6f;   // m/s before the camera starts swinging behind
        private const float MinDistance = 2.5f;    // metres
        private const float MinPitch = -60f;       // degrees, out of doors; under 0 the view looks up, to the tops of the buildings
        private const float MaxPitch = 80f;        // degrees

        public static IModLogger Logger;

        /// <summary>True while the mouse is steering the camera (cursor locked).</summary>
        public static bool IsLooking { get; private set; }

        /// <summary>True after the player pressed the free-cursor key (Left Alt unless rebound).</summary>
        public static bool CursorFreed { get; private set; }

        /// <summary>True while the look-behind key (the right mouse button unless rebound) is held in a car.</summary>
        public static bool LookBehind { get; private set; }

        /// <summary>True while one of the game's on-foot cameras carries this mod's figures.</summary>
        public static bool Steering { get; private set; }

        /// <summary>While set, the camera watches this instead of the player's root: the body of a player knocked down.</summary>
        public static Transform Focus { get; set; }

        /// <summary>
        /// While set, the player rides inside it (the mod's bus): out of doors the camera keeps within its sides and ends,
        /// coming in closer as a wall would have it; its top is open. One box test a frame while set.
        /// </summary>
        public static Transform Inside { get; private set; }

        private static Vector3 _insideCentre;
        private static Vector3 _insideHalf;

        /// <summary>Keeps the out-of-doors camera inside a box of a frame (its middle and half sizes in the frame's space).</summary>
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

        private const float LookBehindHoldTime = 0.2f;   // seconds: a shorter right click stays the game's own right-click action

        // The game flags a right-button drag here to stop the release from also firing a right-click action.
        private static readonly FieldInfo RightButtonHeldField = GameContract.RightButtonHeld.Field;

        private float _rightHeldFor;

        private sealed class CamState
        {
            public PedestrianCam Cam;
            public CinemachineVirtualCameraBase VCam;
            public FigureHandover Handover;         // the game's figures as taken, given back exactly
            public bool Applied;
            public bool Known;                      // a distance has been wanted since the city loaded
            public int AppliedDistance;
            public CinemachineVirtualCamera Lens;   // null if the camera is not a plain virtual camera
            public Transform Follow;                // the game's follow and look-at points, as taken
            public Transform LookAt;
            public Transform Target;                // the orbit and look-at point at the player's upper body
            public Transform Aim;                   // what the camera looks at: the target, or above it when the view looks up past the lowest swing
            public float Floor;                     // out of doors, the lowest the camera may swing, in degrees of tilt, eased
            public bool Indoor;
            public float Desired;                   // the distance the player asked for, before walls pull the camera in
            public float LastWritten;
        }

        private const float IndoorMinDistance = 1f;   // metres
        private const float IndoorMinPitch = 4f;      // degrees: interiors are built to be seen from above, and looking up shows no ceiling
        private const float CollisionRadius = 0.25f;  // metres
        private static readonly RaycastHit[] CollisionHits = new RaycastHit[24];

        // Out of doors the view may look up further than the camera can swing down: the camera stops over the ground
        // and the rest of the tilt is a look upwards from where it stands (Orbit).
        private const float GroundClearance = 0.45f;   // metres between the camera and the ground under it
        private const float LowOrbit = 15f;            // degrees: at this tilt or steeper the camera is well over the ground, not looked at
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

        private PlacementCam _placement;   // the game's camera for placing items and the interior designer, found when first needed
        private bool _moment;              // the last frame stood aside for a moment of the game's

        private static ThirdPersonCamera _instance;   // for the feature's clean-up

        /// <summary>True while the on-foot camera carrying this mod's figures is live, in a blend out of it too.</summary>
        public static bool InPicture { get; private set; }

        private void Awake()
        {
            _instance = this;
            CameraYield.Reset();   // a new city: nothing has been seen of another mod's camera in it yet
            ScenePrewarm.Add("camera: the city's bridges being found", BridgeHold.Prewarm);
        }

        private static readonly int HotUpdate = HotSpots.Slot("Camera: ThirdPersonCamera.Update");
        // The frame's work is a method of its own, so that a game lacking something it names fails there, inside the try:
        // said once, the game's cameras given back, and the mod's camera off for the session.
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults
                GiveBack();
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
                    GiveBack();
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
                BridgeHold.Hold(false);
                return;
            }
            CameraYield.Observe();
            bool mine = !CameraYield.StoodDown;   // false: another mod has the camera, and nothing below touches it
            CameraOrders orders = CameraYield.Orders;
            BridgeHold.Hold(orders.HoldBridges);

            if (_outdoor == null)
            {
                _outdoor = Capture(gm.pedestrianCamera, "outdoor");
                _indoor = Capture(gm.indoorCamera, "indoor");
                if (_indoor != null)
                {
                    _indoor.Indoor = true;
                }
            }

            // The camera key switches between this camera and the game's, on foot and in a car. Empire Motors' view acts on
            // V too: while the key is V that view is kept off, so a press in a car does one thing.
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

            // A camera of the game's not of ordinary play gets the game's figures back only once it has left the picture,
            // so its blend in starts from where the game's own camera was.
            if (orders.Moment && !_moment)
            {
                Uncopy(gm);   // the first frame of the moment, before its camera has blended in
            }
            bool ended = _moment && !orders.Moment;
            _moment = orders.Moment;
            Transform player = gm.playerController.transform;
            Apply(_outdoor, Keep(_outdoor, orders.OutdoorFigures, orders.OutdoorLinger), player, ended);
            Apply(_indoor, Keep(_indoor, orders.IndoorFigures, orders.IndoorLinger), player, ended);
            InPicture = _outdoor != null && _outdoor.Applied && CinemachineCore.Instance.IsLive(_outdoor.VCam);
            Steering = (_outdoor != null && _outdoor.Applied) || (_indoor != null && _indoor.Applied);

            if (Keys.Down(KeyAction.FreeCursor))
            {
                CursorFreed = !CursorFreed;
            }

            // The pedestrian cameras are used on scooters and hand trucks too, so those get mouse look as well.
            CamState active = ActiveState();
            bool look = orders.Look && active != null;
            SetLooking(look);
            UpdateLookBehind();

            if (look)
            {
                MouseLook(active);
            }
            else
            {
                FollowPlayer(gm, active);
            }
            TrackPlayer(gm);
        }

        // Ordered, or given back for a moment of the game's but kept until that camera has left the picture.
        private static bool Keep(CamState state, bool ordered, bool linger)
        {
            return ordered || (linger && state != null && state.Applied && CinemachineCore.Instance.IsLive(state.VCam));
        }

        // The placement camera copies the indoor camera's distance as it opens and keeps it in the save: if what it copied
        // is this mod's, it gets the game's own instead, before it has blended in. Once a moment.
        private void Uncopy(GameManager gm)
        {
            if (_indoor == null || !_indoor.Applied)
            {
                return;   // the indoor camera had the game's own figures
            }
            if (_placement == null && gm.indoorPlacementCamera != null)
            {
                _placement = gm.indoorPlacementCamera.GetComponentInParent<PlacementCam>();
            }
            if (_placement == null)
            {
                return;
            }
            float copied = _placement.distance;
            float own = CameraYieldRules.Uncopied(copied, _indoor.LastWritten, _indoor.Handover.Game.Distance);
            if (own != copied)
            {
                _placement.distance = own;   // its own frame places its camera at this distance
                Logger?.Info(System.FormattableString.Invariant($"Camera: the placement camera had copied this mod's indoor distance ({copied:F1} m); it has the game's own ({own:F1} m)."));
            }
        }

        private void OnDestroy()
        {
            GiveBack();   // the bridge too: the city unloading
            DestroyTarget(_outdoor);
            DestroyTarget(_indoor);
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Gives the pointer, both on-foot cameras' figures and the held bridge back to the game at once: for the feature's
        /// clean-up and the options page. Safe at any time.
        /// </summary>
        public static void StandAside()
        {
            if (_instance != null)
            {
                _instance.GiveBack();
            }
            BridgeHold.Release();
        }

        private void GiveBack()
        {
            SetLooking(false);
            Apply(_outdoor, false, null);
            Apply(_indoor, false, null);
            Steering = false;
            InPicture = false;
            BridgeHold.Release();
        }

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
                Destroy(state.Target.gameObject);   // the aim point goes with it, as its child
            }
        }

        /// <summary>Any screen that needs the mouse cursor. Left Alt covers the ones not listed here.</summary>
        public static bool IsUiOpen()
        {
            // Photo mode and the phone's camera are the mod's own screens over the world.
            return Leisure.PhotoMode.Active || Photos.CameraMode.Active || AnyWindow(true);
        }

        /// <summary>A window that needs the pointer, the mod's own cameras left out; <paramref name="menuCounts"/>: whether the full menu counts.</summary>
        public static bool AnyWindow(bool menuCounts)
        {
            // The item and vehicle panel is not counted: it is up the whole time the player drives or carries something,
            // and it works by keys.
            return (menuCounts && FullMenu.IsOpen) || CityMap.IsOpen || MiniMenu.IsOpen || BuildingPreview.isPreviewing
                   || HudConfirm.isOpen || Feedback.IsOpen || PlayerActivityUI.IsPanelOpen
                   || PurchaseUI.IsPanelOpen || PurchaseVehicleUI.IsPanelOpen || InteriorDesignerUI.IsOpen
                   || BlueprintsPanel.IsOpen || AcknowledgeWarning.isOpen
                   || CustomColorPicker.isOpen || LoadScene.isLoading
                   || BigAmbitions.PlacementSystem.PlacementSystem.IsInPlacementMode
                   || GameManager.ShouldBlockKeyboardShortcuts()

                   || Meta.MetaUi.AnyOpen        // the options page's messages and questions
                   || Economy.Dealership.ShowroomView.Active   // the Car Showroom's camera, and choosing a bay
                   || JusticePanel.IsOpen        // bail, hearings, the Legal file
                   || GameUi.TravelList.AnyOpen; // the game's own list window: bus stops, the cooking list, the custom shop and more
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

        // The camera carrying this mod's figures that the game is looking through.
        private CamState ActiveState()
        {
            GameView view = CameraYield.View;
            if (view == GameView.OnFoot && _outdoor != null && _outdoor.Applied)
            {
                return _outdoor;
            }
            if (view == GameView.Indoor && _indoor != null && _indoor.Applied)
            {
                return _indoor;
            }
            return null;
        }

        private void MouseLook(CamState state)
        {
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
            Logger?.Info($"Camera: {label} vanilla offset={cam.offset} minMax={cam.minMaxDistance} distance={cam.distance} "
                         + $"fov={(lens != null ? lens.m_Lens.FieldOfView : 0f)} near={(lens != null ? lens.m_Lens.NearClipPlane : 0f)} far={(lens != null ? lens.m_Lens.FarClipPlane : 0f)} "
                         + $"follow={(vcam.Follow != null ? vcam.Follow.name : "none")} lookAt={(vcam.LookAt != null ? vcam.LookAt.name : "none")}");
            return new CamState { Cam = cam, VCam = vcam, Lens = lens };
        }

        private static CameraFigures Figures(CamState state)
        {
            PedestrianCam cam = state.Cam;
            return new CameraFigures
            {
                OffsetX = cam.offset.x,
                OffsetY = cam.offset.y,
                OffsetZ = cam.offset.z,
                Near = cam.minMaxDistance.x,
                Far = cam.minMaxDistance.y,
                Distance = cam.distance,
                Speed = cam.distanceSpeed,
                Fov = state.Lens != null ? state.Lens.m_Lens.FieldOfView : 0f
            };
        }

        /// <summary>
        /// This mod's figures on one of the game's on-foot cameras, or the game's own given back exactly as taken. Each take
        /// writes the game's figures down first, so what the game changed while it had the camera is what is given back.
        /// </summary>
        private void Apply(CamState state, bool enabled, Transform player, bool momentEnded = false)
        {
            if (state == null || state.Cam == null)
            {
                return;
            }

            if (!enabled)
            {
                if (state.Handover.Give(out CameraFigures back))
                {
                    state.Cam.offset = new Vector3(back.OffsetX, back.OffsetY, back.OffsetZ);
                    state.Cam.minMaxDistance = new Vector2(back.Near, back.Far);
                    state.Cam.distance = back.Distance;
                    state.Cam.distanceSpeed = back.Speed;
                    state.VCam.Follow = state.Follow;
                    state.VCam.LookAt = state.LookAt;
                    SetFov(state, back.Fov);
                    state.Applied = false;
                    if (Meta.Dev.Verbose)
                    {
                        bool same = Figures(state).Same(back) && state.VCam.Follow == state.Follow && state.VCam.LookAt == state.LookAt;
                        Logger?.Info(System.FormattableString.Invariant(
                            $"Camera check (given back, {(state.Indoor ? "indoor" : "on foot")}): the game's figures as taken (distance {back.Distance:F1} m, lens {back.Fov:F0}): ")
                                     + (same ? "PASS" : "FAIL (the camera does not have them)") + ".");
                    }
                }
                return;
            }

            bool taken = state.Handover.Take(Figures(state));
            if (taken)
            {
                state.Follow = state.VCam.Follow;   // the game's own points, as it has them now
                state.LookAt = state.VCam.LookAt;
            }
            CameraFigures game = state.Handover.Game;
            bool fresh = CameraYieldRules.Fresh(taken, momentEnded);

            // The game's camera is a long-lens top-down view aimed at the feet; up close that frames only the legs, so the
            // camera orbits and aims at the upper body instead, with a wider lens.
            if (state.Follow != null && player != null)
            {
                if (state.Target == null)
                {
                    state.Target = new GameObject("RealLifeOverhaul Camera Target").transform;
                }
                if (state.Target.parent != state.Follow)
                {
                    state.Target.SetParent(state.Follow, false);
                }
                if (state.Aim == null)
                {
                    state.Aim = new GameObject("RealLifeOverhaul Camera Aim").transform;
                    state.Aim.SetParent(state.Target, false);
                }
            // Parented to the game's follow point, so it moves with the player without a frame of lag.
                Vector3 follow = Focus != null ? Focus.position : state.Follow.position;
                state.Target.position = new Vector3(follow.x, player.position.y + Settings.CameraHeight / 10f, follow.z);
                state.VCam.Follow = state.Target;
                if (state.LookAt != null)
                {
                    state.VCam.LookAt = state.Aim;
                }
            }
            SetFov(state, Settings.CameraFov);

            // The game's compass direction of the offset is kept; only how high the camera swings round changes.
            var flat = new Vector3(game.OffsetX, 0f, game.OffsetZ);
            flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.back;
            float pitch = Orbit(state, flat) * Mathf.Deg2Rad;
            state.Cam.offset = flat * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch);
            bool confined = !state.Indoor && Inside != null && state.Target != null;
            float min = state.Indoor || confined ? IndoorMinDistance : MinDistance;
            float max = Mathf.Max(game.Far, min + 1f);
            state.Cam.minMaxDistance = new Vector2(min, max);

            // Whatever the scroll wheel changed since this mod's last write is the player's zoom; what the game wrote while
            // it had the camera is not.
            state.Desired = CameraYieldRules.Wanted(fresh, state.Known, state.AppliedDistance != Settings.CameraDistance, Settings.CameraDistance,
                state.Desired, state.Cam.distance, state.LastWritten, min, max);
            state.Known = true;
            state.AppliedDistance = Settings.CameraDistance;

            // Interiors are built for the top-down view, so indoors the camera stays in front of walls; only while that
            // camera is looked through, as a cast for one that is not live is wasted.
            float distance = state.Indoor && CinemachineCore.Instance.IsLive(state.VCam) ? Collide(state, player, state.Desired)
                : confined && CinemachineCore.Instance.IsLive(state.VCam) ? Confined(state, state.Desired) : state.Desired;
            state.Cam.distance = distance;
            state.LastWritten = distance;
            state.Cam.distanceSpeed = state.Indoor || confined ? 14f : game.Speed;
            state.Applied = true;
        }

        /// <summary>
        /// How high the camera swings round its target, in degrees of tilt, and what it looks at. Indoors that is the tilt, at
        /// least <see cref="IndoorMinPitch"/>. Out of doors the view may look up to the skyline, but the camera stops
        /// <see cref="GroundClearance"/> over the ground; past that it aims above the target, as high as makes the view's tilt
        /// the one asked for. One ray at most, and only while the camera may swing low enough for the ground to matter.
        /// </summary>
        private float Orbit(CamState state, Vector3 flat)
        {
            if (state.Indoor || state.Target == null || state.Aim == null)
            {
                return Mathf.Max(_pitch, IndoorMinPitch);   // with no target of its own there is nothing to aim above either
            }

            Vector3 target = state.Target.position;
            // The game eases the camera to its distance, so the distance that counts is where it is now.
            float distance = Mathf.Max(Vector3.Distance(state.Cam.transform.position, target), 0.5f);

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

            // Nothing above the target while the camera can swing as far as the tilt, so nothing jumps between the two.
            float raise = 0f;
            if (_pitch < orbit)
            {
                float swing = orbit * Mathf.Deg2Rad;
                raise = distance * (Mathf.Cos(swing) * Mathf.Tan(-_pitch * Mathf.Deg2Rad) + Mathf.Sin(swing));
            }
            state.Aim.position = target + Vector3.up * raise;   // straight up in the world, whatever the player's lean
            return orbit;
        }

        /// <summary>
        /// The lowest a camera may swing round its target and stay a clearance over the ground, in degrees of tilt (under 0,
        /// below the target looking up). <paramref name="above"/> is the target's height over that ground. Never deeper than
        /// 25 degrees under the target, nor higher than 30. The chase camera's too.
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
                // Distance 0 means the cast started inside the collider; the player's own colliders do not count either.
                if (hit.distance <= 0f || hit.collider.transform.IsChildOf(player))
                {
                    continue;
                }
                nearest = Mathf.Min(nearest, hit.distance);
            }
            return Mathf.Max(IndoorMinDistance, nearest - 0.15f);
        }

        /// <summary>
        /// How far the camera may be from its target and stay inside <see cref="Inside"/>'s sides and ends: where the line
        /// from the target to the camera leaves the box, a little short of it; the desired distance if the target is outside.
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

        private void FollowPlayer(GameManager gm, CamState active)
        {
            float dt = Time.unscaledDeltaTime;
            if (active == null || !_hasLastPos || dt <= 0f || Settings.CameraFollow <= 0 || Settings.MouseLook)
            {
                return;
            }

            // The right mouse button is the game's manual camera rotation: never fight it.
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
            Steer(active, heading, blend);
        }

        private static void Steer(CamState state, float heading, float blend)
        {
            // PedestrianCam places the camera at offset turned by angle round the player, so the camera is behind the player
            // when offsetYaw + angle == heading + 180.
            Vector3 offset = state.Cam.offset;
            float offsetYaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            float target = heading + 180f - offsetYaw;
            state.Cam.angle = Mathf.LerpAngle(state.Cam.angle, target, blend);
        }
    }
}
