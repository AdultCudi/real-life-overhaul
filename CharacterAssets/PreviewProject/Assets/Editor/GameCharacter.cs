using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace CharacterPreview
{
    /// <summary>
    /// The game's detailed character, rebuilt from the local reference (scripts/extract_preview.py):
    /// the same transforms, meshes, bind poses, blend shapes and textures, and for the stock HDRP hair
    /// shader the same material state, value for value. The game's own shader graphs are not available
    /// outside the game; skin, eyes and clothes are stood in for by HDRP/Lit (see <see cref="GameMaterials"/>).
    /// </summary>
    internal sealed class GameCharacter
    {
        public readonly GameObject Root;
        public readonly GameMaterials Materials;
        public readonly List<SkinnedMeshRenderer> Shown = new List<SkinnedMeshRenderer>();

        private readonly string _folder;
        private readonly JObject _data;
        private readonly Transform[] _transforms;
        private readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();
        private readonly Dictionary<string, SkinnedMeshRenderer> _renderers = new Dictionary<string, SkinnedMeshRenderer>();

        public GameCharacter(string referenceFolder)
        {
            _folder = referenceFolder;
            _data = JObject.Parse(File.ReadAllText(Path.Combine(referenceFolder, "character.json")));
            Materials = new GameMaterials(referenceFolder, (JObject)_data["materials"], (JObject)_data["textures"]);

            var transforms = (JArray)_data["transforms"];
            _transforms = new Transform[transforms.Count];
            for (int i = 0; i < transforms.Count; i++)
            {
                JToken t = transforms[i];
                var go = new GameObject((string)t["name"]);
                int parent = (int)t["parent"];
                if (parent >= 0)
                {
                    go.transform.SetParent(_transforms[parent], false);
                    go.transform.localPosition = Vector(t["pos"]);
                    go.transform.localRotation = new Quaternion((float)t["rot"][0], (float)t["rot"][1], (float)t["rot"][2], (float)t["rot"][3]);
                    go.transform.localScale = Vector(t["scale"]);
                }
                _transforms[i] = go.transform;
            }
            Root = _transforms[0].gameObject;
        }

        public Transform Bone(string name)
        {
            foreach (Transform t in _transforms)
            {
                if (t.name == name)
                {
                    return t;
                }
            }
            return null;
        }

        /// <summary>
        /// Shows one sex with the named parts ("Hair/MaleHair05", "Torso/HighNeckShirt", ...), the head always.
        /// Everything else is switched off, the way the game leaves unpicked variants.
        /// </summary>
        public void Wear(string sex, IEnumerable<string> parts)
        {
            foreach (SkinnedMeshRenderer renderer in _renderers.Values)
            {
                renderer.gameObject.SetActive(false);
            }
            Shown.Clear();
            var wanted = new HashSet<string> { "Model/" + sex + "/" + (sex == "Male" ? "MaleHead" : "FemaleHead") };
            foreach (string part in parts)
            {
                wanted.Add("Model/" + sex + "/" + part);
            }
            foreach (JToken entry in (JArray)_data["renderers"])
            {
                string path = (string)entry["path"];
                if (wanted.Remove(path))
                {
                    SkinnedMeshRenderer renderer = Renderer(entry);
                    renderer.gameObject.SetActive(true);
                    Shown.Add(renderer);
                }
            }
            foreach (string missing in wanted)
            {
                Debug.LogWarning("[CharacterPreview] no such part in the reference: " + missing);
            }
        }

        public SkinnedMeshRenderer Find(string slot)
        {
            foreach (SkinnedMeshRenderer renderer in Shown)
            {
                Transform parent = renderer.transform.parent;
                if (parent != null && parent.name == slot || renderer.name == slot)
                {
                    return renderer;
                }
            }
            return null;
        }

        /// <summary>The game writes the hair colour to _BaseColor of everything on the hair, beard and eyebrow renderers.</summary>
        public void SetHairColour(Color colour)
        {
            foreach (string slot in new[] { "Hair", "Beard", "Eyebrows" })
            {
                SkinnedMeshRenderer renderer = Find(slot);
                if (renderer == null)
                {
                    continue;
                }
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null && !GameMaterials.IsSkin(material))
                    {
                        material.SetColor("_BaseColor", colour);
                    }
                }
            }
        }

        /// <summary>A garment's colour (the game tints clothes through the colour mask).</summary>
        public void SetClothColour(string slot, Color colour)
        {
            SkinnedMeshRenderer renderer = Find(slot);
            if (renderer == null)
            {
                return;
            }
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material != null && !GameMaterials.IsSkin(material))
                {
                    material.SetColor("_BaseColor", colour);
                }
            }
        }

        /// <summary>The game writes the skin tone to _MaskColorRed of the skin materials; the stand-in multiplies it in.</summary>
        public void SetSkinColour(Color colour)
        {
            foreach (SkinnedMeshRenderer renderer in Shown)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null && GameMaterials.IsSkin(material))
                    {
                        material.SetColor("_BaseColor", colour);
                    }
                }
            }
        }

        private SkinnedMeshRenderer Renderer(JToken entry)
        {
            string path = (string)entry["path"];
            if (_renderers.TryGetValue(path, out SkinnedMeshRenderer renderer))
            {
                return renderer;
            }
            Transform own = _transforms[(int)entry["transform"]];
            for (Transform t = own.parent; t != null && t != Root.transform; t = t.parent)
            {
                t.gameObject.SetActive(true);
            }
            renderer = own.gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = Mesh((string)entry["mesh"]);
            var bones = (JArray)entry["bones"];
            var list = new Transform[bones.Count];
            for (int i = 0; i < list.Length; i++)
            {
                int index = (int)bones[i];
                list[i] = index >= 0 ? _transforms[index] : null;
            }
            renderer.bones = list;
            RestPose(renderer.sharedMesh, list);
            int root = (int)entry["rootBone"];
            renderer.rootBone = root >= 0 ? _transforms[root] : null;
            renderer.updateWhenOffscreen = true;
            renderer.quality = SkinQuality.Bone4;
            var weights = (JArray)entry["weights"];
            for (int i = 0; i < weights.Count && i < renderer.sharedMesh.blendShapeCount; i++)
            {
                renderer.SetBlendShapeWeight(i, (float)weights[i]);
            }
            var names = (JArray)entry["materials"];
            var materials = new Material[names.Count];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = names[i].Type == JTokenType.Null ? null : Materials.Get((string)names[i]);
            }
            renderer.sharedMaterials = materials;
            // As in the game: clothes cast shadows, hair and face do not.
            bool clothes = path.Contains("/Torso/") || path.Contains("/Legs/") || path.Contains("/Feet/");
            renderer.shadowCastingMode = clothes ? ShadowCastingMode.On : ShadowCastingMode.Off;
            _renderers.Add(path, renderer);
            return renderer;
        }

        // The scene file holds the skeleton as the animator left it. The head mesh's bind poses say where every
        // bone stands at rest; the game's meshes agree on that pose, whatever space each one was modelled in.
        private void RestPose(Mesh mesh, Transform[] bones)
        {
            if (_posed || !mesh.name.Contains("Face"))
            {
                return;
            }
            _posed = true;
            _arms = bones;
            Matrix4x4[] bind = mesh.bindposes;
            Matrix4x4 root = Root.transform.localToWorldMatrix;
            for (int i = 0; i < bones.Length && i < bind.Length; i++)
            {
                if (bones[i] != null)
                {
                    Matrix4x4 world = root * bind[i].inverse;
                    bones[i].SetPositionAndRotation(world.GetColumn(3), world.rotation);
                }
            }
        }

        private bool _posed;
        private Transform[] _arms;

        /// <summary>Arms down from the T-pose, so the silhouette is the one seen in play.</summary>
        public void LowerArms(float degrees)
        {
            if (_arms == null)
            {
                return;
            }
            foreach (Transform bone in _arms)
            {
                if (bone != null && bone.name.StartsWith("UpperArm."))
                {
                    // From the bind pose every time, so a shot can ask for its own angle (0 = arms at shoulder height).
                    if (!_armRest.TryGetValue(bone, out Quaternion rest))
                    {
                        rest = bone.localRotation;
                        _armRest[bone] = rest;
                    }
                    bone.localRotation = rest;
                    float sign = bone.name.EndsWith(".L") ? 1f : -1f;
                    bone.rotation = Quaternion.AngleAxis(sign * degrees, Root.transform.forward) * bone.rotation;
                }
            }
        }

        private readonly Dictionary<Transform, Quaternion> _armRest = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, Quaternion> _rest = new Dictionary<Transform, Quaternion>();

        /// <summary>
        /// A pose for a shot: bone name to extra local rotation (Euler degrees), on top of the standing pose; null stands.
        /// Enough to bend a knee, an ankle or an elbow and see what a garment does there.
        /// </summary>
        public void Pose(JObject pose)
        {
            foreach (KeyValuePair<Transform, Quaternion> rest in _rest)
            {
                rest.Key.localRotation = rest.Value;
            }
            _rest.Clear();
            if (pose == null)
            {
                return;
            }
            foreach (JProperty bone in pose.Properties())
            {
                Transform t = Bone(bone.Name);
                if (t == null)
                {
                    Debug.LogWarning("[CharacterPreview] no bone '" + bone.Name + "' to pose");
                    continue;
                }
                _rest[t] = t.localRotation;
                t.localRotation = t.localRotation * Quaternion.Euler((float)bone.Value[0], (float)bone.Value[1], (float)bone.Value[2]);
            }
        }

        private Mesh Mesh(string name)
        {
            if (!_meshes.TryGetValue(name, out Mesh mesh))
            {
                mesh = ReadMesh(Path.Combine(_folder, "meshes", name + ".bam"), name);
                _meshes.Add(name, mesh);
            }
            return mesh;
        }

        /// <summary>Reads one "BAM1" mesh (layout: scripts/extract_preview.py, write_mesh).</summary>
        public static Mesh ReadMesh(string file, string name)
        {
            using (var reader = new BinaryReader(File.OpenRead(file)))
            {
                if (new string(reader.ReadChars(4)) != "BAM1")
                {
                    throw new InvalidDataException(file + " is not a BAM1 mesh");
                }
                int count = reader.ReadInt32(), submeshes = reader.ReadInt32(), poses = reader.ReadInt32(), shapes = reader.ReadInt32(), flags = reader.ReadInt32();
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.vertices = Vectors3(reader, count);
                if ((flags & 2) != 0)
                {
                    mesh.normals = Vectors3(reader, count);
                }
                if ((flags & 4) != 0)
                {
                    var tangents = new Vector4[count];
                    for (int i = 0; i < count; i++)
                    {
                        tangents[i] = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    }
                    mesh.tangents = tangents;
                }
                if ((flags & 8) != 0)
                {
                    mesh.uv = Vectors2(reader, count);
                }
                if ((flags & 16) != 0)
                {
                    mesh.uv2 = Vectors2(reader, count);
                }
                if ((flags & 32) != 0)
                {
                    var indices = new int[count * 4];
                    for (int i = 0; i < indices.Length; i++)
                    {
                        indices[i] = reader.ReadInt32();
                    }
                    var weights = new BoneWeight[count];
                    for (int i = 0; i < count; i++)
                    {
                        weights[i] = new BoneWeight
                        {
                            boneIndex0 = indices[i * 4], boneIndex1 = indices[i * 4 + 1], boneIndex2 = indices[i * 4 + 2], boneIndex3 = indices[i * 4 + 3],
                            weight0 = reader.ReadSingle(), weight1 = reader.ReadSingle(), weight2 = reader.ReadSingle(), weight3 = reader.ReadSingle()
                        };
                    }
                    mesh.boneWeights = weights;
                }
                var bind = new Matrix4x4[poses];
                for (int i = 0; i < poses; i++)
                {
                    var m = new Matrix4x4();
                    for (int k = 0; k < 16; k++)
                    {
                        m[k / 4, k % 4] = reader.ReadSingle();
                    }
                    bind[i] = m;
                }
                mesh.bindposes = bind;
                mesh.subMeshCount = submeshes;
                for (int s = 0; s < submeshes; s++)
                {
                    var triangles = new int[reader.ReadInt32()];
                    for (int i = 0; i < triangles.Length; i++)
                    {
                        triangles[i] = reader.ReadInt32();
                    }
                    mesh.SetTriangles(triangles, s, false);
                }
                for (int s = 0; s < shapes; s++)
                {
                    string shape = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));
                    Vector3[] delta = Vectors3(reader, count), normal = Vectors3(reader, count);
                    mesh.AddBlendShapeFrame(shape, 100f, delta, normal, null);
                }
                if ((flags & 2) == 0)
                {
                    mesh.RecalculateNormals();
                }
                if ((flags & 4) == 0 && (flags & 8) != 0)
                {
                    mesh.RecalculateTangents();
                }
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        private static Vector3[] Vectors3(BinaryReader reader, int count)
        {
            var values = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            }
            return values;
        }

        private static Vector2[] Vectors2(BinaryReader reader, int count)
        {
            var values = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            }
            return values;
        }

        private static Vector3 Vector(JToken v)
        {
            return new Vector3((float)v[0], (float)v[1], (float)v[2]);
        }
    }

    /// <summary>
    /// The game's materials by name.
    ///  - HDRP/Hair and HDRP/Lit are stock shaders of the same HDRP version: every float, colour, texture, keyword,
    ///    disabled pass and the render queue are copied, so those draw as in the game.
    ///  - The game's own graphs (SH_Character*, SH_InteriorAssetColorMask) are stood in for by HDRP/Lit: base colour
    ///    (with the colour mask multiplied in), normal map, mask map (AO in green, smoothness in alpha), alpha clipping,
    ///    surface type. An approximation: good for seeing hair against skin, not for judging the game's cloth shading.
    /// </summary>
    internal sealed class GameMaterials
    {
        private const string SkinTag = "PreviewSkin";

        private readonly string _folder;
        private readonly JObject _materials;
        private readonly JObject _textureInfo;
        private readonly Dictionary<string, Material> _made = new Dictionary<string, Material>();
        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
        private readonly Shader _hair = Shader.Find("HDRP/Hair");
        private readonly Shader _lit = Shader.Find("HDRP/Lit");

        public GameMaterials(string folder, JObject materials, JObject textures)
        {
            _folder = folder;
            _materials = materials;
            _textureInfo = textures;
        }

        public static bool IsSkin(Material material)
        {
            return material.GetTag(SkinTag, false, "") == "1";
        }

        public JObject Entry(string name)
        {
            return (JObject)_materials[name];
        }

        /// <summary>One shared instance per game material, as in the game.</summary>
        public Material Get(string name)
        {
            if (!_made.TryGetValue(name, out Material material))
            {
                material = Make(name);
                _made.Add(name, material);
            }
            return material;
        }

        /// <summary>A fresh copy of a game material in the state the game ships it in.</summary>
        public Material Make(string name)
        {
            JObject entry = Entry(name);
            string shader = (string)entry["shader"];
            return shader == "HDRP/Hair" || shader == "HDRP/Lit" ? Exact(name, entry, shader == "HDRP/Hair" ? _hair : _lit) : StandIn(name, entry, shader);
        }

        public Texture2D Texture(string name)
        {
            if (name == null)
            {
                return null;
            }
            if (!_textures.TryGetValue(name, out Texture2D texture))
            {
                JToken info = _textureInfo[name];
                bool linear = info != null && (int)info["srgb"] == 0;
                texture = new Texture2D(4, 4, TextureFormat.RGBA32, true, linear) { name = name };
                texture.LoadImage(File.ReadAllBytes(Path.Combine(_folder, "textures", name + ".png")));
                texture.filterMode = FilterMode.Trilinear;
                texture.anisoLevel = 8;
                _textures.Add(name, texture);
            }
            return texture;
        }

        private Material Exact(string name, JObject entry, Shader shader)
        {
            var material = new Material(shader) { name = name };
            foreach (JProperty value in ((JObject)entry["floats"]).Properties())
            {
                if (material.HasProperty(value.Name))
                {
                    material.SetFloat(value.Name, (float)value.Value);
                }
            }
            foreach (JProperty value in ((JObject)entry["colors"]).Properties())
            {
                if (material.HasProperty(value.Name))
                {
                    material.SetColor(value.Name, Colour(value.Value));
                }
            }
            foreach (JProperty value in ((JObject)entry["textures"]).Properties())
            {
                if (material.HasProperty(value.Name))
                {
                    material.SetTexture(value.Name, Texture((string)value.Value["name"]));
                    material.SetTextureScale(value.Name, new Vector2((float)value.Value["scale"][0], (float)value.Value["scale"][1]));
                    material.SetTextureOffset(value.Name, new Vector2((float)value.Value["offset"][0], (float)value.Value["offset"][1]));
                }
            }
            var keywords = new List<string>();
            foreach (JToken keyword in (JArray)entry["keywords"])
            {
                keywords.Add((string)keyword);
            }
            material.shaderKeywords = keywords.ToArray();
            // HDRP derives which passes run from the floats; the game's files record the outcome. Start from all on.
            foreach (string pass in new[] { "TransparentDepthPrepass", "TransparentDepthPostpass", "TransparentBackface", "MOTIONVECTORS", "RayTracingPrepass" })
            {
                material.SetShaderPassEnabled(pass, true);
            }
            foreach (JToken pass in (JArray)entry["disabledPasses"])
            {
                material.SetShaderPassEnabled((string)pass, false);
            }
            material.renderQueue = (int)entry["queue"];
            return material;
        }

        private Material StandIn(string name, JObject entry, string shader)
        {
            var floats = (JObject)entry["floats"];
            var colours = (JObject)entry["colors"];
            var textures = (JObject)entry["textures"];
            var material = new Material(_lit) { name = name };

            Texture2D colourMap = Texture(TextureName(textures, "_BaseColorMap"));
            Texture2D mask = Texture(TextureName(textures, "_ColorMaskMap"));
            Color tint = colours["_BaseColor"] != null ? Colour(colours["_BaseColor"]) : Color.white;
            bool skin = false;
            if (Float(floats, "_ColorMaskMapHasRedChannel") > 0.5f && colours["_MaskColorRed"] != null && (mask == null || mask.width <= 64))
            {
                // One colour over the whole surface (skin tone, plain cloth): a tint the preview can change afterwards.
                Color red = Colour(colours["_MaskColorRed"]);
                tint = new Color(tint.r * red.r, tint.g * red.g, tint.b * red.b, tint.a);
                skin = name.Contains("Skin") || name.Contains("Face");
            }
            else if (mask != null && mask.width > 64 && colourMap != null)
            {
                colourMap = Tinted(colourMap, mask, floats, colours, name);
                skin = (name.Contains("Skin") || name.Contains("Face")) && Float(floats, "_ColorMaskMapHasRedChannel") > 0.5f;
                if (skin)
                {
                    // Body skin: the mask is red everywhere but a few moles; treat the tone as a tint.
                    colourMap = Texture(TextureName(textures, "_BaseColorMap"));
                    Color red = Colour(colours["_MaskColorRed"]);
                    tint = new Color(tint.r * red.r, tint.g * red.g, tint.b * red.b, tint.a);
                }
            }
            material.SetColor("_BaseColor", tint);
            if (colourMap != null)
            {
                material.SetTexture("_BaseColorMap", colourMap);
            }
            if (skin)
            {
                material.SetOverrideTag(SkinTag, "1");
            }

            Texture2D normal = Texture(TextureName(textures, "_NormalMap"));
            if (normal != null)
            {
                material.SetTexture("_NormalMap", normal);
                material.SetFloat("_NormalScale", floats["_NormalStrength"] != null ? Float(floats, "_NormalStrength") : 1f);
            }
            Texture2D maskMap = Texture(TextureName(textures, "_MaskMap"));
            if (maskMap != null && floats["_AOMin"] != null)   // the cloth graphs' masks are packed differently: left out
            {
                material.SetTexture("_MaskMap", maskMap);
                material.SetFloat("_SmoothnessRemapMin", floats["_SmoothnessMin"] != null ? Float(floats, "_SmoothnessMin") : 0f);
                material.SetFloat("_SmoothnessRemapMax", floats["_SmoothnessMax"] != null ? Float(floats, "_SmoothnessMax") : 1f);
                material.SetFloat("_AORemapMin", floats["_AOMin"] != null ? Float(floats, "_AOMin") : 0f);
                material.SetFloat("_AORemapMax", floats["_AOMax"] != null ? Float(floats, "_AOMax") : 1f);
            }
            // The cloth graphs pack their mask differently (red is not metal); nothing a character wears is metal.
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.25f);
            material.SetFloat("_MetallicRemapMin", 0f);
            material.SetFloat("_MetallicRemapMax", 0f);
            material.SetFloat("_SurfaceType", Float(floats, "_SurfaceType"));
            material.SetFloat("_AlphaCutoffEnable", Float(floats, "_AlphaCutoffEnable"));
            material.SetFloat("_AlphaCutoff", floats["_AlphaClippingThreshold"] != null ? Float(floats, "_AlphaClippingThreshold") : 0.5f);
            material.SetFloat("_DoubleSidedEnable", Float(floats, "_DoubleSidedEnable"));
            material.SetFloat("_TransparentSortPriority", Float(floats, "_TransparentSortPriority"));
            HDMaterial.ValidateMaterial(material);
            material.renderQueue = (int)entry["queue"];
            return material;
        }

        // Base colour with every switched-on channel of the colour mask multiplied in, done once on the CPU.
        private Texture2D Tinted(Texture2D colourMap, Texture2D mask, JObject floats, JObject colours, string name)
        {
            int width = colourMap.width, height = colourMap.height;
            Color[] pixels = colourMap.GetPixels();
            var channels = new[] { "Red", "Green", "Blue", "Alpha" };
            var tints = new Color[4];
            var on = new bool[4];
            for (int c = 0; c < 4; c++)
            {
                on[c] = Float(floats, "_ColorMaskMapHas" + channels[c] + "Channel") > 0.5f && colours["_MaskColor" + channels[c]] != null;
                tints[c] = on[c] ? Colour(colours["_MaskColor" + channels[c]]) : Color.white;   // stored, like the pixels, in sRGB
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color m = mask.GetPixelBilinear((x + 0.5f) / width, (y + 0.5f) / height);
                    Color p = pixels[y * width + x];
                    for (int c = 0; c < 4; c++)
                    {
                        if (on[c] && m[c] > 0.004f)
                        {
                            p.r *= Mathf.Lerp(1f, tints[c].r, m[c]);
                            p.g *= Mathf.Lerp(1f, tints[c].g, m[c]);
                            p.b *= Mathf.Lerp(1f, tints[c].b, m[c]);
                        }
                    }
                    pixels[y * width + x] = p;
                }
            }
            var baked = new Texture2D(width, height, TextureFormat.RGBA32, true, false) { name = name + " tinted", filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            baked.SetPixels(pixels);
            baked.Apply(true);
            return baked;
        }

        private static string TextureName(JObject textures, string property)
        {
            return textures[property] != null ? (string)textures[property]["name"] : null;
        }

        private static float Float(JObject floats, string name)
        {
            return floats[name] != null ? (float)floats[name] : 0f;
        }

        private static Color Colour(JToken c)
        {
            return new Color((float)c[0], (float)c[1], (float)c[2], (float)c[3]);
        }
    }
}
