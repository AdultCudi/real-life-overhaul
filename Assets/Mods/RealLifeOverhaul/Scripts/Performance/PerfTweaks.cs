using BigAmbitions.Mods;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Every performance tweak, in the order of the options page and the benchmark table, with the player's saved choices.
    /// Measured and left out, as saving nothing: animator culling and off-screen skinning (the game does both), shorter sun
    /// shadows, trees' own detail, four bone weights, motion vectors, fewer streaming checks, the post chain, HBAO,
    /// subsurface scattering, shadow mask, light layers. Decals (+0.11 ms) are visible and stay.
    /// </summary>
    public static class PerfTweaks
    {
        /// <summary>The picture settings' tweaks, which their sliders and choice tell when they move.</summary>
        public static readonly ShadowReach ShadowOption = new ShadowReach(false);
        public static readonly ShadowCascades CascadesOption = new ShadowCascades(false);
        public static readonly LodReach LodOption = new LodReach(false);

        private static readonly string[] CascadeChoices =
        {
            "reallife_render_cascades_game", "reallife_render_cascades_1", "reallife_render_cascades_2", "reallife_render_cascades_3",
            "reallife_render_cascades_4"
        };

        public static readonly PerfTweak[] All =
        {
            new RefractionOff(),
            new SkyThrottle(),
            new StaleLampLights(),
            new FarDistricts(false),
            new LayerCull("view_radius", "reallife_perf_view_radius", 0f,
                "Default", "StreetProps", "InteractiveItems", "InteractiveItemsOutlined", "ParkedVehicles", "AiVehicles", "Human", "Sidewalk"),
            new LayerCull("far_props", "reallife_perf_far_props2", 200f, "StreetProps", "InteractiveItems", "InteractiveItemsOutlined", "ParkedVehicles"),
            new FarDistricts(true),
            new TexturesWhole(),
            new ModCull(),
            ShadowOption,
            CascadesOption,
            LodOption,
            new FarObstacleTrial(),
            // what the pipeline's passes cost, a group at a time (several change the picture while their row runs)
            new FrameSettingTrial("trial_no_custom_pass", "custom passes off: whatever the game injects (outlines, see-through of walls)", FrameSettingsField.CustomPass),
            new ShadowReach(true),
            new ShadowCascades(true),
            new LodReach(true),
            new FrameSettingTrial("trial_no_ssr", "screen-space reflections off", FrameSettingsField.SSR),
            new FrameSettingTrial("trial_no_ssao", "the pipeline's screen-space ambient occlusion off", FrameSettingsField.SSAO),
            new FrameSettingTrial("trial_no_volumetric_fog", "volumetric fog and lighting off", FrameSettingsField.Volumetrics),
            new FrameSettingTrial("trial_no_volumetric_clouds", "volumetric clouds off", FrameSettingsField.VolumetricClouds),
            new FrameSettingTrial("trial_no_contact_shadows", "contact shadows off", FrameSettingsField.ContactShadows),
        };

        /// <summary>Set whenever a choice or the benchmark's override changes; the director then brings the game in line.</summary>
        public static bool Dirty;

        /// <summary>A measurement may put its timestamps into the engine's frame loop (<see cref="PerfLoop"/>), the one reach into the engine.</summary>
        public static bool StepTiming = true;

        private const string StepTimingId = "perf_step_timing";

        private static string OptionId(PerfTweak tweak) => "perf_" + tweak.Id;

        /// <summary>Every tweak's option id, for the one start that brings older values over (Core/OptionKeys).</summary>
        internal static void PrefIds(System.Collections.Generic.List<string> into)
        {
            foreach (PerfTweak tweak in All)
            {
                into.Add(OptionId(tweak));
            }
        }

        /// <summary>Reads the saved choices; the game fires option callbacks only once its options screen has opened.</summary>
        public static void Load()
        {
            foreach (PerfTweak tweak in All)
            {
                tweak.Wanted = tweak.BySetting ? tweak.SettingWants
                    : !tweak.Trial && UnityEngine.PlayerPrefs.GetInt(OptionKeysRules.Key(OptionId(tweak)), tweak.DefaultOn ? 1 : 0) != 0;
            }
            StepTiming = UnityEngine.PlayerPrefs.GetInt(OptionKeysRules.Key(StepTimingId), 1) != 0;
            Dirty = true;
        }

        /// <summary>The options page's Performance group: a toggle per tweak, the picture settings, the two measuring buttons.</summary>
        public static ModOptions AddPerformance(this ModOptions options)
        {
            options.AddHeader("reallife_perf_header");
            foreach (PerfTweak tweak in All)
            {
                if (tweak.Trial || tweak.BySetting)
                {
                    continue;
                }
                PerfTweak captured = tweak;
                options.AddToggle(OptionId(tweak), tweak.LabelKey, tweak.Wanted, on =>
                {
                    captured.Wanted = on;
                    Dirty = true;
                });
            }
            options.AddToggle(StepTimingId, "reallife_perf_step_timing", StepTiming, on =>
            {
                StepTiming = on;
                FrameSteps.Refresh();   // the session record's timestamps leave the engine's loop, or go back in, at once
            });
            options.AddSlider(Settings.ShadowReachId, "reallife_render_shadow_reach", RenderLevelRules.Least, RenderLevelRules.Game, Settings.ShadowReach,
                v => Moved(ShadowOption, () => Settings.ShadowReach = RenderLevelRules.Percent(v)), Meta.SettingsTable.SliderPercent);
            options.AddDropdown(Settings.ShadowCascadesId, "reallife_render_shadow_cascades", CascadeChoices, Settings.ShadowCascades,
                i => Moved(CascadesOption, () => Settings.ShadowCascades = RenderLevelRules.Cascades(i)));
            options.AddSlider(Settings.LodReachId, "reallife_render_lod_reach", RenderLevelRules.Least, RenderLevelRules.Game, Settings.LodReach,
                v => Moved(LodOption, () => Settings.LodReach = RenderLevelRules.Percent(v)), Meta.SettingsTable.SliderPercent);
            options.AddButton("reallife_perf_probe_button", () => PerfDirector.Instance?.StartProbe());
            options.AddButton("reallife_perf_bench_button4", () => PerfDirector.Instance?.StartBench());
            return options.AddSplitter();
        }

        // a picture setting moved: its tweak comes on, goes off, or on again with the new value (PerfDirector.Sync)
        private static void Moved(PerfTweak tweak, System.Action set)
        {
            set();
            tweak.Wanted = tweak.SettingWants;
            tweak.Stale = tweak.Active;
            Dirty = true;
        }

        /// <summary>Scenes loaded since the mod started: a look made behind the loading screen holds while this stands.</summary>
        public static int Arrivals { get; private set; }

        /// <summary>For ScenePrewarm, behind the loading screen: each wanted tweak looks at the scene now, so switching on searches nothing.</summary>
        public static bool Prewarm()
        {
            for (int i = 0; i < All.Length; i++)
            {
                All[i].RunPrewarm(PerfDirector.Logger);
            }
            return true;
        }

        /// <summary>A scene was loaded or unloaded: tweaks keeping lists of scene objects look again on their next tick.</summary>
        public static void WorldChanged(bool loaded)
        {
            if (loaded)
            {
                Arrivals++;
            }
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Active)
                {
                    All[i].WorldChanged(loaded);
                }
            }
        }
    }
}
