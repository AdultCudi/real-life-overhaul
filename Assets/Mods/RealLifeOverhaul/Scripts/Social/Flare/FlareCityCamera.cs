using System;
using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The camera of the city's photos: an off-screen camera at 320 by 180 with the player's camera's settings, two renders
    /// so the automatic exposure settles, the pixels read back without waiting and a worker writing the small copy. Cost:
    /// nothing per frame between photos; the costly passes are left out and everything made is let go on every path.
    /// </summary>
    internal static class FlareCityCamera
    {
        private const int Renders = 2;                // renders of one photo: the second is kept, once the exposure has settled
        private const float Lens = 60f;               // degrees of the field of view: a phone's main camera
        private const float EyeHeight = 1.7f;         // metres
        private const float AcrossStreet = 11f;       // metres from a business's door to where the photo of its front is taken
        private const float AheadOfPlayer = 2.5f;     // metres in front of the player a street photo is taken from, so they are not in it
        private const float LookFar = 40f;            // metres a way down the street is looked along for the most open one
        private const float StreetPitch = 4f;         // degrees up for a street: a strip of sky shows the weather
        private const float FocusRise = 3f;           // metres above a sight's or an event's middle the camera looks at
        private const float FocusLeast = 4f;          // metres: a sight's middle closer than this is looked past, down the street

        private static readonly FrameSettingsField[] Off =
        {
            FrameSettingsField.Volumetrics, FrameSettingsField.MotionVectors, FrameSettingsField.SSR, FrameSettingsField.ContactShadows,
            FrameSettingsField.CustomPass, FrameSettingsField.Distortion
        };

        private static GameObject _made;
        private static Camera _camera;
        private static HDAdditionalCameraData _data;
        private static byte[] _pixels;   // the read-back, RGBA: one for the session, made with the first photo
        private static byte[] _rows;     // ...as the small copy's RGB rows
        private static RenderTexture _pending;   // the render target while its pixels are on their way back: let go by Clear if the photo is stopped

        /// <summary>The result of one photo: whether it was written, and the main thread's time of its renders.</summary>
        public sealed class Result
        {
            public bool Written;
            public double RenderMs;
            public string Why;   // why, in a few words for the log, when not written
        }

        /// <summary>One photo of the bank, written as the small copy at <paramref name="thumb"/>; <paramref name="focus"/> null for a front or a street.</summary>
        public static IEnumerator Take(FlarePhotoKind kind, string place, Vector3? focus, int seed, string thumb, Result result)
        {
            Vector3 from, at;
            if (!Pose(kind, place, focus, seed, out from, out at, out result.Why))
            {
                yield break;
            }
            yield return null;   // where it stands in one frame, the renders in the next
            RenderTexture target = null;
            AsyncGPUReadbackRequest request = default;
            bool asked = false;
            try
            {
                target = RenderTexture.GetTemporary(FlareCityPhotoRules.Width, FlareCityPhotoRules.Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                _pending = target;
                var watch = Stopwatch.StartNew();
                using (SessionWatch.Piece("flare: a photo of the city for the bank"))   // two dozen a game day at most, one in twenty real seconds at most: two small renders
                {
                    asked = Render(from, at, target, out request);
                }
                watch.Stop();
                result.RenderMs = watch.Elapsed.TotalMilliseconds;
            }
            catch (Exception e)
            {
                result.Why = "the render failed (" + e.Message + ")";
                asked = false;
            }
            if (!asked)
            {
                Release();
                result.Why = result.Why ?? "the city could not be rendered";
                yield break;
            }
            while (!request.done)
            {
                yield return null;
            }
            bool read = false;
            try
            {
                if (!request.hasError)
                {
                    NativeArray<byte> data = request.GetData<byte>();
                    int bytes = FlareCityPhotoRules.Width * FlareCityPhotoRules.Height * 4;
                    if (data.Length >= bytes)
                    {
                        _pixels ??= new byte[bytes];
                        NativeArray<byte>.Copy(data, _pixels, bytes);
                        read = true;
                    }
                }
            }
            finally
            {
                Release();
            }
            if (!read)
            {
                result.Why = "the graphics card did not give the pixels back";
                yield break;
            }
            Task<bool> write = Task.Run(() => Write(thumb));
            while (!write.IsCompleted)
            {
                yield return null;
            }
            result.Written = write.Status == TaskStatus.RanToCompletion && write.Result;
            result.Why = result.Written ? null : "its file could not be written";
        }

        // Where the camera stands and what it looks at; false (with why) for a place that cannot be seen from here now.
        private static bool Pose(FlarePhotoKind kind, string place, Vector3? focus, int seed, out Vector3 from, out Vector3 at, out string why)
        {
            from = at = Vector3.zero;
            why = null;
            Vector3 player, facing;
            if (!FlarePictureGame.Outdoors(out player, out facing))
            {
                why = "the player is indoors";
                return false;
            }
            if (kind == FlarePhotoKind.Front)
            {
                Vector3 door, outward;
                if (!FlarePictureGame.Front(place, out door, out outward))
                {
                    why = "the place's front is not there to be seen";
                    return false;
                }
                if (!FlareCityPhotoRules.InRange(door.x - player.x, door.z - player.z))
                {
                    why = "the place is too far from the player";
                    return false;
                }
                Vector3 side = Vector3.Cross(Vector3.up, outward) * ((seed % 7) - 3);   // a step or three to one side: not every front square on
                from = door + outward * AcrossStreet + side + Vector3.up * EyeHeight;
                at = door + Vector3.up * 2.5f;
                return true;
            }
            Vector3 eye = player + Vector3.up * EyeHeight;
            if (focus.HasValue)
            {
                Vector3 to = focus.Value - player;
                to.y = 0f;
                if (to.sqrMagnitude >= FocusLeast * FocusLeast)
                {
                    from = eye;
                    at = new Vector3(focus.Value.x, player.y + FocusRise, focus.Value.z);   // the player's own ground: a sight's middle may sit high or low
                    return true;
                }
            }
            float start = seed % 360, best = -1f, heading = start;
            for (int k = 0; k < 4; k++)
            {
                float angle = start + k * 90f;
                Vector3 way = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                RaycastHit hit;
                float open = Physics.Raycast(eye, way, out hit, LookFar, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? hit.distance : LookFar;
                if (open > best)
                {
                    best = open;
                    heading = angle;
                }
            }
            Vector3 ahead = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
            from = eye + ahead * Mathf.Min(AheadOfPlayer, Mathf.Max(0f, best - 1f));
            at = from + Quaternion.AngleAxis(-StreetPitch, Vector3.Cross(Vector3.up, ahead)) * ahead * 10f;
            return true;
        }

        // The two renders with the player's camera's settings and a settling exposure, and the read-back asked for.
        private static bool Render(Vector3 from, Vector3 at, RenderTexture target, out AsyncGPUReadbackRequest request)
        {
            request = default;
            Camera main = GameManager.GetMainCamera();
            if (main == null)
            {
                return false;
            }
            if (_made == null)
            {
                _made = new GameObject("RealLifeOverhaul Flare city photo");
                _made.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(_made);
                _made.hideFlags = HideFlags.HideAndDontSave;
                _camera = _made.AddComponent<Camera>();
                _data = _made.AddComponent<HDAdditionalCameraData>();
            }
            _camera.CopyFrom(main);
            HDAdditionalCameraData mainData = main.GetComponent<HDAdditionalCameraData>();
            if (mainData != null)
            {
                mainData.CopyTo(_data);
            }
            FrameSettings settings = _data.renderingPathCustomFrameSettings;
            FrameSettingsOverrideMask mask = _data.customRenderingSettings ? _data.renderingPathCustomFrameSettingsOverrideMask : default;
            for (int i = 0; i < Off.Length; i++)
            {
                settings.SetEnabled(Off[i], false);
                mask.mask[(uint)Off[i]] = true;
            }
            _data.renderingPathCustomFrameSettings = settings;
            _data.renderingPathCustomFrameSettingsOverrideMask = mask;
            _data.customRenderingSettings = true;
            _data.antialiasing = HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing;
            _camera.enabled = false;
            _camera.fieldOfView = Lens;
            _camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
            _camera.targetTexture = target;
            _camera.aspect = FlareCityPhotoRules.Width / (float)FlareCityPhotoRules.Height;

            GameObject exposureHost = null;
            VolumeProfile profile = null;
            try
            {
                // The game's automatic exposure, made to settle at once for these renders only.
                exposureHost = new GameObject("RealLifeOverhaul Flare city photo exposure");
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                Exposure exposure = profile.Add<Exposure>(true);
                exposure.mode.Override(ExposureMode.Automatic);
                exposure.adaptationMode.Override(AdaptationMode.Fixed);
                var volume = exposureHost.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 2000f;
                volume.sharedProfile = profile;
                _made.SetActive(true);
                for (int i = 0; i < Renders; i++)
                {
                    _camera.Render();
                }
                request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32);
                return true;
            }
            finally
            {
                _camera.targetTexture = null;
                _made.SetActive(false);
                if (exposureHost != null)
                {
                    exposureHost.SetActive(false);   // out of the game's volumes now, before its own camera draws the frame
                    UnityEngine.Object.Destroy(exposureHost);
                }
                CodeAssets.DropProfile(profile);   // its exposure setting with it, else one leaks a photo
            }
        }

        // On the worker: the read-back (RGBA) as the small copy's RGB rows, both bottom row first.
        private static bool Write(string thumb)
        {
            int w = FlareCityPhotoRules.Width, h = FlareCityPhotoRules.Height;
            _rows ??= new byte[w * h * 3];
            for (int i = 0, o = 0; i < w * h * 4; i += 4, o += 3)
            {
                _rows[o] = _pixels[i];
                _rows[o + 1] = _pixels[i + 1];
                _rows[o + 2] = _pixels[i + 2];
            }
            PhotoThumbs.Write(thumb, _rows, w, h);
            return System.IO.File.Exists(thumb);
        }

        // The render target back to the pool; a photo stopped between its renders never reaches this, so Clear lets it go then.
        private static void Release()
        {
            if (_pending != null)
            {
                RenderTexture.ReleaseTemporary(_pending);
            }
            _pending = null;
        }

        /// <summary>The camera's object destroyed, and a render target a stopped photo held let go: the city is unloading, or Flare stops.</summary>
        public static void Clear()
        {
            Release();
            if (_made != null)
            {
                UnityEngine.Object.Destroy(_made);
            }
            _made = null;
            _camera = null;
            _data = null;
        }
    }
}
