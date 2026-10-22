using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The sun's shadows as the game sets them for the street: its global volumes' shadow settings ('Default Volume' and
    /// 'OutdoorShadowVolume' in the census), whose distance and number of cascades the shadow tweaks take down from. Each
    /// component's own values are kept as found and put back exactly; only a value the game itself sets (overrides) is
    /// changed, so a volume that leaves the pipeline's default is left alone, and the local volumes of interiors are never
    /// touched. The volumes are listed again when a scene arrives or the player goes in or out (on the tweaks' timers).
    /// Nothing here runs per frame.
    /// </summary>
    internal static class PerfShadows
    {
        private sealed class Held
        {
            public HDShadowSettings Settings;
            public float Distance;
            public int Count;
            public readonly bool[] SplitSet = new bool[3];
            public readonly float[] Split = new float[3];
        }

        private static readonly List<Held> Found = new List<Held>(4);
        private static readonly float[] Splits = new float[3];
        private static int _distanceBy = RenderLevelRules.Game, _distanceTrial = RenderLevelRules.Game;
        private static int _cascadesBy = RenderLevelRules.GameCascades, _cascadesTrial = RenderLevelRules.GameCascades;
        private static int _arrivals = -1;
        private static bool _inside;

        /// <summary>The share of the game's distance asked for (the option's, or a benchmark row's).</summary>
        public static void Distance(bool trial, int percent)
        {
            if (trial)
            {
                _distanceTrial = percent;
            }
            else
            {
                _distanceBy = percent;
            }
            Apply();
        }

        /// <summary>The cascades asked for (0: the game's own).</summary>
        public static void Cascades(bool trial, int cascades)
        {
            if (trial)
            {
                _cascadesTrial = cascades;
            }
            else
            {
                _cascadesBy = cascades;
            }
            Apply();
        }

        /// <summary>On the tweaks' timers: the volumes listed again only after a scene arrived or the player went in or out.</summary>
        public static void Keep()
        {
            if (_arrivals != PerfTweaks.Arrivals || _inside != BuildingManager.IsInsideBuilding)
            {
                Apply();
            }
        }

        /// <summary>One line for the reports.</summary>
        public static string Describe()
        {
            if (Found.Count == 0)
            {
                return "the game sets no sun shadow distance or cascades of its own in a global volume: nothing changed";
            }
            HDShadowSettings first = Found[0].Settings;
            return Found.Count + " of the game's shadow settings changed: " + (first != null ? first.maxShadowDistance.value.ToString("F0") + " m in "
                   + first.cascadeShadowSplitCount.value + " cascade(s), the game's " + Found[0].Distance.ToString("F0") + " m in " + Found[0].Count : "gone");
        }

        // The game's values back first: what is changed is always taken from them, never from a value of ours.
        private static void Apply()
        {
            Restore();
            _arrivals = PerfTweaks.Arrivals;
            _inside = BuildingManager.IsInsideBuilding;
            int percent = Mathf.Min(RenderLevelRules.Percent(_distanceBy), RenderLevelRules.Percent(_distanceTrial));
            int cascades = _cascadesTrial > 0 ? _cascadesTrial : _cascadesBy;
            if (!RenderLevelRules.Changes(percent) && cascades <= RenderLevelRules.GameCascades)
            {
                return;
            }
            int splits = RenderLevelRules.Splits(cascades, Splits);
            foreach (Volume volume in VolumeManager.instance.GetVolumes(~0))   // the manager's own list (a copy): on a change only
            {
                VolumeProfile profile = volume != null && volume.isGlobal ? volume.sharedProfile : null;
                if (profile == null || !profile.TryGet(out HDShadowSettings settings) || Holds(settings))
                {
                    continue;
                }
                bool distance = RenderLevelRules.Changes(percent) && settings.maxShadowDistance.overrideState;
                bool count = cascades > RenderLevelRules.GameCascades && settings.cascadeShadowSplitCount.overrideState;
                if (!distance && !count)
                {
                    continue;
                }
                var held = new Held { Settings = settings, Distance = settings.maxShadowDistance.value, Count = settings.cascadeShadowSplitCount.value };
                Keep(settings.cascadeShadowSplit0, held, 0);
                Keep(settings.cascadeShadowSplit1, held, 1);
                Keep(settings.cascadeShadowSplit2, held, 2);
                Found.Add(held);
                if (distance)
                {
                    settings.maxShadowDistance.value = RenderLevelRules.Scaled(held.Distance, percent);
                }
                if (count)
                {
                    settings.cascadeShadowSplitCount.value = cascades;
                    Set(settings.cascadeShadowSplit0, 0, splits);
                    Set(settings.cascadeShadowSplit1, 1, splits);
                    Set(settings.cascadeShadowSplit2, 2, splits);
                }
            }
        }

        /// <summary>Every value changed put back as it was found. Safe at any time, and with settings since destroyed.</summary>
        public static void Restore()
        {
            for (int i = 0; i < Found.Count; i++)
            {
                Held held = Found[i];
                HDShadowSettings settings = held.Settings;
                if (settings == null)
                {
                    continue;
                }
                settings.maxShadowDistance.value = held.Distance;
                settings.cascadeShadowSplitCount.value = held.Count;
                Back(settings.cascadeShadowSplit0, held, 0);
                Back(settings.cascadeShadowSplit1, held, 1);
                Back(settings.cascadeShadowSplit2, held, 2);
            }
            Found.Clear();
        }

        private static bool Holds(HDShadowSettings settings)
        {
            for (int i = 0; i < Found.Count; i++)
            {
                if (Found[i].Settings == settings)
                {
                    return true;
                }
            }
            return false;
        }

        private static void Keep(VolumeParameter<float> split, Held held, int index)
        {
            held.SplitSet[index] = split.overrideState;
            held.Split[index] = split.value;
        }

        private static void Set(VolumeParameter<float> split, int index, int splits)
        {
            if (index < splits)
            {
                split.overrideState = true;
                split.value = Splits[index];
            }
        }

        private static void Back(VolumeParameter<float> split, Held held, int index)
        {
            split.overrideState = held.SplitSet[index];
            split.value = held.Split[index];
        }
    }

    /// <summary>
    /// How far the sun's shadows reach, as a share of the game's (Core/Settings.ShadowReach, or half for the benchmark's
    /// trial row): fewer things are drawn into the shadow maps, and shadows end nearer the camera. It changes the picture:
    /// off (100%) until the player moves the slider. Its timer lists the game's volumes again after a scene arrives.
    /// </summary>
    public sealed class ShadowReach : PerfTweak
    {
        private readonly bool _trial;

        public ShadowReach(bool trial)
        {
            _trial = trial;
        }

        public override string Id => _trial ? "trial_shadow_half" : "shadow_reach";
        public override string LabelKey => null;
        public override bool DefaultOn => false;
        public override bool Trial => _trial;
        public override bool BySetting => !_trial;
        public override bool SettingWants => RenderLevelRules.Changes(Settings.ShadowReach);
        public override float Interval => 2f;

        private int Percent => _trial ? RenderLevelRules.TrialPercent : Settings.ShadowReach;

        protected override void Apply() => PerfShadows.Distance(_trial, Percent);

        protected override void Tick(float now) => PerfShadows.Keep();

        protected override void Restore() => PerfShadows.Distance(_trial, RenderLevelRules.Game);

        public override string Describe() => "sun shadows to " + RenderLevelRules.Percent(Percent) + "% of the game's distance: " + PerfShadows.Describe();
    }

    /// <summary>
    /// How many cascades the sun's shadow map is split into (Core/Settings.ShadowCascades, or one for the benchmark's trial
    /// row): fewer cascades are fewer passes over the shadow casters, and shadows a little softer near the camera. The
    /// game's own until the player picks a number.
    /// </summary>
    public sealed class ShadowCascades : PerfTweak
    {
        private readonly bool _trial;

        public ShadowCascades(bool trial)
        {
            _trial = trial;
        }

        public override string Id => _trial ? "trial_shadow_one_cascade" : "shadow_cascades";
        public override string LabelKey => null;
        public override bool DefaultOn => false;
        public override bool Trial => _trial;
        public override bool BySetting => !_trial;
        public override bool SettingWants => Settings.ShadowCascades > RenderLevelRules.GameCascades;
        public override float Interval => 2f;

        private int Count => _trial ? RenderLevelRules.TrialCascades : RenderLevelRules.Cascades(Settings.ShadowCascades);

        protected override void Apply() => PerfShadows.Cascades(_trial, Count);

        protected override void Tick(float now) => PerfShadows.Keep();

        protected override void Restore() => PerfShadows.Cascades(_trial, RenderLevelRules.GameCascades);

        public override string Describe() => "sun shadows in " + Count + " cascade(s): " + PerfShadows.Describe();
    }

    /// <summary>
    /// How far away things keep their detailed models, as a share of the game's distances (Core/Settings.LodReach, or half
    /// for the benchmark's trial row): the player's camera only, through its own frame settings (PerfFrameSettings), as a
    /// scale of the game's level-of-detail bias. Simpler models nearer the camera; the game's own until the slider moves.
    /// </summary>
    public sealed class LodReach : PerfTweak
    {
        private readonly bool _trial;

        public LodReach(bool trial)
        {
            _trial = trial;
        }

        public override string Id => _trial ? "trial_lod_half" : "lod_reach";
        public override string LabelKey => null;
        public override bool DefaultOn => false;
        public override bool Trial => _trial;
        public override bool BySetting => !_trial;
        public override bool SettingWants => RenderLevelRules.Changes(Settings.LodReach);
        public override float Interval => 2f;

        private int Percent => _trial ? RenderLevelRules.TrialPercent : Settings.LodReach;

        protected override void Apply() => PerfFrameSettings.LodScale(this, RenderLevelRules.Scaled(1f, Percent));

        protected override void Tick(float now) => PerfFrameSettings.Apply();

        protected override void Restore() => PerfFrameSettings.LodScale(this, 1f);

        public override string Describe() => "detailed models to " + RenderLevelRules.Percent(Percent) + "% of the game's distances (the player's camera)";
    }
}
