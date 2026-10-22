using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
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
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// The one place the Camera and the Gallery touch the game and the engine. Nothing per frame of its own: the open
    /// camera's frame reads <see cref="Situation"/> (a dozen of the game's flags) and two keys; the rest runs on a click,
    /// as the camera opens and closes, or once a second.
    /// </summary>
    internal static class PhotosGame
    {
        public static IModLogger Logger;

        private const NavigationBlocker Blocker = (NavigationBlocker)7319;   // a value of our own, so nobody else's is ever lifted
        private const int JpgQuality = 90;                                    // a 1080-line photo in some 500 KB

        /// <summary>True while a city is loaded and its player is there.</summary>
        public static bool Loaded => SaveGameManager.Current != null && !LoadScene.isLoading && PlayerHelper.PlayerController != null;

        /// <summary>The game's character id, shared by every save file of one playthrough, or null.</summary>
        public static string SaveId => SaveGameManager.Current != null ? SaveGameManager.Current.characterId : null;

        public static int Day => SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;

        public static int Hour => SaveGameManager.Current != null ? SaveGameManager.Current.Hour : 0;

        /// <summary>persistentDataPath/RealLifeOverhaul/Photos; main thread only, as the engine answers it nowhere else.</summary>
        public static string Root => Path.Combine(Application.persistentDataPath, "RealLifeOverhaul", PhotoStoreRules.RootFolder);

        /// <summary>The game's folder of saves; reading it makes nothing.</summary>
        public static string SaveGames => SaveGamePathHelper.SaveGameFolderPath;

        /// <summary>
        /// The moment, for CameraRules. The phone's menu counts as a window only with <paramref name="phoneCounts"/>, since the
        /// phone is put away when the camera is asked for from it. A control left chosen by a click is let go first: the game
        /// takes it for typing, and would read it as a window open.
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
            s.Busy = GameManager.IsAnyMiniGameActive() || PoliceAttention.IsBusy;
            s.WalkHeld = player.NavigationDisabled;
            s.OwnHold = s.WalkHeld && OwnHoldOnly(player);
            s.Window = (phoneCounts && FullMenu.IsOpen) || ThirdPersonCamera.AnyWindow(false);
            return s;
        }

        private static readonly FieldInfo BlockersField = GameContract.NavBlockers.Field;

        // Without the game's list of blockers, a walk held while the camera is open is taken for the camera's own.
        private static bool OwnHoldOnly(PlayerController player)
        {
            if (BlockersField?.GetValue(player) is HashSet<NavigationBlocker> blockers)
            {
                return blockers.Count == 1 && blockers.Contains(Blocker);
            }
            return CameraMode.Active;
        }

        public static bool MenuOpen => FullMenu.IsOpen;

        /// <summary>Puts the phone away through the game's own close of the full menu.</summary>
        public static void CloseMenu()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.fullMenu != null && FullMenu.IsOpen)
            {
                uis.fullMenu.Toggle(false);
            }
        }

        public static bool HudShown => ScreenshotController.uiIsVisible;

        /// <summary>The game's own "hide the interface"; with no interface, as the city unloads, the static flag alone is set.</summary>
        public static void Hud(bool shown)
        {
            if (ScreenshotController.uiIsVisible == shown)
            {
                return;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.screenshot != null)
            {
                uis.screenshot.ToggleUIVisibility();
            }
            else
            {
                ScreenshotController.uiIsVisible = shown;
            }
        }

        public static Camera MainCamera => GameManager.GetMainCamera();

        public static CinemachineBrain BrainOf(Camera camera)
        {
            return camera != null ? camera.GetComponent<CinemachineBrain>() : null;
        }

        /// <summary>The game's street (0) or indoor (1) camera's turn and distance, which the mouse and the turn keys move.</summary>
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

        /// <summary>The player's walk held or let go through the game's blocker list, with a value of the camera's own.</summary>
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

        /// <summary>True while the player sits on a bench or a chair.</summary>
        public static bool Seated
        {
            get
            {
                PlayerController player = PlayerHelper.PlayerController;
                return player != null && player.Character != null && player.Character.isSittingOn != null;
            }
        }

        private static readonly FieldInfo WorldMaskField = GameContract.WorldMask.Field;

        /// <summary>The layers the game's mouse points at and its cursor over them, which a mop sets; false if they cannot be read.</summary>
        public static bool ReadWorld(out int mask, out int cursor)
        {
            cursor = (int)MouseController.cursorOnHover;
            mask = 0;
            if (WorldMaskField?.GetValue(null) is LayerMask layers)
            {
                mask = layers.value;
                return true;
            }
            return false;
        }

        /// <summary>Holds the world's clicks, as the game's placement does, and returns the mask set.</summary>
        public static int HoldWorld()
        {
            MouseController.SetRestrictedObjectTypes();
            return 0;   // no layer named: the empty mask
        }

        /// <summary>Puts the world's clicks back while the mask is still the camera's own; unread, the game's defaults and a mop's floor.</summary>
        public static void GiveWorld(bool read, int mask, int cursor, int cameras)
        {
            if (read && WorldMaskField != null)
            {
                if (ReadWorld(out int now, out _) && CameraHold.WorldBack(now, cameras))
                {
                    LayerMask layers = mask;
                    WorldMaskField.SetValue(null, layers);
                    MouseController.cursorOnHover = (CursorType)cursor;
                }
                return;
            }
            MouseController.Reset();
            if (PlayerHelper.IsHoldingAMop)
            {
                MouseController.cursorOnHover = CursorType.Mop;
                MouseController.SetRestrictedObjectTypes(InteractiveObjectType.Ground);
            }
        }

        /// <summary>The game's confirm key pressed this frame, taken so the game's own handling does nothing with it.</summary>
        public static bool TakeConfirm()
        {
            if (!PlayerAction.Confirm.Pressed())
            {
                return false;
            }
            PlayerAction.Confirm.Reset();
            return true;
        }

        /// <summary>The game's cancel key pressed this frame, taken so the game's menu does not open too.</summary>
        public static bool TakeCancel()
        {
            if (!PlayerAction.Cancel.Pressed())
            {
                return false;
            }
            PlayerAction.Cancel.Reset();
            return true;
        }

        /// <summary>Takes the turn keys, which would turn the game's camera behind the phone.</summary>
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

        public static string ConfirmKey => PlayerAction.Confirm.GetDisplayName();

        public static string CancelKey => PlayerAction.Cancel.GetDisplayName();

        /// <summary>Lets go of a control chosen by a click: the game takes a chosen control for typing and holds its keys.</summary>
        public static void Unselect()
        {
            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null)
            {
                events.SetSelectedGameObject(null);
            }
        }

        /// <summary>A volume of the camera's own for its filter, over the mod's own grading.</summary>
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
            if (volume != null)
            {
                CodeAssets.DropProfile(volume.sharedProfile);   // its colour setting with it
            }
            UnityEngine.Object.Destroy(grade);
        }

        /// <summary>The screen read at the end of a frame, its pixels copied outside the managed heap for the worker.</summary>
        public sealed class Shot
        {
            public NativeArray<byte> Pixels;
            public int Width, Height, Bytes;
            public bool Bgr;
            public double ReadMs;   // the main thread's share
        }

        private static readonly int HotRead = HotSpots.Slot("Photos: PhotosGame.Read");

        /// <summary>The one read of the screen on the main thread, after WaitForEndOfFrame; null if not taken or of pixels not read.</summary>
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

        /// <summary>Lets the screen's pixels go; a second call does nothing.</summary>
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
            public int Width, Height;
            public byte[] Small;                 // the small copy's rows
            public int SmallWidth, SmallHeight;
            public long Bytes;                   // the JPG's size
            public double Ms;
        }

        /// <summary>On a worker: the shot as a JPG and its small copy at <paramref name="path"/>, its pixels let go; null if not written.</summary>
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
                Drop(shot);   // its megabytes go before the encoder's
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

        /// <summary>A small copy's rows as a texture, uploaded once with no copy kept in memory; null for none.</summary>
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
        /// A whole photo: read on a worker by its path (never a file URL, which '+', '#', '%' or a letter outside English can
        /// break), then decoded on the main thread, about 10 to 20 ms. <paramref name="done"/> gets null if it could not be
        /// read, or if <paramref name="wanted"/>, asked after the read, says it is no longer wanted.
        /// </summary>
        public static IEnumerator Load(string path, Action<Texture2D> done, Func<bool> wanted = null)
        {
            Texture2D texture = null;
            Task<byte[]> work = Task.Run(() => PhotoStore.ReadPhoto(path));
            while (!work.IsCompleted)
            {
                yield return null;
            }
            byte[] bytes = work.Status == TaskStatus.RanToCompletion ? work.Result : null;
            if (wanted != null && !wanted())
            {
                done(null);   // not decoded: a stale load costs the main thread nothing
                yield break;
            }
            if (bytes == null)
            {
                Logger?.Warn("Gallery: " + Path.GetFileName(path) + " could not be read.");
            }
            else
            {
                using var hotSpot = HotSpots.Time(HotLoaded);
                var watch = Stopwatch.StartNew();
                texture = Decode(bytes);
                if (texture == null)
                {
                    Logger?.Warn("Gallery: " + Path.GetFileName(path) + " is no picture the engine can read.");
                }
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Gallery check (a photo of " + (bytes.Length / 1024) + " KB decoded on the main thread in "
                                 + watch.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture)
                                 + " ms, wanted under " + CameraRules.ReadMost.ToString("0") + "): " + (texture == null ? "no picture" : watch.Elapsed.TotalMilliseconds < CameraRules.ReadMost ? "PASS" : "slow"));
                }
            }
            done(texture);
        }

        // The copy in memory is let go: the texture is only drawn.
        private static Texture2D Decode(byte[] bytes)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false) { name = "RealLifeOverhaul Photo", wrapMode = TextureWrapMode.Clamp };
            try
            {
                if (texture.LoadImage(bytes, true) && texture.width > 2)
                {
                    return texture;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            UnityEngine.Object.Destroy(texture);
            return null;
        }

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
