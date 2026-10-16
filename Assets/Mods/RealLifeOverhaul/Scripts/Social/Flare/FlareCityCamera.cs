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
    /// The camera of the city's photos (FlareCityPhotoRules): a camera of the mod's, off screen, set where a phone would be
    /// held for the post (FlareDirector.Photos.cs chooses the post and says what it shows), rendering the city as the
    /// player's own camera does (its settings copied: the same lighting, the same sky, the same hour), at 320 by 180.
    ///
    /// ONE PHOTO, OVER A FEW FRAMES. The first frame works out where the camera stands: a business's front from across the
    /// street (its door and the way out of it), or the street where the player is, looking down the most open of four ways
    /// from the post's seed (one ray each, once) and up at the sky for the weather. The next frame renders it twice (the
    /// game's automatic exposure settles in the second, through a volume that exists only for those two renders, as the
    /// showroom's car photos do) and asks the graphics card for the pixels without waiting for them. A frame or two later
    /// they are copied once into a buffer kept for the session, and a worker thread writes the photo's small copy.
    ///
    /// LIGHTER THAN THE PLAYER'S CAMERA: the photo's own camera has no volumetric fog, motion vectors, screen-space
    /// reflections, contact shadows, the game's custom passes (outlines, see-through walls) or distortion, and the fast
    /// antialiasing (no history to need). The main thread's time for the two renders is measured and returned: the director
    /// waits longer after a slow one (FlareCityPhotoRules.NextPace).
    ///
    /// Everything it makes is let go on every path: the render target when the pixels are back (or the request fails), the
    /// volume and its profile in the same frame as the renders, the camera object when the city unloads (<see cref="Clear"/>).
    /// COST: nothing per frame and nothing between photos; a photo as above (FlareDirector.Photos.cs paces them).
    /// </summary>
    internal static class FlareCityCamera
    {
        private const int Renders = 2;                // renders of one photo: the second is kept, once the exposure has settled
        private const float Lens = 60f;               // degrees of the field of view: a phone's main camera
        private const float EyeHeight = 1.7f;         // metres
        private const float AcrossStreet = 11f;       // metres from a business's door to where the photo of its front is taken
        private const float AheadOfPlayer = 2.5f;     // metres in front of the player a street photo is taken from (they are not in it)
        private const float LookFar = 40f;            // metres a way down the street is looked along for the most open one
        private const float SkyPitch = 14f;           // degrees up for the weather's sky
        private const float StreetPitch = 2f;         // degrees down for a street

        private static readonly FrameSettingsField[] Off =
        {
            FrameSettingsField.Volumetrics, FrameSettingsField.MotionVectors, FrameSettingsField.SSR, FrameSettingsField.ContactShadows,
            FrameSettingsField.CustomPass, FrameSettingsField.Distortion
        };

        private static GameObject _made;
        private static Camera _camera;
        private static HDAdditionalCameraData _data;
        private static byte[] _pixels;   // the read-back, RGBA (one for the session, made with the first photo)
        private static byte[] _rows;     // ...as the small copy's RGB rows

        /// <summary>The result of one photo: whether it was written, and the main thread's time of its renders.</summary>
        public sealed class Result
        {
            public bool Written;
            public double RenderMs;
            public string Why;   // when not written: why, in a few words, for the log
        }

        /// <summary>
        /// One photo of a post, written as the small copy at 'thumb'. Started by the director through Guard.Routine; the
        /// result is filled in before it ends. 'seed' turns the camera a little, the same way each time for the same post.
        /// </summary>
        public static IEnumerator Take(FlarePhotoSubject subject, string place, int seed, string thumb, Result result)
        {
            Vector3 from, at;
            if (!Pose(subject, place, seed, out from, out at, out result.Why))
            {
                yield break;
            }
            yield return null;   // (where it stands in one frame, the renders in the next)
            RenderTexture target = null;
            AsyncGPUReadbackRequest request = default;
            bool asked = false;
            try
            {
                target = RenderTexture.GetTemporary(FlareCityPhotoRules.Width, FlareCityPhotoRules.Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var watch = Stopwatch.StartNew();
                using (SessionWatch.Piece("flare: a photo of the city for a post"))   // a few a game day at most: two small renders
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
                if (target != null)
                {
                    RenderTexture.ReleaseTemporary(target);
                }
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
                RenderTexture.ReleaseTemporary(target);
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
        private static bool Pose(FlarePhotoSubject subject, string place, int seed, out Vector3 from, out Vector3 at, out string why)
        {
            from = at = Vector3.zero;
            why = null;
            Vector3 player, facing;
            if (!FlarePictureGame.Outdoors(out player, out facing))
            {
                why = "the player is indoors";
                return false;
            }
            if (subject == FlarePhotoSubject.Front)
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
            float pitch = subject == FlarePhotoSubject.Sky ? SkyPitch : -StreetPitch;
            at = from + Quaternion.AngleAxis(-pitch, Vector3.Cross(Vector3.up, ahead)) * ahead * 10f;
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
                if (profile != null)
                {
                    UnityEngine.Object.Destroy(profile);
                }
            }
        }

        // On the worker: the read-back (RGBA, bottom row first) as the small copy's RGB rows (bottom row first), written whole.
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

        /// <summary>The camera's object destroyed: the city is unloading, or Flare stops.</summary>
        public static void Clear()
        {
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
