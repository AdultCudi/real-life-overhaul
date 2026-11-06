using BAModAPI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A high-priority global HDRP volume grading the image on top of the game's day and night volumes. Contrast and
    /// saturation are baked into HDRP's colour-grading table, so they cost nothing a frame; bloom and detail distance do,
    /// and are off unless the player raises them. A frame compares five settings; the engine is told only on a change.
    /// </summary>
    public class VisualEnhancer : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("visuals", "VisualEnhancer", typeof(VisualEnhancer));

        public static IModLogger Logger;

        private Volume _volume;
        private VolumeProfile _profile;
        private ColorAdjustments _color;
        private Bloom _bloom;
        private float _vanillaLodBias;   // the game's level of detail before the mod first set it
        private bool _lodBiasHeld;       // the mod has set it, and _vanillaLodBias is what it was: only then is it put back

        // The values last pushed to the engine.
        private bool _appliedOn;
        private int _appliedSaturation = int.MinValue;
        private int _appliedContrast = int.MinValue;
        private int _appliedBloom = int.MinValue;
        private int _appliedDetail = int.MinValue;

        private void Start()
        {
            Logger?.Info($"Visuals: vanilla lodBias={QualitySettings.lodBias} aniso={QualitySettings.anisotropicFiltering}");

            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _color = _profile.Add<ColorAdjustments>();
            _bloom = _profile.Add<Bloom>();

            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 1000f;
            _volume.sharedProfile = _profile;
            _volume.weight = 0f;
        }

        private static readonly int HotUpdate = HotSpots.Slot("World: VisualEnhancer.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults
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

            if (!_lodBiasHeld)
            {
                _vanillaLodBias = QualitySettings.lodBias;   // recorded where it is first set, so what is put back is what it was
                _lodBiasHeld = true;
            }
            QualitySettings.lodBias = on ? _vanillaLodBias * Settings.Detail / 100f : _vanillaLodBias;
        }

        private void OnDestroy()
        {
            if (_lodBiasHeld)
            {
                QualitySettings.lodBias = _vanillaLodBias;   // never set (gone before its first frame): the game's own stays
            }
            CodeAssets.DropProfile(_profile);   // its colour and bloom settings with it
        }
    }
}
