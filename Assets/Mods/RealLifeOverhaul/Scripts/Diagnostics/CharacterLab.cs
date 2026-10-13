using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using BAModAPI;
using BigAmbitions.Characters.Appearance;
using RealLifeOverhaul;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[assembly: RegisterModClass(typeof(CharacterLabEntry))]

namespace RealLifeOverhaul
{
    /// <summary>
    /// TEMPORARY development aid for the character-quality work. F9 works in every scene,
    /// including character creation: it saves a screenshot and writes what every character on
    /// screen is made of (meshes, triangle counts, materials, shaders, textures) to a text file.
    /// Remove before release.
    /// </summary>
    [ModEntryOnInitializationLoad]
    public class CharacterLabEntry : IModBigAmbitions
    {
        private GameObject _host;

        public string[] RelativeAssetBundlePaths => Array.Empty<string>();

        public Task OnLoadAsync(ModContext context)
        {
            CharacterLab.Logger = context.Logger;
            _host = new GameObject("RealLifeOverhaul Character Lab");
            UnityEngine.Object.DontDestroyOnLoad(_host);
            _host.AddComponent<CharacterLab>();
            return Task.CompletedTask;
        }

        public Task OnUnloadAsync()
        {
            if (_host != null)
            {
                UnityEngine.Object.Destroy(_host);
                _host = null;
            }
            return Task.CompletedTask;
        }
    }

    public class CharacterLab : MonoBehaviour
    {
        private const int MaxCharacters = 3;      // others near the camera, after the ones the character work is about
        private const int MaxBoneLines = 160;

        public static IModLogger Logger;

        private static readonly int HotUpdate = HotSpots.Slot("Diagnostics: CharacterLab.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (!Keys.Down(KeyAction.CharacterReport))
            {
                return;
            }
            try
            {
                string root = Application.persistentDataPath;
                string folder = Path.Combine(root, "RealLifeOverhaul_shots");
                Directory.CreateDirectory(folder);
                string stamp = DateTime.Now.ToString("HHmmss");
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "char_" + stamp + ".png"));
                string report = Path.Combine(root, "RealLifeOverhaul_character_" + stamp + ".txt");
                File.WriteAllText(report, Describe());
                Logger?.Info("Character report written to " + report);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static string Describe()
        {
            var text = new StringBuilder();
            text.AppendLine("scene: " + SceneManager.GetActiveScene().name + "  loaded scenes: " + SceneManager.sceneCount);
            text.AppendLine("screen: " + Screen.width + "x" + Screen.height + "  quality level: " + QualitySettings.GetQualityLevel()
                            + "  skin weights: " + QualitySettings.skinWeights + "  anisotropic: " + QualitySettings.anisotropicFiltering
                            + "  pipeline: " + (GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "built-in"));

            Camera camera = Camera.main;
            if (camera != null)
            {
                text.AppendLine("main camera: '" + camera.name + "' fov " + camera.fieldOfView.ToString("F1") + " pos " + camera.transform.position.ToString("F2"));
            }
            Light[] lights = FindObjectsOfType<Light>();
            text.AppendLine("active lights: " + lights.Length);
            foreach (Light light in lights)
            {
                if (light.type == LightType.Directional || lights.Length <= 12 || light.name.StartsWith("RealLifeOverhaul", StringComparison.Ordinal))
                {
                    text.AppendLine("  light '" + light.name + "' " + light.type + " intensity " + light.intensity.ToString("F1") + " colour " + ColorUtility.ToHtmlStringRGB(light.color) + " shadows " + light.shadows
                                    + " pos " + light.transform.position.ToString("F2") + " under '" + (light.transform.parent != null ? light.transform.parent.name : "none") + "'");
                }
            }

            DescribeMod(text);

            AppearanceSetter[] characters = FindObjectsOfType<AppearanceSetter>();
            text.AppendLine("characters with an appearance in the scene: " + characters.Length);

            // The ones the character work is about first (some of them are switched off), then the nearest others.
            Vector3 origin = camera != null ? camera.transform.position : Vector3.zero;
            var described = new List<AppearanceSetter>();
            AppearanceSetter player = null;
            try
            {
                PlayerController controller = Helpers.PlayerHelper.PlayerController;
                player = controller != null && controller.Character != null ? controller.Character.appearanceSetter : null;
            }
            catch (Exception)
            {
                // No player in this scene.
            }
            PlayerDouble playerDouble = PlayerDouble.Instance;
            AppearanceSetter doubleLook = playerDouble != null ? playerDouble.DoubleLook : null;
            EmployeeUniformPreview preview = null;
            AppearanceSetter portrait = null;
            try
            {
                preview = CharacterTools.FindPreview();
                portrait = CharacterTools.FindPortraitCharacter();
            }
            catch (Exception)
            {
                // Not in this scene.
            }
            AppearanceSetter previewLook = preview != null ? preview.appearanceSetter : null;

            DescribeOnce(text, described, player, origin, "PLAYER");
            DescribeOnce(text, described, doubleLook, origin, "HIGH-DETAIL DOUBLE MADE BY THE MOD");
            DescribeOnce(text, described, previewLook, origin, "SHOP PREVIEW CHARACTER");
            DescribeOnce(text, described, portrait, origin, "PORTRAIT CHARACTER");

            Array.Sort(characters, (a, b) => Vector3.Distance(a.transform.position, origin).CompareTo(Vector3.Distance(b.transform.position, origin)));
            int others = 0;
            for (int i = 0; i < characters.Length && others < MaxCharacters; i++)
            {
                if (!described.Contains(characters[i]))
                {
                    DescribeOnce(text, described, characters[i], origin, null);
                    others++;
                }
            }

            // Skeletons: what the pose copy has to line up.
            if (player != null)
            {
                DescribeSkeleton(text, "PLAYER", player);
            }
            foreach (AppearanceSetter character in described)
            {
                if (character != player && CharacterTools.IsHighDetail(character))
                {
                    DescribeSkeleton(text, "'" + character.name + "'", character);
                }
            }
            return text.ToString();
        }

        // What the character-quality features are doing right now.
        private static void DescribeMod(StringBuilder text)
        {
            text.AppendLine();
            text.AppendLine("options: high-detail player " + Settings.CharacterHighDetail + ", fuller hair " + Settings.CharacterHair + ", character lighting " + Settings.CharacterLighting);
            PlayerDouble playerDouble = PlayerDouble.Instance;
            text.AppendLine("high-detail double: " + (playerDouble != null ? playerDouble.Describe() : "not in this scene"));
            CharacterQuality quality = CharacterQuality.Instance;
            if (quality == null)
            {
                text.AppendLine("character quality: not running in this scene");
            }
            else
            {
                CharacterLighting lighting = quality.Lighting;
                text.AppendLine("character quality: managed characters " + quality.ManagedCharacters + ", others in strand hair with the hair finish " + quality.OtherCharacters
                                + ", " + HairLook.Describe()
                                + ", textures sharpened " + CharacterTools.SharpenedCount
                                + ", lighting " + (lighting == null ? "no set-up found" : (lighting.Applied ? "applied" : "not applied") + " (softened " + lighting.SoftenedLights + ", added " + lighting.AddedLights + ")"));
            }
            ModLooks.Describe(text);
            SkinLook.Describe(text);
            text.AppendLine();
        }

        private static void DescribeOnce(StringBuilder text, List<AppearanceSetter> described, AppearanceSetter character, Vector3 origin, string role)
        {
            if (character == null || described.Contains(character))
            {
                return;
            }
            described.Add(character);
            DescribeCharacter(text, character, origin, role);
        }

        private static void DescribeCharacter(StringBuilder text, AppearanceSetter character, Vector3 origin, string role)
        {
            text.AppendLine();
            text.AppendLine("===== '" + character.name + "'" + (role != null ? " (" + role + ")" : "")
                            + "  " + Vector3.Distance(character.transform.position, origin).ToString("F1") + " m from camera"
                            + "  parent '" + (character.transform.parent != null ? character.transform.parent.name : "none") + "'"
                            + "  active " + character.gameObject.activeInHierarchy + "  layer " + LayerMask.LayerToName(character.gameObject.layer)
                            + "  detailed set " + CharacterTools.IsHighDetail(character) + "  skipMeshMerging " + character.skipMeshMerging + " =====");
            DescribeVariants(text, character);

            int totalTriangles = 0;
            foreach (Renderer renderer in character.GetComponentsInChildren<Renderer>(true))
            {
                // A switched-off character still has its current outfit switched on underneath it.
                if (!IsActiveBelow(renderer.transform, character.transform))
                {
                    continue;
                }
                Mesh mesh = null;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    mesh = skinned.sharedMesh;
                }
                else
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    mesh = filter != null ? filter.sharedMesh : null;
                }

                int triangles = 0;
                if (mesh != null)
                {
                    for (int s = 0; s < mesh.subMeshCount; s++)
                    {
                        triangles += (int)(mesh.GetIndexCount(s) / 3);
                    }
                }
                totalTriangles += triangles;

                text.AppendLine("renderer '" + renderer.name + "' (" + renderer.GetType().Name + ") under '" + (renderer.transform.parent != null ? renderer.transform.parent.name : "none") + "'"
                                + (mesh != null
                                    ? "  mesh '" + mesh.name + "' verts " + mesh.vertexCount + " tris " + triangles + " submeshes " + mesh.subMeshCount + " blendshapes " + mesh.blendShapeCount
                                      + " normals " + (mesh.normals != null && mesh.isReadable ? "readable" : "not readable") + " tangents " + mesh.HasVertexAttribute(VertexAttribute.Tangent)
                                      + " uv2 " + mesh.HasVertexAttribute(VertexAttribute.TexCoord1) + " colours " + mesh.HasVertexAttribute(VertexAttribute.Color)
                                    : "  no mesh")
                                + "  enabled " + renderer.enabled + "  forceRenderingOff " + renderer.forceRenderingOff + "  renderingLayerMask 0x" + renderer.renderingLayerMask.ToString("X")
                                + "  layer " + LayerMask.LayerToName(renderer.gameObject.layer) + "  shadows " + renderer.shadowCastingMode);

                if (mesh != null && mesh.blendShapeCount > 0)
                {
                    var names = new StringBuilder("    blendshapes:");
                    for (int b = 0; b < mesh.blendShapeCount; b++)
                    {
                        names.Append(' ').Append(b).Append('=').Append(mesh.GetBlendShapeName(b));
                        if (renderer is SkinnedMeshRenderer weighted)
                        {
                            float weight = weighted.GetBlendShapeWeight(b);
                            if (weight != 0f)
                            {
                                names.Append('(').Append(weight.ToString("0.#")).Append(')');
                            }
                        }
                    }
                    text.AppendLine(names.ToString());
                }
                if (renderer is SkinnedMeshRenderer bound)
                {
                    text.AppendLine("    bones " + bound.bones.Length + "  rootBone '" + (bound.rootBone != null ? bound.rootBone.name : "none") + "'");
                }

                foreach (Material material in renderer.sharedMaterials)
                {
                    DescribeMaterial(text, material);
                }
            }
            text.AppendLine("total triangles on this character: " + totalTriangles);
        }

        private static bool IsActiveBelow(Transform part, Transform root)
        {
            for (Transform t = part; t != null && t != root; t = t.parent)
            {
                if (!t.gameObject.activeSelf)
                {
                    return false;
                }
            }
            return true;
        }

        // How many choices each slot has, per gender: the two character sets must agree for the double to work.
        private static void DescribeVariants(StringBuilder text, AppearanceSetter character)
        {
            DescribeVariants(text, "male", character.maleModel);
            DescribeVariants(text, "female", character.femaleModel);
        }

        private static void DescribeVariants(StringBuilder text, string gender, Transform model)
        {
            if (model == null)
            {
                text.AppendLine("  variants " + gender + ": no model");
                return;
            }
            var line = new StringBuilder("  variants " + gender + " (under '" + model.name + "', active " + model.gameObject.activeSelf + "):");
            foreach (AppearanceElementType type in (AppearanceElementType[])Enum.GetValues(typeof(AppearanceElementType)))
            {
                Transform slot = model.Find(type.ToString());
                if (slot == null)
                {
                    continue;
                }
                AppearanceElementVariant[] variants = slot.GetComponentsInChildren<AppearanceElementVariant>(true);
                int withMesh = 0;
                foreach (AppearanceElementVariant variant in variants)
                {
                    if (variant.GetComponent<SkinnedMeshRenderer>() != null)
                    {
                        withMesh++;
                    }
                }
                line.Append(' ').Append(type).Append('=').Append(variants.Length).Append('(').Append(withMesh).Append(" with a mesh)");
            }
            text.AppendLine(line.ToString());
        }

        private static void DescribeSkeleton(StringBuilder text, string label, AppearanceSetter character)
        {
            SkinnedMeshRenderer head = character.HeadMesh;
            Transform hips = head != null ? head.rootBone : null;
            text.AppendLine();
            if (hips == null)
            {
                text.AppendLine("===== skeleton of " + label + ": the head mesh has no root bone =====");
                return;
            }
            Transform top = hips.parent != null ? hips.parent : hips;
            text.AppendLine("===== skeleton of " + label + ": root bone '" + hips.name + "' under '" + top.name + "'"
                            + " (lossyScale " + top.lossyScale.ToString("F3") + ", localRotation " + top.localEulerAngles.ToString("F1") + ")"
                            + "  path " + PathTo(top, character.transform) + " =====");
            int lines = 0;
            DescribeBone(text, hips, 0, ref lines);
        }

        private static string PathTo(Transform bone, Transform root)
        {
            string path = bone.name;
            for (Transform t = bone.parent; t != null; t = t.parent)
            {
                path = t.name + "/" + path;
                if (t == root)
                {
                    break;
                }
            }
            return path;
        }

        private static void DescribeBone(StringBuilder text, Transform bone, int depth, ref int lines)
        {
            if (lines >= MaxBoneLines)
            {
                return;
            }
            lines++;
            text.Append(' ', depth * 2).Append(bone.name)
                .Append("  pos ").Append(bone.localPosition.ToString("F4"))
                .Append("  rot ").Append(bone.localEulerAngles.ToString("F1"))
                .Append("  scale ").AppendLine(bone.localScale.ToString("F2"));
            for (int i = 0; i < bone.childCount; i++)
            {
                DescribeBone(text, bone.GetChild(i), depth + 1, ref lines);
            }
        }

        private static void DescribeMaterial(StringBuilder text, Material material)
        {
            if (material == null)
            {
                text.AppendLine("    material: none");
                return;
            }
            Shader shader = material.shader;
            text.AppendLine("    material '" + material.name + "'  shader '" + (shader != null ? shader.name : "none") + "'  queue " + material.renderQueue
                            + "  keywords [" + string.Join(" ", material.shaderKeywords) + "]");
            if (shader == null)
            {
                return;
            }

            var numbers = new StringBuilder();
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                string property = shader.GetPropertyName(i);
                switch (shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Texture:
                        Texture texture = material.GetTexture(property);
                        if (texture != null)
                        {
                            text.AppendLine("      texture " + property + " = '" + texture.name + "' " + texture.width + "x" + texture.height + " mips " + texture.mipmapCount + " filter " + texture.filterMode + " aniso " + texture.anisoLevel);
                        }
                        break;
                    case ShaderPropertyType.Color:
                        numbers.Append(' ').Append(property).Append('=').Append(ColorUtility.ToHtmlStringRGBA(material.GetColor(property)));
                        break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        numbers.Append(' ').Append(property).Append('=').Append(material.GetFloat(property).ToString("0.###"));
                        break;
                }
            }
            if (numbers.Length > 0)
            {
                text.AppendLine("      values:" + (numbers.Length > 900 ? numbers.ToString(0, 900) + " ..." : numbers.ToString()));
            }
        }
    }
}
