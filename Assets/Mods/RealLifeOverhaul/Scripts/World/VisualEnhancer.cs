using BAModAPI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Adds a high-priority global HDRP volume that grades the image on top of the game's own
    /// day/night volumes. Contrast and saturation are baked into HDRP's existing colour-grading
    /// lookup table, so they cost nothing per frame. Bloom and detail distance do cost
    /// performance and are therefore off unless the player raises them.
    /// Settings are only pushed to the engine when one of them changes.
    /// </summary>
    public class VisualEnhancer : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("visuals", "VisualEnhancer", typeof(VisualEnhancer));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        public static IModLogger Logger;

        private Volume _volume;
        private VolumeProfile _profile;
        private ColorAdjustments _color;
        private Bloom _bloom;
        private float _vanillaLodBias;

        // Last values pushed to the engine.
        private bool _appliedOn;
        private int _appliedSaturation = int.MinValue;
        private int _appliedContrast = int.MinValue;
        private int _appliedBloom = int.MinValue;
        private int _appliedDetail = int.MinValue;

        private void Start()
        {
            _vanillaLodBias = QualitySettings.lodBias;
            Logger?.Info($"Visuals: vanilla lodBias={_vanillaLodBias} aniso={QualitySettings.anisotropicFiltering}");

            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _color = _profile.Add<ColorAdjustments>();
            _bloom = _profile.Add<Bloom>();

            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 1000f;
            _volume.sharedProfile = _profile;
            _volume.weight = 0f;
        }

        private void Update()
        {
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (_volume == null)
            {
                return;
            }

            bool on = Settings.VisualsEnabled;
            if (on == _appliedOn && Settings.Saturation == _appliedSaturation && Settings.Contrast == _appliedContrast
                && Settings.Bloom == _appliedBloom && Settings.Detail == _appliedDetail)
            {
                return;
            }
            _appliedOn = on;
            _appliedSaturation = Settings.Saturation;
            _appliedContrast = Settings.Contrast;
            _appliedBloom = Settings.Bloom;
            _appliedDetail = Settings.Detail;

            _volume.weight = on ? 1f : 0f;
            _color.saturation.overrideState = on;
            _color.saturation.value = Settings.Saturation;
            _color.contrast.overrideState = on;
            _color.contrast.value = Settings.Contrast;

            // Bloom at 0 leaves the game's own bloom setting untouched.
            bool bloom = on && Settings.Bloom > 0;
            _bloom.intensity.overrideState = bloom;
            _bloom.intensity.value = Settings.Bloom / 100f;

            QualitySettings.lodBias = on ? _vanillaLodBias * Settings.Detail / 100f : _vanillaLodBias;
        }

        private void OnDestroy()
        {
            QualitySettings.lodBias = _vanillaLodBias;
            if (_profile != null)
            {
                Destroy(_profile);
            }
        }
    }
}
