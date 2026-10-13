using System;
using System.IO;
using BAModAPI;
using CameraControllers;
using Cinemachine;
using Helpers;
using Localizor;
using TMPro;
using UI;
using UI.Load;
using UI.MiniMenu;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// Photo mode: one key stops the game (its own pause: the picture goes on being drawn) and
    /// frees the camera to fly, tilt, roll and zoom; exposure, a handful of colour filters and a
    /// depth-of-field focus distance go through a Volume that exists only while the mode is on;
    /// the player can be hidden, and Enter saves the frame as a PNG at the screen's resolution to
    /// the mod's Shots folder. The mod draws nothing of its own: the mode starts with the game's
    /// interface showing and one of the game's notifications naming the controls and the settings;
    /// the "hide or show" key puts the game's interface away (the game's own "hide the interface")
    /// for a clean view, and brings it back with the notification again. A picture is always taken
    /// with the interface away. Everything touched
    /// is recorded on the way in and put back exactly on the way out, on every way out (the
    /// key, a menu opening, the game being unpaused, the city unloading).
    /// One key turns the free camera into a selfie (<see cref="Selfie"/>): the camera is held at
    /// arm's length in front of the player, the mouse and the wheel move it round and nearer, and
    /// every other setting goes on working; the same key brings the free camera back where it was.
    /// Never in a vehicle or on the bus: the game's own notification says so. In a Going Public
    /// session the clock is everyone's and the game cannot be paused for one player: the mode then
    /// leaves the clock running and the speed controls alone (Core/GoingPublic).
    ///
    /// Idle cost: one key test a frame. While the mode is on it runs every frame, which is the
    /// point of it, and allocates only when a notification is asked for.
    /// </summary>
    public sealed class PhotoMode : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("leisure", "PhotoMode", typeof(PhotoMode));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private struct Filter
        {
            public string NameKey;
            public Color Tint;
            public float Saturation, Contrast, Exposure;
        }

        private static readonly Filter[] Filters =
        {
            new Filter { NameKey = "reallife_photo_filter_none", Tint = Color.white },
            new Filter { NameKey = "reallife_photo_filter_warm", Tint = new Color(1f, 0.93f, 0.82f), Saturation = 8f },
            new Filter { NameKey = "reallife_photo_filter_cool", Tint = new Color(0.85f, 0.93f, 1f), Saturation = -4f },
            new Filter { NameKey = "reallife_photo_filter_mono", Tint = Color.white, Saturation = -100f, Contrast = 15f },
            new Filter { NameKey = "reallife_photo_filter_vivid", Tint = Color.white, Saturation = 35f, Contrast = 12f },
            new Filter { NameKey = "reallife_photo_filter_faded", Tint = new Color(1f, 0.97f, 0.93f), Saturation = -30f, Contrast = -18f, Exposure = 0.2f }
        };

        public static IModLogger Logger;
        public static PhotoMode Instance { get; private set; }

        /// <summary>True while photo mode is on: everything of the mod's that acts on keys or draws a HUD stands aside.</summary>
        public static bool Active { get; private set; }

        /// <summary>
        /// Raised when a picture has been saved: its path, whether it is a selfie, and the friend beside the player in it
        /// (the social life's number; -1 for nobody). Heard by Wiring/FlareLinks. A handler that throws is logged and the
        /// mode goes on.
        /// </summary>
        public static event Action<string, bool, int> Saved;

        private Camera _camera;
        private CinemachineBrain _brain;
        private bool _brainWasOn;
        private float _fovBefore;
        private CursorLockMode _cursorBefore;
        private bool _cursorShown;
        private bool _uiWasVisible;
        private bool _zoomWasBlocked;
        private bool _timeWasLocked;
        private bool _timeShared;    // in a Going Public session the clock is everyone's: the mode leaves it running (Core/GoingPublic)
        private bool _playerHidden;
        private GameSpeedController _speed;

        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private float _yaw, _pitch, _roll, _fov;
        private float _focus = 6f;
        private float _exposure;
        private int _filter;
        private bool _blur;
        private bool _help = true;

        private GameObject _volumeObject;
        private VolumeProfile _profile;
        private ColorAdjustments _color;
        private DepthOfField _depth;

        private bool _stateDirty;
        private int _shotStep;          // 0 none; counts frames through a capture with the interface away
        private string _shotPath;
        private bool _armed;            // asked for from the options page: starts when the menu is closed
        private float _armedUntil;
        private int _enteredFrame;
        private bool _failed;

        private const float SelfieFieldOfView = 65f;   // degrees: about what a phone's front camera sees

        private Selfie _selfie;
        private bool _inSelfie;
        private Vector3 _freePosition;  // where the free camera was when the selfie started: it goes back there
        private string _liked;          // the friend who liked the selfie just saved, or null

        private void Awake()
        {
            Instance = this;
            Selfie.Logger = Logger;
            _selfie = gameObject.AddComponent<Selfie>();   // switched off until a selfie is asked for
        }

        private void OnDestroy()
        {
            Leave();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Options-page button. The options page is itself a menu, so the mode starts when it is closed.</summary>
        public void Test()
        {
            if (!Settings.Leisure || !Settings.PhotoMode || _failed)
            {
                TestRefusals.Tell(Logger, "Photo mode", TestRefusal.FeatureOff);
                return;
            }
            if (SaveGameManager.Current == null || PlayerHelper.PlayerController == null || LoadScene.isLoading)
            {
                TestRefusals.Tell(Logger, "Photo mode", TestRefusal.NotLoaded);
                return;
            }
            _armed = true;
            _armedUntil = Time.unscaledTime + 30f;
            UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, "reallife_photo_armed", null, 6f);
        }

        private void Update()
        {
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (_failed)
            {
                return;
            }
            try
            {
                if (!Active)
                {
                    bool asked = Keys.Down(KeyAction.PhotoMode);
                    if (_armed)
                    {
                        if (Time.unscaledTime > _armedUntil)
                        {
                            _armed = false;
                        }
                        else if (!ThirdPersonCamera.IsUiOpen())
                        {
                            _armed = false;
                            asked = true;
                        }
                    }
                    if (asked && Settings.Leisure && Settings.PhotoMode)
                    {
                        Enter();
                    }
                    return;
                }
                Run();
            }
            catch (Exception e)
            {
                _failed = true;
                Logger?.Error(e);
                Logger?.Warn("Photo mode: switched off for this session after the error above; the camera, the HUD and the game's speed were put back.");
                Leave();
            }
        }

        // ---------------------------------------------------------------- in

        private void Enter()
        {
            var uis = InstanceBehavior<UIs>.Instance;
            Camera camera = GameManager.GetMainCamera();
            if (uis == null || camera == null || SaveGameManager.Current == null || PlayerHelper.PlayerController == null || LoadScene.isLoading
                || ThirdPersonCamera.IsUiOpen() || (uis.timeMachine != null && uis.timeMachine.isRunning) || ScreenshotController.isInFreeLookMode)
            {
                Logger?.Info("Photo mode: not now (a menu is open, time is being skipped, or the game's own free camera is on).");
                return;
            }
            CinemachineBrain brain = camera.GetComponent<CinemachineBrain>();
            if (brain == null)
            {
                Logger?.Warn("Photo mode: the main camera has no Cinemachine brain; left alone.");
                return;
            }
            _camera = camera;
            _brain = brain;
            _speed = uis.gameSpeed;

            // Recorded first, changed after: Leave puts back exactly these.
            _brainWasOn = brain.enabled;
            _fovBefore = camera.fieldOfView;
            _cursorBefore = Cursor.lockState;
            _cursorShown = Cursor.visible;
            _zoomWasBlocked = PedestrianCam.blockCameraZoom;
            _timeWasLocked = _speed != null && _speed.isTimeControlDisabled;
            _timeShared = GoingPublic.ClockShared;
            _startPosition = camera.transform.position;
            _startRotation = camera.transform.rotation;

            Active = true;
            _enteredFrame = Time.frameCount;
            if (_speed != null && !_timeShared)
            {
                _speed.SetPause(true, false);
                _speed.DisableTimeControl(true);   // the game's pause key must not start time under a free camera
            }
            brain.enabled = false;
            PedestrianCam.blockCameraZoom = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Vector3 angles = _startRotation.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x > 180f ? angles.x - 360f : angles.x;
            _roll = 0f;
            _fov = _fovBefore;
            _exposure = 0f;
            _filter = 0;
            _blur = false;
            _focus = Mathf.Clamp(Vector3.Distance(_startPosition, PlayerHelper.PlayerController.transform.position), 1f, 40f);

            _uiWasVisible = ScreenshotController.uiIsVisible;
            _help = true;
            Interface(true);
            LeisureDirector director = LeisureDirector.Instance;
            if (director != null && director.Screen != null)
            {
                director.Screen.Suppress(true);   // the activity's panels stay away while the camera is free
            }
            BuildVolume();
            ApplyLook();
            _stateDirty = true;
            Help();
            Logger?.Info("Photo mode: on.");
        }

        // The game's own "hide the interface" (its canvases go clear, nothing is destroyed): put to the state asked for, and only if it is not
        // already there.
        private void Interface(bool visible)
        {
            try
            {
                var uis = InstanceBehavior<UIs>.Instance;
                if (uis != null && uis.screenshot != null && ScreenshotController.uiIsVisible != visible)
                {
                    uis.screenshot.ToggleUIVisibility();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The controls and the settings, as one of the game's notifications.
        private void Help()
        {
            string text = "reallife_photo_help".GetLocalization().Replace("{key}", Keys.Label(KeyAction.PhotoMode))   // (not through JobText: that would put the interact key in)
                          + "\n" + StateWords();
            UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, "reallife_leisure_text",
                new System.Collections.Generic.Dictionary<string, string>(1) { { "text", text } }, 20f);
        }

        private void BuildVolume()
        {
            _volumeObject = new GameObject("RealLifeOverhaul Photo Volume");
            _volumeObject.transform.SetParent(transform, false);
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _color = _profile.Add<ColorAdjustments>();
            _depth = _profile.Add<DepthOfField>();
            var volume = _volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1100f;   // over the mod's own grading (1000)
            volume.sharedProfile = _profile;
            volume.weight = 1f;
        }

        // ---------------------------------------------------------------- while it is on

        private void Run()
        {
            // Anything that takes the screen or starts time ends the mode.
            if (_camera == null || _brain == null || FullMenu.IsOpen || CityMap.IsOpen || MiniMenu.IsOpen || LoadScene.isLoading
                || !Settings.PhotoMode || !Settings.Leisure
                || (!_timeShared && Time.frameCount > _enteredFrame + 30 && Time.timeScale > 0.01f)
                || (Keys.Down(KeyAction.PhotoMode) && Time.frameCount > _enteredFrame))
            {
                Leave();
                return;
            }
            if (_shotStep > 0)
            {
                Shot();
                return;
            }
            float delta = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (_inSelfie && !_selfie.On)
            {
                FreeCamera();   // the selfie ended itself (the player went away, or a fault): back to flying
            }
            if (PhotoKeys.Selfie)
            {
                if (_inSelfie)
                {
                    _selfie.End();
                    FreeCamera();
                }
                else
                {
                    StartSelfie();
                }
            }

            float sensitivity = 0.6f + Settings.MouseSensitivity * 0.2f;
            float mouseX = Input.GetAxis("Mouse X") * sensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * sensitivity;
            if (!_inSelfie)
            {
                _yaw += mouseX * (_fov / 60f);
                _pitch = Mathf.Clamp(_pitch - mouseY * (_fov / 60f), -88f, 88f);
            }
            float roll = PhotoKeys.Roll;
            if (roll != 0f)
            {
                _roll = Mathf.Clamp(_roll - roll * 40f * delta, -90f, 90f);
                _stateDirty = true;
            }
            float wheel = Input.mouseScrollDelta.y;
            if (wheel != 0f && !_inSelfie)
            {
                _fov = Mathf.Clamp(_fov - wheel * 3f, 8f, 100f);
                _stateDirty = true;
            }
            float focus = PhotoKeys.Focus;
            if (focus != 0f)
            {
                _focus = Mathf.Clamp(_focus * (1f + focus * 0.9f * delta), 0.4f, 120f);
                _stateDirty = true;
            }
            float exposure = PhotoKeys.Exposure;
            if (exposure != 0f)
            {
                _exposure = Mathf.Clamp(_exposure + exposure * 1.2f * delta, -3f, 3f);
                _stateDirty = true;
            }
            if (PhotoKeys.NextFilter)
            {
                _filter = (_filter + 1) % Filters.Length;
                _stateDirty = true;
            }
            if (PhotoKeys.ToggleBlur)
            {
                _blur = !_blur;
                _stateDirty = true;
            }
            if (PhotoKeys.TogglePlayer && !_inSelfie)
            {
                ShowPlayer(_playerHidden);
            }
            if (PhotoKeys.NextPose && _inSelfie)
            {
                Say(JobText.T("reallife_photo_pose").Replace("{pose}", JobText.T(_selfie.NextPose())), 5f);
            }
            if (PhotoKeys.ToggleHelp)
            {
                _help = !_help;
                Interface(_help);
                if (_help)
                {
                    Help();
                }
            }
            if (PhotoKeys.ResetView && _inSelfie)
            {
                _selfie.Recentre();
                _focus = _selfie.Distance;
                _roll = 0f;
                _stateDirty = true;
            }
            else if (PhotoKeys.ResetView)
            {
                _camera.transform.SetPositionAndRotation(_startPosition, _startRotation);
                Vector3 angles = _startRotation.eulerAngles;
                _yaw = angles.y;
                _pitch = angles.x > 180f ? angles.x - 360f : angles.x;
                _roll = 0f;
                _fov = _fovBefore;
                _stateDirty = true;
            }

            if (_inSelfie)
            {
                float was = _selfie.Distance;
                _selfie.Steer(mouseX, mouseY, wheel, _roll);
                if (_blur && Mathf.Abs(_selfie.Distance - was) > 0.001f)
                {
                    _focus = _selfie.Distance;   // the face stays sharp as the phone comes nearer or goes farther
                    _stateDirty = true;
                }
            }
            else
            {
                Fly(delta);
            }

            if (_stateDirty)
            {
                _stateDirty = false;
                ApplyLook();
            }
            if (PhotoKeys.Shoot)
            {
                _shotStep = 1;
            }
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        // The free camera: W A S D and the rest, within 150 m of the player.
        private void Fly(float delta)
        {
            Transform view = _camera.transform;
            Quaternion look = Quaternion.Euler(_pitch, _yaw, _roll);
            float speed = 6f * (PhotoKeys.Fast ? 4f : 1f) * (PhotoKeys.Slow ? 0.2f : 1f) * delta;
            Vector3 move = look * new Vector3(PhotoKeys.Sideways, 0f, PhotoKeys.Forward) + Vector3.up * PhotoKeys.Rise;
            Vector3 position = view.position + move * speed;
            // Never further from the player than the city is drawn round them.
            Vector3 home = PlayerHelper.PlayerController != null ? PlayerHelper.PlayerController.transform.position : _startPosition;
            Vector3 away = position - home;
            if (away.sqrMagnitude > 150f * 150f)
            {
                position = home + away.normalized * 150f;
            }
            view.SetPositionAndRotation(position, look);
            _camera.fieldOfView = _fov;
        }

        // The selfie: on foot only (indoors or out); the game's own notification says why not otherwise.
        private void StartSelfie()
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || PlayerHelper.IsUsingVehicle || TransitDirector.Riding)
            {
                UI.Notification.Notifications.Show(UI.Notification.NotificationType.Error, "reallife_photo_selfie_vehicle", null, 6f);
                return;
            }
            if (_playerHidden)
            {
                ShowPlayer(true);   // a selfie of nobody is not one
            }
            _freePosition = _camera.transform.position;
            if (!_selfie.Begin(_camera, player, _freePosition))
            {
                UI.Notification.Notifications.Show(UI.Notification.NotificationType.Error, "reallife_photo_selfie_cannot", null, 6f);
                return;
            }
            _inSelfie = true;
            _camera.fieldOfView = SelfieFieldOfView;   // the free camera's zoom is its own: Fly puts it back
            _focus = _selfie.Distance;
            _stateDirty = true;
            if (_help)
            {
                Say(JobText.T("reallife_photo_selfie_help").Replace("{pose}", JobText.T(_selfie.PoseKey)), 12f);
            }
            Logger?.Info("Photo mode: selfie.");
        }

        // Back to flying from where the free camera was before the selfie, looking the way it looked.
        private void FreeCamera()
        {
            _inSelfie = false;
            _camera.transform.SetPositionAndRotation(_freePosition, Quaternion.Euler(_pitch, _yaw, _roll));
            _stateDirty = true;
        }

        // One line in one of the game's notifications. Only on a key press or a saved picture: the text is built then.
        private static void Say(string text, float seconds)
        {
            UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, "reallife_leisure_text",
                new System.Collections.Generic.Dictionary<string, string>(1) { { "text", text } }, seconds);
        }

        // The Volume's values and the line that names them. Called when one changes.
        private void ApplyLook()
        {
            Filter filter = Filters[_filter];
            bool graded = _filter != 0;
            _color.postExposure.overrideState = _exposure != 0f || filter.Exposure != 0f;
            _color.postExposure.value = _exposure + filter.Exposure;
            _color.colorFilter.overrideState = graded;
            _color.colorFilter.value = filter.Tint;
            _color.saturation.overrideState = graded;
            _color.saturation.value = filter.Saturation;
            _color.contrast.overrideState = graded;
            _color.contrast.value = filter.Contrast;

            _depth.focusMode.overrideState = _blur;
            _depth.focusMode.value = DepthOfFieldMode.Manual;
            _depth.nearFocusStart.overrideState = _blur;
            _depth.nearFocusStart.value = 0f;
            _depth.nearFocusEnd.overrideState = _blur;
            _depth.nearFocusEnd.value = _focus * 0.7f;
            _depth.farFocusStart.overrideState = _blur;
            _depth.farFocusStart.value = _focus * 1.5f;
            _depth.farFocusEnd.overrideState = _blur;
            _depth.farFocusEnd.value = _focus * 3.5f + 2f;

        }

        // The settings in words.
        private string StateWords()
        {
            return JobText.T("reallife_photo_state")
                .Replace("{fov}", Mathf.RoundToInt(_fov).ToString())
                .Replace("{roll}", Mathf.RoundToInt(_roll).ToString())
                .Replace("{exposure}", _exposure.ToString("+0.0;-0.0;0.0", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{filter}", JobText.T(Filters[_filter].NameKey))
                .Replace("{focus}", _blur ? _focus.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m" : JobText.T("reallife_photo_focus_off"));
        }

        private void ShowPlayer(bool show)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || PlayerHelper.IsUsingVehicle)
            {
                return;   // in a car the game has already hidden them, and it is the game that shows them again
            }
            if (show)
            {
                player.Show();
            }
            else
            {
                player.Hide();
            }
            _playerHidden = !show;
        }

        // A capture takes three frames: the overlay off, the frame grabbed, the overlay back.
        private void Shot()
        {
            switch (_shotStep)
            {
                case 1:
                    Interface(false);
                    _shotStep = 2;
                    break;
                case 2:
                    try
                    {
                        string folder = Path.Combine(Application.persistentDataPath, "RealLifeOverhaul", "Shots");
                        Directory.CreateDirectory(folder);
                        _shotPath = Path.Combine(folder, (_inSelfie ? "RealLifeOverhaul_Selfie_" : "RealLifeOverhaul_") + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                        ScreenCapture.CaptureScreenshot(_shotPath);
                        LeisureSound.Cue(LeisureCue.Shutter);
                        Logger?.Info("Photo mode: saved " + _shotPath + " (" + Screen.width + " x " + Screen.height + ").");
                        // A selfie on an outing with the friend beside the player: they like it (once an outing).
                        _liked = _inSelfie && PlayerHelper.PlayerController != null ? SocialHooks.Selfie(PlayerHelper.PlayerController.transform.position) : null;
                    }
                    catch (Exception e)
                    {
                        Logger?.Error(e);
                        _shotPath = null;
                    }
                    if (_shotPath != null && Saved != null)
                    {
                        try
                        {
                            Saved(_shotPath, _inSelfie, PlayerHelper.PlayerController != null ? SocialHooks.Companion(PlayerHelper.PlayerController.transform.position) : -1);
                        }
                        catch (Exception e)
                        {
                            Logger?.Error(e);
                        }
                    }
                    _shotStep = 3;
                    break;
                default:
                    _shotStep = 0;
                    Interface(_help);
                    if (_help)
                    {
                        Say(_shotPath != null ? JobText.T("reallife_photo_saved").Replace("{path}", _shotPath) : JobText.T("reallife_photo_save_failed"), 8f);
                    }
                    if (_liked != null)
                    {
                        // Said with the interface away too: it is news, and it waits in the game's list.
                        Say(JobText.T("reallife_photo_selfie_liked").Replace("{name}", _liked), 8f);
                        _liked = null;
                    }
                    break;
            }
        }

        // ---------------------------------------------------------------- out

        /// <summary>Puts everything back. Safe to call when the mode is not on, and never throws.</summary>
        public void Leave()
        {
            if (!Active)
            {
                return;
            }
            Active = false;
            _shotStep = 0;
            _liked = null;
            try
            {
                if (_selfie != null)
                {
                    _selfie.End();   // first: the body's facing and the near plane, before the camera goes back
                }
                _inSelfie = false;
                if (_playerHidden)
                {
                    ShowPlayer(true);
                }
                _playerHidden = false;
                if (_camera != null)
                {
                    _camera.fieldOfView = _fovBefore;
                    _camera.transform.SetPositionAndRotation(_startPosition, _startRotation);
                }
                if (_brain != null)
                {
                    _brain.enabled = _brainWasOn;
                }
                PedestrianCam.blockCameraZoom = _zoomWasBlocked;
                Cursor.lockState = _cursorBefore;
                Cursor.visible = _cursorShown;
                Interface(_uiWasVisible);
                LeisureDirector director = LeisureDirector.Instance;
                if (director != null && director.Screen != null)
                {
                    director.Screen.Suppress(false);
                }
                if (_speed != null && !_timeShared)
                {
                    _speed.DisableTimeControl(_timeWasLocked);
                    _speed.Reset();   // back to the speed, or the pause, the player had
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (_volumeObject != null)
            {
                Destroy(_volumeObject);
            }
            if (_profile != null)
            {
                Destroy(_profile);
            }
            _volumeObject = null;
            _profile = null;
            _color = null;
            _depth = null;
            _camera = null;
            _brain = null;
            _speed = null;
            Logger?.Info("Photo mode: off; camera, HUD and game speed put back.");
        }
    }
}
