using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Skies update every two seconds instead of every frame (the game's clock changes their colours each frame, so the
    /// pipeline re-renders the cubemap), and at once on a door, the map or a new hour. No cost a frame: the director's
    /// timer; scenes arriving in play are searched only behind a black screen.
    /// </summary>
    public sealed class SkyThrottle : PerfTweak
    {
        private const float Seconds = 2f;   // seconds between sky updates

        private struct Saved
        {
            public SkySettings Sky;
            public EnvironmentUpdateMode Mode;
            public bool ModeOverridden;
            public float Period;
            public bool PeriodOverridden;
        }

        private readonly List<Saved> _saved = new List<Saved>(16);
        private bool _stale;
        private bool _hooked;

        // what the look behind the loading screen found, good for its city while no scene has arrived since
        private Volume[] _keptVolumes;
        private StaticLightingSky[] _keptSkies;
        private PerfDirector _keptFor;
        private int _keptArrivals;

        public override string Id => "sky_updates";
        public override string LabelKey => "reallife_perf_sky_updates";
        public override bool DefaultOn => true;   // third benchmark: +0.13 ms, all three pairs agreed
        public override float Interval => _stale ? HiddenEvery : 5f;

        private const float HiddenEvery = 0.25f;   // seconds between looks at whether the screen is black, while a new scene's skies wait

        protected override void Apply()
        {
            _onBuilding = _onBuilding ?? Guard.Wrap<Address>(OnBuilding);   // through the guard: a fault never reaches the game's handlers
            _onMap = _onMap ?? Guard.Wrap<bool>(OnMap);
            GlobalEvents.onEnterBuilding += _onBuilding;
            GlobalEvents.onExitBuilding += _onBuilding;
            GlobalEvents.onCityMapToggle += _onMap;
            GlobalEvents.onNewHour += Hourly;
            _hooked = true;
            if (Kept || SceneFinds.Hidden)
            {
                Sweep();
            }
            else
            {
                _stale = true;   // on in play with nothing found behind the loading screen: searched for behind the next black screen
            }
        }

        private bool Kept => _keptVolumes != null && _keptFor != null && ReferenceEquals(_keptFor, PerfDirector.Instance) && _keptArrivals == PerfTweaks.Arrivals;

        internal override void WorldChanged(bool loaded) => _stale |= loaded;   // a scene that has gone brought no sky

        protected override void Tick(float now)
        {
            if (_stale && SceneFinds.Hidden)
            {
                Sweep();   // two whole-scene searches: only where a long frame is not seen
            }
        }

        private const string SkySearch = "performance: the skies being listed (two searches of the whole scene, after scenes arrive)";

        // The two searches (95 and 87 ms) behind the loading screen; the components are kept, and the profile a volume
        // uses is read when the tweak comes on.
        protected override void Prewarm()
        {
            _keptVolumes = SceneFinds.All<Volume>(SkySearch, FindObjectsInactive.Include);
            _keptSkies = SceneFinds.All<StaticLightingSky>(SkySearch, FindObjectsInactive.Include);
            _keptFor = PerfDirector.Instance;
            _keptArrivals = PerfTweaks.Arrivals;
        }

        // every sky a volume or static-lighting component of the loaded scenes can put in front of the camera
        private void Sweep()
        {
            _stale = false;
            bool kept = Kept;
            Volume[] volumes = kept ? _keptVolumes : SceneFinds.All<Volume>(SkySearch, FindObjectsInactive.Include);
            StaticLightingSky[] skies = kept ? _keptSkies : SceneFinds.All<StaticLightingSky>(SkySearch, FindObjectsInactive.Include);
            _keptVolumes = null;   // once: a scene that arrives later is looked for on the timer
            _keptSkies = null;
            _keptFor = null;
            foreach (Volume volume in volumes)
            {
                if (volume != null)
                {
                    Take(volume.HasInstantiatedProfile() ? volume.profile : volume.sharedProfile);
                }
            }
            foreach (StaticLightingSky sky in skies)
            {
                if (sky != null)
                {
                    Take(sky.profile);
                }
            }
            Refresh();
        }

        private void Take(VolumeProfile profile)
        {
            if (profile == null)
            {
                return;
            }
            foreach (VolumeComponent component in profile.components)
            {
                if (!(component is SkySettings sky) || Has(sky))
                {
                    continue;
                }
                _saved.Add(new Saved
                {
                    Sky = sky, Mode = sky.updateMode.value, ModeOverridden = sky.updateMode.overrideState,
                    Period = sky.updatePeriod.value, PeriodOverridden = sky.updatePeriod.overrideState,
                });
                sky.updateMode.value = EnvironmentUpdateMode.Realtime;
                sky.updateMode.overrideState = true;
                sky.updatePeriod.value = Seconds;
                sky.updatePeriod.overrideState = true;
            }
        }

        private bool Has(SkySettings sky)
        {
            foreach (Saved saved in _saved)
            {
                if (ReferenceEquals(saved.Sky, sky))
                {
                    return true;
                }
            }
            return false;
        }

        private void OnBuilding(Address address) => Refresh();

        private static readonly Action Hourly = Guard.Wrap(Refresh);
        private Action<Address> _onBuilding;
        private Action<bool> _onMap;

        private void OnMap(bool open) => Refresh();

        // the sky is about to change: the pipeline takes it up on the next frame, not on the period
        private static void Refresh()
        {
            if (RenderPipelineManager.currentPipeline is HDRenderPipeline pipeline)
            {
                pipeline.RequestSkyEnvironmentUpdate();
            }
        }

        protected override void Restore()
        {
            if (_hooked)
            {
                _hooked = false;
                GlobalEvents.onEnterBuilding -= _onBuilding;
                GlobalEvents.onExitBuilding -= _onBuilding;
                GlobalEvents.onCityMapToggle -= _onMap;
                GlobalEvents.onNewHour -= Hourly;
            }
            foreach (Saved saved in _saved)
            {
                if (saved.Sky == null)
                {
                    continue;
                }
                saved.Sky.updateMode.value = saved.Mode;
                saved.Sky.updateMode.overrideState = saved.ModeOverridden;
                saved.Sky.updatePeriod.value = saved.Period;
                saved.Sky.updatePeriod.overrideState = saved.PeriodOverridden;
            }
            _saved.Clear();
            _stale = false;
            Refresh();
        }

        public override string Describe() => "skies updating every " + Seconds + " s instead of on every change: " + _saved.Count
                                             + (_saved.Count > 0 ? " (the first was set to " + _saved[0].Mode + ")" : string.Empty);
    }
}
