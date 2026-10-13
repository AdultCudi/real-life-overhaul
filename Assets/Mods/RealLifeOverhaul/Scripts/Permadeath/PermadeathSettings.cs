using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Permadeath
{
    /// <summary>
    /// The feature's entry in the mod's feature list, and the figures its Guide page prints: every {name} in its text is
    /// filled from <see cref="PermadeathRules"/> here, so the page can never say anything the rules do not do. No preset
    /// sets the switch (it has no Preset rule): it is the player's, in each save. Called once, when the feature list is
    /// built (Meta/FeatureList); each figure is worked out when the page is opened.
    /// </summary>
    public static class PermadeathGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("permadeath", "reallife_meta_title_permadeath")
                .Group("reallife_pd_header")
                .About("reallife_guide_permadeath_about").Help("reallife_guide_permadeath")
                .Owns(Settings.PermadeathId)
                // In a session another player's world is never ended, and the page says so (Core/GoingPublic).
                .StandsAside(GoingPublic.PermadeathSentence, Settings.PermadeathId)
                .Data(PermadeathState.Key, PermadeathState.RecordKey, PermadeathState.SeenKey)
                .Test("reallife_pd_test_obituary", "reallife_pd_test_rehearsal", "reallife_pd_test_die", "reallife_pd_test_flu", "reallife_pd_test_near_miss")
                .Fact("pd_crash_from", () => Kmh(PermadeathRules.CrashFrom))
                .Fact("pd_crash_worst", () => Kmh(PermadeathRules.CrashWorst))
                .Fact("pd_crash_least", () => Guide.Percent(PermadeathRules.CrashLeast))
                .Fact("pd_crash_most", () => Guide.Percent(PermadeathRules.CrashMost))
                .Fact("pd_struck_from", () => Kmh(PermadeathRules.StruckFrom))
                .Fact("pd_struck_worst", () => Kmh(PermadeathRules.StruckWorst))
                .Fact("pd_struck_least", () => Guide.Percent(PermadeathRules.StruckLeast))
                .Fact("pd_struck_most", () => Guide.Percent(PermadeathRules.StruckMost))
                .Fact("pd_mug_one_in", () => OneIn(PermadeathRules.MuggingOdds))
                .Fact("pd_assault_one_in", () => OneIn(PermadeathRules.AssaultOdds))
                .Fact("pd_ill_first", () => Days(PermadeathRules.IllWarnFirst))
                .Fact("pd_ill_second", () => Days(PermadeathRules.IllWarnSecond))
                .Fact("pd_ill_last", () => Days(PermadeathRules.IllWarnLast))
                .Fact("pd_ill_fatal", () => Days(PermadeathRules.IllFatalFrom))
                .Fact("pd_ill_day", () => Guide.Percent(PermadeathRules.IllPerDay))
                .ResetData(() => PermadeathDirector.Instance?.Forget());   // a death is never forgotten (Permadeath/PermadeathDirector.Forget)
        }

        private static string Kmh(float metresPerSecond)
        {
            return ((int)System.Math.Round(metresPerSecond * 3.6f)).ToString(Inv);
        }

        private static string OneIn(float odds)
        {
            return ((int)System.Math.Round(1f / odds)).ToString(Inv);
        }

        private static string Days(int hours)
        {
            return (hours / 24).ToString(Inv);
        }
    }

    /// <summary>Permadeath's place on the options page, under the mod's own namespace so that it is one line there.</summary>
    public static class PermadeathOptions
    {
        /// <summary>The "Permadeath" block of the options page: its one switch (off by default, kept per save) and the test buttons.</summary>
        public static ModOptions AddPermadeath(this ModOptions options)
        {
            return options
                .AddHeader("reallife_pd_header")
                .AddToggle(Settings.PermadeathId, "reallife_pd_enabled", Settings.Permadeath,
                    v => { Settings.Permadeath = v; PermadeathDirector.OptionChanged(v); })
                .AddButton("reallife_pd_test_obituary", () => PermadeathDirector.Instance?.TestObituary())
                .AddButton("reallife_pd_test_rehearsal", () => PermadeathDirector.Instance?.TestRehearsal())
                .AddButton("reallife_pd_test_die", () => PermadeathDirector.Instance?.TestDie())
                .AddButton("reallife_pd_test_flu", () => PermadeathDirector.Instance?.TestFlu())
                .AddButton("reallife_pd_test_near_miss", () => PermadeathDirector.Instance?.TestNearMiss())
                .AddSplitter();
        }
    }
}
