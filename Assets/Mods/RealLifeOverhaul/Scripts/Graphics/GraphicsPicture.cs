using System;

namespace RealLifeOverhaul.Gfx
{
    /// <summary>
    /// One piece of the render pipeline's custom tone curve: a power curve, moved and scaled. Three of them (the toe, the
    /// straight middle, the shoulder) meet with the same height and slope where they join.
    /// </summary>
    public struct ToneSegment
    {
        public float OffsetX, OffsetY, ScaleX, ScaleY, LnA, B;

        public float At(float x)
        {
            float v = (x - OffsetX) * ScaleX;
            float y = v > 0f ? MathF.Exp(LnA + B * MathF.Log(v)) : 0f;
            return y * ScaleY + OffsetY;
        }
    }

    /// <summary>
    /// The render pipeline's custom tone curve as it builds it from the six sliders of its Tonemapping setting (toe
    /// strength and length, shoulder strength, length and angle, gamma): what a look's sliders make of a brightness is
    /// worked out here exactly as the game's graphics card will, so the looks can be checked with no game running.
    /// A value, made once per look; nothing of it runs in a frame.
    /// </summary>
    public struct ToneCurve
    {
        private const float Tiny = 1e-5f;   // the pipeline's own floor for a strength, a height and gamma

        public ToneSegment Toe, Middle, Shoulder;
        public float White;     // the brightness that becomes white; anything over it is white too
        public float ToeEnd;    // where the toe hands over to the middle, as a share of White
        public float MiddleEnd; // where the middle hands over to the shoulder

        public static ToneCurve Make(float toeStrength, float toeLength, float shoulderStrength, float shoulderLength, float shoulderAngle, float gamma)
        {
            toeLength = MathF.Pow(Clamp01(toeLength), 2.2f);
            toeStrength = Clamp01(toeStrength);
            shoulderAngle = Clamp01(shoulderAngle);
            shoulderStrength = Math.Max(Tiny, Math.Min(0.99999f, shoulderStrength));
            shoulderLength = Math.Max(0f, shoulderLength);
            gamma = Math.Max(Tiny, gamma);

            float x0 = toeLength * 0.5f;
            float y0 = (1f - toeStrength) * x0;
            float left = 1f - y0;
            float rise = (1f - shoulderStrength) * left;
            float x1 = x0 + rise;
            float y1 = y0 + rise;
            float white = x0 + left + MathF.Pow(2f, shoulderLength) - 1f;
            float overX = 2f * shoulderAngle * shoulderLength;   // how far past white the shoulder is aimed, as a share of White
            float overY = 0.5f * shoulderAngle * shoulderLength;
            x0 /= white;
            x1 /= white;

            float run = x1 - x0;
            float slope = run == 0f ? 1f : (y1 - y0) / run;
            float lift = y0 - x0 * slope;
            var curve = new ToneCurve { White = white, ToeEnd = x0, MiddleEnd = x1 };
            curve.Middle = new ToneSegment { OffsetX = -lift / slope, OffsetY = 0f, ScaleX = 1f, ScaleY = 1f, LnA = gamma * MathF.Log(slope), B = gamma };
            float toeSlope = gamma * slope * MathF.Pow(slope * x0 + lift, gamma - 1f);
            float shoulderSlope = gamma * slope * MathF.Pow(slope * x1 + lift, gamma - 1f);
            y0 = Math.Max(Tiny, MathF.Pow(y0, gamma));
            y1 = Math.Max(Tiny, MathF.Pow(y1, gamma));
            overY = MathF.Pow(1f + overY, gamma) - 1f;

            float b = toeSlope * x0 / y0;
            curve.Toe = new ToneSegment { OffsetX = 0f, OffsetY = 0f, ScaleX = 1f, ScaleY = 1f, LnA = MathF.Log(y0) - b * MathF.Log(x0), B = b };
            float reachX = 1f + overX - x1, reachY = 1f + overY - y1;
            b = shoulderSlope * reachX / reachY;
            curve.Shoulder = new ToneSegment { OffsetX = 1f + overX, OffsetY = 1f + overY, ScaleX = -1f, ScaleY = -1f, LnA = MathF.Log(reachY) - b * MathF.Log(reachX), B = b };

            // The pipeline scales all three so that the brightness White comes out at exactly 1.
            float scale = 1f / curve.Shoulder.At(1f);
            curve.Toe.OffsetY *= scale;
            curve.Toe.ScaleY *= scale;
            curve.Middle.OffsetY *= scale;
            curve.Middle.ScaleY *= scale;
            curve.Shoulder.OffsetY *= scale;
            curve.Shoulder.ScaleY *= scale;
            return curve;
        }

        /// <summary>What a brightness of the frame becomes, 0 to 1 on the screen before its encoding.</summary>
        public float At(float brightness)
        {
            float x = Math.Max(0f, brightness) / White;
            float y = x < ToeEnd ? Toe.At(x) : x < MiddleEnd ? Middle.At(x) : Shoulder.At(x);
            return Math.Max(0f, Math.Min(1f, y));
        }

        private static float Clamp01(float v) => Math.Max(0f, Math.Min(1f, v));
    }

    /// <summary>
    /// The arithmetic of the picture, game-free (checked by EconomyCheck): the curve the game ships with, the screen's
    /// encoding, the colour of a light of a given temperature, what the pipeline's white balance and its contrast do to a
    /// grey, and the matrix that strengthens colour without moving a grey. The rules build the looks from it and the
    /// checker follows a grey card through the game's picture and through each look's. Nothing here runs in a frame.
    /// </summary>
    public static class GraphicsPicture
    {
        // The luminance weights of the wide colour space the game's pipeline grades in (its global settings say ACEScg).
        public const float LumaRed = 0.2722287f, LumaGreen = 0.6740818f, LumaBlue = 0.0536895f;

        // The luminance weights of the screen's own primaries, which the pipeline's split toning uses.
        public const float ScreenRed = 0.2126729f, ScreenGreen = 0.7151522f, ScreenBlue = 0.072175f;

        public const float MidGrey = 0.18f;          // the grey card's reflectance, and the pivot of the pipeline's contrast
        public const float ContrastPivot = 0.4135884f;   // that pivot in the pipeline's log encoding

        /// <summary>
        /// The game's own tone curve for a grey: the pipeline's fitted ACES curve, which tops out a little under white
        /// (0.949), so the game's whites are never quite white.
        /// </summary>
        public static float GameCurve(float brightness)
        {
            float x = Math.Max(0f, brightness);
            return x * (278.5085f * x + 10.7772f) / (x * (293.6045f * x + 88.7122f) + 80.6889f);
        }

        /// <summary>The screen's encoding of a linear value, 0 to 1 (sRGB).</summary>
        public static float Screen(float linear)
        {
            float x = Math.Max(0f, Math.Min(1f, linear));
            return x <= 0.0031308f ? 12.92f * x : 1.055f * MathF.Pow(x, 1f / 2.4f) - 0.055f;
        }

        /// <summary>The encoding a light's colour is kept in by the engine, for a linear multiplier of 0 to 1.</summary>
        public static float LightEncoding(float linear) => Screen(linear);

        /// <summary>
        /// The pipeline's contrast on a grey, in its two grading modes: with the game's own curve the pivot is the grey
        /// card itself; with a custom curve the contrast is done in the camera's log encoding, whose pivot sits a little
        /// higher (0.225). <paramref name="contrast"/> is the option's figure, -100 to 100.
        /// </summary>
        public static float Contrast(float brightness, float contrast, bool customCurve)
        {
            float x = Math.Max(0f, brightness);
            float c = 1f + contrast / 100f;
            if (c == 1f || x <= 0f)
            {
                return x;
            }
            if (!customCurve)
            {
                return MathF.Pow(x, c) * MathF.Pow(MidGrey, 1f - c);
            }
            const float A = 5.555556f, B = 0.047996f, C = 0.244161f, D = 0.386036f;   // the camera log's constants
            float log = C * MathF.Log10(A * x + B) + D;
            log = (log - ContrastPivot) * c + ContrastPivot;
            return Math.Max(0f, (MathF.Pow(10f, (log - D) / C) - B) / A);
        }

        /// <summary>
        /// The colour of a glowing body at <paramref name="kelvin"/>, linear, its largest part 1 (the Planckian locus by
        /// Kang's fit, good from 1667 K to 25000 K). Only ever used as a ratio of two such colours, so what the fit is out
        /// by cancels.
        /// </summary>
        public static void Kelvin(float kelvin, out float r, out float g, out float b)
        {
            double k = Math.Max(1667.0, Math.Min(25000.0, kelvin));
            double x = k <= 4000.0
                ? -0.2661239e9 / (k * k * k) - 0.2343589e6 / (k * k) + 0.8776956e3 / k + 0.179910
                : -3.0258469e9 / (k * k * k) + 2.1070379e6 / (k * k) + 0.2226347e3 / k + 0.240390;
            double y = k <= 2222.0 ? -1.1063814 * x * x * x - 1.34811020 * x * x + 2.18555832 * x - 0.20219683
                : k <= 4000.0 ? -0.9549476 * x * x * x - 1.37418593 * x * x + 2.09137015 * x - 0.16748867
                : 3.0817580 * x * x * x - 5.87338670 * x * x + 3.75112997 * x - 0.37001483;
            double bigX = x / y, bigZ = (1.0 - x - y) / y;
            double red = 3.2404542 * bigX - 1.5371385 - 0.4985314 * bigZ;
            double green = -0.9692660 * bigX + 1.8760108 + 0.0415560 * bigZ;
            double blue = 0.0556434 * bigX - 0.2040259 + 1.0572252 * bigZ;
            double most = Math.Max(red, Math.Max(green, blue));
            r = (float)Math.Max(0.0, red / most);
            g = (float)Math.Max(0.0, green / most);
            b = (float)Math.Max(0.0, blue / most);
        }

        /// <summary>
        /// The filter that makes a light of the game's <paramref name="fromKelvin"/> shine as one of
        /// <paramref name="toKelvin"/>: the ratio of the two colours, its largest part 1, so the light is never made
        /// brighter than the game has it.
        /// </summary>
        public static void Filter(float toKelvin, float fromKelvin, out float r, out float g, out float b)
        {
            Kelvin(toKelvin, out float tr, out float tg, out float tb);
            Kelvin(fromKelvin, out float fr, out float fg, out float fb);
            r = tr / Math.Max(0.0001f, fr);
            g = tg / Math.Max(0.0001f, fg);
            b = tb / Math.Max(0.0001f, fb);
            float most = Math.Max(r, Math.Max(g, b));
            r /= most;
            g /= most;
            b /= most;
        }

        /// <summary>How bright a colour is on the screen's own weights.</summary>
        public static float Luminance(float r, float g, float b) => ScreenRed * r + ScreenGreen * g + ScreenBlue * b;

        /// <summary>
        /// What the pipeline's white balance does to a grey's brightness at a temperature setting (-100 to 100, warmer
        /// above 0): it scales the eye's three cone responses, which moves a grey's luminance a little.
        /// </summary>
        public static float BalanceGain(float temperature)
        {
            float t = temperature / 65f;   // the pipeline's own scale
            float x = 0.31271f - t * (t < 0f ? 0.1f : 0.05f);
            float y = 2.87f * x - 3f * x * x - 0.27509507f;
            float bigX = x / y, bigZ = (1f - x - y) / y;
            float l = 0.949237f / (0.7328f * bigX + 0.4296f - 0.1624f * bigZ);
            float m = 1.03542f / (-0.7036f * bigX + 1.6975f + 0.0061f * bigZ);
            float s = 1.08728f / (0.0030f * bigX + 0.0136f + 0.9834f * bigZ);
            l *= 0.949272f;   // a grey's own cone responses
            m *= 1.035371f;
            s *= 1.087374f;
            float r = 2.85847f * l - 1.62879f * m - 0.0248910f * s;
            float g = -0.210182f * l + 1.15820f * m + 0.000324281f * s;
            float b = -0.0418120f * l - 0.118169f * m + 1.06867f * s;
            return Luminance(r, g, b);
        }

        /// <summary>
        /// One row of the channel mixer that strengthens colour by <paramref name="strength"/> (1 leaves it): each output is
        /// its own input pushed away from the picture's luminance. Rows add up to 1, so a grey stays the grey it was.
        /// In percent, as the pipeline's mixer takes it.
        /// </summary>
        public static void MixerRow(int row, float strength, out float fromRed, out float fromGreen, out float fromBlue)
        {
            float spare = 1f - strength;
            fromRed = LumaRed * spare * 100f;
            fromGreen = LumaGreen * spare * 100f;
            fromBlue = LumaBlue * spare * 100f;
            if (row == 0)
            {
                fromRed += strength * 100f;
            }
            else if (row == 1)
            {
                fromGreen += strength * 100f;
            }
            else
            {
                fromBlue += strength * 100f;
            }
        }

        /// <summary>
        /// The green of a split-toning tint whose red and blue are given, chosen so that the tint is as bright as the
        /// neutral grey it replaces: the toning then colours the shadows or the highlights and leaves their brightness.
        /// </summary>
        public static float ToningGreen(float red, float blue)
        {
            return (0.5f - ScreenRed * red - ScreenBlue * blue) / ScreenGreen;
        }
    }
}
