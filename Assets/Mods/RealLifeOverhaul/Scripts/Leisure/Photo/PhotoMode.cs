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
    /// the game's HUD and the mod's are hidden, the player can be too, and Enter saves the
    /// frame as a PNG at the screen's resolution to the mod's Shots folder. Everything touched
    /// is recorded on the way in and put back exactly on the way out, on every way out (the
    /// key, a menu opening, the game being unpaused, the city unloading).
    ///
    /// Idle cost: one key test a frame. While the mode is on it runs every frame, which is the
    /// point of it, and allocates only when a setting changes (the line that names it).
    /// </summary>
    public sealed class PhotoMode : MonoBehaviour
    {
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

        private Camera _camera;
        private CinemachineBrain _brain;
        private bool _brainWasOn;
        private float _fovBefore;
        private CursorLockMode _cursorBefore;
        private bool _cursorShown;
        private bool _hidUi;
        private bool _zoomWasBlocked;
        private bool _timeWasLocked;
        private bool _playerHidden;
        private bool _hudWasOn, _jobHudWasOn;
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

        private GameObject _overlay;
        private Canvas _overlayCanvas;
        private TextMeshProUGUI _helpText, _stateText;
        private bool _stateDirty;
        private int _shotStep;          // 0 none; counts frames through a capture with the overlay off
        private string _shotPath;
        private float _savedUntil;
        private bool _armed;            // asked for from the options page: starts when the menu is closed
        private float _armedUntil;
        private int _enteredFrame;
        private bool _failed;

        private void Awake()
        {
            Instance = this;
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
            _startPosition = camera.transform.position;
            _startRotation = camera.transform.rotation;

            Active = true;
            _enteredFrame = Time.frameCount;
            if (_speed != null)
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

            HideHuds(true);
            BuildVolume();
            BuildOverlay();
            ApplyLook();
            _stateDirty = true;
            Logger?.Info("Photo mode: on.");
        }

        private void HideHuds(bool hide)
        {
            var uis = InstanceBehavior<UIs>.Instance;
            if (hide)
            {
                _hidUi = false;
                try
                {
                    if (uis != null && uis.screenshot != null && ScreenshotController.uiIsVisible)
                    {
                        _hidUi = true;   // before the call: if it faults half way, the way out still toggles it back
                        uis.screenshot.ToggleUIVisibility();   // the game's own "hide the interface"
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                _hudWasOn = Hud.Exists && SetCanvas(Hud.Instance, false);
                _jobHudWasOn = JobHud.Exists && SetCanvas(JobHud.Instance, false);
                if (LeisureHud.Exists)
                {
                    LeisureHud.Instance.Suppress(true);
                }
                return;
            }
            try
            {
                if (_hidUi && uis != null && uis.screenshot != null && !ScreenshotController.uiIsVisible)
                {
                    uis.screenshot.ToggleUIVisibility();   // its first act is to show the interface again
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _hidUi = false;
            if (_hudWasOn && Hud.Exists)
            {
                SetCanvas(Hud.Instance, true);
            }
            if (_jobHudWasOn && JobHud.Exists)
            {
                SetCanvas(JobHud.Instance, true);
            }
            _hudWasOn = false;
            _jobHudWasOn = false;
            if (LeisureHud.Exists)
            {
                LeisureHud.Instance.Suppress(false);
            }
        }

        // Switches a HUD's canvas; answers whether it was on before.
        private static bool SetCanvas(Component hud, bool on)
        {
            Canvas canvas = hud.GetComponent<Canvas>();
            if (canvas == null)
            {
                return false;
            }
            bool was = canvas.enabled;
            canvas.enabled = on;
            return was;
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

        private void BuildOverlay()
        {
            _overlay = new GameObject("RealLifeOverhaul Photo Overlay");
            _overlay.transform.SetParent(transform, false);
            _overlayCanvas = _overlay.AddComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = 50;
            var scaler = _overlay.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            _helpText = Line("Help", 44f, 17f);
            _stateText = Line("State", 16f, 19f);
            _helpText.SetText("reallife_photo_help".GetLocalization().Replace("{key}", Keys.Label(KeyAction.PhotoMode)));   // (not through JobText: that would put the interact key in)
        }

        private TextMeshProUGUI Line(string objectName, float fromBottom, float size)
        {
            var plate = new GameObject(objectName, typeof(RectTransform));
            plate.transform.SetParent(_overlay.transform, false);
            var rect = (RectTransform)plate.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, fromBottom);
            rect.sizeDelta = new Vector2(1800f, 26f);
            var image = plate.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.45f);
            image.raycastTarget = false;
            var holder = new GameObject("Text", typeof(RectTransform));
            holder.transform.SetParent(plate.transform, false);
            var text = holder.AddComponent<TextMeshProUGUI>();
            var textRect = (RectTransform)holder.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = size;
            text.color = Color.white;
#pragma warning disable CS0618
            text.enableWordWrapping = false;
#pragma warning restore CS0618
            if (Hud.Font != null)
            {
                text.font = Hud.Font;
            }
            return text;
        }

        // ---------------------------------------------------------------- while it is on

        private void Run()
        {
            // Anything that takes the screen or starts time ends the mode.
            if (_camera == null || _brain == null || FullMenu.IsOpen || CityMap.IsOpen || MiniMenu.IsOpen || LoadScene.isLoading
                || !Settings.PhotoMode || !Settings.Leisure
                || (Time.frameCount > _enteredFrame + 30 && Time.timeScale > 0.01f)
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

            float sensitivity = 0.6f + Settings.MouseSensitivity * 0.2f;
            _yaw += Input.GetAxis("Mouse X") * sensitivity * (_fov / 60f);
            _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * sensitivity * (_fov / 60f), -88f, 88f);
            float roll = PhotoKeys.Roll;
            if (roll != 0f)
            {
                _roll = Mathf.Clamp(_roll - roll * 40f * delta, -90f, 90f);
                _stateDirty = true;
            }
            float wheel = Input.mouseScrollDelta.y;
            if (wheel != 0f)
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
            if (PhotoKeys.TogglePlayer)
            {
                ShowPlayer(_playerHidden);
            }
            if (PhotoKeys.ToggleHelp)
            {
                _help = !_help;
                _overlayCanvas.enabled = _help;
            }
            if (PhotoKeys.ResetView)
            {
                _camera.transform.SetPositionAndRotation(_startPosition, _startRotation);
                Vector3 angles = _startRotation.eulerAngles;
                _yaw = angles.y;
                _pitch = angles.x > 180f ? angles.x - 360f : angles.x;
                _roll = 0f;
                _fov = _fovBefore;
                _stateDirty = true;
            }

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

            if (_stateDirty)
            {
                _stateDirty = false;
                ApplyLook();
            }
            if (_savedUntil > 0f && Time.unscaledTime >= _savedUntil)
            {
                _savedUntil = 0f;
                _stateDirty = true;   // the line goes back to naming the settings
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

            if (_stateText != null && _savedUntil <= 0f)
            {
                _stateText.SetText(JobText.T("reallife_photo_state")
                    .Replace("{fov}", Mathf.RoundToInt(_fov).ToString())
                    .Replace("{roll}", Mathf.RoundToInt(_roll).ToString())
                    .Replace("{exposure}", _exposure.ToString("+0.0;-0.0;0.0", System.Globalization.CultureInfo.InvariantCulture))
                    .Replace("{filter}", JobText.T(filter.NameKey))
                    .Replace("{focus}", _blur ? _focus.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m" : JobText.T("reallife_photo_focus_off")));
            }
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
                    _overlayCanvas.enabled = false;
                    _shotStep = 2;
                    break;
                case 2:
                    try
                    {
                        string folder = Path.Combine(Application.persistentDataPath, "RealLifeOverhaul", "Shots");
                        Directory.CreateDirectory(folder);
                        _shotPath = Path.Combine(folder, "RealLifeOverhaul_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                        ScreenCapture.CaptureScreenshot(_shotPath);
                        LeisureSound.Cue(LeisureCue.Shutter);
                        Logger?.Info("Photo mode: saved " + _shotPath + " (" + Screen.width + " x " + Screen.height + ").");
                    }
                    catch (Exception e)
                    {
                        Logger?.Error(e);
                        _shotPath = null;
                    }
                    _shotStep = 3;
                    break;
                default:
                    _shotStep = 0;
                    _overlayCanvas.enabled = _help;
                    _stateText.SetText(_shotPath != null
                        ? JobText.T("reallife_photo_saved").Replace("{path}", _shotPath)
                        : JobText.T("reallife_photo_save_failed"));
                    _savedUntil = Time.unscaledTime + 4f;
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
            try
            {
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
                HideHuds(false);
                if (_speed != null)
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
            if (_overlay != null)
            {
                Destroy(_overlay);
            }
            _volumeObject = null;
            _profile = null;
            _color = null;
            _depth = null;
            _overlay = null;
            _overlayCanvas = null;
            _helpText = null;
            _stateText = null;
            _camera = null;
            _brain = null;
            _speed = null;
            Logger?.Info("Photo mode: off; camera, HUD and game speed put back.");
        }
    }
}
