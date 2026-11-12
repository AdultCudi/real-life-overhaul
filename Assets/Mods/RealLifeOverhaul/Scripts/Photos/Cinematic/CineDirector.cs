using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using BAModAPI;
using Cinemachine;
using UI.Notification;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// The cinematic shots, a developer's tool for the mod's own pictures: one press takes a set of composed pictures of the
    /// city as it stands and puts everything back. It runs as one coroutine over some hundreds of frames: the game paused
    /// through its own speed control, its interface hidden through its own screenshot switch, the camera taken from the
    /// engine's brain and placed for each shot (<see cref="CineRules"/>, <see cref="CineGame"/>), the light of four hours
    /// shown through the game's own time of day tool with its clock never moved, and each picture drawn at 3840 by 2160
    /// into a target of its own and written on a worker. Everything changed is first written in a <see cref="CineHold"/>
    /// and given back once on every way out: the end, Esc, a fault (the run's finally), the feature's cleanup and the city
    /// unloading. Off while no run is on: no Update, nothing allocated. While one is on, a frame reads the cancel key and
    /// writes the camera's place. Refused in a Going Public session, where the camera and the light are everybody's.
    /// </summary>
    [DefaultExecutionOrder(-31000)]
    public sealed partial class CineDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("photos", "CineDirector", typeof(CineDirector));

        private const float NearPlane = 0.1f;    // metres: a lens at wheel height is a hand from the tyre
        private const float FarLeast = 2500f;    // metres: the far end of the skyline, where the game's own camera sees less far
        private const float DoneSeconds = 20f;   // seconds the notice with the folder stays

        public static IModLogger Logger;
        public static CineDirector Instance { get; private set; }

        /// <summary>True while a run is on: the mod's cameras and its minimap stand aside.</summary>
        public static bool Active { get; private set; }

        private readonly CineHold _hold = new CineHold();
        private readonly float[] _back = new float[CineHold.Values];
        private readonly float[] _hours = new float[(int)CineLight.Count];
        private StringBuilder _list, _left;   // the run's list of pictures, and of the shots left out
        private Camera _camera;
        private CinemachineBrain _brain;
        private GameObject _look;
        private DepthOfField _blur;
        private CineLine _line;
        private RenderTexture _target;
        private Texture2D _reader;
        private CinePose _pose;
        private CineFound _found;
        private Vector3 _eye;
        private Quaternion _turn;
        private float _fov, _focus;
        private bool _placed;                 // a shot's place stands: LateUpdate keeps the camera at it
        private bool _armed, _stopAsked, _finished;
        private string _stopWhy;
        private int _worldMask;               // the mask the tool set on the world's clicks
        private string _folder, _where;
        private int _taken, _skipped, _number, _planned, _heldAtStart;
        private float _clockBefore;
        private int _dayBefore;

        private void Awake()
        {
            Instance = this;
            CineGame.Logger = Logger;
            enabled = false;
        }

        private void OnDestroy()
        {
            try
            {
                Stop("the city unloads");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Instance == this)
            {
                Instance = null;
                Active = false;
            }
        }

        // Switching the object off stops its coroutines, and a stopped coroutine runs no finally: everything is given back here.
        private void OnDisable()
        {
            try
            {
                if (!gameObject.activeInHierarchy)
                {
                    Stop("the mod's object was switched off");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Ends a run now, everything given back in this call. Safe with no run on, and never throws.</summary>
        public void Stop(string why)
        {
            Stop(why, false);
        }

        private void Stop(string why, bool fault)
        {
            if (!Active && !_armed && !_hold.Any)
            {
                return;
            }
            _armed = false;
            _stopWhy = _stopWhy ?? why;
            _finished = !fault;
            try
            {
                StopAllCoroutines();   // the run is the only one
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            End();
        }

        // ------------------------------------------------------------------ the frame, while a run is on

        private static readonly int HotUpdate = HotSpots.Slot("Photos: CineDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                Stop("its feature was switched off after its faults");
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Stop("a fault", true);
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // Before the game's own key handling, so Esc ends the run and does not open the game's menu too.
        private void OnUpdate()
        {
            if (!Active)
            {
                enabled = false;
                return;
            }
            if (PhotosGame.TakeCancel() || Input.GetKeyDown(KeyCode.Escape))
            {
                _stopAsked = true;
            }
        }

        private static readonly int HotLateUpdate = HotSpots.Slot("Photos: CineDirector.LateUpdate");
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
                Stop("a fault", true);
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnLateUpdate()
        {
            if (Active && _placed && _camera != null)
            {
                _camera.transform.SetPositionAndRotation(_eye, _turn);
                if (_camera.fieldOfView != _fov)
                {
                    _camera.fieldOfView = _fov;
                }
            }
        }

        // ------------------------------------------------------------------ the run

        // The whole run. The try's finally is what puts everything back when it ends, is stopped or faults.
        private IEnumerator Run(bool allLights)
        {
            float until = Time.unscaledTime + CineRules.ArmedSeconds;
            while (ThirdPersonCamera.IsUiOpen())   // the menu its button was pressed in
            {
                if (Time.unscaledTime > until || !_armed)
                {
                    _armed = false;
                    Logger?.Info("Cinematic shots: not started (the menu was not closed within " + CineRules.ArmedSeconds.ToString("0", CultureInfo.InvariantCulture) + " s).");
                    yield break;
                }
                yield return null;
            }
            if (!_armed || !MayStart())
            {
                _armed = false;
                yield break;
            }
            _armed = false;
            try
            {
                Begin(allLights);
                int frame = 0;
                until = Time.unscaledTime + CineRules.FirstSeconds;
                while (Waiting(ref frame, CineRules.FirstFrames, until))
                {
                    yield return null;
                }
                CineLight light = CineLight.Count;   // none shown yet
                for (int i = 0; i < CineRules.Shots.Length && _stopWhy == null; i++)
                {
                    CineShot shot = CineRules.Shots[i];
                    if (!allLights && shot.Twin > 0)
                    {
                        CineRules.Take(shot, false, true, null, out string twin);
                        Leave(shot, twin);
                        continue;
                    }
                    _number++;
                    _placed = false;
                    string words = PhotosText.Fill("reallife_cine_line", "shot", _number.ToString(CultureInfo.InvariantCulture), "count", _planned.ToString(CultureInfo.InvariantCulture));
                    _line?.Show(words);
                    if (allLights && shot.Light != light)
                    {
                        if (Light(shot.Light))
                        {
                            light = shot.Light;
                        }
                        frame = 0;
                        until = Time.unscaledTime + CineRules.LightSeconds;
                        while (Waiting(ref frame, CineRules.LightFrames, until))
                        {
                            yield return null;
                        }
                        if (_stopWhy != null)
                        {
                            break;
                        }
                    }
                    if (!Compose(shot, allLights, out string why))
                    {
                        Leave(shot, why);
                        continue;
                    }
                    frame = 0;
                    until = Time.unscaledTime + CineRules.SettleSeconds;
                    while (Waiting(ref frame, CineRules.SettleFrames, until))
                    {
                        yield return null;
                    }
                    if (_stopWhy != null)
                    {
                        break;
                    }
                    if (!InView(out Vector3 framed))
                    {
                        Leave(shot, "the subject fell outside the frame from the only clear place");
                        continue;
                    }
                    _line?.Hide();
                    Target(true);
                    frame = 0;
                    while (Waiting(ref frame, CineRules.TargetFrames, 0f))
                    {
                        yield return null;
                    }
                    if (_stopWhy != null)
                    {
                        break;
                    }
                    yield return new WaitForEndOfFrame();
                    CineGame.Frame picture = Read(true);
                    bool fromScreen = false;
                    if (picture == null && _stopWhy == null)
                    {
                        // The target gave one flat colour: the screen is read, a frame after the camera draws to it again.
                        yield return null;
                        yield return new WaitForEndOfFrame();
                        picture = Read(false);
                        fromScreen = true;
                    }
                    _line?.Show(words);
                    if (picture == null)
                    {
                        Leave(shot, "the frame could not be read, from its target or from the screen");
                        continue;
                    }
                    string name = CineRules.FileName(shot, allLights), folder = _folder;
                    int width = picture.Width, height = picture.Height;
                    Task<bool> work = Task.Run(() => CineGame.Develop(picture, folder, name));   // the worker lets the pixels go itself
                    while (!work.IsCompleted)
                    {
                        yield return null;   // not cut short by Esc: a picture half written is no picture
                    }
                    if (work.Status != TaskStatus.RanToCompletion || !work.Result)
                    {
                        Leave(shot, "the picture could not be written to " + folder
                                    + (work.Exception != null ? " (" + work.Exception.GetBaseException().Message + ")" : string.Empty));
                        continue;
                    }
                    bool lit = allLights && light == shot.Light;   // false where the game's tool could not be set: the light is the clock's own
                    Taken(shot, name, lit ? _hours[(int)light] : _clockBefore, lit, width, height, fromScreen, framed);
                }
                _finished = true;
            }
            finally
            {
                End();
            }
        }

        // One more frame of a wait, unless the run must end: frames at the least, and real seconds at the least.
        private bool Waiting(ref int frame, int frames, float until)
        {
            if (Halt())
            {
                return false;
            }
            return frame++ < frames || Time.unscaledTime < until;
        }

        private bool Halt()
        {
            if (_stopWhy != null)
            {
                return true;
            }
            _stopWhy = _stopAsked ? "Esc" : CineGame.MustStop(_camera);
            if (_stopWhy == null && _hold.Held(CinePart.Speed))
            {
                CineGame.KeepPaused();   // nothing may start time while the camera is away from the player
            }
            return _stopWhy != null;
        }

        // What must hold as the menu closes: the same as when the button was pressed.
        private bool MayStart()
        {
            if (GoingPublic.InSession)
            {
                RefuseSession();
                return false;
            }
            TestRefusal why = CineGame.Refuse();
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, TestName, why);
                return false;
            }
            if (PhotosGame.BrainOf(PhotosGame.MainCamera) == null)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.NotLoaded, 0, "the game's camera has no brain to take it from");
                return false;
            }
            return true;
        }

        private void Begin(bool allLights)
        {
            Active = true;   // first: whatever faults from here on, the run's end tells it and gives back what was taken
            enabled = true;
            _camera = PhotosGame.MainCamera;
            _brain = PhotosGame.BrainOf(_camera);
            _stopAsked = _finished = _placed = false;
            _stopWhy = null;
            _taken = _skipped = _number = 0;
            _planned = CineRules.Planned(allLights);
            _list = _list ?? new StringBuilder(4096);   // made at the first run, so a session that never takes a shot never pays for them
            _left = _left ?? new StringBuilder(1024);
            _list.Length = 0;
            _left.Length = 0;
            _clockBefore = CineGame.HourNow;
            _dayBefore = PhotosGame.Day;
            PhotoSidecar note = PhotoShot.Capture(0, 0);   // the place the HUD names and the weather, read before the HUD is hidden
            _where = string.IsNullOrEmpty(note.Place) ? note.District : string.IsNullOrEmpty(note.District) || note.District == note.Place ? note.Place : note.Place + ", " + note.District;
            DateTime now = DateTime.Now;
            _folder = Path.Combine(CineGame.Root(), CineRules.RunFolder(now));
            bool read = CineGame.Hours(_hours);
            Hold();
            _heldAtStart = Count();
            _target = CineGame.MakeTarget();
            _reader = CineGame.MakeReader();
            _list.Append("Real Life Overhaul: cinematic shots\r\n")
                .Append("Taken ").Append(now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(" by the computer's clock. The game's clock: day ")
                .Append(_dayBefore).Append(", ").Append(CineRules.Clock(_clockBefore)).Append(". Weather: ").Append(note.Weather ?? "not known").Append(". Place: ")
                .Append(string.IsNullOrEmpty(_where) ? "not named" : _where).Append(".\r\n");
            if (allLights)
            {
                _list.Append("The whole set, in four lights shown through the game's own time of day tool: midday ").Append(CineRules.Clock(_hours[(int)CineLight.Midday]))
                    .Append(", golden ").Append(CineRules.Clock(_hours[(int)CineLight.Golden])).Append(", blue ").Append(CineRules.Clock(_hours[(int)CineLight.Blue]))
                    .Append(", night ").Append(CineRules.Clock(_hours[(int)CineLight.Night]))
                    .Append(read ? " (from the game's own sun and lamps)" : " (an ordinary day's: the game's own could not be read)")
                    .Append(". The game's clock was not moved and the game stood paused.\r\n");
            }
            else
            {
                _list.Append("At this hour only: the light is the game's own at ").Append(CineRules.Clock(_clockBefore)).Append(", and the game stood paused.\r\n");
            }
            _list.Append("Each picture is a PNG with a JPG (quality ").Append(CineRules.JpgQuality).Append(") beside it.\r\n");
            Logger?.Info("Cinematic shots: started (" + (allLights ? "the whole set in four lights" : "at this hour only") + ", " + _planned + " shots planned, into " + _folder + ").");
        }

        // Each part is written in the hold before it is changed, so a fault half way gives back exactly what was taken.
        private void Hold()
        {
            PhotosGame.Unselect();
            _hold.Take(CinePart.Walk);
            CineGame.HoldWalk(true);
            if (CineGame.TimeLocked(out bool locked))
            {
                _hold.Take(CinePart.Speed, locked ? 1f : 0f);
                CineGame.Pause();
            }
            _hold.Take(CinePart.Zoom, PhotosGame.ZoomBlocked ? 1f : 0f);
            PhotosGame.ZoomBlocked = true;
            bool read = PhotosGame.ReadWorld(out int mask, out int cursor);
            CameraHold.Split(mask, out float low, out float high);
            _hold.Take(CinePart.World, low, high, cursor, read ? 1f : 0f);
            _worldMask = PhotosGame.HoldWorld();
            _hold.Take(CinePart.Pointer, (int)Cursor.lockState, Cursor.visible ? 1f : 0f);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
            _hold.Take(CinePart.Hud, PhotosGame.HudShown ? 1f : 0f);
            PhotosGame.Hud(false);
            if (PhotosGame.ReadTurn(0, out float angle, out float distance))
            {
                _hold.Take(CinePart.StreetTurn, angle, distance);
            }
            if (PhotosGame.ReadTurn(1, out angle, out distance))
            {
                _hold.Take(CinePart.IndoorTurn, angle, distance);
            }
            _hold.Take(CinePart.Brain, _brain.enabled ? 1f : 0f);
            _brain.enabled = false;
            Vector3 p = _camera.transform.position;
            Quaternion q = _camera.transform.rotation;
            _hold.Take(CinePart.Place, p.x, p.y, p.z, q.x, q.y, q.z, q.w);
            _hold.Take(CinePart.Lens, _camera.fieldOfView, _camera.nearClipPlane, _camera.farClipPlane);
            _camera.nearClipPlane = NearPlane;
            _camera.farClipPlane = Mathf.Max(_camera.farClipPlane, FarLeast);
            CameraHold.Split(_camera.cullingMask, out low, out high);
            _hold.Take(CinePart.Layers, low, high);
            _camera.cullingMask &= ~CineGame.Markers;
            _hold.Take(CinePart.Look);
            _look = CineGame.MakeLook(transform, out _blur);
            _hold.Take(CinePart.Line);
            _line = new CineLine(transform);
        }

        private int Count()
        {
            int held = 0;
            for (int i = 0; i < (int)CinePart.Count; i++)
            {
                held += _hold.Held((CinePart)i) ? 1 : 0;
            }
            return held;
        }

        // The light of a shot's hour; the game's tool is written in the hold the first time it is touched.
        private bool Light(CineLight light)
        {
            try
            {
                if (!_hold.Held(CinePart.Light))
                {
                    if (!CineGame.ReadLight(out bool tool, out float forced, out float shown))
                    {
                        return false;
                    }
                    _hold.Take(CinePart.Light, tool ? 1f : 0f, forced, shown);
                }
                return CineGame.SetLight(_hours[(int)light]);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        // The subject looked for, the shot decided, the camera placed and its focus set. False, with why, for a shot left out.
        private bool Compose(CineShot shot, bool allLights, out string why)
        {
            try
            {
                _found = CineSubjects.Find(shot.Subject);
                if (!CineRules.Take(shot, allLights, _found.There, _found.Why, out why))
                {
                    return false;
                }
                if (!CineGame.Place(shot, _found, out _pose, out _eye, out why))
                {
                    return false;
                }
                _turn = CineGame.Facing(_pose, _eye);
                _fov = shot.Fov;
                _focus = Vector3.Distance(_eye, new Vector3(_pose.FocusX, _pose.FocusY, _pose.FocusZ));
                CineGame.Blur(_blur, shot.Blur, _focus, _pose.NearOnly);
                _placed = true;
                OnLateUpdate();
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                why = "a fault while composing (" + e.GetType().Name + ")";
                return false;
            }
        }

        private bool InView(out Vector3 framed)
        {
            framed = CineGame.Framed(_camera, _pose);
            return CineRules.InFrame(framed.x, framed.y, framed.z);
        }

        // The camera draws into the picture's own target, or to the screen again.
        private void Target(bool on)
        {
            if (on)
            {
                if (_camera != null && _target != null && _hold.Take(CinePart.Target))
                {
                    _camera.targetTexture = _target;
                }
                return;
            }
            if (_hold.Give(CinePart.Target, _back))
            {
                Put(CinePart.Target, _back);
            }
        }

        // The picture read at the end of a frame: from its target, which is then taken off the camera, or from the screen.
        private CineGame.Frame Read(bool target)
        {
            try
            {
                return target ? (_hold.Held(CinePart.Target) ? CineGame.ReadTarget(_target, _reader) : null) : CineGame.ReadScreen();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return null;
            }
            finally
            {
                if (target)
                {
                    Target(false);
                }
            }
        }

        private void Taken(CineShot shot, string name, float hour, bool lit, int width, int height, bool fromScreen, Vector3 framed)
        {
            _taken++;
            float over = CineGame.Ground(_eye, out float ground) ? _eye.y - ground : 0f;
            _list.Append("\r\n").Append(name).Append(".png\r\n")
                .Append("  subject: ").Append(_found.Name ?? CineRules.SubjectWord(shot.Subject)).Append("\r\n")
                .Append("  place: ").Append(string.IsNullOrEmpty(_where) ? "not named" : _where).Append("\r\n")
                .Append("  hour: ").Append(CineRules.Clock(hour))
                .Append(lit ? " (" + CineRules.LightWord(shot.Light) + ", shown by the game's time of day tool)" : " (the game's own clock)").Append("\r\n")
                .Append("  lens: ").Append(CineRules.LensWords(shot.Fov)).Append("; ").Append(CineRules.StyleWord(shot.Style)).Append("\r\n")
                .Append("  camera: ").Append(F(over)).Append(" m over the ground, ").Append(F(_focus)).Append(" m from what is sharp; ")
                .Append(shot.Blur ? (_pose.NearOnly ? "depth of field asked for, soft close to the lens" : "depth of field asked for, focus at " + F(_focus) + " m") : "no depth of field")
                .Append("; the subject at ").Append((framed.x * 100f).ToString("0", CultureInfo.InvariantCulture)).Append("% across and ")
                .Append((framed.y * 100f).ToString("0", CultureInfo.InvariantCulture)).Append("% up the frame\r\n")
                .Append("  size: ").Append(width).Append(" x ").Append(height)
                .Append(fromScreen ? " (read from the screen: the picture's own target gave nothing)" : " (its own target)").Append("\r\n");
            Logger?.Info("Cinematic shots: " + name + " taken (" + (_found.Name ?? "?") + "; " + CineRules.LensWords(shot.Fov) + "; " + width + " x " + height
                         + (fromScreen ? ", from the screen" : string.Empty) + ").");
        }

        private void Leave(CineShot shot, string why)
        {
            _skipped++;
            _left.Append("  ").Append(shot.Number.ToString("00", CultureInfo.InvariantCulture)).Append(' ').Append(shot.Slug).Append(": ").Append(why ?? "no reason kept").Append("\r\n");
            Logger?.Info("Cinematic shots: " + shot.Number.ToString("00", CultureInfo.InvariantCulture) + " " + shot.Slug + " left out (" + (why ?? "no reason kept") + ").");
        }

        private static string F(float value)
        {
            return value.ToString("0.0", CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------------ giving back

        // The end of a run on every way out: everything given back, the list written on a worker, one notice. A second call does nothing.
        private void End()
        {
            if (!Active && !_hold.Any)
            {
                return;
            }
            bool ran = Active;
            Active = false;
            enabled = false;
            _placed = false;
            int held = Count();
            GiveBack();
            int left = Count();
            CineGame.DropTarget(_target);
            CineGame.DropReader(_reader);
            _target = null;
            _reader = null;
            _camera = null;
            _brain = null;
            if (!ran)
            {
                return;
            }
            bool fault = !_finished;
            string folder = _folder;
            if (_taken > 0)
            {
                if (_left.Length > 0)
                {
                    _list.Append("\r\nLeft out:\r\n").Append(_left);
                }
                if (_stopWhy != null)
                {
                    _list.Append("\r\nStopped before the end: ").Append(_stopWhy).Append(".\r\n");
                }
                string text = _list.ToString();
                Task.Run(() => CineGame.WriteList(folder, text));
            }
            float moved = (CineGame.HourNow - _clockBefore) * 60f + (PhotosGame.Day - _dayBefore) * 1440f;
            bool sameClock = Mathf.Abs(moved) < 1.5f;
            Logger?.Info("Cinematic shots: ended (" + (fault ? "a fault" : _stopWhy ?? "the set is done") + "); " + _taken + " taken, " + _skipped + " left out"
                         + (_taken > 0 ? ", in " + folder : string.Empty) + ". The camera, the lens, the light, the interface, the pointer, the game's speed and the player's walk put back.");
            Logger?.Info("Cinematic shots check (" + held + " parts of the game's held at the end, " + _heldAtStart + " at the start, " + left
                         + " still held after giving back, wanted 0; the game's clock moved " + moved.ToString("0.0", CultureInfo.InvariantCulture)
                         + " minutes, wanted under 1.5): " + (left == 0 && sameClock ? "PASS" : "FAIL"));
            if (!PhotosGame.Loaded)
            {
                return;   // the city is going: nobody to tell
            }
            if (fault)
            {
                Say(NotificationType.Error, "reallife_cine_failed", DoneSeconds);
            }
            else if (_taken == 0)
            {
                Say(NotificationType.Warning, "reallife_cine_none", DoneSeconds);
            }
            else if (_stopWhy != null)
            {
                Say(NotificationType.Info, "reallife_cine_stopped", DoneSeconds, "taken", PhotosText.N(_taken), "folder", folder);
            }
            else
            {
                Say(NotificationType.Info, "reallife_cine_done", DoneSeconds, "taken", PhotosText.N(_taken), "skipped", PhotosText.N(_skipped), "folder", folder);
            }
        }

        // Each part in a try of its own, so one that fails costs only itself.
        private void GiveBack()
        {
            CinePart[] order = CineHold.GiveOrder;
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

        private void Put(CinePart part, float[] v)
        {
            switch (part)
            {
                case CinePart.Line:
                    _line?.Destroy();
                    _line = null;
                    break;
                case CinePart.Target:
                    if (_camera != null && _camera.targetTexture == _target)
                    {
                        _camera.targetTexture = null;
                    }
                    break;
                case CinePart.Light:
                    CineGame.GiveLight(v[0] > 0.5f, v[1], v[2]);
                    break;
                case CinePart.Look:
                    CineGame.DropLook(_look);
                    _look = null;
                    _blur = null;
                    break;
                case CinePart.Lens:
                    if (_camera != null)
                    {
                        _camera.fieldOfView = v[0];
                        _camera.nearClipPlane = v[1];
                        _camera.farClipPlane = v[2];
                    }
                    break;
                case CinePart.Layers:
                    if (_camera != null)
                    {
                        _camera.cullingMask = CameraHold.Join(v[0], v[1]);
                    }
                    break;
                case CinePart.Place:
                    if (_camera != null)
                    {
                        _camera.transform.SetPositionAndRotation(new Vector3(v[0], v[1], v[2]), new Quaternion(v[3], v[4], v[5], v[6]));
                    }
                    break;
                case CinePart.Brain:
                    if (_brain != null)
                    {
                        _brain.enabled = v[0] > 0.5f;
                    }
                    break;
                case CinePart.StreetTurn:
                    PhotosGame.WriteTurn(0, v[0], v[1]);
                    break;
                case CinePart.IndoorTurn:
                    PhotosGame.WriteTurn(1, v[0], v[1]);
                    break;
                case CinePart.Hud:
                    PhotosGame.Hud(v[0] > 0.5f);
                    break;
                case CinePart.Pointer:
                    Cursor.lockState = (CursorLockMode)(int)v[0];
                    Cursor.visible = v[1] > 0.5f;
                    break;
                case CinePart.World:
                    PhotosGame.GiveWorld(v[3] > 0.5f, CameraHold.Join(v[0], v[1]), (int)v[2], _worldMask);
                    break;
                case CinePart.Zoom:
                    PhotosGame.ZoomBlocked = v[0] > 0.5f;
                    break;
                case CinePart.Speed:
                    CineGame.Resume(v[0] > 0.5f);
                    break;
                case CinePart.Walk:
                    CineGame.HoldWalk(false);
                    break;
            }
        }

        // One of the game's notifications, its {names} filled from name and value pairs.
        private static void Say(NotificationType type, string key, float seconds, params string[] pairs)
        {
            Dictionary<string, string> data = null;
            if (pairs != null && pairs.Length > 1)
            {
                data = new Dictionary<string, string>(pairs.Length / 2);
                for (int i = 0; i + 1 < pairs.Length; i += 2)
                {
                    data[pairs[i]] = pairs[i + 1] ?? string.Empty;
                }
            }
            Notifications.Show(type, key, data, seconds);
        }
    }
}
