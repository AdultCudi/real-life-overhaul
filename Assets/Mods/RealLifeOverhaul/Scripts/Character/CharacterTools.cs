using System;
using System.Collections.Generic;
using System.Reflection;
using BigAmbitions.Characters.Appearance;
using Character.Customization;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Shared helpers for the character-quality work: telling the game's two character sets
    /// apart, finding a detailed character to copy, making a copy inert, and sharpening the
    /// textures of the characters the mod looks after. Nothing here runs per frame.
    /// </summary>
    internal static class CharacterTools
    {
        public const string HairShader = "HDRP/Hair";
        public const string StandardShader = "Shader Graphs/SH_CharacterStandard";

        // The game itself applies facial blendshapes only to heads with more shapes than this
        // (AppearanceSetter.UpdateHeadBlendshapes): the detailed head has 66, the city one 3.
        private const int DetailedHeadBlendshapes = 11;

        private const int TextureAniso = 8;
        private const int SmallTexture = 64;   // the game's flat placeholder textures are 64 px

        private static readonly List<Material> MaterialScratch = new List<Material>(8);
        private static readonly List<int> PropertyScratch = new List<int>(16);
        private static readonly List<Component> ComponentScratch = new List<Component>(16);
        private static readonly HashSet<int> SharpenedIds = new HashSet<int>();
        private static readonly List<SharpenedTexture> Sharpened = new List<SharpenedTexture>(64);

        private struct SharpenedTexture
        {
            public Texture Texture;
            public FilterMode Filter;
            public int Aniso;
        }

        // ---------------------------------------------------------------- which set is this

        public static int BlendshapeCount(SkinnedMeshRenderer head)
        {
            return head != null && head.sharedMesh != null ? head.sharedMesh.blendShapeCount : 0;
        }

        /// <summary>True if this character is built from the game's detailed set (either gender).</summary>
        public static bool IsHighDetail(AppearanceSetter character)
        {
            return character != null
                   && (BlendshapeCount(character.maleHeadMesh) > DetailedHeadBlendshapes
                       || BlendshapeCount(character.femaleHeadMesh) > DetailedHeadBlendshapes);
        }

        /// <summary>True if both genders are detailed and every part the game's appearance code needs is there.</summary>
        public static bool IsCompleteHighSet(AppearanceSetter character, out string problem)
        {
            problem = null;
            if (character == null)
            {
                problem = "no character";
            }
            else if (character.maleModel == null || character.femaleModel == null)
            {
                problem = "a gender model is missing";
            }
            else if (BlendshapeCount(character.maleHeadMesh) <= DetailedHeadBlendshapes
                     || BlendshapeCount(character.femaleHeadMesh) <= DetailedHeadBlendshapes)
            {
                problem = "its heads have " + BlendshapeCount(character.maleHeadMesh) + " / " + BlendshapeCount(character.femaleHeadMesh)
                          + " blendshapes, so it is not the detailed set";
            }
            else if (!HasDressableVariant(character.maleModel) || !HasDressableVariant(character.femaleModel))
            {
                // Without one the game's UpdateVisuals would call itself for ever.
                problem = "a gender model has no clothing variants";
            }
            return problem == null;
        }

        private static bool HasDressableVariant(Transform model)
        {
            foreach (AppearanceElementVariant variant in model.GetComponentsInChildren<AppearanceElementVariant>(true))
            {
                if (variant.GetComponent<SkinnedMeshRenderer>() != null)
                {
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- where the detailed set lives

        /// <summary>The character shown in the hairdresser, clothes shop and plastic surgery screens.</summary>
        public static EmployeeUniformPreview FindPreview()
        {
            GameManager game = InstanceBehavior<GameManager>.Instance;
            return game != null ? game.employeeUniformPreview : null;
        }

        /// <summary>The character the game photographs for portraits. Private in the game, hence reflection.</summary>
        public static AppearanceSetter FindPortraitCharacter()
        {
            try
            {
                var generator = GameContract.PortraitInstance.Field?.GetValue(null) as PortraitGenerator;   // (resolved once: Core/GameContract)
                if (generator == null)
                {
                    return null;
                }
                return GameContract.PortraitCharacter.Field?.GetValue(generator) as AppearanceSetter;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// A complete detailed character to copy. The detailed set is not a loadable prefab; it only
        /// exists as the preview characters in the scene. One-off: the last resort walks every loaded object.
        /// </summary>
        public static AppearanceSetter FindHighSource(AppearanceSetter exclude, out string searched)
        {
            searched = "";
            EmployeeUniformPreview preview = FindPreview();
            AppearanceSetter candidate = preview != null ? preview.appearanceSetter : null;
            if (Accept(candidate, exclude, "uniform preview", ref searched))
            {
                return candidate;
            }
            candidate = FindPortraitCharacter();
            if (Accept(candidate, exclude, "portrait character", ref searched))
            {
                return candidate;
            }
            foreach (AppearanceSetter other in SceneFinds.InMemory<AppearanceSetter>("character: a detailed character being looked for (everything in memory gone through, a last resort)"))
            {
                if (other.GetComponent<BaseHuman>() == null && other.name.IndexOf("RealLifeOverhaul", StringComparison.Ordinal) < 0
                    && Accept(other, exclude, "'" + other.name + "'", ref searched))
                {
                    return other;
                }
            }
            return null;
        }

        private static bool Accept(AppearanceSetter candidate, AppearanceSetter exclude, string label, ref string searched)
        {
            if (candidate == null)
            {
                searched += label + ": not there. ";
                return false;
            }
            if (candidate == exclude)
            {
                return false;
            }
            if (!IsCompleteHighSet(candidate, out string problem))
            {
                searched += label + ": " + problem + ". ";
                return false;
            }
            return true;
        }

        /// <summary>A private field of the game's, read through its line of the game contract (null when it is not there).</summary>
        public static T ReadField<T>(object owner, GameMember field) where T : class
        {
            try
            {
                return owner != null && field.Field != null ? field.Field.GetValue(owner) as T : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---------------------------------------------------------------- making a copy inert

        /// <summary>
        /// Removes everything from a copied object that is not needed to draw it. Call while the copy
        /// is still inactive, so that none of the removed scripts ever wakes up.
        /// Scripts go first, those that others depend on last; whatever cannot be removed is switched off.
        /// </summary>
        public static void Strip(GameObject root, Predicate<MonoBehaviour> keepScript, bool keepLights)
        {
            var scripts = new List<MonoBehaviour>(root.GetComponentsInChildren<MonoBehaviour>(true));
            for (int i = scripts.Count - 1; i >= 0; i--)
            {
                if (scripts[i] == null || keepScript(scripts[i]))
                {
                    scripts.RemoveAt(i);
                }
            }
            // A script named in another's RequireComponent cannot be removed before that other one.
            for (int pass = 0; pass < 6 && scripts.Count > 0; pass++)
            {
                for (int i = scripts.Count - 1; i >= 0; i--)
                {
                    MonoBehaviour script = scripts[i];
                    if (script == null)
                    {
                        scripts.RemoveAt(i);
                    }
                    else if (!IsRequiredBySibling(script))
                    {
                        UnityEngine.Object.DestroyImmediate(script);
                        scripts.RemoveAt(i);
                    }
                }
            }
            foreach (MonoBehaviour left in scripts)
            {
                if (left != null)
                {
                    left.enabled = false;
                }
            }

            DestroyAll<Joint>(root);
            DestroyAll<Collider>(root);
            DestroyAll<NavMeshAgent>(root);
            DestroyAll<Rigidbody>(root);
            DestroyAll<AudioSource>(root);
            DestroyAll<AudioListener>(root);
            DestroyAll<Camera>(root);
            if (!keepLights)
            {
                DestroyAll<Light>(root);
            }
        }

        private static void DestroyAll<T>(GameObject root) where T : Component
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
            {
                if (component != null)
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
            }
        }

        private static bool IsRequiredBySibling(Component component)
        {
            Type type = component.GetType();
            component.GetComponents(ComponentScratch);
            for (int i = 0; i < ComponentScratch.Count; i++)
            {
                Component sibling = ComponentScratch[i];
                if (sibling == null || sibling == component)
                {
                    continue;
                }
                foreach (object attribute in sibling.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                {
                    var require = (RequireComponent)attribute;
                    if (Needs(require.m_Type0, type) || Needs(require.m_Type1, type) || Needs(require.m_Type2, type))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool Needs(Type required, Type present)
        {
            return required != null && required.IsAssignableFrom(present);
        }

        public static void SetLayer(Transform root, int layer)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }

        // ---------------------------------------------------------------- textures

        /// <summary>
        /// Trilinear filtering and anisotropy for the textures this character is wearing right now.
        /// The game ships them bilinear with no anisotropy, which smears at a glancing angle and
        /// shows a seam between mip levels. Each texture is touched once per scene; the sampler
        /// state costs nothing to set and nothing measurable to draw.
        /// </summary>
        public static void SharpenTextures(AppearanceSetter character)
        {
            IReadOnlyList<SkinnedMeshRenderer> renderers = character.GetSelectedMeshRenderers();
            for (int i = 0; i < renderers.Count; i++)
            {
                SharpenTextures(renderers[i]);
            }
            SharpenTextures(character.HeadMesh);
        }

        private static void SharpenTextures(Renderer renderer)
        {
            if (renderer == null)
            {
                return;
            }
            renderer.GetSharedMaterials(MaterialScratch);
            for (int m = 0; m < MaterialScratch.Count; m++)
            {
                Material material = MaterialScratch[m];
                if (material == null)
                {
                    continue;
                }
                material.GetTexturePropertyNameIDs(PropertyScratch);
                for (int p = 0; p < PropertyScratch.Count; p++)
                {
                    Texture texture = material.GetTexture(PropertyScratch[p]);
                    if (texture == null || !SharpenedIds.Add(texture.GetInstanceID()))
                    {
                        continue;
                    }
                    if (texture.width <= SmallTexture || texture.mipmapCount <= 1 || texture is RenderTexture)
                    {
                        continue;
                    }
                    Sharpened.Add(new SharpenedTexture { Texture = texture, Filter = texture.filterMode, Aniso = texture.anisoLevel });
                    if (texture.filterMode == FilterMode.Bilinear)
                    {
                        texture.filterMode = FilterMode.Trilinear;
                    }
                    if (texture.anisoLevel < TextureAniso)
                    {
                        texture.anisoLevel = TextureAniso;
                    }
                }
            }
            MaterialScratch.Clear();
        }

        public static int SharpenedCount => Sharpened.Count;

        public static void RestoreTextures()
        {
            for (int i = 0; i < Sharpened.Count; i++)
            {
                SharpenedTexture entry = Sharpened[i];
                if (entry.Texture != null)
                {
                    entry.Texture.filterMode = entry.Filter;
                    entry.Texture.anisoLevel = entry.Aniso;
                }
            }
            Sharpened.Clear();
            SharpenedIds.Clear();
        }
    }
}
