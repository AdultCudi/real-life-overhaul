using System;
using System.IO;
using RealLifeOverhaul.Leisure;

// The game-free rules of sightseeing's selfie (Leisure/SightseeingRules.cs): the way the player turns to have the
// landmark behind them, standing on it, and how long it lasts.
internal static partial class Program
{
    private static void Sightseeing()
    {
        // Their back to it: the way faced runs from the landmark through the player, wherever round it they stand.
        bool away = true, unit = true;
        for (int k = 0; k < 16; k++)
        {
            double a = k * Math.PI / 8;
            float px = 100f + 40f * (float)Math.Cos(a), pz = -50f + 40f * (float)Math.Sin(a);
            bool ok = SightseeingRules.BackTo(px, pz, 100f, -50f, out float x, out float z);
            away &= ok && x * (px - 100f) + z * (pz + 50f) > 39.9f;        // along the line out from the landmark
            unit &= Near((float)Math.Sqrt(x * x + z * z), 1f, 0.0001f);
        }
        Check(away, "sightseeing: the player faces straight away from the landmark, from all round it");
        Check(unit, "sightseeing: the way to face is a unit direction");
        SightseeingRules.BackTo(0f, 30f, 0f, 0f, out float nx, out float nz);
        Check(Near(nx, 0f) && Near(nz, 1f), "sightseeing: 30 m north of the landmark, the player faces north (the landmark due south, behind)");
        SightseeingRules.BackTo(-45f, 0f, 0f, 0f, out float wx, out float wz);
        Check(Near(wx, -1f) && Near(wz, 0f), "sightseeing: 45 m west of it, they face west");

        // On the landmark's point there is no way from it: they stay as they stand.
        Check(!SightseeingRules.BackTo(5f, 5f, 5f, 5f, out float sx, out float sz) && sx == 0f && sz == 0f,
            "sightseeing: on the landmark's point itself, no turn");
        Check(!SightseeingRules.BackTo(5.5f, 5f, 5f, 5f, out _, out _), "sightseeing: half a metre from it, no turn");
        Check(SightseeingRules.BackTo(5f + SightseeingRules.TooNear, 5f, 5f, 5f, out _, out _), "sightseeing: a metre from it, a turn");
        Check(!SightseeingRules.BackTo(float.NaN, 0f, 0f, 0f, out _, out _), "sightseeing: a position that is not a number turns nobody");

        // The same numbers give the same way, every time and on every machine (Going Public: nothing random in it).
        SightseeingRules.BackTo(12.25f, -7.5f, 3f, 4f, out float a1, out float b1);
        SightseeingRules.BackTo(12.25f, -7.5f, 3f, 4f, out float a2, out float b2);
        Check(a1 == a2 && b1 == b2, "sightseeing: the turn is the same for the same places");

        // How long: about what it was with take_photo (5.2 s), and a moment without a clip.
        Check(Near(SightseeingRules.Lasts(true), 5.2f) && Near(SightseeingRules.Lasts(false), 2.5f), "sightseeing: 5.2 s with the clip, 2.5 s without");

        // The clips are the mod's own, both with the props bundle's phone (no new prop), and the activity plays the selfie
        // turned by this rule.
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\.."));
        string clipList = Path.Combine(root, @"CityAssets\scripts\clips.py");
        string activity = Path.Combine(root, @"Assets\Mods\RealLifeOverhaul\Scripts\Leisure\Activities\NightOut.cs");
        string clipsText = File.Exists(clipList) ? File.ReadAllText(clipList) : "";
        Check(clipsText.Contains("Clip(\"" + SightseeingRules.SelfieClip + "\", 5.0, selfie, loop=False, tool=\"phone\"")
              && clipsText.Contains("Clip(\"" + SightseeingRules.PhotoClip + "\", 5.0, take_photo, loop=False, tool=\"phone\""),
            "sightseeing: CityAssets makes the selfie and take_photo clips, 5 s each, with the phone prop");
        string code = File.Exists(activity) ? Code(File.ReadAllText(activity)) : "";
        int at = code.IndexOf("class Sightseeing", StringComparison.Ordinal);
        string body = at >= 0 ? code.Substring(at, Math.Min(2500, code.Length - at)) : "";
        Check(body.Contains("Play(SightseeingRules.SelfieClip") && body.Contains("SightseeingRules.BackTo(") && body.Contains("Ctx.Body.Face(")
              && body.Contains("SightseeingRules.Lasts(") && body.Contains("LeisureState.See(_mark.Id)"),
            "sightseeing: the activity plays the selfie, turns the player by the rule, lasts as the rule says and still ticks the landmark off");
    }
}
