using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Pipeline features off and a level-of-detail scale for the player's camera only, through its frame settings' override
    /// mask; the smallest scale wins, and the last user letting go puts the flag, settings and mask back.
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
            FrameSettings settings = _wasSettings;   // always from what the camera had, never from the last state of ours
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
                settings.lodBiasMode = LODBiasMode.ScaleQualitySettings;   // a share of the bias the quality level gives
                settings.lodBias = scale;
                mask.mask[(uint)FrameSettingsField.LODBiasMode] = true;
                mask.mask[(uint)FrameSettingsField.LODBias] = true;
            }
            data.renderingPathCustomFrameSettings = settings;
            data.renderingPathCustomFrameSettingsOverrideMask = mask;
            data.customRenderingSettings = true;
        }
    }

    /// <summary>A benchmark row: one pipeline feature off, the only way to see its cost, as its timers are compiled out.</summary>
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

    /// <summary>Why a pipeline feature does nothing for the player's camera (frame settings or volumes), or null. Report time.</summary>
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
                    return occlusion != null && occlusion.intensity.value <= 0f
                        ? "the game's volumes leave the pipeline's ambient occlusion at nothing (the game uses its HBAO)" : null;
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
