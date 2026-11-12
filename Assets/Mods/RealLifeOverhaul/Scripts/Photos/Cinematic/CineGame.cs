using System;
using System.IO;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using Helpers;
using Parking.UndergroundParking;
using RealLifeOverhaul.Flare;
using TMPro;
using UI;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// The one place the cinematic shots touch the game and the engine, with <see cref="CineSubjects"/> for what is in the
    /// street: the pause, the game's own time of day tool, the camera's place and lens, the look, the picture's target and
    /// its reading, and the test of a place for the camera (a line cast from the subject and the place's own clear air).
    /// Nothing per frame of its own; every member is called from a run, a few times a shot.
    /// </summary>
    internal static class CineGame
    {
        public static IModLogger Logger;

        private const NavigationBlocker Blocker = (NavigationBlocker)7320;   // a value of the tool's own, so nobody else's is ever lifted
        private const int SampleColumns = 48, SampleRows = 27;                // pixels looked at to tell a frame nothing was drawn into

        private static int _solid, _ground;

        /// <summary>What a camera cannot stand in or see through: everything but people, the interface and the game's markers. Triggers are never asked.</summary>
        public static int Solid
        {
            get
            {
                if (_solid == 0)
                {
                    int soft = Bit(LayerHelper.UiLayerIndex) | Bit(LayerHelper.IgnoreRaycastLayerIndex) | Bit(LayerHelper.HumanLayerIndex) | Bit(LayerHelper.PlayerLayerIndex)
                               | Bit(LayerHelper.AutoParkSpotsLayerIndex) | Bit(LayerHelper.ParkingAreaLayerIndex) | LayerHelper.triggerLayerIndex.value
                               | LayerHelper.guidersLayerMask.value | LayerHelper.wallPlacementLayerMask.value;
                    _solid = ~soft;
                }
                return _solid;
            }
        }

        /// <summary>The ground a person walks on and a car drives on.</summary>
        public static int GroundMask => _ground != 0 ? _ground : (_ground = LayerHelper.groundLayerMask.value | LayerHelper.groundUnplacableLayerMask.value);

        /// <summary>
        /// The layers left out of what the camera draws while a run is on: the interface's, which markers and name tags that
        /// stand in the world are on, and the game's guiders to a destination.
        /// </summary>
        public static int Markers => Bit(LayerHelper.UiLayerIndex) | LayerHelper.guidersLayerMask.value;

        /// <summary>The city's buildings, which make a street's walls and its roofs.</summary>
        public static int Walls => LayerHelper.buildingAndOutlinesLayerMask.value;

        private static int Bit(int layer)
        {
            return layer >= 0 && layer < 32 ? 1 << layer : 0;
        }

        // ------------------------------------------------------------------ whether a run can start, and must stop

        /// <summary>Why the shots cannot start now, or None. The menu the button is pressed in is not asked about: the run waits for it to close.</summary>
        public static TestRefusal Refuse()
        {
            if (!PhotosGame.Loaded || !ModState.Loaded || PhotosGame.MainCamera == null)
            {
                return TestRefusal.NotLoaded;
            }
            if (BuildingManager.IsInsideBuilding || UndergroundParkingManager.IsInsideParking)
            {
                return TestRefusal.Indoors;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            bool skipping = uis != null && uis.timeMachine != null && uis.timeMachine.isRunning;
            if (skipping || GameMembers.PlayerDead || Law.Custody.Active || GameManager.IsAnyMiniGameActive() || PoliceAttention.IsBusy || TransitDirector.Riding
                || Leisure.PhotoMode.Active || CameraMode.Active || ScreenshotController.isInFreeLookMode)
            {
                return TestRefusal.ScreenBusy;
            }
            return TestRefusal.None;
        }

        /// <summary>Why a run under way must end now, or null: the city went, the player went indoors, a window opened, a session began.</summary>
        public static string MustStop(Camera camera)
        {
            if (!PhotosGame.Loaded || camera == null)
            {
                return "the city or its camera is gone";
            }
            if (GoingPublic.InSession)
            {
                return "a Going Public session began";
            }
            if (BuildingManager.IsInsideBuilding)
            {
                return "the player is indoors";
            }
            return ThirdPersonCamera.AnyWindow(true) ? "a window of the game's opened" : null;
        }

        // ------------------------------------------------------------------ the game's speed and the player's walk

        /// <summary>Whether the game's pause key was locked before the tool locks it; false with no speed control to pause through.</summary>
        public static bool TimeLocked(out bool locked)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            GameSpeedController speed = uis != null ? uis.gameSpeed : null;
            locked = speed != null && speed.isTimeControlDisabled;
            return speed != null;
        }

        /// <summary>The game paused through its own speed control, without its overlay, and the pause key locked.</summary>
        public static void Pause()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            GameSpeedController speed = uis != null ? uis.gameSpeed : null;
            if (speed != null)
            {
                speed.SetPause(true, false);
                speed.DisableTimeControl(true);   // the game's pause key must not start time under a camera that is away
            }
        }

        /// <summary>
        /// The pause put back where something of the game's ended it under the run, as a menu's own closing does a moment
        /// after it shuts. One of the game's flags read a frame of a run.
        /// </summary>
        public static void KeepPaused()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            GameSpeedController speed = uis != null ? uis.gameSpeed : null;
            if (speed != null && !speed.Paused)
            {
                speed.SetPause(true, false);
            }
        }

        /// <summary>The lock as it was, and the speed, or the pause, the player had.</summary>
        public static void Resume(bool wasLocked)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            GameSpeedController speed = uis != null ? uis.gameSpeed : null;
            if (speed != null)
            {
                speed.DisableTimeControl(wasLocked);
                speed.Reset();
            }
        }

        public static void HoldWalk(bool hold)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null)
            {
                return;
            }
            if (hold)
            {
                player.SetNavigationBlocker(Blocker);
            }
            else
            {
                player.UnsetNavigationBlocker(Blocker);
            }
        }

        // ------------------------------------------------------------------ the light

        private static TimeOfDayController Clock()
        {
            GameManager manager = InstanceBehavior<GameManager>.Instance;
            return manager != null ? manager.timeOfDayController : null;
        }

        /// <summary>The hour the game's clock shows, with the minutes as a fraction.</summary>
        public static float HourNow => SaveGameManager.Current != null ? SaveGameManager.Current.Hour + SaveGameManager.Current.Minute / 60f : 12f;

        /// <summary>
        /// The four lights' hours from the game's own day: when it puts its sun out, when its street lamps come on and how
        /// high its sun stands by the hour. False where they cannot be read, with the hours of an ordinary day.
        /// </summary>
        public static bool Hours(float[] into)
        {
            float sunOff = 20f, lampsOn = 19f;
            Func<float, float> turn = null;
            TimeOfDayController day = Clock();
            var settings = day != null && GameContract.DaySettings.Field != null ? GameContract.DaySettings.Field.GetValue(day) as EnvironmentSettings : null;
            if (settings != null)
            {
                sunOff = settings.hourToTurnOnMoon;
                lampsOn = settings.outdoorLightsOnStartHour;
                AnimationCurve sun = settings.outdoorsSunRotationX;
                if (sun != null && sun.length > 1)
                {
                    turn = sun.Evaluate;
                }
            }
            CineRules.Hours(sunOff, lampsOn, turn, into);
            return settings != null;
        }

        /// <summary>The game's time of day tool as it stands: whether it is on, the hour it forces and the hour it shows. False with no city.</summary>
        public static bool ReadLight(out bool tool, out float forced, out float shown)
        {
            TimeOfDayController day = Clock();
            tool = day != null && day.usingSliderDebugTool;
            forced = day != null ? day.forcedHour : -1f;
            shown = InGameTimeOfDayPanel.selectedTimeInHours;
            return day != null;
        }

        /// <summary>
        /// The light of an hour through the game's own time of day tool, as its panel sets it: the sun, the sky, the fog,
        /// the windows and the lamps of that hour, the lamps lit at once as when the city map closes. The clock itself is
        /// never touched, so no hour and no day turns.
        /// </summary>
        public static bool SetLight(float hour)
        {
            TimeOfDayController day = Clock();
            if (day == null)
            {
                return false;
            }
            day.usingSliderDebugTool = true;
            day.forcedHour = hour;
            InGameTimeOfDayPanel.selectedTimeInHours = hour;
            day.SetEnvironmentSettings(hour);
            day.UpdateHourlyValues(hour);
            Lamps();
            return true;
        }

        /// <summary>The tool as it was and the light of the clock's own hour.</summary>
        public static void GiveLight(bool tool, float forced, float shown)
        {
            TimeOfDayController day = Clock();
            if (day == null)
            {
                return;
            }
            day.forcedHour = forced;
            day.usingSliderDebugTool = tool;
            InGameTimeOfDayPanel.selectedTimeInHours = shown;
            if (SaveGameManager.Current == null)
            {
                return;
            }
            day.SetEnvironmentSettings(HourNow);
            day.UpdateHourlyValues(SaveGameManager.Current.Hour);
            Lamps();
        }

        // The game's lamps flicker on over scaled seconds, which a pause never ends: forced, they are lit or put out at once.
        private static void Lamps()
        {
            GlobalEvents.outdoorLightsStatusChanged?.Invoke(true);
        }

        // ------------------------------------------------------------------ the look

        /// <summary>A volume of the tool's own over the mod's grading: a slight vignette on every shot and the depth of field of a close one. There while a run is on.</summary>
        public static GameObject MakeLook(Transform parent, out DepthOfField blur)
        {
            var made = new GameObject("RealLifeOverhaul Cinematic Look");
            made.transform.SetParent(parent, false);
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            blur = profile.Add<DepthOfField>();
            Vignette edge = profile.Add<Vignette>();
            edge.intensity.overrideState = true;
            edge.intensity.value = CineRules.Vignette;
            edge.smoothness.overrideState = true;
            edge.smoothness.value = CineRules.VignetteSmooth;
            var volume = made.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1100f;   // over the mod's own grading (1000), as the Camera's
            volume.sharedProfile = profile;
            volume.weight = 1f;
            return made;
        }

        /// <summary>The depth of field for a subject a distance off; off, the picture is sharp all through as the game draws it.</summary>
        public static void Blur(DepthOfField blur, bool on, float distance, bool nearOnly)
        {
            if (blur == null)
            {
                return;
            }
            CineRules.Focus(distance, nearOnly, out float nearStart, out float nearEnd, out float farStart, out float farEnd);
            blur.focusMode.overrideState = on;
            blur.focusMode.value = DepthOfFieldMode.Manual;
            blur.nearFocusStart.overrideState = on;
            blur.nearFocusStart.value = nearStart;
            blur.nearFocusEnd.overrideState = on;
            blur.nearFocusEnd.value = nearEnd;
            blur.farFocusStart.overrideState = on;
            blur.farFocusStart.value = farStart;
            blur.farFocusEnd.overrideState = on;
            blur.farFocusEnd.value = farEnd;
        }

        public static void DropLook(GameObject look)
        {
            if (look == null)
            {
                return;
            }
            var volume = look.GetComponent<Volume>();
            VolumeProfile profile = volume != null ? volume.sharedProfile : null;
            CodeAssets.DropProfile(profile);   // its depth of field and its vignette with it
            UnityEngine.Object.Destroy(look);
        }

        // ------------------------------------------------------------------ a place for the camera

        /// <summary>The height of the ground under a point, looked for from a little above it; false where none is met.</summary>
        public static bool Ground(Vector3 over, out float height)
        {
            if (Physics.Raycast(over + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 40f, GroundMask, QueryTriggerInteraction.Ignore))
            {
                height = hit.point.y;
                return true;
            }
            height = over.y;
            return false;
        }

        /// <summary>
        /// The camera for a shot of a subject: each side and facing in turn until one is clear, with the subject in sight.
        /// False, with why, when none is. Some eight physics queries a try, on a press.
        /// </summary>
        public static bool Place(CineShot shot, in CineFound found, out CinePose pose, out Vector3 eye, out string why)
        {
            pose = default(CinePose);
            eye = default(Vector3);
            for (int attempt = 0; attempt < CineRules.Tries; attempt++)
            {
                CineRules.Try(attempt, out int side, out bool flip);
                if (flip && !CineRules.CanFlip(shot.Style))
                {
                    break;
                }
                CineBox box = found.Box;
                if (flip)
                {
                    box.Fx = -box.Fx;
                    box.Fz = -box.Fz;
                }
                if (shot.Style == CineStyle.Establishing)
                {
                    CineRules.Establish(box, found.Roof, found.Back, found.Sideways, out pose);
                }
                else
                {
                    CineRules.Frame(shot.Style, box, shot.Fov, side, out pose);
                }
                if (Clear(shot.Style, pose, out eye))
                {
                    why = null;
                    return true;
                }
                if (shot.Style == CineStyle.Establishing)
                {
                    break;   // a roof has one place
                }
            }
            why = "no clear place for the camera with " + CineRules.SubjectWord(shot.Subject) + " in sight";
            return false;
        }

        // The line from the subject to the camera is cast first, and the camera pulled in to stand before whatever is met:
        // a wall crossed is how a camera comes to be inside a building. Then the place itself must be clear air over the ground.
        private static bool Clear(CineStyle style, in CinePose pose, out Vector3 eye)
        {
            eye = new Vector3(pose.EyeX, pose.EyeY, pose.EyeZ);
            var aim = new Vector3(pose.AimX, pose.AimY, pose.AimZ);
            var anchor = new Vector3(pose.AnchorX, pose.AnchorY, pose.AnchorZ);
            Vector3 back = aim - eye;   // the way the place moves while it is not clear: toward what it looks at
            if (!pose.Free)
            {
                Vector3 line = eye - anchor;
                float wanted = line.magnitude;
                if (wanted < 0.05f)
                {
                    return false;
                }
                Vector3 way = line / wanted;
                float from = Mathf.Clamp(pose.Reach, 0f, Mathf.Max(0f, wanted - CineRules.LensRadius * 2f));   // the subject's own body is not in its own way
                float met = Physics.SphereCast(anchor + way * from, CineRules.LensRadius, way, out RaycastHit hit, wanted - from, Solid, QueryTriggerInteraction.Ignore)
                    ? from + hit.distance
                    : -1f;
                if (!CineRules.Pull(wanted, met, CineRules.Margin, CineRules.LeastShare(style), out float at))
                {
                    return false;
                }
                eye = anchor + way * at;
                back = -way;
            }
            back = back.sqrMagnitude > 0.0001f ? back.normalized : Vector3.up;
            if (style == CineStyle.Establishing)
            {
                back = Vector3.up;   // on a roof the way out of a parapet or a tank is up
            }
            for (int step = 0; Physics.CheckSphere(eye, CineRules.LensRadius, Solid, QueryTriggerInteraction.Ignore); step++)
            {
                if (step >= CineRules.Slides)
                {
                    return false;
                }
                eye += back * CineRules.SlideStep;
            }
            if (Ground(eye, out float ground) && eye.y < ground + CineRules.LeastHeight)
            {
                eye.y = ground + CineRules.LeastHeight;
            }
            return true;
        }

        /// <summary>The camera's turn at a place: at what it looks at, then turned to put the subject on a third.</summary>
        public static Quaternion Facing(in CinePose pose, Vector3 eye)
        {
            Vector3 way = new Vector3(pose.AimX, pose.AimY, pose.AimZ) - eye;
            Quaternion look = way.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(way.normalized, Vector3.up) : Quaternion.identity;
            return Mathf.Approximately(pose.Turn, 0f) ? look : Quaternion.AngleAxis(pose.Turn, Vector3.up) * look;
        }

        /// <summary>Where the lens puts the shot's sharp point: across and up the frame from 0 to 1, and metres ahead.</summary>
        public static Vector3 Framed(Camera camera, in CinePose pose)
        {
            return camera != null ? camera.WorldToViewportPoint(new Vector3(pose.FocusX, pose.FocusY, pose.FocusZ)) : Vector3.zero;
        }

        // ------------------------------------------------------------------ the picture

        /// <summary>The picture's own target at 3840 by 2160: the camera draws into it for a few frames, post processing and all, and no interface is ever in it.</summary>
        public static RenderTexture MakeTarget()
        {
            var target = new RenderTexture(CineRules.Width, CineRules.Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "RealLifeOverhaul Cinematic Frame", antiAliasing = 1, useMipMap = false, autoGenerateMips = false
            };
            if (target.Create())
            {
                return target;
            }
            DropTarget(target);
            return null;
        }

        /// <summary>The target's memory on the graphics card let go and the target destroyed. Safe with null.</summary>
        public static void DropTarget(RenderTexture target)
        {
            if (target != null)
            {
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
        }

        /// <summary>The texture the target is read into, one for the whole run, some 25 MB.</summary>
        public static Texture2D MakeReader()
        {
            var reader = new Texture2D(CineRules.Width, CineRules.Height, TextureFormat.RGB24, false) { name = "RealLifeOverhaul Cinematic Reader" };
            return reader;
        }

        public static void DropReader(Texture2D reader)
        {
            if (reader != null)
            {
                UnityEngine.Object.Destroy(reader);
            }
        }

        /// <summary>A picture's pixels outside the managed heap, for the worker: RGB rows from the target, or the screen as the engine gave it.</summary>
        public sealed class Frame
        {
            public NativeArray<byte> Pixels;       // 3 bytes a pixel, from the target
            public PhotosGame.Shot Screen;         // or the screen's own read, turned into RGB on the worker
            public int Width, Height;
        }

        /// <summary>
        /// The target read at the end of a frame, some 25 MB copied once. Null for a frame of one flat colour, which is a
        /// target nothing was drawn into: the caller reads the screen then.
        /// </summary>
        public static Frame ReadTarget(RenderTexture target, Texture2D reader)
        {
            if (target == null || reader == null)
            {
                return null;
            }
            RenderTexture was = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                reader.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0, false);
            }
            finally
            {
                RenderTexture.active = was;
            }
            NativeArray<byte> raw = reader.GetRawTextureData<byte>();
            Spread(raw, reader.width, reader.height, out int darkest, out int brightest);
            if (CineRules.Flat(darkest, brightest))
            {
                return null;
            }
            return new Frame { Pixels = new NativeArray<byte>(raw, Allocator.Persistent), Width = reader.width, Height = reader.height };
        }

        /// <summary>The screen read at the end of a frame, at the screen's own size, when the target gave nothing.</summary>
        public static Frame ReadScreen()
        {
            PhotosGame.Shot shot = PhotosGame.Read();
            return shot != null ? new Frame { Screen = shot, Width = shot.Width, Height = shot.Height } : null;
        }

        // The darkest and the brightest of a grid of pixels, by their green.
        private static void Spread(NativeArray<byte> rgb, int width, int height, out int darkest, out int brightest)
        {
            darkest = 255;
            brightest = 0;
            for (int row = 0; row < SampleRows; row++)
            {
                int y = (int)((row + 0.5f) * height / SampleRows);
                for (int column = 0; column < SampleColumns; column++)
                {
                    int x = (int)((column + 0.5f) * width / SampleColumns);
                    int at = (y * width + x) * 3 + 1;
                    if (at < 0 || at >= rgb.Length)
                    {
                        continue;
                    }
                    int value = rgb[at];
                    darkest = value < darkest ? value : darkest;
                    brightest = value > brightest ? value : brightest;
                }
            }
        }

        /// <summary>Lets a picture's pixels go; a second call does nothing.</summary>
        public static void Drop(Frame frame)
        {
            if (frame == null)
            {
                return;
            }
            if (frame.Pixels.IsCreated)
            {
                frame.Pixels.Dispose();
            }
            PhotosGame.Drop(frame.Screen);
        }

        /// <summary>
        /// On a worker: the picture written as a PNG and, beside it, a JPG at quality 92, each under a name of its own and
        /// then moved into place. Its pixels are let go. False if either was not written.
        /// </summary>
        public static bool Develop(Frame frame, string folder, string name)
        {
            NativeArray<byte> turned = default;
            NativeArray<byte> png = default, jpg = default;
            try
            {
                Directory.CreateDirectory(folder);
                NativeArray<byte> rgb = frame.Pixels;
                if (frame.Screen != null)
                {
                    PhotosGame.Shot screen = frame.Screen;
                    turned = new NativeArray<byte>(screen.Width * screen.Height * 3, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    if (!PhotoPixels.Shrink(screen.Pixels.AsReadOnlySpan(), screen.Width, screen.Height, screen.Bytes, screen.Bgr, turned.AsSpan(), screen.Width, screen.Height))
                    {
                        return false;
                    }
                    rgb = turned;
                }
                if (!rgb.IsCreated)
                {
                    return false;
                }
                png = ImageConversion.EncodeNativeArrayToPNG(rgb, GraphicsFormat.R8G8B8_SRGB, (uint)frame.Width, (uint)frame.Height);
                jpg = ImageConversion.EncodeNativeArrayToJPG(rgb, GraphicsFormat.R8G8B8_SRGB, (uint)frame.Width, (uint)frame.Height, 0u, CineRules.JpgQuality);
                return Write(png, Path.Combine(folder, name + ".png")) && Write(jpg, Path.Combine(folder, name + ".jpg"));
            }
            finally
            {
                if (turned.IsCreated)
                {
                    turned.Dispose();
                }
                if (png.IsCreated)
                {
                    png.Dispose();
                }
                if (jpg.IsCreated)
                {
                    jpg.Dispose();
                }
                Drop(frame);
            }
        }

        private static bool Write(NativeArray<byte> encoded, string path)
        {
            if (!encoded.IsCreated || encoded.Length == 0)
            {
                return false;
            }
            byte[] bytes = encoded.ToArray();
            return PhotoStore.WriteWhole(path, bytes, bytes.Length);
        }

        /// <summary>On a worker: the run's list of pictures written beside them. False if it was not written.</summary>
        public static bool WriteList(string folder, string text)
        {
            try
            {
                Directory.CreateDirectory(folder);
                byte[] bytes = new System.Text.UTF8Encoding(true).GetBytes(text);
                return PhotoStore.WriteWhole(Path.Combine(folder, "shots.txt"), bytes, bytes.Length);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// Pictures/Real Life Overhaul/Cinematic under the player's own profile, where a picture is looked for; the game's
        /// own data folder where the computer names no pictures folder. Main thread, at the start of a run.
        /// </summary>
        public static string Root()
        {
            string pictures = null;
            try
            {
                pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                if (string.IsNullOrEmpty(pictures))
                {
                    string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    pictures = string.IsNullOrEmpty(home) ? null : Path.Combine(home, "Pictures");
                }
            }
            catch (Exception e) when (e is ArgumentException || e is PlatformNotSupportedException)
            {
                pictures = null;
            }
            return Path.Combine(string.IsNullOrEmpty(pictures) ? Path.Combine(Application.persistentDataPath, "RealLifeOverhaul") : pictures, "Real Life Overhaul", "Cinematic");
        }
    }

    /// <summary>
    /// The run's one line of words, "Taking cinematic shots 3 of 18, press Esc to stop": a small canvas of its own at the
    /// foot of the screen, since the game's whole interface is hidden while the shots are taken. It is never in a picture
    /// (a picture's target takes no interface, and it is put away before the screen is read), and it goes with the run.
    /// Cost: nothing a frame; its words are set once a shot.
    /// </summary>
    internal sealed class CineLine
    {
        private const float Wide = 1500f, High = 84f, FromFoot = 90f;   // canvas units of the game's 3840 by 2160
        private const int SortingOrder = 30000;                         // over everything: it is the only interface left

        private readonly GameObject _root;
        private readonly TextMeshProUGUI _words;

        public CineLine(Transform parent)
        {
            _root = new GameObject("RealLifeOverhaul Cinematic Line", typeof(RectTransform));
            _root.transform.SetParent(parent, false);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(CineRules.Width, CineRules.Height);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            Image pill = FlareKit.Box(_root.transform, "Pill", new Color(0f, 0f, 0f, 0.62f), High * 0.5f);
            RectTransform at = pill.rectTransform;
            at.anchorMin = at.anchorMax = at.pivot = new Vector2(0.5f, 0f);
            at.anchoredPosition = new Vector2(0f, FromFoot);
            at.sizeDelta = new Vector2(Wide, High);
            _words = FlareKit.Text(pill.transform, "Words", 40f, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            _words.textWrappingMode = TextWrappingModes.NoWrap;
            FlareKit.Stretch(_words.rectTransform, 30f, 0f, 30f, 0f);
            _root.SetActive(false);
        }

        public void Show(string words)
        {
            if (_root == null)
            {
                return;
            }
            FlareKit.Set(_words, words);
            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        public void Destroy()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }
        }
    }
}
