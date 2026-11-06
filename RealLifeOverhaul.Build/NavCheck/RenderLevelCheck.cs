using System;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// Checks for the performance section's picture settings (Performance/RenderLevelRules.cs) and their options as they
// ship: each is the game's own until moved (100% and "the game's own" cascades), a slider only goes down from the game's
// value to a quarter of it, the cascades are one to four, the splits set for three and four cascades rise and stay inside
// the distance, and the benchmark's trial rows try half the distance, one cascade and half the detail.
internal static partial class Program
{
    private static void RenderLevels()
    {
        Check(RenderLevelRules.Least == 25 && RenderLevelRules.Game == 100, "picture settings: the distance sliders run from 25% to the game's own 100%");
        Check(RenderLevelRules.Percent(0) == 25 && RenderLevelRules.Percent(-40) == 25 && RenderLevelRules.Percent(250) == 100 && RenderLevelRules.Percent(60) == 60,
            "picture settings: a distance setting from a saved value is kept within its limits");
        Check(!RenderLevelRules.Changes(100) && !RenderLevelRules.Changes(400) && RenderLevelRules.Changes(99) && RenderLevelRules.Changes(25),
            "picture settings: only a distance under the game's own changes anything");
        Check(RenderLevelRules.Cascades(-1) == 0 && RenderLevelRules.Cascades(0) == 0 && RenderLevelRules.Cascades(3) == 3 && RenderLevelRules.Cascades(9) == 4,
            "picture settings: the cascades are the game's own (0) or one to four");
        Check(Near(RenderLevelRules.Scaled(100f, 50), 50f) && Near(RenderLevelRules.Scaled(100f, 100), 100f) && Near(RenderLevelRules.Scaled(1f, 25), 0.25f)
              && Near(RenderLevelRules.Scaled(100f, 10), 25f) && RenderLevelRules.Scaled(0f, 50) == 0f && RenderLevelRules.Scaled(-1f, 50) == -1f,
            "picture settings: a share of the game's value, never below a quarter, nothing made of no value");
        var splits = new float[3];
        Check(RenderLevelRules.Splits(1, splits) == 0 && RenderLevelRules.Splits(2, splits) == 0 && RenderLevelRules.Splits(0, splits) == 0,
            "picture settings: one cascade has no split to set, two keep the game's own");
        for (int cascades = 3; cascades <= RenderLevelRules.MostCascades; cascades++)
        {
            int n = RenderLevelRules.Splits(cascades, splits);
            bool rising = n == cascades - 1;
            for (int i = 0; i < n; i++)
            {
                rising &= splits[i] > (i == 0 ? 0f : splits[i - 1]) && splits[i] < 1f;
            }
            Check(rising, "picture settings: " + cascades + " cascades get " + (cascades - 1) + " splits that rise inside the distance");
        }
        Check(RenderLevelRules.Splits(4, new float[2]) == 0 && RenderLevelRules.Splits(4, null) == 0, "picture settings: splits are never written past their place");
        Check(RenderLevelRules.TrialPercent == 50 && RenderLevelRules.TrialCascades == 1, "picture settings: the trial rows try half and one cascade");

        // The options as they ship: the sliders' ends are the rules', the defaults the game's own, the choice has five entries.
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string tweaks = Source(scripts, @"Performance\PerfTweaks.cs");
        string settings = Source(scripts, @"Core\Settings.cs");
        foreach (string id in new[] { "ShadowReachId", "LodReachId" })
        {
            Check(Regex.IsMatch(tweaks, @"AddSlider\(Settings\." + id + @",\s*""[^""]+"",\s*RenderLevelRules\.Least,\s*RenderLevelRules\.Game,"),
                "picture settings: the " + id + " slider runs from RenderLevelRules.Least to the game's own");
        }
        Check(Regex.IsMatch(tweaks, @"AddDropdown\(Settings\.ShadowCascadesId,\s*""[^""]+"",\s*CascadeChoices,"), "picture settings: the cascades are a choice");
        Check(Regex.Matches(Body(tweaks, "private static readonly string[] CascadeChoices"), @"""reallife_render_cascades_\w+""").Count == RenderLevelRules.MostCascades + 1,
            "picture settings: the cascades' choice has the game's own and one to four");
        Check(settings.Contains("public static int ShadowReach = RenderLevelRules.Game;") && settings.Contains("public static int LodReach = RenderLevelRules.Game;")
              && settings.Contains("public static int ShadowCascades = RenderLevelRules.GameCascades;"), "picture settings: every one is the game's own until the player moves it");
        string levels = Source(scripts, @"Performance\Tweaks\ShadowReach.cs");
        Check(levels.Contains("public override bool DefaultOn => false;") && !levels.Contains("DefaultOn => true"), "picture settings: none is on by default");
        Check(levels.Contains("settings.maxShadowDistance.overrideState;") && levels.Contains("settings.cascadeShadowSplitCount.overrideState;") && levels.Contains("volume.isGlobal"),
            "picture settings: only the values the game's own global volumes set are changed, never an interior's");
    }
}
