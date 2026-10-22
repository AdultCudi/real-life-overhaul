using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Switches features of the render pipeline off for the player's camera only, through the camera's own
    /// "custom frame settings" (an override mask: only the named features are affected, everything else keeps
    /// following the game's defaults), and scales its level-of-detail bias (Tweaks/ShadowReach.cs: LodReach). Several users
    /// may each switch features off or ask for a scale (the smallest is used); when the last one lets go the camera has
    /// back exactly the three values it had: the flag, the settings and the mask.
    /// </summary>
    internal static class PerfFrameSettings
    {
        private static readonly List<FrameSettingsField> Off = new List<FrameSettingsField>(16);   // one entry per user: a feature two users want off appears twice
        private static readonly List<object> LodUsers = new List<object>(2);
        private static readonly List<float> LodScales = new List<float>(2);
        private static HDAdditionalCameraData _data;
        private static bool _wasCustom;
        private static FrameSettings _wasSettings;
        private static FrameSettingsOverrideMask _wasMask;

        public static void Disable(FrameSettingsField[] fields)
        {
            Off.AddRange(fields);
            Apply();
        }

        public static void Release(FrameSettingsField[] fields)
        {
            foreach (FrameSettingsField field in fields)
            {
                Off.Remove(field);
            }
            Apply();
        }

        /// <summary>A user's scale of the game's level-of-detail bias (under 1: simpler models nearer); 1 lets go.</summary>
        public static void LodScale(object user, float scale)
        {
            int at = LodUsers.IndexOf(user);
            if (at >= 0)
            {
                LodUsers.RemoveAt(at);
                LodScales.RemoveAt(at);
            }
            if (scale < 1f)
            {
                LodUsers.Add(user);
                LodScales.Add(scale);
            }
            Apply();
        }

        private static bool Wanted => Off.Count > 0 || LodUsers.Count > 0;

        /// <summary>Brings the camera in line again; also follows the game to a new main camera.</summary>
        public static void Apply()
        {
            Camera camera = GameManager.GetMainCamera();
            HDAdditionalCameraData data = camera != null ? camera.GetComponent<HDAdditionalCameraData>() : null;
            if (_data != null && (!Wanted || data != _data))
            {
                _data.customRenderingSettings = _wasCustom;
                _data.renderingPathCustomFrameSettings = _wasSettings;
                _data.renderingPathCustomFrameSettingsOverrideMask = _wasMask;
                _data = null;
            }
            if (!Wanted || data == null)
            {
                return;
            }
            if (_data == null)
            {
                _data = data;
                _wasCustom = data.customRenderingSettings;
                _wasSettings = data.renderingPathCustomFrameSettings;
                _wasMask = data.renderingPathCustomFrameSettingsOverrideMask;
            }
            FrameSettings settings = _wasSettings;   // always from what the camera had, never from our own last state
            FrameSettingsOverrideMask mask = _wasCustom ? _wasMask : default;
            foreach (FrameSettingsField field in Off)
            {
                settings.SetEnabled(field, false);
                mask.mask[(uint)field] = true;
            }
            if (LodScales.Count > 0)
            {
                float scale = 1f;
                foreach (float asked in LodScales)
                {
                    scale = Mathf.Min(scale, asked);
                }
                settings.lodBiasMode = LODBiasMode.ScaleQualitySettings;   // a share of the bias the game's quality level gives
                settings.lodBias = scale;
                mask.mask[(uint)FrameSettingsField.LODBiasMode] = true;
                mask.mask[(uint)FrameSettingsField.LODBias] = true;
            }
            data.renderingPathCustomFrameSettings = settings;
            data.renderingPathCustomFrameSettingsOverrideMask = mask;
            data.customRenderingSettings = true;
        }
    }

    /// <summary>
    /// A measurement, not an option: one feature (or one group of features) of the render pipeline switched off
    /// for the player's camera while the benchmark compares the frame with and without it. The pipeline's own
    /// code is 3.8 ms of the 9.6 ms frame (2.5 ms of it with nothing to draw), and its timers are compiled out
    /// of this build, so this is the only way to learn which of its passes that time is spent in. Several of
    /// these change the picture; that is expected for the few seconds the row lasts. One that turns out to be
    /// both worth something and invisible can be made an option afterwards.
    /// </summary>
    public sealed class FrameSettingTrial : PerfTweak
    {
        private readonly string _id;
        private readonly string _what;
        private readonly FrameSettingsField[] _fields;

        public FrameSettingTrial(string id, string what, params FrameSettingsField[] fields)
        {
            _id = id;
            _what = what;
            _fields = fields;
        }

        public override string Id => _id;
        public override string LabelKey => null;
        public override bool DefaultOn => false;
        public override bool Trial => true;
        public override float Interval => 2f;
        public override string NotInUse => PerfRender.Unused(_fields[0]);

        protected override void Apply() => PerfFrameSettings.Disable(_fields);

        protected override void Tick(float now) => PerfFrameSettings.Apply();

        protected override void Restore() => PerfFrameSettings.Release(_fields);

        public override string Describe() => _what;
    }

    /// <summary>
    /// Whether a feature of the render pipeline does anything for the player's camera in this game, for the benchmark: a
    /// trial of a feature that is off already measures nothing and is listed as such instead (PerfBench). Off when the
    /// camera's frame settings leave it out (the quality level's pipeline asset does not support it, or the game's settings
    /// switch it off), or when the volumes the camera renders with leave it off. Report time only.
    /// </summary>
    internal static class PerfRender
    {
        public static string Unused(FrameSettingsField field)
        {
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                return null;   // nothing known: measured as it is
            }
            HDCamera hd = HDCamera.GetOrCreate(camera);
            if (!hd.frameSettings.IsEnabled(field))
            {
                return "the game's render pipeline has it off for the player's camera (its asset or its frame settings)";
            }
            VolumeStack stack = hd.volumeStack;
            if (stack == null)
            {
                return null;
            }
            switch (field)
            {
                case FrameSettingsField.ContactShadows:
                    ContactShadows contact = stack.GetComponent<ContactShadows>();
                    return contact != null && (!contact.enable.value || contact.length.value <= 0f) ? "the game's volumes leave contact shadows off" : null;
                case FrameSettingsField.SSR:
                    ScreenSpaceReflection reflection = stack.GetComponent<ScreenSpaceReflection>();
                    return reflection != null && !reflection.enabled.value ? "the game's volumes leave screen-space reflections off" : null;
                case FrameSettingsField.SSAO:
                    ScreenSpaceAmbientOcclusion occlusion = stack.GetComponent<ScreenSpaceAmbientOcclusion>();
                    return occlusion != null && occlusion.intensity.value <= 0f ? "the game's volumes leave the pipeline's ambient occlusion at nothing (the game uses its HBAO)" : null;
                case FrameSettingsField.Volumetrics:
                    Fog fog = stack.GetComponent<Fog>();
                    return fog != null && (!fog.enabled.value || !fog.enableVolumetricFog.value) ? "the game's volumes leave volumetric fog off" : null;
                case FrameSettingsField.VolumetricClouds:
                    VolumetricClouds clouds = stack.GetComponent<VolumetricClouds>();
                    return clouds != null && !clouds.enable.value ? "the game's volumes leave volumetric clouds off" : null;
                default:
                    return null;
            }
        }
    }
}
