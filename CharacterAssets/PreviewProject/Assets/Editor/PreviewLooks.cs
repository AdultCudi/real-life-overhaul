using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using RealLifeOverhaul;
using UnityEngine;

namespace CharacterPreview
{
    /// <summary>
    /// Puts a candidate look on the preview character: the hook between a shot's "finish" list and the code
    /// that does the same in the game (Character/HairFinish.cs and friends are compiled into this project from
    /// the mod's own folder by preview.ps1). With no finish, the character is the game's own.
    ///
    /// Finish entries:  "wear:top_m_knit"   a garment of the build folder in place of the game's piece of its slot
    ///                  "hair"              finished hair layers with the default settings
    ///                  "hair:core=0.95,gain=3,edge=0.04,tile=3,normal=0.6,calm=1,maps=0"   the same, with overrides
    ///                  "underlay"          the mod's scalp cap under the game's hair (ModLooks: an underlay), from the build folder
    ///                  "underlay:old=1"    the same with the numbers it had before 11 October (the game's transmission colour)
    ///                  "underlay"          the mod's scalp cap under the game's hair (ModLooks: an underlay), from the build folder
    ///                  "underlay:old=1"    the same with the numbers it had before 11 October (the game's transmission colour)
    ///                  "skin"              finished skin (Character/SkinFinish.cs) with the default settings
    ///                  "skin:smooth=0.9,dark=0.75,pores=0.12,body=0.06,grain=0.3,scale=2,compress=1"   the same, with overrides
    /// </summary>
    internal static class PreviewLooks
    {
        /// <summary>CharacterAssets\build: the meshes and textures a look would ship with.</summary>
        public static string Folder;

        // The alpha-tested hair material whose keyword set finished layers borrow; in the game it is found on the character.
        private const string TemplateName = "M_FemHair_01";

        private static readonly List<Material> Made = new List<Material>();
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        private static readonly HashSet<string> Logged = new HashSet<string>();
        private static Material _template;

        public static void Apply(GameCharacter character, JToken shot, Color hair, Color skin)
        {
            foreach (JToken entry in (JArray)shot["finish"] ?? new JArray())
            {
                string text = (string)entry;
                string kind = text.Split(':')[0];
                Dictionary<string, float> options = Options(text);
                if (kind == "hair")
                {
                    FinishHair(character, options, hair);
                }
                else if (kind == "skin")
                {
                    FinishSkin(character, options, skin);
                }
                else if (kind == "underlay")
                {
                    Underlay(character, options, hair, (string)shot["sex"]);
                }
                else if (kind == "wear")
                {
                    Wear(character, text.Substring(text.IndexOf(':') + 1), shot);
                }
                else
                {
                    Debug.LogWarning("[CharacterPreview] unknown finish '" + text + "'");
                }
            }
        }

        public static void Undo()
        {
            foreach (Material material in Made)
            {
                Object.DestroyImmediate(material);
            }
            Made.Clear();
        }

        private static void FinishHair(GameCharacter character, Dictionary<string, float> options, Color colour)
        {
            HairFinish.Settings settings = HairFinish.Settings.Default;
            settings.CoreAlpha = Option(options, "core", settings.CoreAlpha);
            settings.EdgeAlpha = Option(options, "edge", settings.EdgeAlpha);
            settings.NormalStrength = Option(options, "normal", settings.NormalStrength);
            settings.Calm = Option(options, "calm", settings.Calm);
            float tile = Option(options, "tile", settings.StrandTiling.x);
            settings.StrandTiling = new Vector2(tile, Option(options, "tilev", settings.StrandTiling.y));
            settings.AlphaGain = Option(options, "gain", settings.AlphaGain);
            settings.WantedCore = Option(options, "want", settings.WantedCore);
            settings.Bounded = Option(options, "bounded", settings.Bounded ? 1f : 0f) > 0.5f;
            settings.Deep = Option(options, "deep", settings.Deep);                                  // 1 = before 13 October
            settings.KeepCover = Option(options, "keep", settings.KeepCover ? 1f : 0f) > 0.5f;     // 0 = before 13 October
            bool maps = Option(options, "maps", 1f) > 0.5f;
            Texture2D mask = maps ? Texture("hair_strand_mask", true) : null;
            Texture2D normal = maps ? Texture("hair_strand_normal", true) : null;
            if (_template == null)
            {
                _template = character.Materials.Make(TemplateName);
            }

            foreach (string slot in new[] { "Hair", "Beard" })
            {
                SkinnedMeshRenderer renderer = character.Find(slot);
                if (renderer == null)
                {
                    continue;
                }
                // The preview's shared materials are the game's; a shot gets its own copies and puts nothing back.
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material finished = HairFinish.Make(materials[i], _template, mask, normal, settings);
                    if (finished != null)
                    {
                        bool strands = materials[i].IsKeywordEnabled(HairFinish.TransparentKeyword);
                        if (settings.AlphaGain > 0f && strands)
                        {
                            Texture source = finished.GetTexture("_BaseColorMap");
                            string key = source.name + settings.AlphaGain + "/" + settings.WantedCore + "/" + settings.Deep + "/" + settings.KeepCover;
                            if (!Textures.TryGetValue(key, out Texture2D denser))
                            {
                                denser = HairFinish.Denser(source, settings, out HairRules.Cover cover);
                                Textures.Add(key, denser);
                                Debug.Log("[CharacterPreview] strands '" + source.name + "': gain " + cover.Gain.ToString("F2", CultureInfo.InvariantCulture) + ", core "
                                          + (cover.Core * 100f).ToString("F1", CultureInfo.InvariantCulture) + "%, edge " + (cover.Edge * 100f).ToString("F1", CultureInfo.InvariantCulture)
                                          + "%, empty " + (cover.Empty * 100f).ToString("F1", CultureInfo.InvariantCulture) + "%");
                            }
                            finished.SetTexture("_BaseColorMap", denser);
                        }
                        HairRules.Finish rule = HairFinish.Colour(finished, materials[i], colour, mask != null && strands, settings);
                        if (Logged.Add(materials[i].name + ColorUtility.ToHtmlStringRGB(colour) + settings.Bounded))
                        {
                            Debug.Log("[CharacterPreview] layer '" + materials[i].name + "' hair " + ColorUtility.ToHtmlStringRGB(colour) + (settings.Bounded ? " bounded" : " as before")
                                      + ": through " + ColorUtility.ToHtmlStringRGB(HairFinish.Through(rule)) + " rim " + rule.Rim.ToString("F3", CultureInfo.InvariantCulture)
                                      + ", specular " + rule.Specular.ToString("F3", CultureInfo.InvariantCulture) + "/" + rule.Secondary.ToString("F3", CultureInfo.InvariantCulture)
                                      + ", gloss " + rule.GlossMin.ToString("F2", CultureInfo.InvariantCulture) + "-" + rule.GlossMax.ToString("F2", CultureInfo.InvariantCulture)
                                      + "; per unit of light: front-lit " + rule.FrontLit.ToString("F4", CultureInfo.InvariantCulture) + ", highlight at most "
                                      + rule.SheenPeak.ToString("F4", CultureInfo.InvariantCulture) + ", back-light at most " + rule.ThroughPeak.ToString("F4", CultureInfo.InvariantCulture)
                                      + " (lit skin " + HairRules.SkinLit.ToString("F4", CultureInfo.InvariantCulture) + ")");
                        }
                        Made.Add(finished);
                        _originals[finished] = materials[i];
                        materials[i] = finished;
                        changed = true;
                    }
                }
                if (changed)
                {
                    _swapped.Add(new KeyValuePair<SkinnedMeshRenderer, Material[]>(renderer, renderer.sharedMaterials));
                    renderer.sharedMaterials = materials;
                }
            }
        }

        // ---------------------------------------------------------------- skin

        private static readonly SkinFinish.Maps SkinMaps = new SkinFinish.Maps();

        /// <summary>
        /// The skin finish on the stand-in skin materials, through the code that ships: HDRP/Lit reads the same
        /// _MaskMap (occlusion in green, smoothness in alpha) and _NormalMap the game's skin graphs do.
        /// </summary>
        private static void FinishSkin(GameCharacter character, Dictionary<string, float> options, Color tone)
        {
            SkinFinish.Settings settings = SkinFinish.Settings.Default;
            settings.Smooth = Option(options, "smooth", settings.Smooth);
            settings.SmoothDark = Option(options, "dark", settings.SmoothDark);
            settings.Pores = Option(options, "pores", settings.Pores);
            settings.PoresBody = Option(options, "body", settings.PoresBody);
            settings.Grain = Option(options, "grain", settings.Grain);
            settings.Scale = Option(options, "scale", settings.Scale);
            settings.Compress = Option(options, "compress", settings.Compress ? 1f : 0f) > 0.5f;
            double before = SkinMaps.Milliseconds;
            int made = SkinMaps.MadeEver;
            foreach (SkinnedMeshRenderer renderer in character.Shown)
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || !GameMaterials.IsSkin(materials[i]))
                    {
                        continue;
                    }
                    // In the game every character has its own copy of a skin material already; here a shot makes one.
                    var own = new Material(materials[i]) { name = materials[i].name };
                    own.SetOverrideTag("PreviewSkin", "1");
                    SkinFinish.Treat(own, own.name.Contains("Face"), tone, settings, SkinMaps);
                    Made.Add(own);
                    materials[i] = own;
                    changed = true;
                }
                if (changed)
                {
                    _swapped.Add(new KeyValuePair<SkinnedMeshRenderer, Material[]>(renderer, renderer.sharedMaterials));
                    renderer.sharedMaterials = materials;
                }
            }
            if (SkinMaps.MadeEver > made)
            {
                Debug.Log("[CharacterPreview] skin maps: " + (SkinMaps.MadeEver - made) + " made in " + (SkinMaps.Milliseconds - before).ToString("F0") + " ms, "
                          + SkinMaps.Count + " held, " + (SkinMaps.Bytes / 1048576f).ToString("F2") + " MB");
            }
        }

        // ---------------------------------------------------------------- garments

        private static readonly string[] Slots = { "hair", "Hair", "beard", "Beard", "top", "Torso", "legs", "Legs", "feet", "Feet" };
        private static readonly List<GameObject> Worn = new List<GameObject>();
        private static readonly List<KeyValuePair<SkinnedMeshRenderer, Mesh>> Bared = new List<KeyValuePair<SkinnedMeshRenderer, Mesh>>();

        /// <summary>
        /// Wears a garment of the build folder the way the mod does: its mesh on the character's bones with the head
        /// mesh's bind poses, in place of the game's piece of that slot, of which only the skin parts stay.
        /// The cloth material is the same HDRP/Lit stand-in as the game's clothes get here.
        /// </summary>
        private static void Wear(GameCharacter character, string id, JToken shot)
        {
            JToken item = null;
            foreach (string catalogue in Directory.GetFiles(Folder, "*catalogue_*.json"))
            {
                foreach (JToken candidate in (JArray)JObject.Parse(File.ReadAllText(catalogue))["items"])
                {
                    if ((string)candidate["id"] == id)
                    {
                        item = candidate;
                    }
                }
            }
            if (item == null)
            {
                Debug.LogWarning("[CharacterPreview] no garment '" + id + "' in " + Folder);
                return;
            }
            string slot = Slots[System.Array.IndexOf(Slots, (string)item["kind"]) + 1];
            SkinnedMeshRenderer head = character.Find("MaleHead") ?? character.Find("FemaleHead");
            SkinnedMeshRenderer game = character.Find(slot);

            Mesh mesh = LookMesh(id, (string)item["mesh"], head);

            var layers = (JArray)item["layers"];
            var materials = new Material[layers.Count];
            Color tint = Color.white;   // as in the game (ModLooks.FinishCloth): the mod's cloth takes its colour from its texture alone
            for (int i = 0; i < materials.Length; i++)
            {
                var material = new Material(Shader.Find("HDRP/Lit")) { name = id + " cloth" };
                Texture2D colourMap = Texture((string)layers[i]["texture"], false);
                if (layers[i]["tintMask"] != null && layers[i]["tintMask"].Type != JTokenType.Null)
                {
                    colourMap = Tinted(id, colourMap, Texture((string)layers[i]["tintMask"], true), (JArray)layers[i]["tints"]);
                }
                material.SetTexture("_BaseColorMap", colourMap);
                material.SetColor("_BaseColor", tint);
                if (layers[i]["normal"] != null)
                {
                    material.SetTexture("_NormalMap", Texture((string)layers[i]["normal"], true));
                }
                material.SetFloat("_Smoothness", 0.25f);
                UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(material);
                materials[i] = material;
                Made.Add(material);
            }
            var holder = new GameObject("RealLifeOverhaul " + id);
            holder.transform.SetParent(head.transform.parent, false);
            var renderer = holder.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = head.bones;
            renderer.rootBone = head.rootBone;
            renderer.updateWhenOffscreen = true;
            renderer.sharedMaterials = materials;
            foreach (JProperty shape in ((JObject)shot["shapes"] ?? new JObject()).Properties())
            {
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    if (string.Equals(mesh.GetBlendShapeName(i), shape.Name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        renderer.SetBlendShapeWeight(i, (float)shape.Value);
                    }
                }
            }
            Worn.Add(holder);

            // Of the game's piece, only the skin stays (hands, neck): the mod's skinFrom path.
            if (game != null)
            {
                Mesh bare = Object.Instantiate(game.sharedMesh);
                Material[] own = game.sharedMaterials;
                for (int i = 0; i < bare.subMeshCount && i < own.Length; i++)
                {
                    if (own[i] == null || !GameMaterials.IsSkin(own[i]))
                    {
                        bare.SetTriangles(new int[0], i);
                    }
                    else if (item["skinHide"] != null && item["skinHide"].Type != JTokenType.Null)
                    {
                        // As ModLooks.SkinMesh: the skin under the cloth is not drawn.
                        int[] all = bare.GetTriangles(i);
                        if ((int)item["skinHide"]["triangles"] * 3 == all.Length)
                        {
                            var drop = new HashSet<int>(item["skinHide"]["hidden"].ToObject<int[]>());
                            var kept = new List<int>(all.Length);
                            for (int t = 0; t < all.Length / 3; t++)
                            {
                                if (!drop.Contains(t))
                                {
                                    kept.Add(all[t * 3]);
                                    kept.Add(all[t * 3 + 1]);
                                    kept.Add(all[t * 3 + 2]);
                                }
                            }
                            bare.SetTriangles(kept.ToArray(), i);
                        }
                        else
                        {
                            Debug.LogWarning("[CharacterPreview] skinHide of '" + id + "' does not match the game's mesh (" + (all.Length / 3) + " triangles)");
                        }
                    }
                }
                Bared.Add(new KeyValuePair<SkinnedMeshRenderer, Mesh>(game, game.sharedMesh));
                game.sharedMesh = bare;
            }
        }

        // ---------------------------------------------------------------- the scalp underlay

        private const string ScalpTemplate = "M_MaleHair03_Scalp";   // what the mod's log names as its Scalp template in the city
        private const float UnderlayShade = 0.72f;                    // ModLooks.UnderlayShade

        /// <summary>
        /// The mod's scalp cap under the worn hairstyle, as ModLooks makes it: a copy of one of the game's soft scalp
        /// materials with the footprint texture of the style, one-sided, drawn before every hair layer, in the hair's
        /// colour a little darkened. Its numbers come from the code that ships (HairFinish.Underlay).
        /// </summary>
        private static void Underlay(GameCharacter character, Dictionary<string, float> options, Color hair, string sex)
        {
            SkinnedMeshRenderer worn = character.Find("Hair");
            SkinnedMeshRenderer head = character.Find("MaleHead") ?? character.Find("FemaleHead");
            string catalogue = Path.Combine(Folder ?? "", "catalogue_underlays.json");
            if (worn == null || worn.sharedMesh == null || head == null || !File.Exists(catalogue))
            {
                return;
            }
            JToken item = null;
            foreach (JToken candidate in (JArray)JObject.Parse(File.ReadAllText(catalogue))["items"])
            {
                if ((string)candidate["under"] == worn.sharedMesh.name && string.Equals((string)candidate["sex"], sex, System.StringComparison.OrdinalIgnoreCase))
                {
                    item = candidate;
                }
            }
            if (item == null)
            {
                Debug.LogWarning("[CharacterPreview] no underlay for '" + worn.sharedMesh.name + "' (" + sex + ")");
                return;
            }
            Material material = character.Materials.Make(ScalpTemplate);
            material.name = "RealLifeOverhaul " + (string)item["id"] + " Scalp";
            material.SetTexture("_BaseColorMap", Texture((string)item["layers"][0]["texture"], false));
            // ModLooks.FinishHair for a scalp layer without maps of its own.
            material.SetFloat("_NormalScale", 0f);
            foreach (string cull in new[] { "_CullMode", "_CullModeForward", "_TransparentCullMode", "_OpaqueCullMode" })
            {
                material.SetFloat(cull, (float)UnityEngine.Rendering.CullMode.Back);
            }
            material.SetFloat("_DoubleSidedEnable", 0f);
            var shade = new Color(hair.r * UnderlayShade, hair.g * UnderlayShade, hair.b * UnderlayShade, 1f);
            if (Option(options, "old", 0f) > 0.5f)
            {
                // As shipped until 11 October (ModLooks.TuneUnderlay): matt, but the game's orange transmission colour kept.
                material.renderQueue = 2998;
                material.SetFloat("_TransparentSortPriority", -2f);
                material.SetFloat("_Specular", Mathf.Min(material.GetFloat("_Specular"), 0.04f));
                material.SetFloat("_SecondarySpecular", Mathf.Min(material.GetFloat("_SecondarySpecular"), 0.04f));
                material.SetFloat("_SmoothnessMax", Mathf.Min(material.GetFloat("_SmoothnessMax"), 0.25f));
                material.SetFloat("_SmoothnessMin", Mathf.Min(material.GetFloat("_SmoothnessMin"), 0.15f));
                material.SetFloat("_TransmissionRim", 0f);
                material.SetColor("_BaseColor", shade);
            }
            else
            {
                HairFinish.Underlay(material, shade);
            }
            Made.Add(material);

            var holder = new GameObject("RealLifeOverhaul " + (string)item["id"]);
            holder.transform.SetParent(head.transform.parent, false);
            var renderer = holder.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = LookMesh((string)item["id"], (string)item["mesh"], head);
            renderer.bones = head.bones;
            renderer.rootBone = head.rootBone;
            renderer.updateWhenOffscreen = true;
            renderer.sharedMaterials = new[] { material };
            Worn.Add(holder);
        }

        /// <summary>A mesh of the build folder, on the character's bones with the bind poses the mod gives every look.</summary>
        private static Mesh LookMesh(string id, string meshName, SkinnedMeshRenderer head)
        {
            JObject file = JObject.Parse(File.ReadAllText(Path.Combine(Folder, "meshes", meshName + ".json")));
            float[] v = file["vertices"].ToObject<float[]>(), n = file["normals"].ToObject<float[]>(), uv = file["uv"].ToObject<float[]>();
            int[] bi = file["boneIndices"].ToObject<int[]>();
            float[] bw = file["boneWeights"].ToObject<float[]>();
            int count = v.Length / 3;
            var vertices = new Vector3[count];
            var normals = new Vector3[count];
            var uvs = new Vector2[count];
            var weights = new BoneWeight[count];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(v[i * 3], v[i * 3 + 1], v[i * 3 + 2]);
                normals[i] = new Vector3(n[i * 3], n[i * 3 + 1], n[i * 3 + 2]);
                uvs[i] = new Vector2(uv[i * 2], uv[i * 2 + 1]);
                weights[i] = new BoneWeight
                {
                    boneIndex0 = bi[i * 4], boneIndex1 = bi[i * 4 + 1], boneIndex2 = bi[i * 4 + 2], boneIndex3 = bi[i * 4 + 3],
                    weight0 = bw[i * 4], weight1 = bw[i * 4 + 1], weight2 = bw[i * 4 + 2], weight3 = bw[i * 4 + 3]
                };
            }
            var mesh = new Mesh { name = id, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, vertices = vertices, normals = normals, uv = uvs, boneWeights = weights };
            var submeshes = (JArray)file["submeshes"];
            mesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++)
            {
                mesh.SetTriangles(submeshes[i]["triangles"].ToObject<int[]>(), i);
            }
            mesh.bindposes = WornBindposes(head);
            mesh.RecalculateTangents();
            // The body shapes, as the bundle has them: the game's sliders move the garment with the body.
            foreach (JToken shape in (JArray)file["shapes"] ?? new JArray())
            {
                float[] d = shape["deltas"].ToObject<float[]>();
                var deltas = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    deltas[i] = new Vector3(d[i * 3], d[i * 3 + 1], d[i * 3 + 2]);
                }
                mesh.AddBlendShapeFrame((string)shape["name"], 100f, deltas, null, null);
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        // The mod gives every look the bind poses of the game's MALE head mesh, whoever wears it (ModAssets.Prepare): the
        // looks are modelled in that mesh's space. The woman's head mesh is bound 7 mm further along the body's depth, so
        // with her own head's bind poses a look sat 7 mm off here, and not in the game.
        private static Matrix4x4[] _wornBind;

        private static Matrix4x4[] WornBindposes(SkinnedMeshRenderer head)
        {
            if (_wornBind == null)
            {
                string[] args = System.Environment.GetCommandLineArgs();
                int at = System.Array.IndexOf(args, "-reference");
                string file = at >= 0 && at + 1 < args.Length ? Path.Combine(args[at + 1], "meshes", "MaleFace1.bam") : null;
                if (file != null && File.Exists(file))
                {
                    _wornBind = GameCharacter.ReadMesh(file, "MaleFace1").bindposes;
                }
                else
                {
                    Debug.LogWarning("[CharacterPreview] no male head mesh in the reference: looks get the worn head's bind poses");
                }
            }
            return _wornBind != null && _wornBind.Length == head.sharedMesh.bindposes.Length ? _wornBind : head.sharedMesh.bindposes;
        }

        // A tint mask as the game's cloth graph is taken to use it (and as GameCharacter stands in for the game's own
        // clothes): the base map times one colour per mask channel. Done once per colourway on the CPU.
        private static Texture2D Tinted(string id, Texture2D colourMap, Texture2D mask, JArray tints)
        {
            if (Textures.TryGetValue("tinted " + id, out Texture2D made))
            {
                return made;
            }
            int width = colourMap.width, height = colourMap.height;
            if (mask.width > width)
            {
                width = mask.width;
                height = mask.height;
            }
            var colours = new Color[3];
            for (int c = 0; c < 3; c++)
            {
                colours[c] = c < tints.Count ? new Color((float)tints[c][0], (float)tints[c][1], (float)tints[c][2]) : Color.white;
            }
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width, v = (y + 0.5f) / height;
                    Color p = colourMap.GetPixelBilinear(u, v);
                    Color m = mask.GetPixelBilinear(u, v);
                    for (int c = 0; c < 3; c++)
                    {
                        p.r *= Mathf.Lerp(1f, colours[c].r, m[c]);
                        p.g *= Mathf.Lerp(1f, colours[c].g, m[c]);
                        p.b *= Mathf.Lerp(1f, colours[c].b, m[c]);
                    }
                    p.a = 1f;
                    pixels[y * width + x] = p;
                }
            }
            made = new Texture2D(width, height, TextureFormat.RGBA32, true, false) { name = id + " tinted", filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            made.SetPixels(pixels);
            made.Apply(true);
            Textures.Add("tinted " + id, made);
            return made;
        }

        private static readonly Dictionary<Material, Material> _originals = new Dictionary<Material, Material>();
        private static readonly List<KeyValuePair<SkinnedMeshRenderer, Material[]>> _swapped = new List<KeyValuePair<SkinnedMeshRenderer, Material[]>>();

        /// <summary>Gives every renderer its game materials back (before the next shot dresses the character).</summary>
        public static void Restore()
        {
            foreach (KeyValuePair<SkinnedMeshRenderer, Material[]> pair in _swapped)
            {
                if (pair.Key != null)
                {
                    pair.Key.sharedMaterials = pair.Value;
                }
            }
            _swapped.Clear();
            _originals.Clear();
            foreach (KeyValuePair<SkinnedMeshRenderer, Mesh> pair in Bared)
            {
                Object.DestroyImmediate(pair.Key.sharedMesh);
                pair.Key.sharedMesh = pair.Value;
            }
            Bared.Clear();
            foreach (GameObject worn in Worn)
            {
                Object.DestroyImmediate(worn);
            }
            Worn.Clear();
        }

        private static Texture2D Texture(string name, bool linear)
        {
            if (!Textures.TryGetValue(name, out Texture2D texture))
            {
                string file = Path.Combine(Folder ?? "", "textures", name + ".png");
                if (!File.Exists(file))
                {
                    Debug.LogWarning("[CharacterPreview] look texture missing: " + file);
                    return null;
                }
                texture = new Texture2D(4, 4, TextureFormat.RGBA32, true, linear) { name = name };
                texture.LoadImage(File.ReadAllBytes(file));
                texture.wrapMode = TextureWrapMode.Repeat;
                texture.filterMode = FilterMode.Trilinear;
                texture.anisoLevel = 8;
                Textures.Add(name, texture);
            }
            return texture;
        }

        private static Dictionary<string, float> Options(string text)
        {
            var options = new Dictionary<string, float>();
            int colon = text.IndexOf(':');
            if (colon >= 0)
            {
                foreach (string pair in text.Substring(colon + 1).Split(','))
                {
                    string[] sides = pair.Split('=');
                    if (sides.Length == 2)
                    {
                        options[sides[0].Trim()] = float.Parse(sides[1], CultureInfo.InvariantCulture);
                    }
                }
            }
            return options;
        }

        private static float Option(Dictionary<string, float> options, string name, float fallback)
        {
            return options.TryGetValue(name, out float value) ? value : fallback;
        }
    }
}
