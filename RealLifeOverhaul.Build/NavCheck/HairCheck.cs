using System;
using System.Diagnostics;
using RealLifeOverhaul;

// Checks for the arithmetic of the hair finish, compiled here as it ships:
//   Character/HairRules.cs   how bright a layer of hair can get under a light (the stock HDRP hair shader's own
//                            formulas), the bounds on its back-light and its highlight, and how solid its strands are
//
// What is not here: anything of the engine. That the shader really computes what HairRules says it does was read
// from its source and seen in the HDRP preview (CharacterAssets, evidence\hair-light); how hair looks in the game is
// for the game to show, with the three hair entries of Options > Tests and the "Hair check" line of Player.log.
internal static partial class Program
{
    private const float Eps = 1.0005f;   // a bound met exactly may be over by rounding
    // The most any dark hair may shine: the palest of it at 1.5 times its own front-lit brightness, 0.3 of lit skin.
    private const float DarkShine = HairRules.PaleSheen * HairRules.DarkLuma / HairRules.Pi;

    private static void Hair()
    {
        HairColourSpace();
        HairModel();
        HairFault();
        HairNamedColours();
        HairEveryColour();
        HairExtremes();
        HairSheenSteps();
        HairCaps();
        HairCover();
        HairGain();
        HairShade();
        HairKeepCover();
        HairTiming();
    }

    private static float[] Lin(int rgb)
    {
        return new[] { HairRules.ToLinear(((rgb >> 16) & 255) / 255f), HairRules.ToLinear(((rgb >> 8) & 255) / 255f), HairRules.ToLinear((rgb & 255) / 255f) };
    }

    private static float LumaOf(int rgb)
    {
        float[] c = Lin(rgb);
        return HairRules.Luma(c[0], c[1], c[2]);
    }

    // A layer as the finish hands it to the rule: strands of the game's commonest hair material, tuned as HairFinish.Colour does.
    private static HairRules.Finish Strands(int rgb, float rim = 0.127f, bool cap = false)
    {
        float[] c = Lin(rgb);
        float[] hue = { c[0] + (1f - c[0]) * 0.4f, c[1] + (1f - c[1]) * 0.4f, c[2] + (1f - c[2]) * 0.4f };
        return HairRules.Rule(c[0], c[1], c[2], 0.43f, 0.529f, 0.2f, 0.577f, rim, HairRules.Luma(hue[0], hue[1], hue[2]), cap);
    }

    private static bool Finite(HairRules.Finish f)
    {
        float[] all = { f.ThroughR, f.ThroughG, f.ThroughB, f.Rim, f.Specular, f.Secondary, f.GlossMin, f.GlossMax, f.SheenPeak, f.ThroughPeak, f.FrontLit };
        foreach (float v in all)
        {
            if (float.IsNaN(v) || float.IsInfinity(v) || v < 0f)
            {
                return false;
            }
        }
        return true;
    }

    private static void HairColourSpace()
    {
        Check(HairRules.ToLinear(0f) == 0f && HairRules.ToLinear(1f) == 1f && HairRules.ToSrgb(0f) == 0f && HairRules.ToSrgb(1f) == 1f, "hair: black and white are the same in both colour spaces");
        Check(Near(HairRules.ToLinear(0.5f), 0.2140f, 0.0005f) && Near(HairRules.ToSrgb(0.2140f), 0.5f, 0.0005f), "hair: sRGB 0.5 is linear 0.214, and back");
        bool round = true, rising = true;
        float last = -1f;
        for (int i = 0; i <= 255; i++)
        {
            float s = i / 255f, l = HairRules.ToLinear(s);
            round &= Near(HairRules.ToSrgb(l), s, 0.0005f);
            rising &= l > last;
            last = l;
        }
        Check(round, "hair: every 8-bit sRGB value survives the way to linear and back");
        Check(rising, "hair: a brighter sRGB value is a brighter linear one");
        Check(HairRules.ToLinear(float.NaN) == 0f && HairRules.ToLinear(-3f) == 0f && HairRules.ToLinear(7f) == 1f && HairRules.ToSrgb(float.NaN) == 0f,
            "hair: values outside 0 to 1, and no value at all, are clamped");
        Check(Near(HairRules.Luma(1f, 1f, 1f), 1f, 0.0001f) && HairRules.Luma(0f, 0f, 0f) == 0f && HairRules.Luma(0f, 1f, 0f) > HairRules.Luma(1f, 0f, 0f)
              && HairRules.Luma(1f, 0f, 0f) > HairRules.Luma(0f, 0f, 1f), "hair: brightness weighs green over red over blue, and white is 1");
    }

    private static void HairModel()
    {
        Check(Near(HairRules.SkinLit, 0.1592f, 0.0001f), "hair: lit skin sends back 0.159 per unit of light (albedo 0.5 over pi)");
        Check(Near(HairRules.Fresnel(1f), 0.0465f, 0.0001f) && Near(HairRules.Fresnel(0f), 1f, 0.0001f), "hair: Fresnel is 0.0465 head on and 1 at a glancing light");
        // A lamp 150 degrees round from the camera: half the angle is 75 degrees.
        Check(Near(HairRules.Fresnel((float)Math.Cos(75.0 * Math.PI / 180.0)), 0.26f, 0.01f), "hair: with the lamp 150 degrees round from the camera, Fresnel is 0.26, over five times head on");
        bool falling = true;
        for (int i = 1; i <= 100; i++)
        {
            falling &= HairRules.Fresnel(i / 100f) <= HairRules.Fresnel((i - 1) / 100f);
        }
        Check(falling, "hair: Fresnel only falls as the light comes round to the side of the camera");
        // The shader for gloss 0.577: roughness (1 - 0.577)^2 = 0.179, n = 2 / 0.179^2 - 2 = 60.5, height (n + 2) / (2 pi) = 9.94.
        Check(Near(HairRules.Sharpness(0.577f), 9.94f, 0.05f), "hair: a highlight of gloss 0.577 is 9.9 high, as the exponent of the shader gives it");
        Check(Near(HairRules.Sharpness(0f), 1f / HairRules.Pi, 0.0001f), "hair: a highlight of no gloss is 1 / pi high");
        bool sharper = true;
        for (int i = 1; i <= 100; i++)
        {
            sharper &= HairRules.Sharpness(i / 100f) >= HairRules.Sharpness((i - 1) / 100f);
        }
        Check(sharper && !float.IsInfinity(HairRules.Sharpness(1f)) && !float.IsNaN(HairRules.Sharpness(1f)), "hair: more gloss is a higher highlight, and full gloss is still a number");
        Check(Near(HairRules.Tint(1f, 0f, 0f), HairRules.WhiteShare, 0.0001f) && Near(HairRules.Tint(1f, 0f, 1f), 1f, 0.0001f) && Near(HairRules.Tint(0f, 1f, 1f), HairRules.SecondShare, 0.0001f)
              && HairRules.Tint(0f, 1f, 0f) == 0f, "hair: the first highlight is 0.7 white whatever its colour, the second half its colour and no white");
        Check(Near(HairRules.ThroughPeak(0.1f, 0.2f), 0.12f, 0.0001f) && HairRules.ThroughPeak(0.1f, -1f) == 0.1f && HairRules.ThroughPeak(-1f, 0.2f) == 0f,
            "hair: the light coming through is the brightness of its colour times one plus the outline glow");
        Check(Near(HairRules.FrontLit(0.5f), HairRules.SkinLit, 0.0001f), "hair: a matt surface of brightness 0.5 lit from the front sends back what lit skin does");
    }

    // The fault this was written for, in numbers: what the hair of the game and the finish as it was send back.
    private static void HairFault()
    {
        // The game: back-light FFA45F on every hair material, highlight colour E5B971, strengths 0.313 and 0.604, gloss 0.577.
        float through = HairRules.ThroughPeak(LumaOf(0xFFA45F), 0.127f);
        float sheen = HairRules.SheenPeak(0.313f, 0.604f, LumaOf(0xE5B971), 0.577f, 1f);
        Check(through > 2.5f * HairRules.SkinLit, "hair fault: the back-light of the game sends back over two and a half times what lit skin does, for every hair colour (" + through.ToString("F3") + ")");
        Check(sheen > 4f * HairRules.SkinLit, "hair fault: the highlight of the game can be over four times lit skin (" + sheen.ToString("F3") + ")");
        float own = HairRules.FrontLit(LumaOf(0x241C14));
        Check(own < 0.03f * HairRules.SkinLit, "hair fault: the hair the player wore, lit from the front, sends back under 3% of what lit skin does");

        // The finish as it was until 11 October: back-light the hair colour lifted a quarter towards white (in sRGB), highlight unbounded.
        float[] lifted = { HairRules.ToLinear(0x24 / 255f * 0.75f + 0.25f), HairRules.ToLinear(0x1C / 255f * 0.75f + 0.25f), HairRules.ToLinear(0x14 / 255f * 0.75f + 0.25f) };
        float was = HairRules.ThroughPeak(HairRules.Luma(lifted[0], lifted[1], lifted[2]), 0.127f);
        Check(was > 0.5f * HairRules.SkinLit && was > 25f * own,
            "hair fault: the finish as it was let that hair glow at over half of lit skin, twenty-five times its own front-lit brightness (" + was.ToString("F3") + ")");
        float[] black = Lin(0x241C14);
        float[] hue = { black[0] + (1f - black[0]) * 0.4f, black[1] + (1f - black[1]) * 0.4f, black[2] + (1f - black[2]) * 0.4f };
        float shone = HairRules.SheenPeak(0.43f, 0.529f, HairRules.Luma(hue[0], hue[1], hue[2]), 0.577f, 1f);
        Check(shone > 4f * HairRules.SkinLit, "hair fault: the finish as it was let its highlight reach over four times lit skin at a glancing light (" + shone.ToString("F3") + ")");
        Check(shone * HairRules.Fresnel((float)Math.Cos(75.0 * Math.PI / 180.0)) > HairRules.SkinLit,
            "hair fault: and brighter than lit skin already with the lamp 150 degrees round from the camera");

        HairRules.Finish now = Strands(0x241C14);
        Check(now.ThroughPeak < was / 20f && now.SheenPeak < shone / 10f, "hair: the same hair now glows under a twentieth and shines under a tenth of that");
    }

    private static void HairNamedColours()
    {
        // black, dark brown, blond, grey: the four the evidence sheets use.
        int[] colours = { 0x1A1614, 0x5A3A22, 0xD9B36A, 0x9A9A98 };
        string[] names = { "black", "dark brown", "blond", "grey" };
        for (int i = 0; i < colours.Length; i++)
        {
            float luma = LumaOf(colours[i]);
            HairRules.Finish f = Strands(colours[i]);
            bool dark = luma <= HairRules.DarkLuma;
            Check(dark == (i < 2), "hair: " + names[i] + " is " + (i < 2 ? "dark hair" : "not dark hair") + " (brightness " + luma.ToString("F3") + ")");
            Check(f.ThroughPeak <= (dark ? HairRules.DarkThrough : HairRules.ThroughCap) * HairRules.SkinLit * Eps,
                "hair: " + names[i] + " with a lamp behind it glows at most " + (dark ? "a quarter" : "half") + " of lit skin (" + (f.ThroughPeak / HairRules.SkinLit).ToString("F3") + ")");
            Check(f.ThroughPeak <= HairRules.ThroughShare * f.FrontLit * (1f + f.Rim) * Eps && HairRules.ThroughShare <= 0.25f,
                "hair: against a lamp " + names[i] + " hair glows at most a quarter as bright as it is lit from the front, plus its outline glow");
            Check(f.SheenPeak <= HairRules.SheenLimit(luma, false) * Eps && (!dark || f.SheenPeak <= DarkShine * Eps),
                "hair: the highlight of " + names[i] + " hair stays under its bound at any angle (" + (f.SheenPeak / HairRules.SkinLit).ToString("F3") + " of lit skin)");
        }
    }

    private static void HairEveryColour()
    {
        float[] rims = { 0f, 0.03f, 0.127f, 0.2f, 1f, 9f };
        float[][] gloss =
        {
            new[] { 0.313f, 0.604f, 0.255f, 0.577f }, new[] { 0.43f, 0.529f, 0.2f, 0.577f }, new[] { 0.178f, 0.8f, 0.255f, 0.574f },
            new[] { 0f, 0f, 0f, 0f }, new[] { 3f, 3f, 0.9f, 1f }, new[] { 0.357f, 0.8f, 0.5f, 0.255f }
        };
        bool through = true, sheen = true, darkOnes = true, finite = true, tamer = true, ownColour = true, ordered = true;
        int count = 0;
        for (int r = 0; r <= 16; r++)
        {
            for (int g = 0; g <= 16; g++)
            {
                for (int b = 0; b <= 16; b++)
                {
                    float lr = HairRules.ToLinear(r / 16f), lg = HairRules.ToLinear(g / 16f), lb = HairRules.ToLinear(b / 16f);
                    float luma = HairRules.Luma(lr, lg, lb);
                    for (int k = 0; k < rims.Length; k++)
                    {
                        float[] v = gloss[(r + g + b + k) % gloss.Length];
                        bool cap = (r + 2 * g + b + k) % 5 == 0;
                        float hueLuma = luma + (1f - luma) * 0.4f;
                        HairRules.Finish f = HairRules.Rule(lr, lg, lb, v[0], v[1], v[2], v[3], rims[k], hueLuma, cap);
                        count++;
                        finite &= Finite(f) && f.GlossMax <= 1f && f.GlossMin <= 1f && f.Rim <= 1f;
                        through &= f.ThroughPeak <= HairRules.ThroughLimit(luma) * Eps && f.ThroughPeak <= HairRules.ThroughCap * HairRules.SkinLit * Eps;
                        sheen &= f.SheenPeak <= HairRules.SheenLimit(luma, cap) * Eps && f.SheenPeak <= HairRules.SheenCap * HairRules.SkinLit * Eps;
                        if (luma <= HairRules.DarkLuma)
                        {
                            darkOnes &= f.ThroughPeak <= HairRules.DarkThrough * HairRules.SkinLit * Eps && f.SheenPeak <= DarkShine * Eps && DarkShine <= 0.3f * HairRules.SkinLit * Eps;
                        }
                        tamer &= f.Specular <= Math.Min(v[0], 4f) * Eps && f.Secondary <= Math.Min(v[1], 4f) * Eps && f.GlossMax <= Math.Max(v[2], v[3]) * Eps;
                        ordered &= f.GlossMin <= f.GlossMax;
                        // The light coming through has the hue of the hair: no channel the hair lacks, none out of proportion.
                        ownColour &= f.ThroughR <= HairRules.ThroughShare * lr / HairRules.Pi * Eps && f.ThroughG <= HairRules.ThroughShare * lg / HairRules.Pi * Eps
                                     && f.ThroughB <= HairRules.ThroughShare * lb / HairRules.Pi * Eps
                                     && Near(f.ThroughR * lg, f.ThroughG * lr, 0.0001f) && Near(f.ThroughG * lb, f.ThroughB * lg, 0.0001f);
                    }
                }
            }
        }
        Check(count == 17 * 17 * 17 * 6, "hair: every colour of a 17-step cube is tried with six outline glows, six sets of the numbers of the game, strands and caps");
        Check(finite, "hair: every number the rule gives is a number, none below zero, gloss and outline glow at most 1");
        Check(through, "hair: with a lamp behind it no hair of any colour glows above its bound, and none above half of lit skin");
        Check(sheen, "hair: no hair of any colour shines above its bound at any angle, and none above lit skin");
        Check(darkOnes, "hair: dark hair of any hue glows at most a quarter and shines at most 0.3 of lit skin");
        Check(tamer, "hair: the rule never makes a highlight stronger or glossier than the game had it");
        Check(ordered, "hair: the lower end of the gloss range is never above the upper end");
        Check(ownColour, "hair: light coming through has the hue of the hair, and at most a quarter of the brightness of the hair lit from the front");

        // Along a ramp of greys the glow only rises with the brightness of the hair, and stops rising at the cap.
        bool rises = true;
        float last = -1f;
        for (int i = 0; i <= 255; i++)
        {
            HairRules.Finish f = Strands(i << 16 | i << 8 | i);
            rises &= f.ThroughPeak >= last - 1e-6f;
            last = f.ThroughPeak;
        }
        Check(rises && last <= HairRules.ThroughCap * HairRules.SkinLit * Eps && last > 0.4f * HairRules.SkinLit, "hair: from black to white the glow only rises, and white hair ends at half of lit skin");

        HairRules.Finish one = Strands(0x6B4423, 0.2f), two = Strands(0x6B4423, 0.2f);
        Check(one.ThroughR == two.ThroughR && one.ThroughG == two.ThroughG && one.ThroughB == two.ThroughB && one.Specular == two.Specular && one.Secondary == two.Secondary
              && one.GlossMax == two.GlossMax && one.GlossMin == two.GlossMin && one.SheenPeak == two.SheenPeak, "hair: the same hair gets the same numbers every time");
    }

    private static void HairExtremes()
    {
        HairRules.Finish black = Strands(0x000000);
        Check(black.ThroughR == 0f && black.ThroughG == 0f && black.ThroughB == 0f && black.ThroughPeak == 0f && black.FrontLit == 0f, "hair: pure black lets nothing through");
        Check(Finite(black) && black.SheenPeak <= HairRules.DarkSheen * HairRules.SkinLit * Eps && black.SheenPeak > 0f, "hair: pure black keeps a highlight, under its bound");

        HairRules.Finish white = Strands(0xFFFFFF);
        Check(Near(white.ThroughR, white.ThroughG, 0.00001f) && Near(white.ThroughG, white.ThroughB, 0.00001f), "hair: pure white lets white through, no tint");
        Check(Near(white.ThroughPeak, HairRules.ThroughCap * HairRules.SkinLit, 0.0001f) && white.ThroughPeak < 0.3f * white.FrontLit, "hair: pure white glows at the cap, half of lit skin, under a third of its own front-lit brightness");
        Check(white.SheenPeak <= HairRules.SheenCap * HairRules.SkinLit * Eps, "hair: pure white shines at most as bright as lit skin");

        int[] dyes = { 0xFF0000, 0x00FF00, 0x0000FF, 0xFF00FF, 0x00FFFF, 0xFFFF00 };
        bool pure = true, bounded = true;
        foreach (int dye in dyes)
        {
            HairRules.Finish f = Strands(dye, 0.2f);
            float[] c = Lin(dye);
            pure &= (c[0] > 0f || f.ThroughR == 0f) && (c[1] > 0f || f.ThroughG == 0f) && (c[2] > 0f || f.ThroughB == 0f)
                    && (c[0] == 0f || f.ThroughR > 0f) && (c[1] == 0f || f.ThroughG > 0f) && (c[2] == 0f || f.ThroughB > 0f);
            float luma = HairRules.Luma(c[0], c[1], c[2]);
            bounded &= Finite(f) && f.ThroughPeak <= HairRules.ThroughLimit(luma) * Eps && f.SheenPeak <= HairRules.SheenLimit(luma, false) * Eps;
        }
        Check(pure, "hair: a saturated dye lets only its own channels through (red hair glows red, never orange)");
        Check(bounded, "hair: saturated dyes are bounded like any other colour");

        HairRules.Finish odd = HairRules.Rule(float.NaN, -1f, 5f, float.NaN, -2f, 7f, float.NaN, float.NaN, float.NaN, false);
        Check(Finite(odd) && odd.ThroughPeak <= HairRules.ThroughCap * HairRules.SkinLit * Eps && odd.Specular == 0f && odd.Secondary == 0f, "hair: numbers that are no numbers come out as a harmless finish");
    }

    private static void HairSheenSteps()
    {
        // A weak highlight that fits is left exactly as the game had it.
        HairRules.Finish kept = HairRules.Rule(0.5f, 0.4f, 0.3f, 0.02f, 0.02f, 0.1f, 0.3f, 0f, 0.5f, false);
        Check(kept.Specular == 0.02f && kept.Secondary == 0.02f && kept.GlossMax == 0.3f && kept.GlossMin == 0.1f, "hair: a highlight that fits under the bound keeps the numbers of the game exactly");

        // Pale hair, a strong glossy highlight: the gloss comes down, the strength stays.
        HairRules.Finish soft = HairRules.Rule(0.69f, 0.45f, 0.14f, 0.313f, 0.264f, 0.2f, 0.49f, 0.127f, 0.75f, false);
        Check(soft.Specular == 0.313f && soft.Secondary == 0.264f && soft.GlossMax < 0.49f && soft.GlossMax >= HairRules.GlossFloor, "hair: where gloss alone can meet the bound, only the gloss comes down");
        Check(Near(soft.SheenPeak, HairRules.SheenLimit(HairRules.Luma(0.69f, 0.45f, 0.14f), false), 0.0005f), "hair: and the highlight then sits exactly at the bound");

        // Dark hair: gloss at the floor is not enough, so the strength comes down too, both highlights alike.
        HairRules.Finish matt = HairRules.Rule(0.017f, 0.012f, 0.007f, 0.43f, 0.529f, 0.2f, 0.577f, 0.127f, 0.2f, false);
        Check(Near(matt.GlossMax, HairRules.GlossFloor, 0.0001f) && matt.Specular < 0.43f && Near(matt.Specular / matt.Secondary, 0.43f / 0.529f, 0.001f),
            "hair: at the gloss floor the two highlights are turned down by the same share");
        Check(Near(matt.SheenPeak, HairRules.DarkSheen * HairRules.SkinLit, 0.0005f), "hair: the highlight of black hair then sits exactly at its bound, 0.2 of lit skin");

        // A range the game has upside down (one material does) is read as a range.
        HairRules.Finish flipped = HairRules.Rule(0.5f, 0.5f, 0.5f, 0.1f, 0.1f, 0.255f, 0.235f, 0f, 0.5f, false);
        Check(flipped.GlossMin <= flipped.GlossMax && Near(flipped.GlossMax, 0.255f, 0.0001f), "hair: a gloss range written upside down is read as a range");

        // Stronger numbers in the game never come out weaker than weaker ones.
        bool monotone = true;
        float before = 0f;
        for (int i = 0; i <= 40; i++)
        {
            HairRules.Finish f = HairRules.Rule(0.3f, 0.2f, 0.1f, i * 0.05f, i * 0.05f, 0.2f, 0.577f, 0f, 0.5f, false);
            monotone &= f.SheenPeak >= before - 1e-6f;
            before = f.SheenPeak;
        }
        Check(monotone, "hair: a stronger highlight in the game is never a weaker one after the rule, up to the bound");
    }

    private static void HairCaps()
    {
        HairRules.Finish cap = Strands(0xD9B36A, 0.2f, true), strands = Strands(0xD9B36A, 0.2f);
        Check(cap.ThroughR == 0f && cap.ThroughG == 0f && cap.ThroughB == 0f && cap.Rim == 0f && cap.ThroughPeak == 0f, "hair: a cap on the skull lets no light through and has no outline glow, whatever the game gave it");
        Check(cap.SheenPeak <= HairRules.CapSheen * HairRules.SheenLimit(LumaOf(0xD9B36A), false) * Eps && cap.SheenPeak < strands.SheenPeak,
            "hair: a cap on the skull is matt: its highlight is under 0.4 of what strands may have");
        Check(HairRules.IsCap("M_MaleHair04_Scalp") && HairRules.IsCap("T_ScalpTop_BC") && HairRules.IsCap("m_malehair01scalpopaque") && HairRules.IsCap("RealLifeOverhaul under_m_malehair04 Scalp"),
            "hair: the scalp materials and textures of the game and of the mod are known as caps by their names");
        Check(!HairRules.IsCap("M_MaleHair01_01") && !HairRules.IsCap("T_MaleHair04_BC") && !HairRules.IsCap("M_BeardShort") && !HairRules.IsCap("") && !HairRules.IsCap(null),
            "hair: strand materials, beards and no name at all are not caps");
        // The scalp underlay of the mod as HairFinish.Underlay asks for it: all but matt, nothing through.
        HairRules.Finish under = HairRules.Rule(0.012f, 0.008f, 0.005f, 0.04f, 0.04f, 0.15f, 0.25f, 0f, 0.5f, true);
        Check(under.ThroughPeak == 0f && under.Specular <= 0.04f && under.Specular > 0f && under.GlossMax == 0.25f && under.SheenPeak <= 0.1f * HairRules.SkinLit, "hair: the scalp underlay stays matt, lets nothing through and sends back under a tenth of lit skin at its brightest");
    }

    // A texture of strands: `core` texels solid, `soft` texels spread evenly over the alphas from 1 to softTop, the rest empty.
    private static int[] StrandTexture(int core, int soft, int softTop, int empty)
    {
        var histogram = new int[HairRules.Levels];
        histogram[255] = core;
        histogram[0] = empty;
        for (int i = 0; i < soft; i++)
        {
            histogram[1 + i % softTop]++;
        }
        return histogram;
    }

    private static void HairCover()
    {
        const float core = 0.95f, edge = 0.04f;
        HairRules.Cover none = HairRules.Measure(null, 3f, core, edge), blank = HairRules.Measure(new int[HairRules.Levels], 3f, core, edge),
            small = HairRules.Measure(new int[16], 3f, core, edge);
        Check(none.Empty == 1f && none.Core == 0f && blank.Empty == 1f && small.Empty == 1f, "hair cover: no texture, a blank one and a wrong-sized histogram all count as empty");

        int[] solid = StrandTexture(1000, 0, 1, 0);
        HairRules.Cover all = HairRules.Measure(solid, 3f, core, edge);
        Check(all.Core == 1f && all.Edge == 0f && all.Empty == 0f, "hair cover: a texture that is all solid is all core");

        int[] mixed = StrandTexture(200, 254 * 2, 254, 292);
        bool sums = true, coreRises = true, emptyFalls = true;
        float lastCore = -1f, lastEmpty = 2f;
        for (float gain = 1f; gain <= 8f; gain += 0.25f)
        {
            HairRules.Cover c = HairRules.Measure(mixed, gain, core, edge);
            sums &= Near(c.Core + c.Edge + c.Empty, 1f, 0.0001f) && c.Core >= 0f && c.Edge >= 0f && c.Empty >= 0f && c.Gain == gain;
            coreRises &= c.Core >= lastCore - 1e-6f;
            emptyFalls &= c.Empty <= lastEmpty + 1e-6f;   // the shares are floats: equal ones may differ in the last place
            lastCore = c.Core;
            lastEmpty = c.Empty;
        }
        Check(sums, "hair cover: core, edge and empty are shares that sum to 1 at every gain");
        Check(coreRises && emptyFalls, "hair cover: a higher gain never makes less of a texture solid, nor more of it empty");
        // As painted: alphas 1 to 10 (under 0.04) are not drawn, 243 to 254 (0.95 and up) are solid; two texels at each.
        HairRules.Cover plain = HairRules.Measure(mixed, 1f, core, edge);
        Check(Near(plain.Empty, (292 + 20) / 1000f, 0.0001f) && Near(plain.Core, (200 + 24) / 1000f, 0.0001f),
            "hair cover: at gain 1 the shares are those of the texture as painted (alpha under 0.04 not drawn, 0.95 and up solid)");
        int[] minus = StrandTexture(100, 0, 1, 100);
        minus[128] = -500;
        Check(Near(HairRules.Measure(minus, 3f, core, edge).Core, 0.5f, 0.0001f), "hair cover: a count below zero is read as none");
    }

    private static void HairGain()
    {
        const float core = 0.95f, edge = 0.04f;
        Check(HairRules.Gain(null, core, edge, 0.7f) == HairRules.BaseGain && HairRules.Gain(new int[HairRules.Levels], core, edge, 0.7f) == HairRules.BaseGain
              && HairRules.Gain(StrandTexture(10, 0, 1, 0), core, edge, 0.7f) == HairRules.BaseGain && HairRules.Gain(StrandTexture(10, 10, 254, 0), 0f, edge, 0.7f) == HairRules.BaseGain,
            "hair gain: no texture, a blank one, a solid one and a finish without a core all get the base gain");

        // Every strand at alpha 60: solid at 0.95 needs 60 x gain >= 242.25, so 4.04, so the step above: 4.25.
        var sixty = new int[HairRules.Levels];
        sixty[60] = 1000;
        Check(HairRules.Gain(sixty, core, edge, 0.7f) == 4.25f, "hair gain: strands painted at alpha 60 get the first step that makes them solid, 4.25");
        // At alpha 20 no gain up to the limit does it.
        var twenty = new int[HairRules.Levels];
        twenty[20] = 1000;
        Check(HairRules.Gain(twenty, core, edge, 0.7f) == HairRules.MaxGain, "hair gain: strands too faint for any allowed gain get the highest, 6, and no more");
        // Haze that is not drawn at the base gain is not a strand: it cannot pull the gain up.
        var hazy = new int[HairRules.Levels];
        hazy[255] = 100;
        hazy[2] = 100000;
        Check(HairRules.Gain(hazy, core, edge, 0.7f) == HairRules.BaseGain && HairRules.SolidShare(hazy, 3f, core, edge) == 1f, "hair gain: haze too faint to be drawn does not count as strands");

        // The thinnest strand texture of the game (the one the player wore), by its measured shape: 2% solid as painted,
        // 30% soft, the soft part spread up to alpha 140.
        int[] thin = StrandTexture(23, 298, 140, 679);
        float gain = HairRules.Gain(thin, core, edge, HairRules.WantedCore);
        Check(gain > HairRules.BaseGain && gain <= HairRules.MaxGain && HairRules.SolidShare(thin, gain, core, edge) >= HairRules.WantedCore
              && HairRules.SolidShare(thin, gain - HairRules.GainStep, core, edge) < HairRules.WantedCore, "hair gain: a thin texture gets the smallest gain that makes 0.7 of its strands solid (" + gain.ToString("F2") + ")");

        var random = new Random(20261011);
        bool bounded = true, onStep = true, risesWithWant = true, shareRises = true, same = true, enough = true;
        for (int t = 0; t < 400; t++)
        {
            var histogram = new int[HairRules.Levels];
            int top = 1 + random.Next(255);
            for (int i = 0; i < 2000; i++)
            {
                histogram[random.Next(top + 1)]++;
            }
            histogram[255] += random.Next(600);
            float lastGain = 0f, lastShare = -1f;
            for (int w = 0; w <= 20; w++)
            {
                float wanted = w / 20f;
                float g = HairRules.Gain(histogram, core, edge, wanted);
                bounded &= g >= HairRules.BaseGain && g <= HairRules.MaxGain;
                onStep &= Near((g - HairRules.BaseGain) / HairRules.GainStep, (float)Math.Round((g - HairRules.BaseGain) / HairRules.GainStep), 0.0001f);
                risesWithWant &= g >= lastGain;
                lastGain = g;
                same &= g == HairRules.Gain(histogram, core, edge, wanted);
                enough &= g == HairRules.MaxGain || HairRules.SolidShare(histogram, g, core, edge) >= wanted;
            }
            for (float g = HairRules.BaseGain; g <= HairRules.MaxGain; g += HairRules.GainStep)
            {
                float share = HairRules.SolidShare(histogram, g, core, edge);
                shareRises &= share >= lastShare && share >= 0f && share <= 1f;
                lastShare = share;
            }
        }
        Check(bounded, "hair gain: on 400 random textures the gain is never under 3 nor over 6");
        Check(onStep, "hair gain: and always on a step of a quarter");
        Check(risesWithWant, "hair gain: asking for more solid strands never gives a lower gain");
        Check(shareRises, "hair gain: a higher gain never makes a smaller share of the strands solid");
        Check(enough, "hair gain: the gain given makes the wanted share solid, unless it is the highest there is");
        Check(same, "hair gain: the same texture gets the same gain every time");
    }

    private static void HairShade()
    {
        const float deep = HairRules.DeepShade;
        Check(HairRules.Shade(1f, deep) == 1f && HairRules.Shade(0.9f, deep) == 1f, "hair shade: the middle of a strand keeps the game's colour");
        Check(Near(HairRules.Shade(0f, deep), deep, 1e-6f) && Near(HairRules.Shade(0.1f, deep), deep, 1e-6f), "hair shade: the faint edge of a strand and the space between strands get the deep shade (" + deep.ToString("F2") + ")");
        bool rises = true, within = true;
        float last = -1f;
        for (int a = 0; a <= 255; a++)
        {
            float s = HairRules.Shade(a / 255f, deep);
            rises &= s >= last - 1e-6f;
            within &= s >= deep - 1e-6f && s <= 1f;
            last = s;
        }
        Check(rises && within, "hair shade: the shade never falls as a strand grows more solid, and stays between the deep shade and 1");
        Check(HairRules.Shade(0f, 1f) == 1f && HairRules.Shade(0f, float.NaN) == 1f && HairRules.Shade(0.5f, 2f) == 1f, "hair shade: a deep shade of 1, more or not a number changes nothing (the finish as before)");
        Check(Near(HairRules.Shade(0f, -1f), 0f, 1e-6f), "hair shade: a deep shade below 0 is read as 0");
    }

    private static void HairKeepCover()
    {
        const float core = 0.95f;
        Check(HairRules.CoverScale(null, core, 0.3f) == 1f && HairRules.CoverScale(new int[HairRules.Levels], core, 0.3f) == 1f
              && HairRules.CoverScale(new int[16], core, 0.3f) == 1f && HairRules.CoverScale(StrandTexture(10, 10, 200, 10), core, 0f) == 1f,
            "hair mipmaps: no histogram, a blank one, a wrong-sized one and nothing to keep all leave alpha as it is");

        // A level that is already as solid as the full size needs nothing.
        int[] kept = StrandTexture(300, 0, 1, 700);
        Check(HairRules.CoverScale(kept, core, 0.3f) == 1f && Near(HairRules.SolidAt(kept, core, 1f), 0.3f, 1e-6f), "hair mipmaps: a level as solid as the full size is left alone");

        // Strands averaged into their gaps: 30% at alpha 128 (half), 70% empty. Solid again at 0.95 x 255 / 128 = 1.89.
        var half = new int[HairRules.Levels];
        half[128] = 300;
        half[0] = 700;
        float scale = HairRules.CoverScale(half, core, 0.3f);
        Check(scale > 1.8f && scale < 1.95f && Near(HairRules.SolidAt(half, core, scale), 0.3f, 1e-6f) && HairRules.SolidAt(half, core, scale - HairRules.CoverScaleStep) < 0.3f,
            "hair mipmaps: strands averaged to half get the smallest scale that makes them solid again (x" + scale.ToString("F2") + ")");
        Check(HairRules.SolidAt(half, core, 1f) == 0f, "hair mipmaps: plain mipmaps would leave none of that level solid");

        // Too faint for the most allowed: the highest scale, and never more.
        var faint = new int[HairRules.Levels];
        faint[20] = 300;
        faint[0] = 700;
        Check(HairRules.CoverScale(faint, core, 0.3f) == HairRules.MaxCoverScale, "hair mipmaps: a level too faint to keep gets the highest scale, " + HairRules.MaxCoverScale.ToString("F0") + ", and no more");

        var random = new Random(20261013);
        bool bounded = true, onStep = true, nearest = true, never = true;
        for (int t = 0; t < 300; t++)
        {
            var histogram = new int[HairRules.Levels];
            int top = 1 + random.Next(255);
            for (int i = 0; i < 3000; i++)
            {
                histogram[random.Next(top + 1)]++;
            }
            float wanted = (float)random.NextDouble() * 0.6f;
            float s = HairRules.CoverScale(histogram, core, wanted);
            bounded &= s >= 1f && s <= HairRules.MaxCoverScale;
            onStep &= Near((s - 1f) / HairRules.CoverScaleStep, (float)Math.Round((s - 1f) / HairRules.CoverScaleStep), 0.0001f);
            float off = Math.Abs(HairRules.SolidAt(histogram, core, s) - wanted);
            for (float o = 1f; o <= HairRules.MaxCoverScale + 1e-4f; o += HairRules.CoverScaleStep)
            {
                nearest &= Math.Abs(HairRules.SolidAt(histogram, core, o) - wanted) >= off - 1e-6f;
            }
            never &= HairRules.SolidAt(histogram, core, s) >= HairRules.SolidAt(histogram, core, 1f);
        }
        Check(bounded && onStep, "hair mipmaps: on 300 random levels the scale is between 1 and 4, on a step of 1/64");
        Check(nearest, "hair mipmaps: and no other allowed scale comes nearer the wanted solid share");
        Check(never, "hair mipmaps: keeping the cover never makes a level less solid than plain mipmaps");
    }

    private static void HairTiming()
    {
        // A change of look runs the rule on two to seven layers; the first wear of a texture runs the gain once.
        var watch = Stopwatch.StartNew();
        float sum = 0f;
        for (int i = 0; i < 100000; i++)
        {
            sum += Strands(i * 167).SheenPeak;
        }
        double rule = watch.Elapsed.TotalMilliseconds / 100000.0;
        int[] thin = StrandTexture(23, 298, 140, 679);
        watch.Restart();
        for (int i = 0; i < 20000; i++)
        {
            sum += HairRules.Gain(thin, 0.95f, 0.04f, 0.7f) + HairRules.Measure(thin, 4.5f, 0.95f, 0.04f).Core;
        }
        double gain = watch.Elapsed.TotalMilliseconds / 20000.0;
        watch.Restart();
        for (int i = 0; i < 2000; i++)
        {
            sum += HairRules.CoverScale(thin, 0.95f, 0.3f);
        }
        double keep = watch.Elapsed.TotalMilliseconds / 2000.0;
        Check(rule < 0.02 && sum > 0f, "hair timing: the rule for one layer takes under 0.02 ms (" + (rule * 1000.0).ToString("F2") + " microseconds)");
        Check(keep < 0.5, "hair timing: the scale that keeps one mip level's cover takes under 0.5 ms (" + (keep * 1000.0).ToString("F1") + " microseconds)");
        Check(gain < 0.2, "hair timing: the gain and the cover of the histogram of one texture take under 0.2 ms (" + (gain * 1000.0).ToString("F1") + " microseconds)");
    }
}
