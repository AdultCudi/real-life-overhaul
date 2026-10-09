using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Switches features of the render pipeline off for the player's camera only, through the camera's own
    /// "custom frame settings" (an override mask: only the named features are affected, everything else keeps
    /// following the game's defaults). Several users may each switch features off; when the last one lets go the
    /// camera has back exactly the three values it had: the flag, the settings and the mask.
    /// </summary>
    internal static class PerfFrameSettings
    {
        private static readonly List<FrameSettingsField> Off = new List<FrameSettingsField>(16);   // one entry per user: a feature two users want off appears twice
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

        /// <summary>Brings the camera in line again; also follows the game to a new main camera.</summary>
        public static void Apply()
        {
            Camera camera = GameManager.GetMainCamera();
            HDAdditionalCameraData data = camera != null ? camera.GetComponent<HDAdditionalCameraData>() : null;
            if (_data != null && (Off.Count == 0 || data != _data))
            {
                _data.customRenderingSettings = _wasCustom;
                _data.renderingPathCustomFrameSettings = _wasSettings;
                _data.renderingPathCustomFrameSettingsOverrideMask = _wasMask;
                _data = null;
            }
            if (Off.Count == 0 || data == null)
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

        protected override void Apply() => PerfFrameSettings.Disable(_fields);

        protected override void Tick(float now) => PerfFrameSettings.Apply();

        protected override void Restore() => PerfFrameSettings.Release(_fields);

        public override string Describe() => _what;
    }
}
