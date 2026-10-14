using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's TimeOfDayController writes the sky's three colours from the clock every frame (its Update
    /// calls SetEnvironmentSettings, which sets GradientSky.top / middle / bottom). The render pipeline keeps a
    /// small picture of the sky all round (a cubemap) for reflections and for the light the sky casts, and with
    /// the sky set to update "on changed" it renders that cubemap again, blurs it for every roughness and
    /// re-measures the sky's light whenever the colours differ from last frame: with a clock that never stands
    /// still, every frame. This sets the skies to update on a period instead (every two seconds), and asks for an
    /// immediate update at the moments the sky really jumps: entering and leaving a building, the city map, a new
    /// hour. The sky you see is drawn fresh every frame as before; only its reflection and its light on things
    /// follow up to two seconds behind a change far too slow to see.
    ///
    /// The skies are searched for behind the loading screen (Prewarm). A scene that arrives later is searched for
    /// behind the next black screen, pause or loading screen (two searches of the whole scene, some 75 ms each: never in
    /// play; until then a sky that scene brought keeps the game's own way of updating).
    ///
    /// Per frame: nothing (the timer is the director's: every five seconds, and four times a second while a scene that
    /// arrived waits for the black screen, one test each).
    /// </summary>
    public sealed class SkyThrottle : PerfTweak
    {
        private const float Seconds = 2f;

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

        // What the look behind the loading screen found (Prewarm), for the Sweep when the tweak comes on: good for the city
        // it was found in (its PerfDirector) while no scene has arrived since.
        private Volume[] _keptVolumes;
        private StaticLightingSky[] _keptSkies;
        private PerfDirector _keptFor;
        private int _keptArrivals;

        public override string Id => "sky_updates";
        public override string LabelKey => "reallife_perf_sky_updates";
        public override bool DefaultOn => true;   // third benchmark: +0.13 ms, all three pairs agreed, all of it in the pipeline's share
        public override float Interval => _stale ? HiddenEvery : 5f;

        private const float HiddenEvery = 0.25f;   // seconds between two looks at whether the screen is black, while a new scene's skies wait for it

        protected override void Apply()
        {
            _onBuilding = _onBuilding ?? Guard.Wrap<Address>(OnBuilding);   // (a fault here never reaches the game's own handlers: Core/Guard)
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
                _stale = true;   // switched on in play with nothing found behind the loading screen: searched for behind the next black screen
            }
        }

        // What the look behind the loading screen found is this city's, and no scene has arrived since.
        private bool Kept => _keptVolumes != null && _keptFor != null && ReferenceEquals(_keptFor, PerfDirector.Instance) && _keptArrivals == PerfTweaks.Arrivals;

        internal override void WorldChanged(bool loaded) => _stale |= loaded;   // a scene that has gone brought no sky: nothing to look for

        protected override void Tick(float now)
        {
            if (_stale && SceneFinds.Hidden)
            {
                Sweep();   // two searches of the whole scene: only where a long frame is not seen
            }
        }

        private const string SkySearch = "performance: the skies being listed (two searches of the whole scene, after scenes arrive)";

        // The two searches, made behind the loading screen when the tweak is wanted there (they were two frames' worth,
        // 95 and 87 ms, in the frame the tweak came on). The components are kept, not their profiles: which profile a
        // volume uses is read when the tweak comes on, as before.
        protected override void Prewarm()
        {
            _keptVolumes = SceneFinds.All<Volume>(SkySearch, FindObjectsInactive.Include);
            _keptSkies = SceneFinds.All<StaticLightingSky>(SkySearch, FindObjectsInactive.Include);
            _keptFor = PerfDirector.Instance;
            _keptArrivals = PerfTweaks.Arrivals;
        }

        // Every sky a volume or a static-lighting component in the loaded scenes can put in front of the camera.
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

        // The sky is about to be a different one: have the pipeline take it up on the next frame, not on the period.
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

        public override string Describe() => "skies updating every " + Seconds + " s instead of on every change: " + _saved.Count + (_saved.Count > 0 ? " (the first was set to " + _saved[0].Mode + ")" : string.Empty);
    }
}
