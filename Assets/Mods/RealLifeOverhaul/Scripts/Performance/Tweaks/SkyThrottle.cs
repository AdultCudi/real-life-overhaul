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

        public override string Id => "sky_updates";
        public override string LabelKey => "reallife_perf_sky_updates";
        public override bool DefaultOn => true;   // third benchmark: +0.13 ms, all three pairs agreed, all of it in the pipeline's share
        public override float Interval => 5f;

        protected override void Apply()
        {
            GlobalEvents.onEnterBuilding += OnBuilding;
            GlobalEvents.onExitBuilding += OnBuilding;
            GlobalEvents.onCityMapToggle += OnMap;
            GlobalEvents.onNewHour += Refresh;
            _hooked = true;
            Sweep();
        }

        internal override void WorldChanged(bool loaded) => _stale |= loaded;   // a scene that has gone brought no sky: nothing to look for

        protected override void Tick(float now)
        {
            if (_stale)
            {
                Sweep();
            }
        }

        private const string SkySearch = "performance: the skies being listed (two searches of the whole scene, after scenes arrive)";

        // Every sky a volume or a static-lighting component in the loaded scenes can put in front of the camera.
        private void Sweep()
        {
            _stale = false;
            foreach (Volume volume in SceneFinds.All<Volume>(SkySearch, FindObjectsInactive.Include))
            {
                Take(volume.HasInstantiatedProfile() ? volume.profile : volume.sharedProfile);
            }
            foreach (StaticLightingSky sky in SceneFinds.All<StaticLightingSky>(SkySearch, FindObjectsInactive.Include))
            {
                Take(sky.profile);
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
                GlobalEvents.onEnterBuilding -= OnBuilding;
                GlobalEvents.onExitBuilding -= OnBuilding;
                GlobalEvents.onCityMapToggle -= OnMap;
                GlobalEvents.onNewHour -= Refresh;
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
