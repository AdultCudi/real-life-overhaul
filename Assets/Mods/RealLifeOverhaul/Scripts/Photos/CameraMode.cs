using System;
using System.Collections;
using System.Globalization;
using System.Threading.Tasks;
using BAModAPI;
using Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// The Camera: the rear camera from where the phone is held, or the selfie through photo mode's own. Everything it
    /// changes is first written in a <see cref="CameraHold"/> and given back once on every way out. Off while shut; while
    /// open a frame reads a dozen of the game's flags and places the lens, allocating only on a shot. It runs before the
    /// game's key handling, so the keys it takes do nothing else. Nothing of it is shared in Going Public.
    /// </summary>
    [DefaultExecutionOrder(-31000)]
    public sealed class CameraMode : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("photos", "CameraMode", typeof(CameraMode));

        private const float EyeHeight = 0.08f;   // metres from the head joint up to the eyes

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
        private bool _inSelfie, _seated, _grid, _shooting, _fromFlare, _dirty;
        private float _yaw, _pitch, _seatYaw, _zoom = 1f;
        private float _placedYaw = float.NaN, _placedFov = -1f;
        private int _filter;
        private CameraFrame _frame;
        private string _toast;
        private float _toastUntil, _flashFrom = -10f;
        private int _worldMask;                          // the mask the camera set on the world's clicks
        private PhotosGame.Shot _pendingShot;            // a shot under way, until its coroutine lets it go
        private Task _pendingWork;                       // the worker that has it, if one does

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
            _grid = Settings.PhoneCameraGrid;
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
                _view.Show(true);
                _view.Busy(false);
                _view.ForShot(false, _grid);   // the controls back, whatever an earlier shot left
                _view.Latest(PhotosDirector.Instance != null ? PhotosDirector.Instance.LatestTexture : null);
                PhotosGame.Grade(_color, _filter);
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
                Logger?.Info("Camera check (opened: " + Count() + " of " + (int)HoldPart.Count + " parts of the game's held to be given back, wanted all but the selfie's): "
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
            _grade = PhotosGame.MakeGrade(transform, out _color);
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
                }
                if (wheel != 0f)
                {
                    _zoom = CameraRules.Zoom(_zoom, wheel);
                    _dirty = true;
                }
            }
            Fades();
            if (_dirty)
            {
                _dirty = false;
                Write();
            }
            _view.Lay(_frame);
        }

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
            }
            _view.Toast(_toast, toast);
        }

        private void Write()
        {
            _view.Write(_inSelfie, _zoom, _grid, _inSelfie && _selfie != null ? _selfie.PoseKey : null, _filter, _frame);
        }

        private void Toast(string words)
        {
            _toast = words;
            _toastUntil = Time.unscaledTime + CameraRules.ToastSeconds;
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
            _dirty = true;
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

        private void ToggleGrid()
        {
            _grid = !_grid;
            _dirty = true;
            PhotosGame.Unselect();
        }

        private void Filter(int filter)
        {
            _filter = CameraRules.ClampFilter(filter);
            PhotosGame.Grade(_color, _filter);
            _dirty = true;
            PhotosGame.Unselect();
        }

        private void Frame(int frame)
        {
            _frame = CameraRules.ClampFrame(frame);
            _dirty = true;
            PhotosGame.Unselect();
        }

        private void Gallery()
        {
            Close("the Gallery button");
            PhotosDirector.Instance?.OpenLatestPhoto();
        }

        private void Shoot()
        {
            PhotosGame.Unselect();
            if (!Active || _shooting)
            {
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
            _shooting = true;
            StartCoroutine(Guard.Routine(Shot(director.Folder, PhotosGame.SaveId, _inSelfie, PhotosGame.Day, PhotosGame.Hour)));
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
                _view.Busy(true);
                _view.ForShot(true, _grid);
                yield return null;                       // a frame drawn without the controls,
                yield return new WaitForEndOfFrame();    // read at its end
                if (!Active)
                {
                    yield break;
                }
                shot = PhotosGame.Read();
                _pendingShot = shot;
                _view.ForShot(false, _grid);
                if (shot == null)
                {
                    Toast(PhotosText.T("reallife_phonecam_failed"));
                    yield break;
                }
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
                PhotosDirector director = PhotosDirector.Instance;
                Texture latest = director != null ? director.Added(folder, path, made) : null;
                Logger?.Info("Camera: a " + (selfie ? "selfie" : "photo") + " saved (" + System.IO.Path.GetFileName(path) + ", " + made.Width + " x " + made.Height + ", " + (made.Bytes / 1024) + " KB).");
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
                    Toast(PhotosText.T("reallife_phonecam_saved"));
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
                    _view.ForShot(false, _grid);
                }
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
