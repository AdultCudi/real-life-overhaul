using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How one layer of the game's strand hair is finished. Game-free on purpose: the HDRP preview
    /// (CharacterAssets/PreviewProject) compiles this very file, so what is judged there is what runs here.
    ///
    /// The game draws detailed hair as alpha-blended strips on the stock HDRP hair shader with the depth
    /// prepass off, no strand maps and one flat colour. Three things follow: the scalp shows through every
    /// strip (thin, even bald, from play distance), strips sort wrongly against each other, and the whole
    /// head carries one even highlight (the "wig" look). A finished layer is a copy of the game's material:
    ///
    ///  - with the keyword set of an alpha-tested hair material the game itself ships (the female styles'),
    ///    so the compiled shader variant is certain to exist, and the depth prepass switched on: the core of
    ///    every strand writes depth, so hair hides what is behind it and sorts correctly, while the soft
    ///    edge of each strand still blends;
    ///  - with strand maps the game leaves empty: a smoothness mask and a normal map of fine streaks, which
    ///    break the highlight up into strands that run along the hair;
    ///  - with gloss and highlight colour that follow the hair colour instead of one fixed blond sheen.
    ///
    /// Cost: one extra depth-only draw per layer, for the one character it is applied to. Nothing per frame.
    /// </summary>
    internal static class HairFinish
    {
        public const string HairShader = "HDRP/Hair";
        public const string AlphaTestKeyword = "_ALPHATEST_ON";
        public const string TransparentKeyword = "_SURFACE_TYPE_TRANSPARENT";
        public const string DepthPrepass = "TransparentDepthPrepass";

        /// <summary>What a finish does; the defaults are the values chosen in the preview.</summary>
        public struct Settings
        {
            /// <summary>
            /// Strand alpha (after <see cref="AlphaGain"/>) above this writes depth: the solid core. 0 = no depth prepass.
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
            /// Above 0: the layer gets a copy of its strand texture with alpha multiplied by this (<see cref="Denser"/>), so
            /// the middle of each strand is solid. Needed with the prepass: a strand that hides the hair behind it must not
            /// itself be half see-through, or skin shows through it. (Raising alpha through _BaseColor instead was tried
            /// in the preview: the shader does not clamp it, and the blend overshoots into coloured fringes.)
            /// </summary>
            public float AlphaGain;

            public static Settings Default => new Settings
            {
                CoreAlpha = 0.95f, EdgeAlpha = 0.04f, StrandTiling = new Vector2(3f, 1f), NormalStrength = 0.6f, Calm = 1f, AlphaGain = 3f
            };
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
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
        private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClipThreshold");
        private static readonly int AlphaCutoffEnableId = Shader.PropertyToID("_AlphaCutoffEnable");
        private static readonly int PrepassEnableId = Shader.PropertyToID("_TransparentDepthPrepassEnable");
        private static readonly int PrepassClipId = Shader.PropertyToID("_AlphaClipThresholdDepthPrepass");

        public static bool IsHair(Material material)
        {
            return material != null && material.shader != null && material.shader.name == HairShader;
        }

        /// <summary>A see-through, alpha-tested hair material: the kind whose variant a finished layer borrows.</summary>
        public static bool IsTemplate(Material material)
        {
            return IsHair(material) && material.IsKeywordEnabled(AlphaTestKeyword) && material.IsKeywordEnabled(TransparentKeyword);
        }

        /// <summary>
        /// A finished copy of <paramref name="layer"/>. The caller owns it (and destroys it), and keeps writing
        /// the hair colour to it through <see cref="Colour"/>. Null if the layer or the template is not what
        /// this was written for; the caller then leaves the game's material alone.
        /// </summary>
        public static Material Make(Material layer, Material template, Texture strandMask, Texture strandNormal, Settings settings)
        {
            if (!IsHair(layer) || !IsTemplate(template) || !layer.IsKeywordEnabled(TransparentKeyword))
            {
                return null;
            }
            var finished = new Material(layer) { name = layer.name + " (RealLifeOverhaul finished)" };
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
            Colour(finished, layer, finished.GetColor(BaseColorId), strandMask != null, settings.Calm);
            return finished;
        }

        private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");

        /// <summary>
        /// A copy of a strand texture with its alpha multiplied, so the middle of every strand is solid and only
        /// its edge is soft. Read back from the GPU once (the game's textures are not readable); the caller owns it.
        /// </summary>
        public static Texture2D Denser(Texture source, float gain)
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
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i].a = (byte)Mathf.Min(255f, pixels[i].a * gain);
            }
            copy.SetPixels32(pixels);
            copy.wrapMode = source.wrapMode;
            copy.filterMode = FilterMode.Trilinear;
            copy.anisoLevel = 8;
            copy.Apply(true, false);
            copy.Compress(false);          // DXT5: a quarter of the memory; strand alpha survives it well
            copy.Apply(false, true);       // and no longer readable: the CPU copy is dropped
            return copy;
        }

        /// <summary>
        /// Sets the hair colour on a finished layer and, from it, the gloss. The game gives every colour the
        /// same strong, blond-tinted highlights; here the highlight takes the hair's own hue, dark hair keeps
        /// a narrow sheen and light hair a broad soft one. Always computed from the game's values in
        /// <paramref name="source"/>, so calling it again never drifts.
        /// </summary>
        public static void Colour(Material finished, Material source, Color colour, bool masked, float calm)
        {
            finished.SetColor(BaseColorId, colour);
            float light = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.7f, colour.grayscale));
            float t = Mathf.Clamp01(calm);

            // The mask scales the highlight strand by strand (it averages about a half), so the multipliers
            // below are relative to a masked highlight; without the mask they are simply turned down.
            float boost = masked ? 1.25f : 1f;
            float specular = source.GetFloat(SpecularId), secondary = source.GetFloat(SecondarySpecularId);
            finished.SetFloat(SpecularId, Mathf.Lerp(specular, specular * boost * Mathf.Lerp(1.1f, 0.8f, light), t));
            finished.SetFloat(SecondarySpecularId, Mathf.Lerp(secondary, secondary * boost * Mathf.Lerp(0.7f, 0.35f, light), t));

            float min = source.GetFloat(SmoothnessMinId), max = source.GetFloat(SmoothnessMaxId);
            finished.SetFloat(SmoothnessMinId, Mathf.Lerp(min, Mathf.Min(min, 0.2f), t));
            finished.SetFloat(SmoothnessMaxId, Mathf.Lerp(max, Mathf.Max(min, max * Mathf.Lerp(1f, 0.85f, light)), t));

            // Highlight and back-light in the hair's own hue, lifted towards white: grey hair shines silver,
            // black hair blue-black, and nobody shines blond.
            Color hue = Color.Lerp(colour, Color.white, 0.4f);
            hue.a = 1f;
            finished.SetColor(SpecularColorId, Color.Lerp(source.GetColor(SpecularColorId), hue, t));
            Color through = Color.Lerp(colour, Color.white, 0.25f);
            through.a = 1f;
            finished.SetColor(TransmissionColorId, Color.Lerp(source.GetColor(TransmissionColorId), through, t));
        }
    }
}
