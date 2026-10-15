using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using BAModAPI;
using BigAmbitions.InputSystem;
using CameraControllers;
using Cinemachine;
using Helpers;
using UI;
using UI.Load;
using UI.Notification;
using UI.Smartphone;
using Unity.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// The one place the Camera and the Gallery touch the game and the engine: what the moment is (on foot, in a vehicle, a
    /// window open), the phone's menu, the HUD, the player's walk and body, the game's cameras, the pointer, the screen read
    /// at the end of a frame and the photo made from it on a worker thread (the engine's JPG encoder, which may run on any
    /// thread), a photo loaded for the full screen (the engine's own loader, which decodes on its worker threads), the grade
    /// of a filter, the save's id and the folders, and the game's notifications.
    ///
    /// Per frame: nothing of its own. The camera's frame reads <see cref="Situation"/> (a dozen of the game's flags) and the
    /// two keys; everything else runs on a click, at the camera's opening and closing, or once a second.
    /// </summary>
    internal static class PhotosGame
    {
        public static IModLogger Logger;

        private const NavigationBlocker Blocker = (NavigationBlocker)7319;   // a value of our own, so nobody else's is ever lifted
        private const int JpgQuality = 90;                                    // a 1080-line photo in some 500 KB

        // ---------------------------------------------------------------- the save and the folders

        /// <summary>True while a city is loaded and its player is there.</summary>
        public static bool Loaded => SaveGameManager.Current != null && !LoadScene.isLoading && PlayerHelper.PlayerController != null;

        /// <summary>The save's id (the game's character id, shared by every save file of one playthrough), or null.</summary>
        public static string SaveId => SaveGameManager.Current != null ? SaveGameManager.Current.characterId : null;

        public static int Day => SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;

        public static int Hour => SaveGameManager.Current != null ? SaveGameManager.Current.Hour : 0;

        /// <summary>persistentDataPath/RealLifeOverhaul/Photos (read on the main thread: the engine answers it nowhere else).</summary>
        public static string Root => Path.Combine(Application.persistentDataPath, "RealLifeOverhaul", PhotoStoreRules.RootFolder);

        /// <summary>The game's folder of saves, which the look over deleted saves only reads (SaveGamePathHelper.SaveGameFolderPath, which makes nothing).</summary>
        public static string SaveGames => SaveGamePathHelper.SaveGameFolderPath;

        // ---------------------------------------------------------------- the moment

        /// <summary>
        /// What the moment is, for CameraRules. <paramref name="phoneCounts"/>: whether the phone's menu open counts as a
        /// window (it does not while the camera is being asked for from the phone, which is then put away). A control left
        /// chosen by a click (the phone's app button, a chip of the viewfinder) is let go first: the game takes a chosen
        /// control for typing, and would read it as a window open.
        /// </summary>
        public static CameraSituation Situation(bool phoneCounts)
        {
            Unselect();
            var s = new CameraSituation { FeatureOn = Settings.PhoneCamera, Loaded = SaveGameManager.Current != null && !LoadScene.isLoading };
            PlayerController player = PlayerHelper.PlayerController;
            UIs uis = InstanceBehavior<UIs>.Instance;
            s.PlayerThere = player != null && player.gameObject.activeInHierarchy && !GameMembers.PlayerDead && uis != null;
            if (!s.Loaded || !s.PlayerThere)
            {
                return s;
            }
            s.PhotoMode = Leisure.PhotoMode.Active || ScreenshotController.isInFreeLookMode;
            s.Custody = Law.Custody.Active;
            s.InVehicle = PlayerHelper.IsUsingVehicle;
            s.OnTransit = TransitDirector.Riding;
            s.Skipping = uis.timeMachine != null && uis.timeMachine.isRunning;
            s.Busy = player.NavigationDisabled || GameManager.IsAnyMiniGameActive() || PoliceAttention.IsBusy;
            s.Window = (phoneCounts && FullMenu.IsOpen) || ThirdPersonCamera.AnyWindow(false);
            return s;
        }

        // ---------------------------------------------------------------- the phone

        public static bool MenuOpen => FullMenu.IsOpen;

        /// <summary>The phone is put away (the game's own close of the full menu).</summary>
        public static void CloseMenu()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.fullMenu != null && FullMenu.IsOpen)
            {
                uis.fullMenu.Toggle(false);
            }
        }

        // ---------------------------------------------------------------- the HUD

        public static bool HudShown => ScreenshotController.uiIsVisible;

        /// <summary>The game's own "hide the interface" (its canvases go clear), put to the state asked for if it is not there already.</summary>
        public static void Hud(bool shown)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.screenshot != null && ScreenshotController.uiIsVisible != shown)
            {
                uis.screenshot.ToggleUIVisibility();
            }
        }

        // ---------------------------------------------------------------- the cameras

        public static Camera MainCamera => GameManager.GetMainCamera();

        public static CinemachineBrain BrainOf(Camera camera)
        {
            return camera != null ? camera.GetComponent<CinemachineBrain>() : null;
        }

        /// <summary>The game's street (0) or indoor (1) camera's turn and distance, which the mouse's right button and the turn keys move.</summary>
        public static bool ReadTurn(int which, out float angle, out float distance)
        {
            PedestrianCam cam = Turnable(which);
            angle = cam != null ? cam.angle : 0f;
            distance = cam != null ? cam.distance : 0f;
            return cam != null;
        }

        public static void WriteTurn(int which, float angle, float distance)
        {
            PedestrianCam cam = Turnable(which);
            if (cam != null)
            {
                cam.angle = angle;
                cam.distance = distance;
            }
        }

        private static PedestrianCam Turnable(int which)
        {
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            CinemachineVirtualCameraBase vcam = gm == null ? null : which == 0 ? gm.pedestrianCamera : gm.indoorCamera;
            return vcam != null ? vcam.GetComponent<PedestrianCam>() : null;
        }

        public static bool ZoomBlocked
        {
            get => PedestrianCam.blockCameraZoom;
            set => PedestrianCam.blockCameraZoom = value;
        }

        // ---------------------------------------------------------------- the player

        /// <summary>The player's walk held (the game's own blocker list, with a value of the camera's own) or let go.</summary>
        public static void HoldWalk(bool hold)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null)
            {
                return;
            }
            if (hold)
            {
                player.ResetNavigation();
                player.SetNavigationBlocker(Blocker);
            }
            else
            {
                player.UnsetNavigationBlocker(Blocker);
            }
        }

        /// <summary>The player's body, the root that turns it, and its head, or false.</summary>
        public static bool Body(out Transform root, out Transform head)
        {
            root = head = null;
            PlayerController player = PlayerHelper.PlayerController;
            ThirdPersonCharacter body = player != null ? player.Character : null;
            Animator animator = body != null ? body.animator : null;
            if (body == null || animator == null || !animator.isHuman)
            {
                return false;
            }
            root = body.transform;
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            return head != null;
        }

        public static PlayerController Player => PlayerHelper.PlayerController;

        /// <summary>True while the player sits (a bench, a chair): the body stays as the seat has it.</summary>
        public static bool Seated
        {
            get
            {
                PlayerController player = PlayerHelper.PlayerController;
                return player != null && player.Character != null && player.Character.isSittingOn != null;
            }
        }

        /// <summary>The world's clicks held (nothing in the world is pointed at or clicked, as in the game's placement), or let go.</summary>
        public static void HoldWorld(bool hold)
        {
            if (hold)
            {
                MouseController.SetRestrictedObjectTypes();
            }
            else
            {
                MouseController.Reset();
            }
        }

        // ---------------------------------------------------------------- keys

        /// <summary>The game's confirm key pressed this frame: taken, so the game's own handling does nothing with it.</summary>
        public static bool TakeConfirm()
        {
            if (!PlayerAction.Confirm.Pressed())
            {
                return false;
            }
            PlayerAction.Confirm.Reset();
            return true;
        }

        /// <summary>The game's cancel key pressed this frame: taken, so the game's menu does not open too.</summary>
        public static bool TakeCancel()
        {
            if (!PlayerAction.Cancel.Pressed())
            {
                return false;
            }
            PlayerAction.Cancel.Reset();
            return true;
        }

        /// <summary>The game's two turn keys taken (they would turn the game's camera behind the phone).</summary>
        public static void TakeTurnKeys()
        {
            if (PlayerAction.RotateLeft.Pressed())
            {
                PlayerAction.RotateLeft.Reset();
            }
            if (PlayerAction.RotateRight.Pressed())
            {
                PlayerAction.RotateRight.Reset();
            }
        }

        /// <summary>The confirm and cancel keys as the game names them, for the viewfinder's line of keys.</summary>
        public static string ConfirmKey => PlayerAction.Confirm.GetDisplayName();

        public static string CancelKey => PlayerAction.Cancel.GetDisplayName();

        /// <summary>Nothing of the viewfinder's stays chosen after a click (the game takes a chosen control for typing and holds its keys).</summary>
        public static void Unselect()
        {
            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null)
            {
                events.SetSelectedGameObject(null);
            }
        }

        // ---------------------------------------------------------------- the grade

        /// <summary>A volume of the camera's own for its filter, over the mod's own grading, until it is taken away.</summary>
        public static GameObject MakeGrade(Transform parent, out ColorAdjustments color)
        {
            var made = new GameObject("RealLifeOverhaul Camera Grade");
            made.transform.SetParent(parent, false);
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            color = profile.Add<ColorAdjustments>();
            var volume = made.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1100f;   // over the mod's own grading (1000), as photo mode's
            volume.sharedProfile = profile;
            volume.weight = 1f;
            return made;
        }

        public static void Grade(ColorAdjustments color, int filter)
        {
            if (color == null)
            {
                return;
            }
            CameraFilter f = CameraRules.Filters[CameraRules.ClampFilter(filter)];
            bool graded = CameraRules.Graded(filter);
            color.colorFilter.overrideState = graded;
            color.colorFilter.value = new Color(f.R, f.G, f.B, 1f);
            color.saturation.overrideState = graded;
            color.saturation.value = f.Saturation;
            color.contrast.overrideState = graded;
            color.contrast.value = f.Contrast;
            color.postExposure.overrideState = graded && f.Exposure != 0f;
            color.postExposure.value = f.Exposure;
        }

        public static void DropGrade(GameObject grade)
        {
            if (grade == null)
            {
                return;
            }
            var volume = grade.GetComponent<Volume>();
            if (volume != null && volume.sharedProfile != null)
            {
                UnityEngine.Object.Destroy(volume.sharedProfile);
            }
            UnityEngine.Object.Destroy(grade);
        }

        // ---------------------------------------------------------------- the screen and the photo

        /// <summary>The screen as it is drawn, read at the end of a frame: its pixels a copy outside the managed heap, for the worker.</summary>
        public sealed class Shot
        {
            public NativeArray<byte> Pixels;
            public int Width, Height, Bytes;
            public bool Bgr;
            public double ReadMs;   // the main thread's share: the engine's read of the screen and one copy
        }

        private static readonly int HotRead = HotSpots.Slot("Photos: PhotosGame.Read");

        /// <summary>
        /// THE ONE READ OF THE SCREEN ON THE MAIN THREAD, measured: called after WaitForEndOfFrame, as "Report a bug" and
        /// Flare's camera take it (ScreenCapture.CaptureScreenshotAsTexture); the pixels copied out, the texture let go at
        /// once. Null when it cannot be taken or is of a kind of pixel the camera does not read.
        /// </summary>
        public static Shot Read()
        {
            using var hotSpot = HotSpots.Time(HotRead);
            var watch = Stopwatch.StartNew();
            Texture2D texture = null;
            try
            {
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (texture == null || !Layout(texture.graphicsFormat, out int bytes, out bool bgr))
                {
                    Logger?.Warn("Camera: the screen came " + (texture == null ? "empty" : "as " + texture.graphicsFormat + ", which the camera does not read") + "; no photo.");
                    return null;
                }
                var shot = new Shot
                {
                    Pixels = new NativeArray<byte>(texture.GetRawTextureData<byte>(), Allocator.Persistent),
                    Width = texture.width, Height = texture.height, Bytes = bytes, Bgr = bgr
                };
                shot.ReadMs = watch.Elapsed.TotalMilliseconds;
                return shot;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return null;
            }
            finally
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
        }

        private static bool Layout(GraphicsFormat format, out int bytes, out bool bgr)
        {
            bytes = 4;
            bgr = false;
            switch (format)
            {
                case GraphicsFormat.R8G8B8A8_SRGB:
                case GraphicsFormat.R8G8B8A8_UNorm:
                    return true;
                case GraphicsFormat.B8G8R8A8_SRGB:
                case GraphicsFormat.B8G8R8A8_UNorm:
                    bgr = true;
                    return true;
                case GraphicsFormat.R8G8B8_SRGB:
                case GraphicsFormat.R8G8B8_UNorm:
                    bytes = 3;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The screen's pixels let go, whatever became of them (a second call does nothing).</summary>
        public static void Drop(Shot shot)
        {
            if (shot != null && shot.Pixels.IsCreated)
            {
                shot.Pixels.Dispose();
            }
        }

        /// <summary>What the worker made of a shot.</summary>
        public sealed class Developed
        {
            public int Width, Height;            // the photo
            public byte[] Small;                 // its small copy's rows
            public int SmallWidth, SmallHeight;
            public long Bytes;                   // the JPG's size
            public double Ms;                    // the worker's time
        }

        /// <summary>
        /// ON A WORKER THREAD: the shot made a photo of about 1080 lines (PhotoPixels, averaged), the engine's JPG encoder, the
        /// file written whole at <paramref name="path"/>, and its small copy written where Flare reads it (PhotoThumbs.FileOf).
        /// The shot's pixels are let go. Null if the photo could not be written (nothing is left half done).
        /// </summary>
        public static Developed Develop(Shot shot, string path)
        {
            var watch = Stopwatch.StartNew();
            NativeArray<byte> photo = default;
            NativeArray<byte> jpg = default;
            try
            {
                PhotoPixels.Fit(shot.Width, shot.Height, PhotoPixels.PhotoWidth, PhotoPixels.PhotoHeight, out int w, out int h);
                if (w <= 0 || h <= 0)
                {
                    return null;
                }
                photo = new NativeArray<byte>(w * h * 3, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                if (!PhotoPixels.Shrink(shot.Pixels.AsReadOnlySpan(), shot.Width, shot.Height, shot.Bytes, shot.Bgr, photo.AsSpan(), w, h))
                {
                    return null;
                }
                Drop(shot);   // (the screen is done with: its megabytes go before the encoder's)
                jpg = ImageConversion.EncodeNativeArrayToJPG(photo, GraphicsFormat.R8G8B8_SRGB, (uint)w, (uint)h, 0u, JpgQuality);
                if (!jpg.IsCreated || jpg.Length == 0)
                {
                    return null;
                }
                byte[] bytes = jpg.ToArray();
                if (!PhotoStore.WriteWhole(path, bytes, bytes.Length))
                {
                    return null;
                }
                PhotoPixels.SmallSize(w, h, out int sw, out int sh);
                var small = new byte[sw * sh * 3];
                PhotoPixels.Shrink(photo.AsReadOnlySpan(), w, h, 3, false, small, sw, sh);
                PhotoThumbs.Write(PhotoThumbs.FileOf(path), small, sw, sh);
                return new Developed { Width = w, Height = h, Small = small, SmallWidth = sw, SmallHeight = sh, Bytes = bytes.Length, Ms = watch.Elapsed.TotalMilliseconds };
            }
            finally
            {
                if (jpg.IsCreated)
                {
                    jpg.Dispose();
                }
                if (photo.IsCreated)
                {
                    photo.Dispose();
                }
                Drop(shot);
            }
        }

        // ---------------------------------------------------------------- pictures

        /// <summary>A small copy's rows as a texture (one upload; the engine keeps no copy in memory). Null for none.</summary>
        public static Texture2D SmallTexture(byte[] rows, int width, int height)
        {
            if (rows == null || width <= 0 || height <= 0 || rows.Length < width * height * 3)
            {
                return null;
            }
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false) { name = "RealLifeOverhaul Photo Small", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.LoadRawTextureData(rows);
            texture.Apply(false, true);
            return texture;
        }

        public static void Destroy(UnityEngine.Object made)
        {
            if (made != null)
            {
                UnityEngine.Object.Destroy(made);
            }
        }

        private static readonly int HotLoaded = HotSpots.Slot("Photos: PhotosGame.Load");

        /// <summary>
        /// A photo loaded whole for the full screen or the wallpaper: read and decoded by the engine's own loader on its worker
        /// threads (UnityWebRequestTexture, from the file on this computer: nothing goes over the network), the texture handed
        /// to <paramref name="done"/> (null if it could not be read). The main thread's share (the texture made from what the
        /// workers decoded) is timed under its own name (Core/HotSpots) and said with Developer diagnostics on.
        /// </summary>
        public static IEnumerator Load(string path, Action<Texture2D> done)
        {
            Texture2D texture = null;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(new Uri(path).AbsoluteUri, true))
                {
                    yield return request.SendWebRequest();
                    using var hotSpot = HotSpots.Time(HotLoaded);
                    var watch = Stopwatch.StartNew();
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        texture = DownloadHandlerTexture.GetContent(request);
                        if (texture != null)
                        {
                            texture.name = "RealLifeOverhaul Photo";
                            texture.wrapMode = TextureWrapMode.Clamp;
                        }
                    }
                    else
                    {
                        Logger?.Warn("Gallery: " + Path.GetFileName(path) + " could not be read (" + request.error + ").");
                    }
                    if (Meta.Dev.Verbose)
                    {
                        Logger?.Info("Gallery check (a photo loaded off the main thread: the main thread's share " + watch.Elapsed.TotalMilliseconds.ToString("0.0")
                                     + " ms, wanted under " + CameraRules.ReadMost.ToString("0") + "): " + (texture == null ? "no picture" : watch.Elapsed.TotalMilliseconds < CameraRules.ReadMost ? "PASS" : "slow"));
                    }
                }
            }
            done(texture);
        }

        // ---------------------------------------------------------------- notifications

        /// <summary>One of the game's notifications, its {names} filled from name and value pairs.</summary>
        public static void Tell(NotificationType type, string key, params string[] pairs)
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
            Notifications.Show(type, key, data, 8f);
        }
    }
}
