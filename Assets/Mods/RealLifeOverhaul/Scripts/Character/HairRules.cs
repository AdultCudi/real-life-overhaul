using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The arithmetic of the hair finish: how bright a layer of hair can get under a light, and how solid its
    /// strands are. No Unity and no game types, so the offline checker compiles this very file (NavCheck).
    ///
    /// Why it exists. The game's hair is the stock HDRP 14 hair shader in its "cards" form (Kajiya-Kay). Read from
    /// its source (Runtime/Material/Hair/Hair.hlsl, EvaluateBSDF, and the HDRP/Hair graph), per unit of light
    /// arriving, a pixel of hair sends back:
    ///
    ///   lit from the front   colour / pi                                      like any matt surface
    ///   the highlight        0.25 x F x tint x (n + 2) / (2 pi)               F is Fresnel: 0.05 head on, 1 when the
    ///                        tint = _Specular x (0.7 white + 0.3 _SpecularColor)   light is behind the hair as seen
    ///                             + _SecondarySpecular x 0.5 x _SpecularColor      by the camera; n grows with gloss
    ///   light coming through _TransmissionColor x (S + _TransmissionRim x R)  S and R are 0 to 1: S for a light
    ///                                                                         behind the hair, R on every outline
    ///
    /// Nothing in the last two lines depends on the hair's own colour. Lit skin sends back about 0.16 (albedo 0.5
    /// over pi). The game sets the transmission colour to orange (1.0, 0.37, 0.11) on every hair material and the
    /// two highlights to 0.31 and 0.60 at a gloss of 0.58: with a lamp behind it, hair of any colour can send back
    /// 0.55 through and 1.06 in the highlight, three and six times lit skin, where black hair lit from the front
    /// sends back 0.004. From play distance, where the strands blur into one mass, the head is then a pale dome in
    /// the lamp's colour: bald. The rules here tie both to the hair's own colour and bound them.
    ///
    /// Cost: a few dozen multiplications per layer, when a look is built or the hair colour changes. Nothing per frame.
    /// </summary>
    internal static class HairRules
    {
        // ---------------------------------------------------------------- the shader's model

        public const float Pi = 3.14159265f;
        /// <summary>What lit light skin sends back per unit of light: albedo 0.5 over pi. The yardstick of every bound here.</summary>
        public const float SkinLit = 0.5f / Pi;
        /// <summary>Hair's reflectance head on (index of refraction 1.55), as the shader has it.</summary>
        public const float FresnelHeadOn = 0.0465f;
        /// <summary>The share of the first highlight that is white whatever the highlight colour: the graph mixes 0.7 white with 0.3 of the colour.</summary>
        public const float WhiteShare = 0.7f;
        /// <summary>The second highlight takes half the highlight colour, and no white.</summary>
        public const float SecondShare = 0.5f;

        // ---------------------------------------------------------------- the bounds

        /// <summary>
        /// Light coming through hair, as a share of what the same hair sends back lit from the front. A quarter: hair
        /// against a lamp is darker than the same hair in the lamp's light, as it is on a real head. (1 was tried in the
        /// preview: under a lamp strong enough to blow out skin, black hair still glowed brown from behind.)
        /// </summary>
        public const float ThroughShare = 0.25f;
        /// <summary>No hair, however pale, glows with a lamp behind it more than this share of lit skin.</summary>
        public const float ThroughCap = 0.5f;
        /// <summary>Hair this dark or darker (linear luma) is "dark hair": the bounds below are stated for it.</summary>
        public const float DarkLuma = 0.1f;
        /// <summary>Dark hair with a lamp behind it never glows more than this share of lit skin.</summary>
        public const float DarkThrough = 0.25f;
        /// <summary>
        /// The brightest highlight the darkest hair may have, at any angle of light, as a share of lit skin. Head on
        /// (Fresnel 0.05) that leaves a sheen about as bright as black hair itself; with the lamp 150 degrees round from
        /// the camera (Fresnel 0.26) a quarter of it. (0.12 and 0.35 were tried in the preview and looked alike against a lamp.)
        /// </summary>
        public const float DarkSheen = 0.2f;
        /// <summary>Paler hair may shine up to this many times its own front-lit brightness (0.3 of lit skin where dark hair ends)...</summary>
        public const float PaleSheen = 1.5f;
        /// <summary>...but never more than this share of lit skin.</summary>
        public const float SheenCap = 1f;
        /// <summary>Gloss is lowered to meet the bound, but not below this: under it a highlight no longer runs along the strands.</summary>
        public const float GlossFloor = 0.3f;
        /// <summary>A layer painted on the skull (a scalp cap) is matt: this share of the bound strands get.</summary>
        public const float CapSheen = 0.4f;

        // ---------------------------------------------------------------- coverage

        public const int Levels = 256;             // an alpha histogram has one bin per 8-bit value
        public const float BaseGain = 3f;          // strand alpha is multiplied by at least this
        public const float MaxGain = 6f;           // and never by more: beyond it wisps turn into bars
        public const float GainStep = 0.25f;
        /// <summary>Of the strands a texture draws, this share must be solid after the gain: below it a layer is mostly haze, and haze lets skin through.</summary>
        public const float WantedCore = 0.7f;

        /// <summary>What a strand texture is made of once its alpha is multiplied: shares of its area, summing to 1.</summary>
        public struct Cover
        {
            public float Core;    // solid: writes depth, hides what is behind it
            public float Edge;    // drawn but see-through
            public float Empty;   // not drawn at all
            public float Gain;
        }

        /// <summary>What a layer is given, in the shader's own terms (colours linear).</summary>
        public struct Finish
        {
            public float ThroughR, ThroughG, ThroughB;   // _TransmissionColor
            public float Rim;                             // _TransmissionRim
            public float Specular, Secondary;             // _Specular, _SecondarySpecular
            public float GlossMin, GlossMax;              // _SmoothnessMin, _SmoothnessMax
            public float SheenPeak;                       // the brightest the highlight gets, per unit of light
            public float ThroughPeak;                     // the brightest the light coming through gets
            public float FrontLit;                        // what the hair sends back lit from the front
        }

        // ---------------------------------------------------------------- colour

        public static float ToLinear(float srgb)
        {
            if (!(srgb > 0f))
            {
                return 0f;   // also NaN
            }
            if (srgb >= 1f)
            {
                return 1f;
            }
            return srgb <= 0.04045f ? srgb / 12.92f : (float)Math.Pow((srgb + 0.055f) / 1.055f, 2.4);
        }

        public static float ToSrgb(float linear)
        {
            if (!(linear > 0f))
            {
                return 0f;
            }
            if (linear >= 1f)
            {
                return 1f;
            }
            return linear <= 0.0031308f ? linear * 12.92f : 1.055f * (float)Math.Pow(linear, 1.0 / 2.4) - 0.055f;
        }

        /// <summary>Brightness of a linear colour (Rec. 709).</summary>
        public static float Luma(float r, float g, float b)
        {
            return 0.2126f * Clamp01(r) + 0.7152f * Clamp01(g) + 0.0722f * Clamp01(b);
        }

        // ---------------------------------------------------------------- the model, as the shader computes it

        /// <summary>Schlick's Fresnel for hair, from the cosine of half the angle between the light and the camera.</summary>
        public static float Fresnel(float cosHalf)
        {
            float x = 1f - Clamp01(cosHalf);
            float x2 = x * x;
            return FresnelHeadOn + (1f - FresnelHeadOn) * x2 * x2 * x;
        }

        /// <summary>The height of a highlight of this gloss: (n + 2) / (2 pi), n being the shader's exponent 2 / roughness^2 - 2.</summary>
        public static float Sharpness(float gloss)
        {
            float rough = 1f - Clamp01(gloss);
            float r4 = rough * rough * rough * rough;
            return 1f / (Pi * Math.Max(r4, 1e-6f));
        }

        /// <summary>Both highlights' strength together, for a highlight colour of this brightness.</summary>
        public static float Tint(float specular, float secondary, float highlightLuma)
        {
            float luma = Clamp01(highlightLuma);
            return Math.Max(0f, specular) * (WhiteShare + (1f - WhiteShare) * luma) + Math.Max(0f, secondary) * SecondShare * luma;
        }

        /// <summary>The brightest the highlight gets per unit of light, at this Fresnel (1 = the worst angle there is).</summary>
        public static float SheenPeak(float specular, float secondary, float highlightLuma, float gloss, float fresnel)
        {
            return 0.25f * Clamp01(fresnel) * Tint(specular, secondary, highlightLuma) * Sharpness(gloss);
        }

        /// <summary>The brightest light coming through gets per unit of light: the back-light term and the outline term at their most.</summary>
        public static float ThroughPeak(float throughLuma, float rim)
        {
            return Math.Max(0f, throughLuma) * (1f + Math.Max(0f, rim));
        }

        /// <summary>What a matt surface of this brightness sends back lit from the front.</summary>
        public static float FrontLit(float luma)
        {
            return Clamp01(luma) / Pi;
        }

        /// <summary>The bound on the light coming through hair of this brightness.</summary>
        public static float ThroughLimit(float hairLuma)
        {
            return (hairLuma <= DarkLuma ? DarkThrough : ThroughCap) * SkinLit;
        }

        /// <summary>The bound on a layer's highlight for hair of this brightness.</summary>
        public static float SheenLimit(float hairLuma, bool cap)
        {
            float limit = Math.Max(DarkSheen * SkinLit, Math.Min(SheenCap * SkinLit, PaleSheen * FrontLit(hairLuma)));
            return cap ? limit * CapSheen : limit;
        }

        // ---------------------------------------------------------------- the rules

        /// <summary>
        /// Everything that depends on the hair colour, for one layer. <paramref name="r"/>, g, b: the hair colour, linear.
        /// The game's own numbers of the layer come in (its two highlight strengths, its gloss range, its outline glow);
        /// <paramref name="highlightLuma"/> is the brightness of the highlight colour the layer will carry.
        ///
        /// Light through the hair takes the hair's own colour at <see cref="ThroughShare"/> of its front-lit brightness,
        /// scaled down if that exceeds <see cref="ThroughLimit"/>; a cap on the skull lets nothing through.
        /// The highlight keeps the game's strength where it fits under <see cref="SheenLimit"/>; where it does not, the
        /// gloss comes down first (a broader, lower highlight) and, at <see cref="GlossFloor"/>, the strength.
        /// </summary>
        public static Finish Rule(float r, float g, float b, float specular, float secondary, float glossMin, float glossMax,
            float rim, float highlightLuma, bool cap)
        {
            r = Clamp01(r);
            g = Clamp01(g);
            b = Clamp01(b);
            var finish = new Finish();
            float luma = Luma(r, g, b);
            finish.FrontLit = FrontLit(luma);

            // Light through the hair.
            finish.Rim = cap ? 0f : Clamp(rim, 0f, 1f);
            if (!cap)
            {
                float share = ThroughShare / Pi;
                float peak = ThroughPeak(luma * share, finish.Rim);
                float allowed = ThroughLimit(luma);
                if (peak > allowed)
                {
                    share *= allowed / peak;
                }
                finish.ThroughR = r * share;
                finish.ThroughG = g * share;
                finish.ThroughB = b * share;
            }
            finish.ThroughPeak = ThroughPeak(Luma(finish.ThroughR, finish.ThroughG, finish.ThroughB), finish.Rim);

            // The highlight.
            specular = Clamp(specular, 0f, 4f);
            secondary = Clamp(secondary, 0f, 4f);
            float low = Clamp01(Math.Min(glossMin, glossMax)), high = Clamp01(Math.Max(glossMin, glossMax));
            float limit = SheenLimit(luma, cap);
            float tint = Tint(specular, secondary, highlightLuma);
            if (tint > 0f && SheenPeak(specular, secondary, highlightLuma, high, 1f) > limit)
            {
                // The gloss at which the peak is exactly the limit: 0.25 x tint / (pi x (1 - gloss)^4) = limit.
                float rough = (float)Math.Pow(0.25f * tint / (Pi * limit), 0.25);
                float fits = 1f - rough;
                float floor = Math.Min(high, GlossFloor);
                if (fits >= floor)
                {
                    high = fits;
                }
                else
                {
                    high = floor;
                    float scale = limit / SheenPeak(specular, secondary, highlightLuma, high, 1f);
                    specular *= scale;
                    secondary *= scale;
                }
            }
            finish.Specular = specular;
            finish.Secondary = secondary;
            finish.GlossMax = high;
            finish.GlossMin = Math.Min(low, high);
            finish.SheenPeak = SheenPeak(specular, secondary, highlightLuma, high, 1f);
            return finish;
        }

        // ---------------------------------------------------------------- coverage

        /// <summary>
        /// What a strand texture is made of at this gain. <paramref name="histogram"/>: how many texels have each
        /// 8-bit alpha (<see cref="Levels"/> bins). Alpha times gain at or above <paramref name="coreAlpha"/> is solid,
        /// below <paramref name="edgeAlpha"/> it is not drawn.
        /// </summary>
        public static Cover Measure(int[] histogram, float gain, float coreAlpha, float edgeAlpha)
        {
            var cover = new Cover { Gain = gain, Empty = 1f };
            if (histogram == null || histogram.Length < Levels)
            {
                return cover;
            }
            long all = 0, core = 0, edge = 0;
            for (int a = 0; a < Levels; a++)
            {
                int count = histogram[a] > 0 ? histogram[a] : 0;
                all += count;
                float alpha = Math.Min(1f, a * gain / (Levels - 1));
                if (alpha >= coreAlpha)
                {
                    core += count;
                }
                else if (alpha >= edgeAlpha && alpha > 0f)
                {
                    edge += count;
                }
            }
            if (all == 0)
            {
                return cover;
            }
            cover.Core = (float)((double)core / all);
            cover.Edge = (float)((double)edge / all);
            cover.Empty = Math.Max(0f, 1f - cover.Core - cover.Edge);
            return cover;
        }

        /// <summary>
        /// The share of a texture's strands that is solid at this gain. "Its strands" are the texels drawn at
        /// <see cref="BaseGain"/>: a fixed count, so the share never falls as the gain rises.
        /// </summary>
        public static float SolidShare(int[] histogram, float gain, float coreAlpha, float edgeAlpha)
        {
            if (histogram == null || histogram.Length < Levels)
            {
                return 0f;
            }
            long strands = 0, core = 0;
            for (int a = 1; a < Levels; a++)
            {
                int count = histogram[a] > 0 ? histogram[a] : 0;
                if (Math.Min(1f, a * BaseGain / (Levels - 1)) >= edgeAlpha)
                {
                    strands += count;
                    if (Math.Min(1f, a * gain / (Levels - 1)) >= coreAlpha)
                    {
                        core += count;
                    }
                }
            }
            return strands == 0 ? 1f : (float)((double)core / strands);
        }

        /// <summary>
        /// The gain a layer's strand texture gets: the smallest, from <see cref="BaseGain"/> up in steps of
        /// <see cref="GainStep"/>, at which <paramref name="wanted"/> of its strands is solid; <see cref="MaxGain"/>
        /// if none does it. A texture with nothing drawn on it gets the base gain.
        /// </summary>
        public static float Gain(int[] histogram, float coreAlpha, float edgeAlpha, float wanted)
        {
            if (histogram == null || histogram.Length < Levels || !(coreAlpha > 0f))
            {
                return BaseGain;
            }
            int steps = (int)Math.Round((MaxGain - BaseGain) / GainStep);
            for (int i = 0; i <= steps; i++)
            {
                float gain = BaseGain + i * GainStep;
                if (SolidShare(histogram, gain, coreAlpha, edgeAlpha) >= wanted)
                {
                    return gain;
                }
            }
            return MaxGain;
        }

        /// <summary>Whether a layer is painted on the skull rather than made of strands: the game names such materials "Scalp".</summary>
        public static bool IsCap(string materialName)
        {
            return materialName != null && materialName.IndexOf("Scalp", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ---------------------------------------------------------------- small things

        public static float Clamp01(float value)
        {
            return value > 0f ? (value < 1f ? value : 1f) : 0f;   // NaN comes out as 0
        }

        private static float Clamp(float value, float low, float high)
        {
            return value > low ? (value < high ? value : high) : low;
        }
    }
}
