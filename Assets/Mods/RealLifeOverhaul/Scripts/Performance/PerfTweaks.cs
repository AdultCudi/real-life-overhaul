using BigAmbitions.Mods;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Every performance tweak there is, in the order of the options page and the benchmark table, with the
    /// player's saved choice for each. Adding a tweak is one line in <see cref="All"/> and one label in en.json.
    ///
    /// Tried, measured and removed again: pedestrian animator culling and off-screen skinning (the game already
    /// does both: 0 objects affected), sun shadows ending at 60 m (no sun shadows at all saved nothing either),
    /// trees choosing their own level of detail, four bone weights per vertex (bench_130629: none beyond noise);
    /// parking the savegame picture camera (perf_134404: the render pipeline drew the main camera and nothing
    /// else in 1030 of 1030 frames, so that camera never cost anything).
    ///
    /// Third benchmark (bench_140730, paired): no saving shown, removed: motion vectors (422,177 renderers touched
    /// for nothing), a quarter of the texture streaming checks (slower if anything); trials that showed nothing:
    /// the whole post chain, the HBAO plug-in, subsurface scattering, shadow mask and light layers, cheap
    /// pedestrian avoidance (they already use it). Decals (+0.11 ms) are visible and stay as they are.
    ///
    /// The entries marked as trials are measurements for the benchmark only: they are never on the options page
    /// and never on outside a benchmark row.
    /// </summary>
    public static class PerfTweaks
    {
        public static readonly PerfTweak[] All =
        {
            new RefractionOff(),
            new SkyThrottle(),
            new StaleLampLights(),
            new FarDistricts(false),
            new LayerCull("view_radius", "reallife_perf_view_radius", 0f, "Default", "StreetProps", "InteractiveItems", "InteractiveItemsOutlined", "ParkedVehicles", "AiVehicles", "Human", "Sidewalk"),
            new LayerCull("far_props", "reallife_perf_far_props2", 200f, "StreetProps", "InteractiveItems", "InteractiveItemsOutlined", "ParkedVehicles"),
            new FarDistricts(true),
            new TexturesWhole(),
            new FarObstacleTrial(),
            // What the render pipeline's passes cost, one group at a time (several change the picture while their row runs).
            new FrameSettingTrial("trial_no_custom_pass", "custom passes off: whatever the game injects (outlines, see-through of walls)", FrameSettingsField.CustomPass),
        };

        /// <summary>Set whenever a choice (or the benchmark's override) changes; the director then brings the game in line.</summary>
        public static bool Dirty;

        /// <summary>
        /// Whether a measurement may put its timestamps into the engine's frame loop (<see cref="PerfLoop"/>). On by
        /// default; the switch is there because that is the one thing here that reaches into the engine itself.
        /// </summary>
        public static bool StepTiming = true;

        private const string StepTimingId = "perf_step_timing";

        private static string OptionId(PerfTweak tweak) => "perf_" + tweak.Id;

        /// <summary>Reads the saved choices. The game only fires option callbacks once its options screen has been opened.</summary>
        public static void Load(string modId)
        {
            foreach (PerfTweak tweak in All)
            {
                tweak.Wanted = !tweak.Trial && UnityEngine.PlayerPrefs.GetInt("m:" + modId + ":" + OptionId(tweak), tweak.DefaultOn ? 1 : 0) != 0;
            }
            StepTiming = UnityEngine.PlayerPrefs.GetInt("m:" + modId + ":" + StepTimingId, 1) != 0;
            Dirty = true;
        }

        /// <summary>The "Performance" group of the options page: one toggle per tweak, then the two measuring buttons.</summary>
        public static ModOptions AddPerformance(this ModOptions options)
        {
            options.AddHeader("reallife_perf_header");
            foreach (PerfTweak tweak in All)
            {
                if (tweak.Trial)
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
                FrameSteps.Refresh();   // the session record's own timestamps leave the engine's loop, or go back in, at once
            });
            options.AddButton("reallife_perf_probe_button", () => PerfDirector.Instance?.StartProbe());
            options.AddButton("reallife_perf_bench_button4", () => PerfDirector.Instance?.StartBench());
            return options.AddSplitter();
        }

        /// <summary>A scene was loaded or unloaded: tweaks that keep lists of scene objects look again on their next tick.</summary>
        public static void WorldChanged(bool loaded)
        {
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
