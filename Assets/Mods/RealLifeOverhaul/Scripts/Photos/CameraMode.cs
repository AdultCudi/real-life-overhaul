using System;
using System.Collections;
using System.Globalization;
using System.Threading.Tasks;
using BAModAPI;
using Cinemachine;
using Helpers;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// The Camera: the rear camera from where the phone is held (a photo, a portrait or a wide shot), or the selfie through photo
    /// mode's own. Everything it changes is first written in a <see cref="CameraHold"/> and given back once on every way out. Off
    /// while shut; while open a frame reads a dozen of the game's flags and places the lens, and allocates only when a setting
    /// changes or on a shot. The live filter, the exposure and the portrait's blur are one post effect volume of its own; the zoom
    /// is the field of view. A portrait looks for its subject with one ray when the aim settles, at most five times a second. It runs
    /// before the game's key handling, so the keys it takes do nothing else. Nothing of it is shared in Going Public.
    /// </summary>
    [DefaultExecutionOrder(-31000)]
    public sealed class CameraMode : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("photos", "CameraMode", typeof(CameraMode));

        private const float EyeHeight = 0.08f;   // metres from the head joint up to the eyes
        private const int ToolTimer = 0, ToolGuide = 1, ToolLevel = 2, ToolAspect = 3, ToolBoost = 4, ToolBody = 5, ToolClean = 6, ToolPose = 7;

        public static IModLogger Logger;
        public static CameraMode Instance { get; private set; }

        /// <summary>True while the camera is open: the mod's other cameras, its HUD and its activities stand aside.</summary>
        public static bool Active { get; private set; }

        private readonly CameraHold _hold = new CameraHold();
        private readonly float[] _back = new float[CameraHold.Values];
        private CameraView _view;
        private Leisure.Selfie _selfie;
        private Camera _camera;
        private CinemachineBrain _brain;
        private Transform _root, _head;
        private GameObject _grade;
        private ColorAdjustments _color;
        private DepthOfField _blur;
        private bool _inSelfie, _seated, _shooting, _fromFlare, _dirty;
        private float _yaw, _pitch, _seatYaw, _zoom = 1f;
        private float _placedYaw = float.NaN, _placedFov = -1f;
        private int _filter;
        private CameraFrame _frame;
        private CameraKind _kind = CameraKind.Photo;     // the rear camera's kind; the selfie is _inSelfie
        private CameraAspect _aspect = CameraAspect.SixteenNine;
        private CameraGuide _guide;
        private bool _level, _boost, _hidden, _clean;
        private int _exposure, _timer;                   // tenths of a stop; seconds
        private float _countStart = -1f, _lastLook = -10f;
        private int _counting;                           // the second the timer shows, 0 when it is not counting
        private bool _lookDue;
        private float _subject = CameraRules.FocusFar;
        private int _linedHour = -1;
        private PhotoSidecar _note;
        private bool _noted;
        private string _toast;
        private float _toastUntil, _flashFrom = -10f;
        private int _worldMask;                          // the mask the camera set on the world's clicks
        private PhotosGame.Shot _pendingShot;            // a shot under way, until its coroutine lets it go
        private Task _pendingWork;                       // the worker that has it, if one does
        private Ui.ViewfinderActions _actions;
        private string[] _modeWords, _filterWords, _frameWords;

        private CameraKind Kind => _inSelfie ? CameraKind.Selfie : _kind;

        private bool BoostOn => _boost && PhotoSidecarRules.Night(PhotosGame.Hour);

        private void Awake()
        {
            Instance = this;
            enabled = false;
        }

        private void OnDestroy()
        {
            try
            {
                Close("the city unloads");
                _view?.Destroy();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            FreeShot();
            _view = null;
            if (Instance == this)
            {
                Instance = null;
                Active = false;
            }
        }

        // Switching the object off stops its coroutines (turning the component off does not), so a shot is let go here.
        private void OnDisable()
        {
            try
            {
                if (!gameObject.activeInHierarchy)
                {
                    FreeShot();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // A stopped coroutine runs no finally, and the screen's copy (some 33 MB at 4K) is outside the managed heap: it is let
        // go now, or when its worker ends. A second call does nothing.
        private void FreeShot()
        {
            PhotosGame.Shot left = _pendingShot;
            Task work = _pendingWork;
            _pendingShot = null;
            _pendingWork = null;
            _shooting = false;
            if (left == null)
            {
                return;
            }
            if (work != null && !work.IsCompleted)
            {
                work.ContinueWith(_ => PhotosGame.Drop(left));
            }
            else
            {
                PhotosGame.Drop(left);
            }
        }

        /// <summary>Opens the camera, the phone put away; none if it opened, else why not, with nothing changed.</summary>
        public CameraRefusal Open(bool selfie, bool fromFlare)
        {
            if (Active)
            {
                return CameraRefusal.None;
            }
            CameraRefusal refusal = CameraRules.Refuse(PhotosGame.Situation(true));
            if (refusal != CameraRefusal.None)
            {
                return refusal;
            }
            Camera camera = PhotosGame.MainCamera;
            CinemachineBrain brain = PhotosGame.BrainOf(camera);
            if (camera == null || brain == null || !PhotosGame.Body(out Transform root, out Transform head))
            {
                Logger?.Warn("Camera: the game's camera, its brain or the player's head was not found; the camera stays shut.");
                return CameraRefusal.Gone;
            }
            _camera = camera;
            _brain = brain;
            _root = root;
            _head = head;
            try
            {
                Hold();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                GiveBack();
                return CameraRefusal.Gone;
            }
            _seated = PhotosGame.Seated;
            _yaw = _seatYaw = CameraRules.Wrap(root.eulerAngles.y);
            _pitch = 0f;
            _zoom = CameraRules.ZoomLeast;
            _filter = 0;
            _frame = CameraFrame.None;
            _kind = CameraKind.Photo;
            _aspect = CameraAspect.SixteenNine;
            _guide = Settings.PhoneCameraGrid ? CameraGuide.Thirds : CameraGuide.Off;
            _level = _boost = _hidden = _clean = false;
            _exposure = _timer = 0;
            _countStart = -1f;
            _counting = 0;
            _lookDue = false;
            _subject = CameraRules.FocusFar;
            _fromFlare = fromFlare;
            _toast = null;
            _flashFrom = -10f;
            Active = true;
            enabled = true;
            try
            {
                if (_view == null)
                {
                    _view = new CameraView(transform, () => Close("its Close button"), Shoot, Flip, ToggleGrid, NextPose, Gallery, Filter, Frame);
                }
                if (_actions == null)
                {
                    _actions = new Ui.ViewfinderActions
                    {
                        Close = () => Close("its Close button"), Shoot = Shoot, Gallery = Gallery, Flip = Flip, Clean = ToggleClean,
                        Mode = Mode, Filter = Filter, Frame = Frame, Tool = Tool, Lens = Lens, Zoom = ZoomStep, Exposure = ExposureStep,
                    };
                }
                Words();
                _view.Show(true);
                _view.Busy(false);
                SyncFinder(true);
                _view.ForShot(false, _guide != CameraGuide.Off);   // the controls back, whatever an earlier shot left
                Ui.ViewfinderHost.Newest = () => PhotosDirector.Instance != null ? PhotosDirector.Instance.LatestTexture : null;
                _view.Latest(PhotosDirector.Instance != null ? PhotosDirector.Instance.LatestTexture : null);
                Grade();
                Rear();
                if (selfie)
                {
                    StartSelfie();
                }
                Write();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Close("a fault");   // everything given back now, not a frame later
                return CameraRefusal.Gone;
            }
            Logger?.Info("Camera: open (" + (_inSelfie ? "selfie" : "rear") + (fromFlare ? ", from Flare" : string.Empty) + (_seated ? ", seated" : string.Empty) + ").");
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Camera check (opened: " + Count() + " of " + (int)HoldPart.Count + " parts of the game's held to be given back, wanted all but the selfie's and the body's): "
                             + (Count() >= (int)HoldPart.Count - 3 ? "PASS" : "parts missing (a camera of the game's not found)"));
            }
            return CameraRefusal.None;
        }

        private void Hold()
        {
            _hold.Take(HoldPart.Walk);
            PhotosGame.HoldWalk(true);
            _hold.Take(HoldPart.Zoom, PhotosGame.ZoomBlocked ? 1f : 0f);
            PhotosGame.ZoomBlocked = true;
            bool read = PhotosGame.ReadWorld(out int mask, out int cursor);
            CameraHold.Split(mask, out float low, out float high);
            _hold.Take(HoldPart.World, low, high, cursor, read ? 1f : 0f);
            _worldMask = PhotosGame.HoldWorld();
            _hold.Take(HoldPart.Pointer, (int)Cursor.lockState, Cursor.visible ? 1f : 0f);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _hold.Take(HoldPart.Hud, PhotosGame.HudShown ? 1f : 0f);
            PhotosGame.Hud(false);
            if (PhotosGame.ReadTurn(0, out float angle, out float distance))
            {
                _hold.Take(HoldPart.StreetTurn, angle, distance);
            }
            if (PhotosGame.ReadTurn(1, out angle, out distance))
            {
                _hold.Take(HoldPart.IndoorTurn, angle, distance);
            }
            _hold.Take(HoldPart.Brain, _brain.enabled ? 1f : 0f);
            _brain.enabled = false;
            Vector3 p = _camera.transform.position;
            Quaternion q = _camera.transform.rotation;
            _hold.Take(HoldPart.Place, p.x, p.y, p.z, q.x, q.y, q.z, q.w);
            _hold.Take(HoldPart.Lens, _camera.fieldOfView, _camera.nearClipPlane);
            _camera.nearClipPlane = CameraRules.NearPlane;
            Quaternion r = _root.rotation;
            _hold.Take(HoldPart.Facing, r.x, r.y, r.z, r.w);
            _hold.Take(HoldPart.Grade);
            _grade = PhotosGame.MakeGrade(transform, out _color, out _blur);
        }

        private int Count()
        {
            int held = 0;
            for (int i = 0; i < (int)HoldPart.Count; i++)
            {
                held += _hold.Held((HoldPart)i) ? 1 : 0;
            }
            return held;
        }

        /// <summary>Shuts the camera, everything held given back once. Safe when it is shut, and never throws.</summary>
        public void Close(string why)
        {
            if (!Active && !_hold.Any)
            {
                return;
            }
            Active = false;
            enabled = false;
            _countStart = -1f;
            _counting = 0;
            try
            {
                if (_selfie != null && _inSelfie)
                {
                    _selfie.End();   // first: it puts back the facing and the near plane it changed
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _inSelfie = false;
            GiveBack();
            try
            {
                Ui.ViewfinderHost.Hide();
                if (_view != null)
                {
                    _view.KitChrome = false;
                }
                _view?.Show(false);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _camera = null;
            _brain = null;
            _root = _head = null;
            _fromFlare = false;
            Logger?.Info("Camera: shut (" + why + "); the game's camera, the walk, the HUD and the pointer put back.");
        }

        // Each part in a try of its own, so one that fails costs only itself.
        private void GiveBack()
        {
            HoldPart[] order = CameraHold.GiveOrder;
            for (int i = 0; i < order.Length; i++)
            {
                if (!_hold.Give(order[i], _back))
                {
                    continue;
                }
                try
                {
                    Put(order[i], _back);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
        }

        private void Put(HoldPart part, float[] v)
        {
            switch (part)
            {
                case HoldPart.Body:
                    PhotosGame.HideBody(false);
                    break;
                case HoldPart.Facing:
                    if (_root != null)
                    {
                        _root.rotation = new Quaternion(v[0], v[1], v[2], v[3]);
                    }
                    break;
                case HoldPart.Grade:
                    PhotosGame.DropGrade(_grade);
                    _grade = null;
                    _color = null;
                    _blur = null;
                    break;
                case HoldPart.Lens:
                    if (_camera != null)
                    {
                        _camera.fieldOfView = v[0];
                        _camera.nearClipPlane = v[1];
                    }
                    break;
                case HoldPart.Place:
                    if (_camera != null)
                    {
                        _camera.transform.SetPositionAndRotation(new Vector3(v[0], v[1], v[2]), new Quaternion(v[3], v[4], v[5], v[6]));
                    }
                    break;
                case HoldPart.Brain:
                    if (_brain != null)
                    {
                        _brain.enabled = v[0] > 0.5f;
                    }
                    break;
                case HoldPart.StreetTurn:
                    PhotosGame.WriteTurn(0, v[0], v[1]);
                    break;
                case HoldPart.IndoorTurn:
                    PhotosGame.WriteTurn(1, v[0], v[1]);
                    break;
                case HoldPart.Hud:
                    PhotosGame.Hud(v[0] > 0.5f);
                    break;
                case HoldPart.Pointer:
                    Cursor.lockState = (CursorLockMode)(int)v[0];
                    Cursor.visible = v[1] > 0.5f;
                    break;
                case HoldPart.World:
                    PhotosGame.GiveWorld(v[3] > 0.5f, CameraHold.Join(v[0], v[1]), (int)v[2], _worldMask);
                    break;
                case HoldPart.Zoom:
                    PhotosGame.ZoomBlocked = v[0] > 0.5f;
                    break;
                case HoldPart.Walk:
                    PhotosGame.HoldWalk(false);
                    break;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Photos: CameraMode.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                Close("its feature was switched off after its faults");
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Close("a fault");
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (!Active)
            {
                enabled = false;
                return;
            }
            CameraRefusal must = CameraRules.MustClose(PhotosGame.Situation(true));
            if (must != CameraRefusal.None || _camera == null || _root == null || _head == null)
            {
                Close(must != CameraRefusal.None ? "the moment: " + must : "the player or the camera went");
                return;
            }
            if (_linedHour != PhotosGame.Hour)
            {
                _linedHour = PhotosGame.Hour;
                if (_boost && !PhotoSidecarRules.Night(_linedHour))
                {
                    Grade();   // morning: the boost is no longer offered, and no longer applied
                }
                if (_view != null && _view.KitChrome)
                {
                    SyncFinder(false);
                }
            }
            PhotosGame.TakeTurnKeys();
            if (PhotosGame.TakeCancel())
            {
                Close("the cancel key");
                return;
            }
            if (PhotosGame.TakeConfirm())
            {
                Shoot();
            }
            Countdown();
            if (!Active)
            {
                return;
            }
            _view.Input.Take(out float dx, out float dy, out float wheel);
            float lines = Screen.height;
            if (_inSelfie)
            {
                if (_selfie == null || !_selfie.On)
                {
                    _inSelfie = false;   // the selfie ended itself on a fault
                    _hold.Give(HoldPart.Selfie, null);
                    Rear();
                }
                else
                {
                    _selfie.Steer(CameraRules.SelfieTurn(dx, lines), CameraRules.SelfieTurn(dy, lines), wheel, 0f);   // every frame, so the lens follows the face
                }
            }
            else
            {
                if (dx != 0f || dy != 0f)
                {
                    CameraRules.Aim(ref _yaw, ref _pitch, dx, dy, lines, _zoom);
                    if (_seated)
                    {
                        _yaw = CameraRules.Seated(_yaw, _seatYaw);
                    }
                    _lookDue = true;
                }
                if (wheel != 0f)
                {
                    _zoom = CameraRules.Zoom(_zoom, wheel, CameraRules.ZoomLeastOf(_kind), CameraRules.ZoomMostOf(_kind));
                    _dirty = true;
                    _lookDue = true;
                }
                Focus();
            }
            Fades();
            if (_dirty)
            {
                _dirty = false;
                Write();
            }
            _view.Lay(_frame);
            Level();
        }

        // The timer: counts in whole seconds on the real clock, and takes the shot at nought.
        private void Countdown()
        {
            if (_countStart < 0f)
            {
                return;
            }
            int left = CameraRules.Counting(_timer, Time.unscaledTime - _countStart);
            if (left != _counting)
            {
                _counting = left;
                _dirty = true;
            }
            if (left <= 0)
            {
                _countStart = -1f;
                _counting = 0;
                _dirty = true;
                Take();
            }
        }

        // The portrait looks for its subject with one ray, when the aim has changed and a fifth of a second has gone since it last looked.
        private void Focus()
        {
            if (_kind != CameraKind.Portrait || !CameraRules.FocusDue(_lookDue, Time.unscaledTime, _lastLook))
            {
                return;
            }
            _lookDue = false;
            _lastLook = Time.unscaledTime;
            _subject = PhotosGame.SubjectAt(_camera);
            PhotosGame.Blur(_blur, true, _subject);
        }

        // The level line: where the horizon stands for the pitch, only while it is on.
        private void Level()
        {
            if (_inSelfie || !_level)
            {
                _view.Shape(ShapeNow, GuideNow, false, 0.5f, false);
                return;
            }
            _view.Shape(ShapeNow, GuideNow, true, CameraRules.HorizonAt(_pitch, CameraRules.FieldOfView(_zoom)), CameraRules.Level(_pitch));
        }

        // The old chrome keeps the whole screen: only the kit's has a shape of its own.
        private CameraAspect ShapeNow => _view != null && _view.KitChrome ? _aspect : CameraAspect.Unknown;

        private CameraGuide GuideNow => _guide;

        private static readonly int HotLateUpdate = HotSpots.Slot("Photos: CameraMode.LateUpdate");
        // After the animation, so the lens is placed at the head where it was drawn.
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (Exception e)
            {
                Close("a fault");
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnLateUpdate()
        {
            if (!Active || _inSelfie || _camera == null || _head == null || _root == null)
            {
                return;
            }
            Quaternion turn = Quaternion.Euler(0f, _yaw, 0f);
            if (!_seated && _yaw != _placedYaw)
            {
                _placedYaw = _yaw;
                _root.rotation = turn;
            }
            Vector3 lens = _head.position + Vector3.up * (EyeHeight - CameraRules.LensBelow) + turn * Vector3.forward * CameraRules.LensForward;
            _camera.transform.SetPositionAndRotation(lens, Quaternion.Euler(-_pitch, _yaw, 0f));
            float fov = CameraRules.FieldOfView(_zoom);
            if (fov != _placedFov)
            {
                _placedFov = fov;
                _camera.fieldOfView = fov;
            }
        }

        // On the real clock.
        private void Fades()
        {
            float now = Time.unscaledTime;
            float flash = 1f - (now - _flashFrom) / CameraRules.FlashSeconds;
            _view.Flash(flash);
            float toast = _toast != null ? Mathf.Clamp01((_toastUntil - now) / 0.4f) : 0f;
            if (_toast != null && toast <= 0f)
            {
                _toast = null;
                _dirty = true;   // the kit's chrome writes the toast away
            }
            _view.Toast(_toast, toast);
        }

        private void Write()
        {
            _view.Write(_inSelfie, _zoom, _guide != CameraGuide.Off, _inSelfie && _selfie != null ? _selfie.PoseKey : null, _filter, _frame);
            if (_view.KitChrome)
            {
                SyncFinder(false);
            }
        }

        // The words that never change while the camera is open, made once an opening.
        private void Words()
        {
            _modeWords = new[]
            {
                Ui.UiText.T("reallife_ui_life_mode_photo"), Ui.UiText.T("reallife_ui_life_mode_selfie"),
                Ui.UiText.T("reallife_ui_life_mode_portrait"), Ui.UiText.T("reallife_ui_life_mode_wide"),
            };
            _filterWords = FilterLabels();
            _frameWords = FrameLabels();
        }

        // The kit's bars, or the view's own when the screen's switch is off.
        private void SyncFinder(bool opening)
        {
            bool kit = _actions != null && Ui.ViewfinderHost.Show(_actions);
            if (!opening)
            {
                kit = Ui.ViewfinderHost.Wanted;
            }
            _view.KitChrome = kit;
            _view.ForShot(false, _guide != CameraGuide.Off);
            if (!kit)
            {
                Ui.ViewfinderHost.Hide();
                return;
            }
            PhotoSidecar side = PhotoShot.Capture(_filter, (int)_frame);
            int today = 0;
            PhotosDirector director = PhotosDirector.Instance;
            if (director != null)
            {
                for (int i = 0; i < director.Photos.Count; i++)
                {
                    today += director.Photos[i].Day == PhotosGame.Day ? 1 : 0;
                }
            }
            string place = string.IsNullOrEmpty(side.District) || string.IsNullOrEmpty(side.Place) || side.Place == side.District
                ? side.Place : side.Place + ", " + side.District;
            bool rear = !_inSelfie;
            float[] lenses = CameraRules.Lenses(_kind);
            var model = new Ui.UiFinderModel
            {
                Title = Ui.UiText.T("reallife_phonecam_app"),
                Modes = _modeWords,
                Mode = _inSelfie ? 1 : _kind == CameraKind.Portrait ? 2 : _kind == CameraKind.Wide ? 3 : 0,
                Place = place,
                When = PhotosGame.Hour.ToString("00", CultureInfo.InvariantCulture) + ":" + PhotosGame.Minute.ToString("00", CultureInfo.InvariantCulture)
                    + ", " + Ui.UiText.T(PhotoShot.Words(side.Weather)),
                Count = Ui.UiText.Fill("reallife_ui_life_photos_today", "count", today.ToString()),
                Filters = _filterWords,
                Filter = _filter,
                Frames = _frameWords,
                Frame = (int)_frame,
                FilterTitle = Ui.UiText.T("reallife_phonecam_filter_title"),
                FrameTitle = Ui.UiText.T("reallife_phonecam_frame_title"),
                Toast = _toast,
                Open = _toast != null ? Ui.UiText.T("reallife_ui_life_open") : null,
                Latest = director != null && director.LatestTexture != null ? Ui.ViewfinderHost.LatestKey : null,
                Newest = Ui.UiText.T("reallife_ui_life_newest_photo"),
                OpenGallery = Ui.UiText.T("reallife_ui_life_open_gallery"),
                Shoot = Ui.UiText.T("reallife_ui_life_take"),
                Zoom = rear ? CameraRules.ZoomWords(_zoom) : null,
                Lenses = rear ? LensWords(lenses) : new string[0],
                Lens = rear ? CameraRules.LensAt(_kind, _zoom) : -1,
                ZoomOut = rear && _zoom > CameraRules.ZoomLeastOf(_kind) + 0.001f,
                ZoomIn = rear && _zoom < CameraRules.ZoomMostOf(_kind) - 0.001f,
                Exposure = CameraRules.ExposureWords(_exposure),
                ExposureDown = _exposure > -CameraRules.ExposureMost,
                ExposureUp = _exposure < CameraRules.ExposureMost,
                Tools = Tools(),
                Countdown = _counting > 0 ? _counting.ToString(CultureInfo.InvariantCulture) : null,
                Clean = _clean,
                CleanBack = Ui.UiText.T("reallife_phonecam_clean_back"),
                Move = Ui.UiText.T("reallife_ui_prompt_move"),
                Select = Ui.UiText.T("reallife_ui_prompt_select"),
                Back = Ui.UiText.T("reallife_ui_prompt_back"),
            };
            Ui.ViewfinderHost.Apply(model);
        }

        private static string[] LensWords(float[] lenses)
        {
            var words = new string[lenses.Length];
            for (int i = 0; i < words.Length; i++)
            {
                words[i] = CameraRules.ZoomWords(lenses[i]);
            }
            return words;
        }

        private Ui.UiFinderTool[] Tools()
        {
            bool night = PhotoSidecarRules.Night(PhotosGame.Hour);
            var tools = new System.Collections.Generic.List<Ui.UiFinderTool>(8);
            tools.Add(new Ui.UiFinderTool
            {
                Id = ToolTimer, Icon = Ui.UiIcon.Timer, On = _timer > 0,
                Label = _timer > 0 ? Ui.UiText.Fill("reallife_phonecam_timer_s", "seconds", _timer.ToString(CultureInfo.InvariantCulture)) : Ui.UiText.T("reallife_phonecam_tool_timer"),
            });
            tools.Add(new Ui.UiFinderTool
            {
                Id = ToolGuide, Icon = Ui.UiIcon.Grid, On = _guide != CameraGuide.Off,
                Label = Ui.UiText.T(_guide == CameraGuide.Thirds ? "reallife_phonecam_guide_thirds" : _guide == CameraGuide.Square ? "reallife_phonecam_guide_square" : "reallife_phonecam_grid"),
            });
            if (!_inSelfie)
            {
                tools.Add(new Ui.UiFinderTool { Id = ToolLevel, Icon = Ui.UiIcon.Level, On = _level, Label = Ui.UiText.T("reallife_phonecam_tool_level") });
            }
            tools.Add(new Ui.UiFinderTool
            {
                Id = ToolAspect, Icon = Ui.UiIcon.Crop, On = _aspect != CameraAspect.SixteenNine, Label = Ui.UiText.T(CameraRules.AspectKeys[(int)_aspect]),
            });
            if (night)
            {
                tools.Add(new Ui.UiFinderTool { Id = ToolBoost, Icon = Ui.UiIcon.Moon, On = _boost, Label = Ui.UiText.T("reallife_phonecam_tool_boost") });
            }
            if (!_inSelfie)
            {
                tools.Add(new Ui.UiFinderTool { Id = ToolBody, Icon = Ui.UiIcon.EyeSlash, On = _hidden, Label = Ui.UiText.T("reallife_phonecam_tool_body") });
            }
            else if (_selfie != null && !string.IsNullOrEmpty(_selfie.PoseKey))
            {
                tools.Add(new Ui.UiFinderTool
                {
                    Id = ToolPose, Icon = Ui.UiIcon.Person, On = false, Label = PhotosText.Fill("reallife_phonecam_pose", "pose", PhotosText.T(_selfie.PoseKey)),
                });
            }
            tools.Add(new Ui.UiFinderTool { Id = ToolClean, Icon = Ui.UiIcon.FrameCorners, On = _clean, Label = Ui.UiText.T("reallife_phonecam_tool_clean") });
            return tools.ToArray();
        }

        private void Mode(int mode)
        {
            if (!Active || _shooting || mode < 0 || mode > 3)
            {
                return;
            }
            if (mode == 1)
            {
                if (!_inSelfie)
                {
                    StartSelfie();
                }
            }
            else
            {
                if (_inSelfie)
                {
                    _selfie.End();
                    _hold.Give(HoldPart.Selfie, null);
                    Rear();
                }
                SetKind(mode == 2 ? CameraKind.Portrait : mode == 3 ? CameraKind.Wide : CameraKind.Photo);
            }
            PhotosGame.Unselect();
            _dirty = true;
        }

        // The rear camera's kind: its lens range, its start zoom and, for a portrait, its blur.
        private void SetKind(CameraKind kind)
        {
            _kind = kind;
            _zoom = CameraRules.ZoomStartOf(kind);
            _placedFov = -1f;
            _lookDue = true;
            _lastLook = -10f;
            PhotosGame.Blur(_blur, kind == CameraKind.Portrait && !_inSelfie, _subject);
            _dirty = true;
        }

        private static string[] FilterLabels()
        {
            var labels = new string[CameraRules.Filters.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = Ui.UiText.T(CameraRules.Filters[i].NameKey);
            }
            return labels;
        }

        private static string[] FrameLabels()
        {
            var labels = new string[CameraRules.FrameKeys.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = Ui.UiText.T(CameraRules.FrameKeys[i]);
            }
            return labels;
        }

        private void Toast(string words)
        {
            _toast = words;
            _toastUntil = Time.unscaledTime + CameraRules.ToastSeconds;
            _dirty = true;
        }

        private void Rear()
        {
            _inSelfie = false;
            _placedFov = -1f;
            _placedYaw = float.NaN;
            if (_camera != null)
            {
                _camera.nearClipPlane = CameraRules.NearPlane;
            }
            PhotosGame.Blur(_blur, _kind == CameraKind.Portrait, _subject);
            _lookDue = true;
            ApplyBody();
            _dirty = true;
        }

        private void StartSelfie()
        {
            if (_selfie == null)
            {
                if (Leisure.Selfie.Logger == null)
                {
                    Leisure.Selfie.Logger = Logger;
                }
                _selfie = gameObject.AddComponent<Leisure.Selfie>();
            }
            Vector3 ahead = _root.position + Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward * 2f;
            if (!_selfie.Begin(_camera, PhotosGame.Player, ahead))
            {
                Toast(PhotosText.T("reallife_phonecam_selfie_cannot"));
                return;
            }
            _hold.Take(HoldPart.Selfie);
            _inSelfie = true;
            _camera.fieldOfView = CameraRules.SelfieFieldOfView;
            PhotosGame.Blur(_blur, false, _subject);
            ApplyBody();
            _dirty = true;
        }

        // The player's body hidden only in the rear camera, where the player may be seen looking down; the selfie is of the player.
        private void ApplyBody()
        {
            bool hide = _hidden && !_inSelfie;
            if (hide && !_hold.Held(HoldPart.Body))
            {
                _hold.Take(HoldPart.Body, 1f);
                PhotosGame.HideBody(true);
            }
            else if (!hide && _hold.Held(HoldPart.Body))
            {
                _hold.Give(HoldPart.Body, null);
                PhotosGame.HideBody(false);
            }
        }

        private void Flip()
        {
            if (!Active || _shooting)
            {
                return;
            }
            if (_inSelfie)
            {
                _selfie.End();
                _hold.Give(HoldPart.Selfie, null);
                Rear();
            }
            else
            {
                StartSelfie();
            }
            PhotosGame.Unselect();
        }

        private void NextPose()
        {
            if (Active && _inSelfie && _selfie != null)
            {
                _selfie.NextPose();
                _dirty = true;
            }
            PhotosGame.Unselect();
        }

        // The old chrome's grid button: thirds on and off. The kit's tool cycles off, thirds and squares.
        private void ToggleGrid()
        {
            _guide = _guide == CameraGuide.Off ? CameraGuide.Thirds : CameraGuide.Off;
            _dirty = true;
            PhotosGame.Unselect();
        }

        private void Filter(int filter)
        {
            _filter = CameraRules.ClampFilter(filter);
            Grade();
            _dirty = true;
            PhotosGame.Unselect();
        }

        private void Frame(int frame)
        {
            _frame = CameraRules.ClampFrame(frame);
            _dirty = true;
            PhotosGame.Unselect();
        }

        // The filter, the exposure and the night boost, one grade.
        private void Grade()
        {
            PhotosGame.Grade(_color, _filter, _exposure, BoostOn);
        }

        private void ZoomStep(int direction)
        {
            if (!Active || _inSelfie)
            {
                return;
            }
            _zoom = CameraRules.Zoom(_zoom, direction * CameraRules.ZoomButtonNotches, CameraRules.ZoomLeastOf(_kind), CameraRules.ZoomMostOf(_kind));
            _lookDue = true;
            _dirty = true;
            PhotosGame.Unselect();
        }

        private void Lens(int index)
        {
            float[] lenses = CameraRules.Lenses(_kind);
            if (Active && !_inSelfie && index >= 0 && index < lenses.Length)
            {
                _zoom = lenses[index];
                _lookDue = true;
                _dirty = true;
            }
            PhotosGame.Unselect();
        }

        private void ExposureStep(int direction)
        {
            if (!Active)
            {
                return;
            }
            _exposure = CameraRules.Exposure(_exposure, direction);
            Grade();
            _dirty = true;
            PhotosGame.Unselect();
        }

        private void Tool(int tool)
        {
            if (!Active || _shooting)
            {
                return;
            }
            switch (tool)
            {
                case ToolTimer:
                    _timer = CameraRules.Timer(_timer);
                    break;
                case ToolGuide:
                    _guide = CameraRules.Guide(_guide);
                    break;
                case ToolLevel:
                    _level = !_level;
                    break;
                case ToolAspect:
                    _aspect = CameraRules.NextAspect(_aspect);
                    break;
                case ToolBoost:
                    _boost = !_boost && PhotoSidecarRules.Night(PhotosGame.Hour);
                    Grade();
                    break;
                case ToolBody:
                    _hidden = !_hidden;
                    ApplyBody();
                    break;
                case ToolClean:
                    _clean = !_clean;
                    break;
                case ToolPose:
                    NextPose();
                    break;
            }
            _dirty = true;
            PhotosGame.Unselect();
        }

        private void ToggleClean()
        {
            _clean = !_clean;
            _dirty = true;
            PhotosGame.Unselect();
        }

        private void Gallery()
        {
            Close("the Gallery button");
            PhotosDirector.Instance?.OpenLatestPhoto();
        }

        // The shutter: with a timer it starts the count (a second press stops it); the shot itself is Take.
        private void Shoot()
        {
            PhotosGame.Unselect();
            if (!Active || _shooting)
            {
                return;
            }
            if (_countStart >= 0f)
            {
                _countStart = -1f;
                _counting = 0;
                _dirty = true;
                return;
            }
            PhotosDirector director = PhotosDirector.Instance;
            if (director == null || !director.Ready)
            {
                Toast(PhotosText.T("reallife_phonecam_failed"));
                return;
            }
            if (PhotoStoreRules.Full(director.Count))
            {
                Toast(PhotosText.Fill("reallife_phonecam_full", "cap", PhotosText.N(PhotoStoreRules.Cap)));
                return;
            }
            if (_timer > 0 && _view != null && _view.KitChrome)
            {
                _countStart = Time.unscaledTime;
                _counting = CameraRules.Counting(_timer, 0f);
                _dirty = true;
                return;
            }
            Take();
        }

        private void Take()
        {
            PhotosDirector director = PhotosDirector.Instance;
            if (!Active || _shooting || director == null || !director.Ready || PhotoStoreRules.Full(director.Count))
            {
                return;
            }
            _shooting = true;
            StartCoroutine(Guard.Routine(Shot(director.Folder, PhotosGame.SaveId, _inSelfie, PhotosGame.Day, PhotosGame.Hour)));
        }

        // What the shot records beside the photo: the camera's settings, what the lens sees, the words that tag it.
        private PhotoSidecar Note(bool selfie, CameraAspect aspect)
        {
            PhotoSidecar note = PhotoShot.Capture(_filter, (int)_frame);
            note.Aspect = (int)aspect;
            note.Kind = (int)Kind;
            note.Zoom = selfie ? 0 : Mathf.RoundToInt(_zoom * 10f);
            note.Exposure = _exposure;
            note.Timer = _timer;
            note.Boost = BoostOn;
            if (_view != null && _view.KitChrome)
            {
                CameraRules.CropRect(Screen.width, Screen.height, aspect, out int x, out int y, out int w, out int h);
                var view = PhotoSeenRules.View.Of(Screen.width, Screen.height, x, y, w, h);
                PhotosSeen.Look(_camera, view, ref note);
            }
            note.Tags = PhotoSeenRules.Tokens(note, selfie);
            return note;
        }

        // The controls away for the frame that is read, then the photo made on a worker. The finally lets the screen's copy
        // go whatever happens, on the worker when the worker has it.
        private IEnumerator Shot(string folder, string saveId, bool selfie, int day, int hour)
        {
            PhotosGame.Shot shot = null;
            Task<PhotosGame.Developed> work = null;
            string path = null;
            try
            {
                CameraAspect aspect = _view.KitChrome ? _aspect : CameraAspect.Unknown;
                _note = Note(selfie, aspect);
                _noted = true;
                _view.Busy(true);
                _view.ForShot(true, _guide != CameraGuide.Off);
                Ui.ViewfinderHost.ForShot(true);
                yield return null;                       // a frame drawn without the controls,
                yield return new WaitForEndOfFrame();    // read at its end
                if (!Active)
                {
                    yield break;
                }
                shot = PhotosGame.Read();
                _pendingShot = shot;
                _view.ForShot(false, _guide != CameraGuide.Off);
                if (shot == null)
                {
                    Toast(PhotosText.T("reallife_phonecam_failed"));
                    yield break;
                }
                if (aspect != CameraAspect.Unknown)
                {
                    CameraRules.CropRect(shot.Width, shot.Height, aspect, out shot.CropX, out shot.CropY, out shot.CropWidth, out shot.CropHeight);
                    shot.Aspect = aspect;
                }
                shot.Frame = _frame;
                _flashFrom = Time.unscaledTime;
                PhotosBridges.Shutter?.Invoke();
                DateTime taken = DateTime.Now;
                PhotosGame.Shot given = shot;
                work = Task.Run(() =>
                {
                    path = PhotoStore.NewPath(folder, saveId, taken, selfie, day, hour);
                    return path != null ? PhotosGame.Develop(given, path) : null;   // no free name: told as a photo not written
                });
                _pendingWork = work;
                while (!work.IsCompleted)
                {
                    yield return null;
                }
                PhotosGame.Developed made = work.Status == TaskStatus.RanToCompletion ? work.Result : null;
                if (made == null)
                {
                    Logger?.Warn("Camera: the photo could not be written" + (work.Exception != null ? " (" + work.Exception.GetBaseException().Message + ")" : string.Empty) + ".");
                    if (Active)
                    {
                        Toast(PhotosText.T("reallife_phonecam_failed"));
                    }
                    yield break;
                }
                if (_noted && path != null)
                {
                    PhotoSidecarFile.Write(path, _note);
                    _noted = false;
                }
                PhotosDirector director = PhotosDirector.Instance;
                Texture latest = director != null ? director.Added(folder, path, made, _note) : null;
                Logger?.Info("Camera: a " + (selfie ? "selfie" : "photo") + " saved (" + System.IO.Path.GetFileName(path)
                    + ", " + made.Width + " x " + made.Height + ", " + (made.Bytes / 1024) + " KB; " + aspect
                    + ", " + _note.People + " people, " + (string.IsNullOrEmpty(_note.Business) ? "no business" : "business " + _note.Business) + ").");
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Camera check (the screen read on the main thread in " + shot.ReadMs.ToString("0.0", CultureInfo.InvariantCulture) + " ms, wanted under "
                                 + CameraRules.ReadMost.ToString("0") + "; the photo "
                                 + made.Width + " x " + made.Height + " in " + made.Ms.ToString("0") + " ms on a worker; its small copy " + made.SmallWidth + " x " + made.SmallHeight + "): "
                                 + (shot.ReadMs < CameraRules.ReadMost ? "PASS" : "slow"));
                }
                if (Active)
                {
                    _view.Latest(latest);
                    Toast(Ui.ViewfinderHost.Wanted ? Ui.UiText.T("reallife_ui_life_saved") : PhotosText.T("reallife_phonecam_saved"));
                }
                if (_fromFlare && Active)
                {
                    Close("the photo goes to Flare");
                    if (PhotosBridges.ShareToFlare == null || !PhotosBridges.ShareToFlare(path, PhotoThumbs.FileOf(path)))
                    {
                        PhotosGame.Tell(UI.Notification.NotificationType.Info, "reallife_gallery_note_saved_only");
                    }
                }
            }
            finally
            {
                _shooting = false;
                if (_pendingShot == shot)
                {
                    _pendingShot = null;
                    _pendingWork = null;
                }
                if (_view != null)
                {
                    _view.Busy(false);              // open or shut, so the next opening has its controls
                    _view.ForShot(false, _guide != CameraGuide.Off);
                }
                Ui.ViewfinderHost.ForShot(false);
                PhotosGame.Shot left = shot;
                if (work != null && !work.IsCompleted)
                {
                    work.ContinueWith(_ => PhotosGame.Drop(left));
                }
                else
                {
                    PhotosGame.Drop(left);   // Develop lets it go itself; a second drop does nothing
                }
            }
        }
    }
}
