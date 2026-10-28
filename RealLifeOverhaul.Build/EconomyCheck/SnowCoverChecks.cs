using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Seasons;

// The lying snow's square (SnowCoverRules and the sources that use it). The owner, 28 Oct 2026: black roads seen while it
// snowed, the snow added a moment later. The square was 150 m across, moved only once a second and only after the player
// had gone 25 m from its middle, and was switched off and on to move. Here: the camera's reach over the ground for each
// of the game's and the mod's cameras; a drive frame by frame, at three frame rates, in which the square must cover the
// camera's reach in every frame (the same drive asked only once a second, the old way, must fail it, which is how this
// check would see the cover lagging); a jump; the cost; and in the sources, a move that never switches the projector
// off and a keeper in the frame's late step, before HDRP's decal job reads the projector's place.
internal static partial class Program
{
    private static void SnowCovers()
    {
        SnowCoverReach();
        SnowCoverDrives();
        SnowCoverCost();
        SnowCoverSources();
    }

    private const float CoverLens = 0.4557f;        // tan of half the game's 49-degree lens, as SeasonLookRules has it
    private const float CoverAspect = 16f / 9f;

    // A camera this far from the player and this many degrees down, looking at them.
    private static float CoverReachOf(float distance, float degreesDown)
    {
        double a = degreesDown * Math.PI / 180.0;
        float above = (float)(distance * Math.Sin(a)), away = (float)(distance * Math.Cos(a));
        return SnowCoverRules.Reach(above, away, (float)Math.Sin(a), (float)Math.Cos(a), CoverLens, CoverLens * CoverAspect);
    }

    private static void SnowCoverReach()
    {
        float streetNear = CoverReachOf(15f, 47f), streetFar = CoverReachOf(35f, 47f), foot = CoverReachOf(6f, 18f), car = CoverReachOf(8f, 12f);
        Check(streetFar > 75f && streetFar > streetNear && streetNear >= SnowCoverRules.ReachLeast,
            "snow cover: the game's street camera at 35 m sees ground past the old square's 75 m edge (" + streetFar.ToString("0") + " m; at 15 m " + streetNear.ToString("0") + " m), and a farther camera reaches farther");
        Check(foot == SnowCoverRules.ReachMost && car == SnowCoverRules.ReachMost,
            "snow cover: a camera whose view reaches the horizon (on foot, in a car) gets the cap, " + SnowCoverRules.ReachMost + " m");
        Check(SnowCoverRules.Reach(float.NaN, -3f, float.NaN, 0f, CoverLens, CoverLens) == SnowCoverRules.ReachMost
              && SnowCoverRules.Reach(-10f, 0f, 1f, 0f, CoverLens, CoverLens) >= SnowCoverRules.ReachLeast
              && CoverReachOf(200f, 89f) <= SnowCoverRules.ReachMost && CoverReachOf(1f, 89f) == SnowCoverRules.ReachLeast,
            "snow cover: the reach is sound for a camera under the feet, straight down, or with no figures, and stays between " + SnowCoverRules.ReachLeast + " and " + SnowCoverRules.ReachMost + " m");
        float half = SnowCoverRules.HalfFor(streetFar, 0f), same = SnowCoverRules.HalfFor(streetFar - 20f, half), grown = SnowCoverRules.HalfFor(SnowCoverRules.ReachMost, half);
        Check(half >= streetFar + SnowCoverRules.Margin && half % SnowCoverRules.SpanStep == 0f && same == half && grown >= SnowCoverRules.ReachMost + SnowCoverRules.Margin
              && SnowCoverRules.HalfFor(SnowCoverRules.ReachLeast, grown) < grown,
            "snow cover: the square's half side is the reach and the margin in steps of " + SnowCoverRules.SpanStep + " m, grown at once, shrunk only when well too big ("
            + half + " m for the street camera, " + grown + " m at the cap)");
    }

    // A drive: standing, pulling away, along a street at 35 m/s, a turn, a sprint at 60 m/s, a stop; the camera's reach
    // the given one. The rules asked every frame, or only every `every` seconds as the old way did. The worst frame's
    // cover past the reach (negative: the camera saw past the square's edge), and the moves made.
    private static float CoverDrive(float fps, float reach, float every, out int moves, out bool ahead)
    {
        var s = default(SnowCoverState);
        float dt = 1f / fps, x = 1200f, z = -800f, heading = 0.3f, worst = float.MaxValue, asked = -1f;
        moves = 0;
        ahead = true;
        int frames = (int)(40f * fps);
        for (int f = 0; f < frames; f++)
        {
            float t = f * dt;
            float pace = t < 2f ? 0f : t < 6f ? (t - 2f) * 8.75f : t < 20f ? 35f : t < 24f ? 20f : t < 34f ? 60f : 0f;
            if (t >= 20f && t < 24f)
            {
                heading += 0.4f * dt;   // a turn at a corner
            }
            x += (float)Math.Cos(heading) * pace * dt;
            z += (float)Math.Sin(heading) * pace * dt;
            if (every <= 0f || t - asked >= every || asked < 0f)
            {
                asked = t;
                bool laid = s.Laid;
                if (SnowCoverRules.Step(ref s, x, 3f, z, every <= 0f ? dt : every, reach) && laid)
                {
                    moves++;
                    if (pace > 20f)
                    {
                        ahead &= (s.Cx - x) * Math.Cos(heading) + (s.Cz - z) * Math.Sin(heading) > 0.0;
                    }
                }
            }
            worst = Math.Min(worst, SnowCoverRules.Covered(s, x, z) - reach);
        }
        return worst;
    }

    // The old way: 150 m square, moved only when the player was 25 m from its middle, asked once a second.
    private static float CoverDriveOld(float fps, float reach)
    {
        float dt = 1f / fps, x = 1200f, z = -800f, cx = x, cz = z, worst = float.MaxValue, asked = 0f;
        for (int f = 0; f < (int)(40f * fps); f++)
        {
            float t = f * dt, pace = t < 2f ? 0f : t < 34f ? 35f : 0f;
            x += pace * dt;
            if (t - asked >= 1f)
            {
                asked = t;
                if ((x - cx) * (x - cx) + (z - cz) * (z - cz) > 25f * 25f)
                {
                    cx = x;
                    cz = z;
                }
            }
            worst = Math.Min(worst, 75f - Math.Max(Math.Abs(x - cx), Math.Abs(z - cz)) - reach);
        }
        return worst;
    }

    private static void SnowCoverDrives()
    {
        float street = CoverReachOf(35f, 47f);
        bool every = true, few = true, lead = true;
        string seen = string.Empty;
        foreach (float fps in new[] { 30f, 60f, 144f })
        {
            foreach (float reach in new[] { street, SnowCoverRules.ReachMost })
            {
                float worst = CoverDrive(fps, reach, 0f, out int moves, out bool ahead);
                every &= worst >= 0f;
                few &= moves > 5 && moves < 40;
                lead &= ahead;
                seen += " " + fps + " fps, " + reach.ToString("0") + " m: " + worst.ToString("0") + " m spare, " + moves + " moves;";
            }
        }
        Check(every, "snow cover: on a drive (standing, 35 m/s, a corner, 60 m/s, a stop) at 30, 60 and 144 frames a second the square covers all the camera shows in every frame (" + seen.Trim() + ")");
        Check(few, "snow cover: the square moves a few dozen times on the 40 s drive at most, not every frame");
        Check(lead, "snow cover: a move while driving puts the square's middle ahead of the player, along their way");
        float lagging = CoverDrive(60f, street, 1f, out _, out _), old = CoverDriveOld(60f, street);
        Check(lagging < 0f && old < 0f, "snow cover: the same drive asked only once a second, and the old 150 m square moved after 25 m, both leave the camera seeing past the edge ("
                                        + lagging.ToString("0") + " m and " + old.ToString("0") + " m): the drive check sees a cover that lags");

        var jump = default(SnowCoverState);
        SnowCoverRules.Lay(ref jump, 0f, 0f, 0f, street);
        bool moved = SnowCoverRules.Step(ref jump, 900f, 0f, -400f, 1f / 60f, street);
        Check(moved && SnowCoverRules.Covered(jump, 900f, -400f) >= street && jump.Vx == 0f && jump.Vz == 0f,
            "snow cover: a jump (a door, a load) lays the square round where the player came out in that same frame, with no pace taken from it");
        var up = default(SnowCoverState);
        SnowCoverRules.Lay(ref up, 0f, 0f, 0f, street);
        Check(!SnowCoverRules.Step(ref up, 0f, 3.5f, 0f, 0.016f, street) && SnowCoverRules.Step(ref up, 0f, 5f, 0f, 0.016f, street) && up.Cy == 5f,
            "snow cover: it follows the player up or down only past " + SnowCoverRules.Rise + " m (a roof, a ramp)");
        var a = default(SnowCoverState);
        var b = default(SnowCoverState);
        bool same = true;
        for (int i = 0; i < 500; i++)
        {
            float px = i * 0.6f, pz = (float)Math.Sin(i * 0.01) * 40f, r = 60f + i % 200;
            same &= SnowCoverRules.Step(ref a, px, 0f, pz, 0.016f, r) == SnowCoverRules.Step(ref b, px, 0f, pz, 0.016f, r);
        }
        Check(same && a.Cx == b.Cx && a.Cz == b.Cz && a.Half == b.Half, "snow cover: the same frames give the same square every time");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double CoverFrames(int frames, out long made, out float sink)
    {
        var s = default(SnowCoverState);
        sink = 0f;
        long before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < frames; i++)
        {
            float reach = SnowCoverRules.Reach(10f + i % 30, 20f, 0.7f, 0.7f, CoverLens, CoverLens * CoverAspect);
            if (SnowCoverRules.Step(ref s, i * 0.5f, 0f, i * 0.2f, 0.016f, reach))
            {
                sink += s.Cx;
            }
            sink += SnowCoverRules.Covered(s, i * 0.5f, i * 0.2f);
        }
        watch.Stop();
        made = GC.GetAllocatedBytesForCurrentThread() - before;
        return watch.Elapsed.TotalMilliseconds / frames;
    }

    private static void SnowCoverCost()
    {
        CoverFrames(20000, out _, out _);
        double each = CoverFrames(200000, out long made, out float sink);
        Check(made == 0 && each < 0.005, "snow cover: a frame's look (the camera's reach, the step, the cover measured) allocates nothing and takes "
                                         + each.ToString("0.00000") + " ms (under 0.005 of the frame's 0.05 budget; " + made + " bytes; " + (sink != 0f ? "ok" : "?") + ")");
    }

    // A method's body in a source, from its signature to its closing brace at the same depth.
    private static string CoverBody(string source, string signature)
    {
        int at = source.IndexOf(signature, StringComparison.Ordinal);
        int open = at >= 0 ? source.IndexOf('{', at) : -1;
        if (open < 0)
        {
            return string.Empty;
        }
        int depth = 0;
        for (int i = open; i < source.Length; i++)
        {
            depth += source[i] == '{' ? 1 : source[i] == '}' ? -1 : 0;
            if (depth == 0)
            {
                return source.Substring(open, i - open + 1);
            }
        }
        return string.Empty;
    }

    private static void SnowCoverSources()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string look = SeasonRead(folder, "Seasons", "SnowLook.cs");
        string keeper = SeasonRead(folder, "Seasons", "SnowCoverFollow.cs");
        string director = SeasonRead(folder, "Seasons", "SeasonDirector.cs");
        string pose = CoverBody(look, "private void Pose(Vector3 player)"), keep = CoverBody(look, "internal void Keep(float dt)");
        string place = CoverBody(look, "private void Place()"), size = CoverBody(look, "private void Size(float half)"), lie = CoverBody(look, "private void Lie(float cover, Vector3 player)");
        Check(pose.Length > 0 && keep.Length > 0 && place.Length > 0 && size.Length > 0
              && !Regex.IsMatch(pose + keep + place + size, @"SetActive\(\s*false\s*\)|\.enabled\s*=\s*false")
              && Regex.IsMatch(lie, @"if \(!_cover\.gameObject\.activeSelf\)\s*\{\s*Pose\(player\);"),
            "snow cover: the projector is never switched off to move (HDRP reads its place every frame); it is shown once, when snow first lies");
        Check(place.Contains("_cover.transform.position = ") && place.Contains("_cover.uvBias = ") && keep.Contains("SnowCoverRules.Step(ref _square") && keep.Contains("Place();")
              && !Regex.IsMatch(look + keeper, @"beginCameraRendering|beginContextRendering|beginFrameRendering"),
            "snow cover: it is moved in the frame's late step, never in a camera callback, which HDRP calls after its decal job has read the projector's place");
        Check(Regex.IsMatch(keeper, @"private void LateUpdate\(\)") && keeper.Contains("Look.Keep(Time.deltaTime);") && keeper.Contains("HotSpots.Slot(\"Seasons: SnowCoverFollow.LateUpdate\")")
              && keeper.Contains("enabled = false;") && pose.Contains("_keeper.Hold(true);") && look.Contains("_keeper.Hold(false);"),
            "snow cover: its keeper runs every frame only while snow lies, and costs nothing otherwise");
        Check(!director.Contains(".Keep(") && !Regex.IsMatch(CoverBody(director, "private void Slow(float now)"), @"Keep|Place|Recentre"),
            "snow cover: nothing of it is added to the seasons' once-a-second step");
        Check(look.Contains("\"Seasons check (snow cover): \"") && look.Contains(" frames with the cover off while snow lies, the cover reaching ") && look.Contains("\" m against the camera's \"")
              && look.Contains("_offFrames++") && look.Contains("CityMap.IsOpen") && look.Contains("Meta.Dev.Verbose"),
            "snow cover: with Developer diagnostics on, its check line says its moves, frames with the cover off, and its reach against the camera's (the city map's camera left out)");
        Check(FixLineEverywhere(SnowCoverFixPieces), "snow cover: What's new has the fix in every language, after its row's fix mark");
    }

    // A piece of the fix's line in each language, in the order of ModLanguages.PerFeature.
    private static readonly string[] SnowCoverFixPieces =
    {
        "the roads far ahead and the stretch in front of a moving car showed black",
        "o trecho à frente de um carro em movimento apareciam pretos", "行驶中车辆前方的路段会显示为黑色", "der Abschnitt vor einem fahrenden Auto schwarz dargestellt", "le tronçon devant une voiture en mouvement apparaissaient en noir", "el tramo delante de un coche en movimiento se veían negros", "участок перед движущейся машиной выглядели чёрными", "het stuk voor een rijdende auto zwart te zien", "hareket eden bir arabanın önündeki kısım siyah görünüyordu"
    };
}
