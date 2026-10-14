using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How one layer of the game's strand hair is finished. Game-free on purpose: the HDRP preview
    /// (CharacterAssets/PreviewProject) compiles this very file, so what is judged there is what runs here.
    ///
    /// The game draws detailed hair as alpha-blended strips on the stock HDRP hair shader with the depth
    /// prepass off, no strand maps and one flat colour. Four things follow: the scalp shows through every
    /// strip (thin, even bald, from play distance), strips sort wrongly against each other, the whole
    /// head carries one even highlight (the "wig" look), and with a lamp behind or beside the head the hair
    /// glows in the lamp's colour, as bright as lit skin or brighter, whatever colour the hair is (the game
    /// gives every hair material the same orange back-light and the same strong white highlight). A finished
    /// layer is a copy of the game's material:
    ///
    ///  - with the keyword set of an alpha-tested hair material the game itself ships (the female styles'),
    ///    so the compiled shader variant is certain to exist, and the depth prepass switched on: the core of
    ///    every strand writes depth, so hair hides what is behind it and sorts correctly, while the soft
    ///    edge of each strand still blends;
    ///  - with a copy of its strand texture whose alpha is multiplied until most of every strand is solid
    ///    (<see cref="HairRules.Gain"/>, measured once from the texture itself), whose colour is darker at the faint
    ///    edge of each strand (<see cref="HairRules.Shade"/>: the game's strand textures are plain white, so under one
    ///    strong lamp, with nothing of the hair casting a shadow, the crown was one smooth patch, read as a bald scalp),
    ///    and whose mipmaps keep as much of the strands solid as the full size has (<see cref="HairRules.CoverScale"/>:
    ///    plain mipmaps melt the cores into haze with distance, and the skull shows through);
    ///  - with strand maps the game leaves empty: a smoothness mask and a normal map of fine streaks, which
    ///    break the highlight up into strands that run along the hair;
    ///  - with back-light and highlight that follow the hair colour and are bounded against lit skin
    ///    (<see cref="HairRules.Rule"/>): black hair stays black with a lamp behind it.
    ///
    /// Cost: one extra depth-only draw per layer, for the characters it is applied to. Nothing per frame. The first wear
    /// of a strand texture in a scene: one read-back and one pass over its texels and mipmaps on the CPU.
    /// </summary>
    internal static class HairFinish
    {
        public const string HairShader = "HDRP/Hair";
        public const string AlphaTestKeyword = "_ALPHATEST_ON";
        public const string TransparentKeyword = "_SURFACE_TYPE_TRANSPARENT";
        public const string DepthPrepass = "TransparentDepthPrepass";

        private const float UnderlayGloss = 0.04f;       // the scalp underlay's two highlight strengths: all but matt
        private const float UnderlayGlossMin = 0.15f;    // and its gloss range
        private const float UnderlayGlossMax = 0.25f;
        private const int UnderlayQueue = 2998;          // before every layer of the game's hair (3000 and up)
        private const float UnderlaySort = -2f;

        /// <summary>What a finish does; the defaults are the values chosen in the preview.</summary>
        public struct Settings
        {
            /// <summary>
            /// Strand alpha (after the gain) above this writes depth: the solid core. 0 = no depth prepass.
            /// Kept just under 1 on purpose: a core hides all hair behind it, so whatever it lets through is skin.
            /// (0.5 to 0.8 were tried in the preview: dark hair turned the colour of the scalp.)
            /// </summary>
            public float CoreAlpha;
            /// <summary>Strand alpha below this is not drawn at all (removes the faint haze around strands).</summary>
            public float EdgeAlpha;
            /// <summary>How many times the strand mask repeats across and along the hair texture.</summary>
            public Vector2 StrandTiling;
            public float NormalStrength;
            /// <summary>0 = the game's gloss, 1 = fully tamed.</summary>
            public float Calm;

            /// <summary>
            /// Above 0: the layer gets a copy of its strand texture with alpha multiplied by at least this (<see cref="Denser"/>),
            /// so the middle of each strand is solid. Needed with the prepass: a strand that hides the hair behind it must not
            /// itself be half see-through, or skin shows through it. (Raising alpha through _BaseColor instead was tried
            /// in the preview: the shader does not clamp it, and the blend overshoots into coloured fringes.)
            /// </summary>
            public float AlphaGain;
            /// <summary>
            /// The share of a texture's strands that must be solid; the gain is raised from <see cref="AlphaGain"/> until
            /// it is (<see cref="HairRules.Gain"/>). 0 = the same gain for every texture, as before 11 October.
            /// </summary>
            public float WantedCore;
            /// <summary>
            /// Back-light and highlight bounded against lit skin (<see cref="HairRules.Rule"/>). Off = as before
            /// 11 October: back-light the hair colour lifted a quarter towards white, the highlight unbounded.
            /// </summary>
            public bool Bounded;
            /// <summary>
            /// Under 1: the copy's colour is darkened towards this at the faint edge of every strand (<see cref="HairRules.Shade"/>).
            /// 1 = the game's colour throughout, as before 13 October (white: hair under a strong lamp was one smooth patch).
            /// </summary>
            public float Deep;
            /// <summary>
            /// The copy's mipmaps keep the share of the strands that is solid (<see cref="HairRules.CoverScale"/>). Off = plain
            /// mipmaps, as before 13 October: with distance the strand cores melt into haze and the skull shows through.
            /// </summary>
            public bool KeepCover;

            public static Settings Default => new Settings
            {
                CoreAlpha = 0.95f, EdgeAlpha = 0.04f, StrandTiling = new Vector2(3f, 1f), NormalStrength = 0.6f, Calm = 1f,
                AlphaGain = HairRules.BaseGain, WantedCore = HairRules.WantedCore, Bounded = true,
                Deep = HairRules.DeepShade, KeepCover = true
            };
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");
        private static readonly int SmoothnessMaskId = Shader.PropertyToID("_SmoothnessMask");
        private static readonly int SmoothnessTilingId = Shader.PropertyToID("_uvSmoothnessST");
        private static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");
        private static readonly int NormalScaleId = Shader.PropertyToID("_NormalScale");
        private static readonly int SmoothnessMinId = Shader.PropertyToID("_SmoothnessMin");
        private static readonly int SmoothnessMaxId = Shader.PropertyToID("_SmoothnessMax");
        private static readonly int SpecularId = Shader.PropertyToID("_Specular");
        private static readonly int SecondarySpecularId = Shader.PropertyToID("_SecondarySpecular");
        private static readonly int SpecularColorId = Shader.PropertyToID("_SpecularColor");
        private static readonly int TransmissionColorId = Shader.PropertyToID("_TransmissionColor");
        private static readonly int TransmissionRimId = Shader.PropertyToID("_TransmissionRim");
        private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClipThreshold");
        private static readonly int AlphaCutoffEnableId = Shader.PropertyToID("_AlphaCutoffEnable");
        private static readonly int PrepassEnableId = Shader.PropertyToID("_TransparentDepthPrepassEnable");
        private static readonly int PrepassClipId = Shader.PropertyToID("_AlphaClipThresholdDepthPrepass");
        private static readonly int SortPriorityId = Shader.PropertyToID("_TransparentSortPriority");

        private static readonly int[] Histogram = new int[HairRules.Levels];   // filled and read within one call

        public static bool IsHair(Material material)
        {
            return material != null && material.shader != null && material.shader.name == HairShader;
        }

        /// <summary>A see-through, alpha-tested hair material: the kind whose variant a finished layer borrows.</summary>
        public static bool IsTemplate(Material material)
        {
            return IsHair(material) && material.IsKeywordEnabled(AlphaTestKeyword) && material.IsKeywordEnabled(TransparentKeyword);
        }

        /// <summary>Whether a layer is painted on the skull (the game's "Scalp" materials and textures) rather than made of strands.</summary>
        public static bool IsCap(Material layer)
        {
            if (layer == null)
            {
                return false;
            }
            Texture map = layer.HasProperty(BaseColorMapId) ? layer.GetTexture(BaseColorMapId) : null;
            return HairRules.IsCap(layer.name) || (map != null && HairRules.IsCap(map.name));
        }

        /// <summary>
        /// A finished copy of <paramref name="layer"/>. The caller owns it (and destroys it), and keeps writing
        /// the hair colour to it through <see cref="Colour"/>. Null if the layer or the template is not what
        /// this was written for; the caller then leaves the game's material alone.
        ///
        /// A see-through layer gets all of the finish. A solid one (the game has one: the opaque scalp of one
        /// style) keeps its keywords, queue and passes exactly, and gets only the bounded back-light and highlight.
        /// </summary>
        public static Material Make(Material layer, Material template, Texture strandMask, Texture strandNormal, Settings settings)
        {
            if (!IsHair(layer) || !IsTemplate(template))
            {
                return null;
            }
            var finished = new Material(layer) { name = layer.name + " (RealLifeOverhaul finished)" };
            if (layer.IsKeywordEnabled(TransparentKeyword))
            {
                // Exactly the keyword set of a material in the build: no variant is asked for that the game lacks.
                finished.shaderKeywords = template.shaderKeywords;
                finished.renderQueue = layer.renderQueue;

                finished.SetFloat(AlphaCutoffEnableId, 1f);
                finished.SetFloat(AlphaClipId, settings.EdgeAlpha);
                bool prepass = settings.CoreAlpha > 0f;
                finished.SetFloat(PrepassEnableId, prepass ? 1f : 0f);
                finished.SetFloat(PrepassClipId, prepass ? settings.CoreAlpha : 1f);
                finished.SetShaderPassEnabled(DepthPrepass, prepass);

                if (strandMask != null)
                {
                    finished.SetTexture(SmoothnessMaskId, strandMask);
                    finished.SetVector(SmoothnessTilingId, new Vector4(settings.StrandTiling.x, settings.StrandTiling.y, 0f, 0f));
                }
                // A style that brings its own normal map keeps it.
                if (strandNormal != null && finished.GetTexture(NormalMapId) == null)
                {
                    finished.SetTexture(NormalMapId, strandNormal);
                    finished.SetFloat(NormalScaleId, settings.NormalStrength);
                }
            }
            Colour(finished, layer, finished.GetColor(BaseColorId), strandMask != null && layer.IsKeywordEnabled(TransparentKeyword), settings);
            return finished;
        }

        /// <summary>
        /// A copy of a strand texture with its alpha multiplied, so the middle of every strand is solid and only
        /// its edge is soft. Read back from the GPU once (the game's textures are not readable); the caller owns it.
        /// The gain is the texture's own (<see cref="HairRules.Gain"/>, from the alpha histogram of the pixels
        /// already in hand) unless the settings ask for one gain for all; <paramref name="cover"/> says what came of it.
        /// </summary>
        public static Texture2D Denser(Texture source, Settings settings, out HairRules.Cover cover)
        {
            int width = source.width, height = source.height;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            var copy = new Texture2D(width, height, TextureFormat.RGBA32, true, true) { name = source.name + " (RealLifeOverhaul denser)" };
            copy.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Color32[] pixels = copy.GetPixels32();
            System.Array.Clear(Histogram, 0, Histogram.Length);
            for (int i = 0; i < pixels.Length; i++)
            {
                Histogram[pixels[i].a]++;
            }
            float gain = settings.WantedCore > 0f
                ? Mathf.Max(settings.AlphaGain, HairRules.Gain(Histogram, settings.CoreAlpha, settings.EdgeAlpha, settings.WantedCore))
                : settings.AlphaGain;
            cover = HairRules.Measure(Histogram, gain, settings.CoreAlpha, settings.EdgeAlpha);

            // Level 0: alpha multiplied, colour shaded by how solid the strand is as painted.
            var alpha = new float[pixels.Length];   // each level's alpha as averaged, before any keeping of the cover
            double drawn = 0, shaded = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                float painted = pixels[i].a / 255f;
                float a = Mathf.Min(1f, painted * gain);
                float shade = HairRules.Shade(painted, settings.Deep);
                pixels[i].r = (byte)(pixels[i].r * shade + 0.5f);
                pixels[i].g = (byte)(pixels[i].g * shade + 0.5f);
                pixels[i].b = (byte)(pixels[i].b * shade + 0.5f);
                pixels[i].a = (byte)(a * 255f + 0.5f);
                alpha[i] = a;
                drawn += a;
                shaded += a * shade;
            }
            cover.Shade = drawn > 0 ? (float)(shaded / drawn) : 1f;
            cover.FarScale = 1f;
            copy.SetPixels32(pixels, 0);

            // The mipmaps, made here rather than by the engine: colour averaged by how much of each texel is drawn (so
            // the empty space between strands lends the hair none of its colour), alpha averaged and then, with
            // KeepCover, multiplied until as much of the level is solid as of the full size.
            int w = width, h = height;
            for (int level = 1; level < copy.mipmapCount; level++)
            {
                int cw = Mathf.Max(1, w >> 1), ch = Mathf.Max(1, h >> 1);
                var next = new Color32[cw * ch];
                var nextAlpha = new float[cw * ch];
                System.Array.Clear(Histogram, 0, Histogram.Length);
                for (int y = 0; y < ch; y++)
                {
                    int y0 = Mathf.Min(2 * y, h - 1), y1 = Mathf.Min(2 * y + 1, h - 1);
                    for (int x = 0; x < cw; x++)
                    {
                        int x0 = Mathf.Min(2 * x, w - 1), x1 = Mathf.Min(2 * x + 1, w - 1);
                        int i00 = y0 * w + x0, i01 = y0 * w + x1, i10 = y1 * w + x0, i11 = y1 * w + x1;
                        float a00 = alpha[i00], a01 = alpha[i01], a10 = alpha[i10], a11 = alpha[i11];
                        float sum = a00 + a01 + a10 + a11;
                        float r, g, b;
                        if (sum > 1e-5f)
                        {
                            r = (pixels[i00].r * a00 + pixels[i01].r * a01 + pixels[i10].r * a10 + pixels[i11].r * a11) / sum;
                            g = (pixels[i00].g * a00 + pixels[i01].g * a01 + pixels[i10].g * a10 + pixels[i11].g * a11) / sum;
                            b = (pixels[i00].b * a00 + pixels[i01].b * a01 + pixels[i10].b * a10 + pixels[i11].b * a11) / sum;
                        }
                        else
                        {
                            r = (pixels[i00].r + pixels[i01].r + pixels[i10].r + pixels[i11].r) * 0.25f;
                            g = (pixels[i00].g + pixels[i01].g + pixels[i10].g + pixels[i11].g) * 0.25f;
                            b = (pixels[i00].b + pixels[i01].b + pixels[i10].b + pixels[i11].b) * 0.25f;
                        }
                        float averaged = sum * 0.25f;
                        int k = y * cw + x;
                        nextAlpha[k] = averaged;
                        next[k] = new Color32((byte)(r + 0.5f), (byte)(g + 0.5f), (byte)(b + 0.5f), 0);
                        Histogram[Mathf.Clamp((int)(averaged * 255f + 0.5f), 0, 255)]++;
                    }
                }
                float scale = settings.KeepCover ? HairRules.CoverScale(Histogram, settings.CoreAlpha, cover.Core) : 1f;
                if (level == HairRules.FarLevel)
                {
                    cover.FarCorePlain = HairRules.SolidAt(Histogram, settings.CoreAlpha, 1f);
                    cover.FarCore = HairRules.SolidAt(Histogram, settings.CoreAlpha, scale);
                    cover.FarScale = scale;
                }
                for (int k = 0; k < next.Length; k++)
                {
                    next[k].a = (byte)(Mathf.Min(1f, nextAlpha[k] * scale) * 255f + 0.5f);
                }
                copy.SetPixels32(next, level);
                // The next level averages this one's colour and its alpha as averaged (not as kept): keeping is done per level.
                pixels = next;
                alpha = nextAlpha;
                w = cw;
                h = ch;
            }
            if (copy.mipmapCount <= HairRules.FarLevel)
            {
                cover.FarCorePlain = cover.FarCore = cover.Core;
            }
            copy.wrapMode = source.wrapMode;
            copy.filterMode = FilterMode.Trilinear;
            copy.anisoLevel = 8;
            copy.Apply(false, false);      // the mipmaps are ours: the engine must not make its own over them
            copy.Compress(false);          // DXT5: a quarter of the memory; strand alpha survives it well
            copy.Apply(false, true);       // and no longer readable: the CPU copy is dropped
            return copy;
        }

        /// <summary>
        /// Sets the hair colour on a finished layer and, from it, the gloss and the back-light. The game gives every
        /// colour the same strong, blond-tinted highlights and the same orange back-light; here the highlight takes the
        /// hair's own hue, dark hair keeps a narrow sheen and light hair a broad soft one, and both the highlight and
        /// the light coming through are bounded against lit skin (<see cref="HairRules.Rule"/>). Always computed from
        /// the game's values in <paramref name="source"/>, so calling it again never drifts. Returns what was set.
        /// </summary>
        public static HairRules.Finish Colour(Material finished, Material source, Color colour, bool masked, Settings settings)
        {
            finished.SetColor(BaseColorId, colour);
            float light = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.7f, colour.grayscale));
            float t = Mathf.Clamp01(settings.Calm);

            // The mask scales the highlight strand by strand (it averages about a half), so the multipliers
            // below are relative to a masked highlight; without the mask they are simply turned down.
            float boost = masked ? 1.25f : 1f;
            float specular = source.GetFloat(SpecularId), secondary = source.GetFloat(SecondarySpecularId);
            specular = Mathf.Lerp(specular, specular * boost * Mathf.Lerp(1.1f, 0.8f, light), t);
            secondary = Mathf.Lerp(secondary, secondary * boost * Mathf.Lerp(0.7f, 0.35f, light), t);

            float min = source.GetFloat(SmoothnessMinId), max = source.GetFloat(SmoothnessMaxId);
            float glossMin = Mathf.Lerp(min, Mathf.Min(min, 0.2f), t);
            float glossMax = Mathf.Lerp(max, Mathf.Max(min, max * Mathf.Lerp(1f, 0.85f, light)), t);

            // The highlight in the hair's own hue, lifted towards white: grey hair shines silver, black hair
            // blue-black, and nobody shines blond.
            Color hue = Color.Lerp(colour, Color.white, 0.4f);
            hue.a = 1f;
            hue = Color.Lerp(source.GetColor(SpecularColorId), hue, t);
            finished.SetColor(SpecularColorId, hue);

            float rim = source.GetFloat(TransmissionRimId);
            float hueLuma = HairRules.Luma(HairRules.ToLinear(hue.r), HairRules.ToLinear(hue.g), HairRules.ToLinear(hue.b));
            HairRules.Finish rule = HairRules.Rule(HairRules.ToLinear(colour.r), HairRules.ToLinear(colour.g), HairRules.ToLinear(colour.b),
                specular, secondary, glossMin, glossMax, rim, hueLuma, IsCap(source));
            if (!settings.Bounded)
            {
                // As before 11 October, kept for the preview's "before" pictures.
                Color lifted = Color.Lerp(colour, Color.white, 0.25f);
                lifted = Color.Lerp(source.GetColor(TransmissionColorId), lifted, t);
                rule.ThroughR = HairRules.ToLinear(lifted.r);
                rule.ThroughG = HairRules.ToLinear(lifted.g);
                rule.ThroughB = HairRules.ToLinear(lifted.b);
                rule.Rim = rim;
                rule.Specular = specular;
                rule.Secondary = secondary;
                rule.GlossMin = glossMin;
                rule.GlossMax = glossMax;
                rule.SheenPeak = HairRules.SheenPeak(specular, secondary, hueLuma, glossMax, 1f);
                rule.ThroughPeak = HairRules.ThroughPeak(HairRules.Luma(rule.ThroughR, rule.ThroughG, rule.ThroughB), rim);
            }
            finished.SetFloat(SpecularId, rule.Specular);
            finished.SetFloat(SecondarySpecularId, rule.Secondary);
            finished.SetFloat(SmoothnessMinId, rule.GlossMin);
            finished.SetFloat(SmoothnessMaxId, rule.GlossMax);
            finished.SetFloat(TransmissionRimId, rule.Rim);
            finished.SetColor(TransmissionColorId, Through(rule));
            return rule;
        }

        /// <summary>
        /// The back-light of a hair material of the mod's own (a look's layer, whose other numbers are the look's):
        /// the hair's own colour, bounded; nothing through a layer painted on the skull. Only two numbers are written,
        /// both from the colour alone, so calling it again never drifts.
        /// </summary>
        public static void Through(Material material, Color colour, bool cap)
        {
            if (material == null || !material.HasProperty(TransmissionColorId))
            {
                return;
            }
            float rim = material.HasProperty(TransmissionRimId) ? material.GetFloat(TransmissionRimId) : 0f;
            HairRules.Finish rule = HairRules.Rule(HairRules.ToLinear(colour.r), HairRules.ToLinear(colour.g), HairRules.ToLinear(colour.b),
                0f, 0f, 0f, 0f, rim, 0f, cap);
            material.SetColor(TransmissionColorId, Through(rule));
            if (cap && material.HasProperty(TransmissionRimId))
            {
                material.SetFloat(TransmissionRimId, 0f);
            }
        }

        /// <summary>
        /// The numbers of the mod's scalp underlay (a smooth cap on the skull under the game's hair, see ModLooks):
        /// drawn before every layer of the game's hair, all but matt so that it reads as the depth of the hair and
        /// never as a shiny cap, in <paramref name="colour"/>, and letting no light through: it lies on the skull.
        /// (Until 11 October it kept the game's orange back-light: with a lamp behind the head its outline glowed
        /// orange under the hair.) Numbers only; no keyword is touched. Returns what was set.
        /// </summary>
        public static HairRules.Finish Underlay(Material material, Color colour)
        {
            if (material.IsKeywordEnabled(TransparentKeyword))
            {
                material.renderQueue = UnderlayQueue;
                if (material.HasProperty(SortPriorityId))
                {
                    material.SetFloat(SortPriorityId, UnderlaySort);
                }
            }
            material.SetColor(BaseColorId, colour);
            Color hue = material.HasProperty(SpecularColorId) ? material.GetColor(SpecularColorId) : Color.white;
            HairRules.Finish rule = HairRules.Rule(HairRules.ToLinear(colour.r), HairRules.ToLinear(colour.g), HairRules.ToLinear(colour.b),
                UnderlayGloss, UnderlayGloss, UnderlayGlossMin, UnderlayGlossMax, 0f,
                HairRules.Luma(HairRules.ToLinear(hue.r), HairRules.ToLinear(hue.g), HairRules.ToLinear(hue.b)), true);
            Set(material, SpecularId, rule.Specular);
            Set(material, SecondarySpecularId, rule.Secondary);
            Set(material, SmoothnessMinId, rule.GlossMin);
            Set(material, SmoothnessMaxId, rule.GlossMax);
            Set(material, TransmissionRimId, 0f);
            if (material.HasProperty(TransmissionColorId))
            {
                material.SetColor(TransmissionColorId, Through(rule));
            }
            return rule;
        }

        /// <summary>The colour to write to _TransmissionColor: the property is a colour, which the engine reads as sRGB.</summary>
        public static Color Through(HairRules.Finish rule)
        {
            return new Color(HairRules.ToSrgb(rule.ThroughR), HairRules.ToSrgb(rule.ThroughG), HairRules.ToSrgb(rule.ThroughB), 1f);
        }

        private static void Set(Material material, int id, float value)
        {
            if (material.HasProperty(id))
            {
                material.SetFloat(id, value);
            }
        }
    }
}
