using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using BAModAPI;
using UnityEngine;
using UnityEngine.Rendering;

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
    ///
    /// PROPS NEWER THAN THE BUNDLE. Building the bundle takes Unity; a prop remade since it was last built can ship as
    /// the asset pipeline's own interchange files instead (Bundles/props_newer: a catalogue and a mesh file per prop,
    /// already in Unity's space, written by PropAssets/scripts/newer_props.py from the same build the bundle is made
    /// from). They are read in the background while the bundle loads, made into meshes as the bundle's builder makes
    /// them, and used in place of the bundle's own of the same id, with the bundle's atlas. All of them or none: a
    /// remade prop can depend on another (the city bus's body on its roof). build_bundle.ps1 removes the folder once the
    /// bundle has them.
    ///
    /// ONLY FOR THE BUNDLE THEY WERE MADE FOR. Their UVs point into the atlas of the bundle beside them when they were
    /// written: with any other bundle they draw with the wrong parts of its atlas (a city bus in fuel price boards
    /// and fire engine lettering, from a folder an install had copied over and left behind). So newer_props.py writes the
    /// SHA-256 of the bundle file into their catalogue ("bundle"), and they are used only beside a bundle file of that
    /// hash. A folder without the stamp, or with another's, is left alone and said once in the log; the bundle's own
    /// props are used. The hash is taken on the background thread that reads the folder, and only when there is one.
    /// </summary>
    public static class PropLibrary
    {
        private const string BundlePath = "Bundles/reallife_props";
        private const string NewerPath = "Bundles/props_newer";
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
            public string bundle;   // props_newer only: the SHA-256 of the bundle file they were made for (newer_props.py)
        }

        private sealed class ItemFile
        {
            public string id;
            public string mesh;
            public string material;
            public float[] size;
        }
        private sealed class MeshFile
        {
            public string name;
            public float[] vertices;
            public float[] normals;
            public float[] uv;
            public int[] triangles;
        }
#pragma warning restore 0649

        // The props newer than the bundle, as read from their files in the background.
        private sealed class Newer
        {
            public readonly List<ItemFile> Items = new List<ItemFile>(4);
            public readonly Dictionary<string, MeshFile> Meshes = new Dictionary<string, MeshFile>(4);
            public string Problem;
        }

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
        private static readonly Dictionary<string, Texture2D> Pictures = new Dictionary<string, Texture2D>(4);   // the bundle's other textures (road paint: see Texture)
        private static State _state = State.NotStarted;
        private static Texture2D _base;
        private static Texture2D _mask;
        private static Material _lit;
        private static Material _glow;
        private static bool _shaderMissing;
        private static Task<Newer> _newer;

        /// <summary>True once the bundle has loaded (asynchronously, once); false while loading or if it failed.</summary>
        public static bool Ready => _state == State.Ready;

        /// <summary>True once the load has ended, loaded or failed: what is built from the bundle now is what it will be.</summary>
        public static bool Settled => _state == State.Ready || _state == State.Failed;

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
                string newer = Path.Combine(context.ModRootPath, NewerPath);
                if (Directory.Exists(newer))
                {
                    _newer = Task.Run(() => ReadNewer(newer, path));
                }
                AssetBundleCreateRequest opening = AssetBundle.LoadFromFileAsync(path);
                opening.completed += Guard.Way<AsyncOperation>(_ => Opened(opening));
            }
            catch (Exception e)
            {
                Fail("the asset bundle could not be opened", e);
            }
        }

        /// <summary>
        /// One of the bundle's textures that is not the props' own atlas, by name (the road paint that RoadPaint lays:
        /// "decal_bus_stop", "decal_dash"). Null while the bundle is loading, or if it has none of that name.
        /// </summary>
        public static Texture2D Texture(string name)
        {
            return _state == State.Ready && name != null && Pictures.TryGetValue(name, out Texture2D picture) ? picture : null;
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
            made = new Material(shader) { name = glows ? "RealLifeOverhaul Props (glow)" : "RealLifeOverhaul Props", renderQueue = Shapes.TakesDecalsQueue };   // (Tinted's copies keep it)
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
                reading.completed += Guard.Way<AsyncOperation>(_ => Loaded(reading));
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
                        else
                        {
                            Pictures[texture.name] = texture;
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
                             + " atlas" + (_mask != null ? " with a mask map" : "") + (Pictures.Count > 0 ? ", and " + Pictures.Count + " picture(s) of road paint" : "") + ").");
                UseNewer();
            }
            catch (Exception e)
            {
                Fail("the prop catalogue could not be read", e);
            }
        }

        // Off the main thread: the newer props' catalogue and mesh files, read and checked, nothing of Unity's touched. Only
        // for the bundle file they were made for: its SHA-256 is their catalogue's stamp.
        private static Newer ReadNewer(string folder, string bundlePath)
        {
            var newer = new Newer();
            try
            {
                string path = Path.Combine(folder, CatalogueName + ".json");
                CatalogueFile catalogue = File.Exists(path) ? Newtonsoft.Json.JsonConvert.DeserializeObject<CatalogueFile>(File.ReadAllText(path)) : null;
                if (catalogue == null || catalogue.version != SupportedVersion || catalogue.items == null || catalogue.items.Length == 0)
                {
                    newer.Problem = "its catalogue is missing or of another version";
                    return newer;
                }
                if (string.IsNullOrEmpty(catalogue.bundle))
                {
                    newer.Problem = "it does not say which build of the asset bundle it was made for (a folder left from an older version of the mod)";
                    return newer;
                }
                string hash = FileHash(bundlePath);
                if (!string.Equals(hash, catalogue.bundle.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    newer.Problem = "it was made for another build of the asset bundle (" + Short(catalogue.bundle) + ", this one is " + Short(hash)
                                    + "): a folder left from an older version of the mod";
                    return newer;
                }
                foreach (ItemFile item in catalogue.items)
                {
                    if (string.IsNullOrEmpty(item.id) || string.IsNullOrEmpty(item.mesh))
                    {
                        newer.Problem = "an entry of its catalogue has no id or mesh";
                        return newer;
                    }
                    if (!newer.Meshes.ContainsKey(item.mesh))
                    {
                        string file = Path.Combine(folder, item.mesh + ".json");
                        MeshFile mesh = File.Exists(file) ? Newtonsoft.Json.JsonConvert.DeserializeObject<MeshFile>(File.ReadAllText(file)) : null;
                        string wrong = Wrong(mesh);
                        if (wrong != null)
                        {
                            newer.Problem = "the mesh of '" + item.id + "' " + wrong;
                            return newer;
                        }
                        newer.Meshes[item.mesh] = mesh;
                    }
                    newer.Items.Add(item);
                }
            }
            catch (Exception e)
            {
                newer.Problem = "its files could not be read (" + e.Message + ")";
            }
            return newer;
        }

        // The SHA-256 of a file, in lower-case hex (as newer_props.py writes it).
        private static string FileHash(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                var text = new System.Text.StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    text.Append(hash[i].ToString("x2"));
                }
                return text.ToString();
            }
        }

        private static string Short(string hash)
        {
            hash = hash.Trim();
            return hash.Length > 12 ? hash.Substring(0, 12) : hash;
        }

        // What is wrong with a mesh file, or null: the checks of the bundle's builder (PropBundleBuilder.BuildMesh).
        private static string Wrong(MeshFile file)
        {
            if (file == null || file.vertices == null || file.vertices.Length < 9 || file.vertices.Length % 3 != 0)
            {
                return "has no vertices";
            }
            int count = file.vertices.Length / 3;
            if (file.normals == null || file.normals.Length != count * 3 || file.uv == null || file.uv.Length != count * 2)
            {
                return "has normals or UVs that do not match its vertices";
            }
            if (file.triangles == null || file.triangles.Length < 3 || file.triangles.Length % 3 != 0)
            {
                return "has no triangles";
            }
            for (int i = 0; i < file.triangles.Length; i++)
            {
                if (file.triangles[i] < 0 || file.triangles[i] >= count)
                {
                    return "has a triangle that points outside its vertices";
                }
            }
            return null;
        }

        // On the main thread, once the bundle is in: the newer props in place of the bundle's own, all of them or none.
        private static void UseNewer()
        {
            if (_newer == null)
            {
                return;
            }
            Newer newer;
            try
            {
                newer = _newer.Result;   // (read long before the bundle has loaded; at worst this waits for it here, once)
            }
            catch (Exception e)
            {
                Logger?.Warn("Props: the props newer than the bundle (" + NewerPath + ") are not used: " + e.Message + ".");
                return;
            }
            finally
            {
                _newer = null;
            }
            if (newer.Problem != null)
            {
                Logger?.Warn("Props: the props newer than the bundle (" + NewerPath + ") are not used: " + newer.Problem + "; the bundle's own are.");
                return;
            }
            var made = new Dictionary<string, Mesh>(newer.Meshes.Count);
            try
            {
                foreach (KeyValuePair<string, MeshFile> pair in newer.Meshes)
                {
                    made[pair.Key] = Build(pair.Value, pair.Key);
                }
            }
            catch (Exception e)
            {
                foreach (Mesh mesh in made.Values)
                {
                    UnityEngine.Object.Destroy(mesh);
                }
                Logger?.Warn("Props: the props newer than the bundle (" + NewerPath + ") are not used: a mesh could not be made (" + e.Message + ").");
                return;
            }
            var names = new List<string>(newer.Items.Count);
            int replaced = 0;
            foreach (ItemFile file in newer.Items)
            {
                Mesh mesh = made[file.mesh];
                replaced += Props.ContainsKey(file.id) ? 1 : 0;
                Props[file.id] = new Prop
                {
                    Mesh = mesh,
                    Glows = file.material == GlowKind,
                    Size = file.size != null && file.size.Length == 3 ? new Vector3(file.size[0], file.size[1], file.size[2]) : mesh.bounds.size
                };
                MissingLogged.Remove(file.id);
                names.Add(file.id);
            }
            Logger?.Info("Props: " + names.Count + " newer than the asset bundle read from " + NewerPath + " (" + string.Join(", ", names) + "), " + replaced
                         + " of them in place of the bundle's own; they draw with its atlas.");
        }

        // A mesh as the bundle's builder makes one (PropBundleBuilder.BuildMesh): the file is in Unity's space already.
        private static Mesh Build(MeshFile file, string name)
        {
            int count = file.vertices.Length / 3;
            var vertices = new Vector3[count];
            var normals = new Vector3[count];
            var uv = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(file.vertices[i * 3], file.vertices[i * 3 + 1], file.vertices[i * 3 + 2]);
                normals[i] = new Vector3(file.normals[i * 3], file.normals[i * 3 + 1], file.normals[i * 3 + 2]).normalized;
                uv[i] = new Vector2(file.uv[i * 2], file.uv[i * 2 + 1]);
            }
            var mesh = new Mesh { name = name, indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.SetTriangles(file.triangles, 0, false);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mesh.Optimize();
            mesh.UploadMeshData(true);
            return mesh;
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
