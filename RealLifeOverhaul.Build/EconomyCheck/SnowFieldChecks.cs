using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Seasons;

// The falling snow as the cameras see it (SeasonLookRules.Field and the sources that use it): flakes a few pixels across
// where every camera of the game's and the mod's looks (the game's own street camera 15 to 35 m off, the mod's on foot
// 6 m, its car camera 8 m), closer together near and on a heavy day, never more than the most, the same every time and
// free of allocation; and in the sources: the flakes are the game's own soft round particle in a box that follows the
// camera, the ground is marked behind every load whatever the look option says, and the game's rain is hidden under the
// snow on this machine's picture alone, its weather never written, with no shower or storm of the seasons' on a snow day.
internal static partial class Program
{
    private static void SnowFields()
    {
        SnowFieldSizes();
        SnowFieldCost();
        SnowFieldSources();
    }

    private static void SnowFieldSizes()
    {
        // The cameras: the game's street camera at its nearest and farthest (pitch about 47 degrees), the mod's on-foot
        // camera (6 m, 18 degrees up) and its car camera (8 m, 12 degrees), and the game's indoor camera's 13 m for a door.
        float[] distances = { 15f, 35f, 6f, 8f, 13f };
        float[] heights = { 11f, 25.6f, 1.9f, 1.7f, 11f };
        bool seen = true, fits = true, within = true, kept = true;
        string worst = string.Empty;
        for (int i = 0; i < distances.Length; i++)
        {
            SnowField heavy = SeasonLookRules.Field(distances[i], heights[i], 1f);
            float pixels = heavy.PixelsAt(distances[i]);
            if (pixels < 3f || pixels > 12f)
            {
                seen = false;
                worst += " " + distances[i] + " m: " + pixels.ToString("0.0") + " px;";
            }
            fits &= heavy.Span >= distances[i] && heavy.Height >= heights[i] && heavy.Life > 0f;
            within &= heavy.Count > 0 && heavy.Count <= SeasonLookRules.FlakesMost;
            kept &= Math.Abs(heavy.Rate * heavy.Life - heavy.Count) <= Math.Max(2f, heavy.Count * 0.05f);
        }
        Check(seen, "snow field: a flake is 3 to 12 pixels across where each camera looks, from the mod's 6 m camera to the game's 35 m one" + (seen ? string.Empty : " (not:" + worst + ")"));
        Check(fits, "snow field: the box reaches past what each camera looks at and over the camera's own height");
        Check(within && kept, "snow field: never more flakes than " + SeasonLookRules.FlakesMost + ", and as many are started a second as keep the count in the air");

        SnowField near = SeasonLookRules.Field(6f, 1.9f, 1f), far = SeasonLookRules.Field(35f, 25.6f, 1f);
        Check(near.Spacing < far.Spacing && near.Size < far.Size, "snow field: a near camera gets smaller flakes closer together, a far one larger flakes wider apart ("
                                                                    + near.Spacing.ToString("0.0") + " m and " + far.Spacing.ToString("0.0") + " m)");
        SnowField light = SeasonLookRules.Field(35f, 25.6f, 0.5f), none = SeasonLookRules.Field(35f, 25.6f, 0f);
        Check(light.Count < far.Count && light.Spacing > far.Spacing && light.Size == far.Size, "snow field: a light day has fewer flakes, as big, wider apart (" + light.Count + " of " + far.Count + ")");
        Check(none.Count == 0 && none.Rate == 0f, "snow field: no fall, no flakes");
        bool same = true;
        for (int i = 0; i < 40; i++)
        {
            SnowField a = SeasonLookRules.Field(i * 1.7f, i * 0.9f, (i % 11) / 10f), b = SeasonLookRules.Field(i * 1.7f, i * 0.9f, (i % 11) / 10f);
            same &= a.Size == b.Size && a.Count == b.Count && a.Span == b.Span && a.Rate == b.Rate;
        }
        SnowField odd = SeasonLookRules.Field(-5f, -3f, 7f);
        Check(same && odd.Count > 0 && odd.Count <= SeasonLookRules.FlakesMost && odd.Size > 0f, "snow field: the same every time asked, and sound for a camera under the player or a strength past 1");
    }

    private static void SnowFieldCost()
    {
        float sink = 0f;
        for (int i = 0; i < 2000; i++)
        {
            sink += SeasonLookRules.Field(i % 60, i % 30, (i % 21) / 20f).Rate;
        }
        var watch = Stopwatch.StartNew();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
        {
            sink += SeasonLookRules.Field(i % 60, i % 30, (i % 21) / 20f).Size;
        }
        long made = GC.GetAllocatedBytesForCurrentThread() - before;
        watch.Stop();
        double each = watch.Elapsed.TotalMilliseconds / 10000.0;
        Check(made == 0 && each < 0.01, "snow field: fitting the flakes to the camera (four times a second while it snows) allocates nothing and takes "
                                        + each.ToString("0.0000") + " ms (under 0.01; " + made + " bytes; " + (sink >= 0f ? "ok" : "?") + ")");
    }

    private static void SnowFieldSources()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string look = SeasonRead(folder, "Seasons", "SnowLook.cs");
        string director = SeasonRead(folder, "Seasons", "SeasonDirector.cs");
        string looks = SeasonRead(folder, "Seasons", "SeasonDirector.Look.cs");
        string snow = SeasonRead(folder, "Seasons", "SeasonGame.Snow.cs");
        string hush = SeasonRead(folder, "Seasons", "SnowHush.cs");
        string tests = SeasonRead(folder, "Seasons", "SeasonDirector.Tests.cs");
        string sky = SeasonRead(folder, "Seasons", "SeasonGame.Sky.cs");
        Check(look.Contains("SeasonLookRules.Field(") && look.Contains("GameManager.GetMainCamera()") && look.Contains("ParticleSystemRenderMode.Billboard")
              && look.Contains("SeasonGame.FlakePaint") && !look.Contains("RainPlace()") && !Regex.IsMatch(look + snow, @"new\s+Material\s*\(\s*(soft|_flakePaint|paint)\b"),
            "snow field: the flakes are the game's own soft round particle, unchanged, in a box fitted to and following the camera, not hung at the player's feet");
        Check(snow.Contains("\"SmoothCircle\"") && snow.Contains("SceneFinds.InMemory<Material>(") && snow.Contains("if (!SceneFinds.Hidden)")
              && director.Contains("ScenePrewarm.Add(\"seasons: the game's soft round particle being found for the snowflakes\", SeasonGame.FindFlakePaint)"),
            "snow field: the game's soft round particle is looked for only behind a loading screen");
        Check(Regex.IsMatch(director, @"_next = now \+ \(_look\.Following \? FollowEvery : SlowEvery\)") && director.Contains("FollowEvery = 0.25f") && director.Contains("_look.Follow(SeasonGame.Player)"),
            "snow field: the flakes follow the camera four times a second while they fall, and once a second otherwise, from one timer");
        int mark = director.IndexOf("private bool MarkGround()", StringComparison.Ordinal);
        int markEnd = mark >= 0 ? director.IndexOf("return true;", mark, StringComparison.Ordinal) : -1;
        string marking = mark >= 0 && markEnd > mark ? director.Substring(mark, markEnd - mark) : string.Empty;
        Check(marking.Contains("Settings.Seasons && SnowGround.Drawable") && !marking.Contains("SnowLook") && !director.Contains("FollowMarks(Settings.SnowLook)"),
            "snow field: the ground is marked behind every load while the seasons are on, whatever the snow's look option says, so switching it on needs no door");
        Check(snow.Contains(".enabled = false;") && snow.Contains(".mute = true;") && snow.Contains("RainPartsOn[i]") && snow.Contains("RainSoundsMuted[i]")
              && !Regex.IsMatch(snow + hush + director + looks, @"NextRainMaxIntensity|isRaining\s*=|transitionProgress\s*=|SetValue\(null")
              && hush.Contains("Shader.SetGlobalFloat(PuddlesId, 0f)") && hush.Contains("\"_hov_Puddles\"") && hush.Contains("\"_Rain\""),
            "snow field: under the snow the game's rain is hidden on this machine's picture alone (renderers, sound, wet look), each put back as it was, its weather never written");
        Check(director.Contains("Hush(Snowing)") && director.Contains("_hush.Hold(snowing && SeasonGame.GameRaining)") && director.Contains("public bool Snowing => ")
              && looks.Contains("bool snowDay = ") && looks.Contains("if (!Settings.SeasonRain || snowDay)") && sky.Contains("public static void EndRain()"),
            "snow field: snow takes the place of rain: the game's rain hidden while it snows, and no shower or storm of the seasons' started on a snow day");
        Check(tests.Contains("rain seen: ") && tests.Contains("FlakeWords()") && tests.Contains("MarkWords()") && tests.Contains("px where it looks"),
            "snow field: the snow test says the flakes' size and spacing for the camera's distance, when the ground was marked, and whether rain is seen");
    }
}
