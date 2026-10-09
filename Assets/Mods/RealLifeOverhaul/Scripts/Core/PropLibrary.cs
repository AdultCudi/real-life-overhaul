using System;
using System.Collections.Generic;
using System.IO;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The modelled street props: cones, barriers, tape, the stretcher, police and paramedic
    /// equipment (the list, with sizes and pivots, is in PropAssets/README.md).
    ///
    /// They live in one asset bundle next to the mod's DLL (Bundles/reallife_props) that holds
    /// static meshes, one shared texture atlas (colour and mask map) and a catalogue; no
    /// materials, shaders, colliders or scripts. The bundle is read once per game session, in
    /// the background, and stays in memory. Every prop is one mesh drawn with one of two
    /// materials made here from the game's lit shader, both on the same atlas and both
    /// instanced: "lit", and "glow" for what should still read at night (tape, cones, flare,
    /// reflective lettering), which lights itself faintly in its own colours. Nothing here runs
    /// per frame, and nothing is made per prop or per scene.
    ///
    /// Whoever builds a prop asks <see cref="TryGet"/> and falls back to its own plain shape
    /// when that says no: while the bundle is still loading, if it is missing, or if it has no
    /// such prop (logged once).
    /// </summary>
    public static class PropLibrary
    {
        private const string BundlePath = "Bundles/reallife_props";
        private const string CatalogueName = "catalogue";
        private const string BaseTexture = "props_base";
        private const string MaskTexture = "props_mask";
        private const string LitShader = "HDRP/Lit";
        private const string GlowKind = "glow";
        private const int SupportedVersion = 1;
        private const float Glow = 1.5f;             // how strongly the glow material shows in the dark, in the units Shapes.Paint uses
        private const float PlainSmoothness = 0.3f;  // used only if the game's build of the shader has no mask-map variant

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");
        private static readonly int MaskMapId = Shader.PropertyToID("_MaskMap");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        private static readonly int AlbedoAffectEmissiveId = Shader.PropertyToID("_AlbedoAffectEmissive");

        // ---------------------------------------------------------------- the catalogue as written by the asset pipeline

#pragma warning disable 0649   // filled in from the catalogue file
        private sealed class CatalogueFile
        {
            public int version;
            public ItemFile[] items;
        }

        private sealed class ItemFile
        {
            public string id;
            public string mesh;
            public string material;
            public float[] size;
        }
#pragma warning restore 0649

        private sealed class Prop
        {
            public Mesh Mesh;
            public bool Glows;
            public Vector3 Size;
        }

        private enum State { NotStarted, Loading, Ready, Failed }

        public static IModLogger Logger;

        private static readonly Dictionary<string, Prop> Props = new Dictionary<string, Prop>(48);
        private static readonly HashSet<string> MissingLogged = new HashSet<string>();
        private static readonly Dictionary<int, Material> Tints = new Dictionary<int, Material>(8);   // by 0xBBGGRR: see Tinted
        private static State _state = State.NotStarted;
        private static Texture2D _base;
        private static Texture2D _mask;
        private static Material _lit;
        private static Material _glow;
        private static bool _shaderMissing;

        /// <summary>True once the bundle has loaded (asynchronously, once); false while loading or if it failed.</summary>
        public static bool Ready => _state == State.Ready;

        /// <summary>Starts the one-off asynchronous load. Safe to call repeatedly.</summary>
        public static void Load(ModContext context)
        {
            if (_state != State.NotStarted || context == null)
            {
                return;
            }
            try
            {
                string path = Path.Combine(context.ModRootPath, BundlePath);
                if (!File.Exists(path))
                {
                    Fail("the asset bundle is not installed (" + path + ")");
                    return;
                }
                _state = State.Loading;
                AssetBundleCreateRequest opening = AssetBundle.LoadFromFileAsync(path);
                opening.completed += _ => Opened(opening);
            }
            catch (Exception e)
            {
                Fail("the asset bundle could not be opened", e);
            }
        }

        /// <summary>
        /// The shared mesh and ready-made shared material for a prop id ("cone", "barrier", ...).
        /// False if the bundle is not ready or has no such prop: the caller then falls back to its primitive.
        /// </summary>
        public static bool TryGet(string id, out Mesh mesh, out Material material)
        {
            mesh = null;
            material = null;
            if (_state != State.Ready || id == null)
            {
                return false;
            }
            if (!Props.TryGetValue(id, out Prop prop))
            {
                if (MissingLogged.Add(id))
                {
                    Logger?.Warn("Props: '" + id + "' is not in the asset bundle; its plain shape is used.");
                }
                return false;
            }
            material = Material(prop.Glows);
            if (material == null)
            {
                return false;
            }
            mesh = prop.Mesh;
            return true;
        }

        /// <summary>
        /// Whether the bundle is loaded and has this prop. Says nothing in the log (unlike <see cref="TryGet"/>),
        /// so a caller that tries one id and then another can ask freely.
        /// </summary>
        public static bool Has(string id)
        {
            return _state == State.Ready && id != null && Props.ContainsKey(id);
        }

        /// <summary>
        /// The lit atlas material with its colour multiplied by a tint: for a part modelled on a neutral
        /// swatch that takes its colour from code (a bicycle's frame, a motorbike's tank). ONE shared,
        /// instanced material per colour, made the first time that colour is asked for and kept until
        /// <see cref="Dispose"/>; never one per object. Null when the atlas material cannot be made.
        /// </summary>
        public static Material Tinted(Color tint)
        {
            Material lit = _state == State.Ready ? Material(false) : null;
            if (lit == null)
            {
                return null;
            }
            Color32 packed = tint;
            int key = packed.r | packed.g << 8 | packed.b << 16;
            if (!Tints.TryGetValue(key, out Material made) || made == null)
            {
                made = new Material(lit) { name = "RealLifeOverhaul Props (tinted)" };
                made.SetColor(BaseColorId, new Color(tint.r, tint.g, tint.b, 1f));
                made.enableInstancing = true;
                Tints[key] = made;
            }
            return made;
        }

        /// <summary>Size of the prop's bounds in metres, for placement. Zero if unknown.</summary>
        public static Vector3 SizeOf(string id)
        {
            return _state == State.Ready && id != null && Props.TryGetValue(id, out Prop prop) ? prop.Size : Vector3.zero;
        }

        /// <summary>
        /// Destroys what it created. Called when the city scene ends. That is the two materials;
        /// the bundle's meshes and textures stay loaded for the session and the materials are
        /// made again the next time a prop is asked for.
        /// </summary>
        public static void Dispose()
        {
            if (_lit != null)
            {
                UnityEngine.Object.Destroy(_lit);
            }
            if (_glow != null)
            {
                UnityEngine.Object.Destroy(_glow);
            }
            foreach (Material tinted in Tints.Values)
            {
                if (tinted != null)
                {
                    UnityEngine.Object.Destroy(tinted);
                }
            }
            Tints.Clear();
            _lit = null;
            _glow = null;
        }

        // ---------------------------------------------------------------- materials

        // One material per kind, made the first time a prop of that kind is asked for.
        private static Material Material(bool glows)
        {
            Material made = glows ? _glow : _lit;
            if (made != null || _shaderMissing)
            {
                return made;
            }
            Shader shader = Shader.Find(LitShader);
            if (shader == null)
            {
                _shaderMissing = true;
                Logger?.Warn("Props: the game's lit shader was not found; plain shapes are used.");
                return null;
            }
            made = new Material(shader) { name = glows ? "RealLifeOverhaul Props (glow)" : "RealLifeOverhaul Props" };
            made.SetColor(BaseColorId, Color.white);
            made.SetTexture(BaseColorMapId, _base);
            made.SetFloat(MetallicId, 0f);
            made.SetFloat(SmoothnessId, PlainSmoothness);
            if (_mask != null)
            {
                made.SetTexture(MaskMapId, _mask);
                made.EnableKeyword("_MASKMAP");
            }
            if (glows)
            {
                // Lights itself in its own colours: white bands and lettering show, black rubber stays black.
                made.SetFloat(AlbedoAffectEmissiveId, 1f);
                made.SetColor(EmissiveColorId, Color.white * Glow);
            }
            made.enableInstancing = true;
            if (glows)
            {
                _glow = made;
            }
            else
            {
                _lit = made;
            }
            return made;
        }

        // ---------------------------------------------------------------- loading

        private static void Opened(AssetBundleCreateRequest opening)
        {
            try
            {
                AssetBundle bundle = opening.assetBundle;
                if (bundle == null)
                {
                    Fail("the asset bundle could not be read (built for another Unity version?)");
                    return;
                }
                AssetBundleRequest reading = bundle.LoadAllAssetsAsync();
                reading.completed += _ => Loaded(reading);
            }
            catch (Exception e)
            {
                Fail("the asset bundle could not be read", e);
            }
        }

        private static void Loaded(AssetBundleRequest reading)
        {
            try
            {
                var meshes = new Dictionary<string, Mesh>(48);
                TextAsset catalogueText = null;
                foreach (UnityEngine.Object asset in reading.allAssets)
                {
                    if (asset is Mesh mesh)
                    {
                        meshes[mesh.name] = mesh;
                    }
                    else if (asset is Texture2D texture)
                    {
                        if (texture.name == BaseTexture)
                        {
                            _base = texture;
                        }
                        else if (texture.name == MaskTexture)
                        {
                            _mask = texture;
                        }
                    }
                    else if (asset is TextAsset text && text.name == CatalogueName)
                    {
                        catalogueText = text;
                    }
                }
                if (catalogueText == null || _base == null)
                {
                    Fail("the asset bundle has no " + (catalogueText == null ? "catalogue" : "texture atlas"));
                    return;
                }
                // Not JsonUtility: Unity's serializer does not know classes from an assembly loaded at run time,
                // and silently leaves their arrays empty.
                CatalogueFile catalogue = Newtonsoft.Json.JsonConvert.DeserializeObject<CatalogueFile>(catalogueText.text);
                if (catalogue == null || catalogue.version != SupportedVersion || catalogue.items == null)
                {
                    Fail("the prop catalogue is version " + (catalogue != null ? catalogue.version : 0) + " with "
                         + (catalogue != null && catalogue.items != null ? catalogue.items.Length : 0) + " items; this build reads version " + SupportedVersion);
                    return;
                }
                int triangles = 0;
                foreach (ItemFile file in catalogue.items)
                {
                    if (string.IsNullOrEmpty(file.id) || string.IsNullOrEmpty(file.mesh) || !meshes.TryGetValue(file.mesh, out Mesh mesh))
                    {
                        Logger?.Warn("Props: '" + file.id + "' is left out, its mesh is not in the bundle.");
                        continue;
                    }
                    Props[file.id] = new Prop
                    {
                        Mesh = mesh,
                        Glows = file.material == GlowKind,
                        Size = file.size != null && file.size.Length == 3 ? new Vector3(file.size[0], file.size[1], file.size[2]) : mesh.bounds.size
                    };
                    triangles += (int)mesh.GetIndexCount(0) / 3;
                }
                _state = State.Ready;
                Logger?.Info("Props: " + Props.Count + " loaded from the asset bundle (" + triangles + " triangles in all, one " + _base.width + " x " + _base.height
                             + " atlas" + (_mask != null ? " with a mask map" : "") + ").");
            }
            catch (Exception e)
            {
                Fail("the prop catalogue could not be read", e);
            }
        }

        private static void Fail(string problem, Exception error = null)
        {
            _state = State.Failed;
            Logger?.Warn("Props: switched off, " + problem + ". Plain shapes are used instead.");
            if (error != null)
            {
                Logger?.Error(error);
            }
        }
    }
}
