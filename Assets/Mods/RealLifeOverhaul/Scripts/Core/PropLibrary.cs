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
    /// The modelled props (PropAssets/README.md), from the asset bundles beside the DLL (PropAssets/sets.json): each
    /// holds static meshes, its own atlas and a catalogue, and all are read once a session in the background, behind the
    /// loading screen. A prop is drawn with one of two instanced materials made here from the game's lit shader on its
    /// own bundle's atlas: "lit", and "glow" for what should still read at night. Callers ask <see cref="TryGet"/> by id,
    /// whichever bundle holds it, and fall back to a plain shape. The first bundle must be there; a later one missing
    /// (an older install) leaves its props out with one warning. Props remade since the first bundle was built may ship
    /// in Bundles/props_newer (newer_props.py), used all or none, and only beside the bundle whose SHA-256 their
    /// catalogue names: their UVs point into its atlas. Nothing per frame, nothing made per prop or per scene.
    /// </summary>
    public static class PropLibrary
    {
        // In the order of PropAssets/sets.json, which StandardCheck holds this list to (RLO-PERF-016). Where two hold the
        // same id, the earlier one's is used; the build allows no such pair.
        private static readonly string[] BundlePaths = { "Bundles/reallife_props", "Bundles/reallife_props_2" };
        private const string NewerPath = "Bundles/props_newer";
        private const string CatalogueName = "catalogue";
        private const string BaseTexture = "props_base";
        private const string MaskTexture = "props_mask";
        private const string LitShader = "HDRP/Lit";
        private const string GlowKind = "glow";
        private const int SupportedVersion = 1;
        private const float Glow = 1.5f;             // how strongly the glow material shows in the dark, in Shapes.Paint's units
        private const float PlainSmoothness = 0.3f;  // used only if the game's build of the shader has no mask-map variant

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");
        private static readonly int MaskMapId = Shader.PropertyToID("_MaskMap");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        private static readonly int AlbedoAffectEmissiveId = Shader.PropertyToID("_AlbedoAffectEmissive");

#pragma warning disable 0649   // filled in from the catalogue file
        private sealed class CatalogueFile
        {
            public int version;
            public ItemFile[] items;
            public string bundle;   // props_newer only: the SHA-256 of the bundle file they were made for
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

        // The props newer than the bundle, read in the background.
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
            public Bundle From;   // whose atlas it is drawn with
        }

        // One bundle: its atlas and the two materials made on it, and a later bundle's props until the first is in.
        private sealed class Bundle
        {
            public int Index;
            public string Name;
            public Texture2D Base;
            public Texture2D Mask;
            public Material Lit;
            public Material Glow;
            public bool Settled;               // read, failed or not installed: nothing more will come from it
            public ItemFile[] Pending;
            public Dictionary<string, Mesh> Meshes;
        }

        private enum State { NotStarted, Loading, Ready, Failed }

        public static IModLogger Logger;

        private static readonly Dictionary<string, Prop> Props = new Dictionary<string, Prop>(48);
        private static readonly HashSet<string> MissingLogged = new HashSet<string>();
        private static readonly Dictionary<int, Material> Tints = new Dictionary<int, Material>(8);   // by 0xIIBBGGRR, II the bundle: see Tinted
        private static readonly Dictionary<string, Texture2D> Pictures = new Dictionary<string, Texture2D>(4);   // the bundles' other textures (road paint: see Texture)
        private static State _state = State.NotStarted;
        private static Bundle[] _bundles = new Bundle[0];
        private static bool _shaderMissing;
        private static Task<Newer> _newer;

        /// <summary>True once the bundles have loaded (a later one missing or unreadable counts as done); false while loading or if the first failed.</summary>
        public static bool Ready => _state == State.Ready;

        /// <summary>True once the load has ended either way: what is built from the bundles now is what it will be.</summary>
        public static bool Settled => _state == State.Ready || _state == State.Failed;

        public static void Load(ModContext context)
        {
            if (_state != State.NotStarted || context == null)
            {
                return;
            }
            try
            {
                _bundles = new Bundle[BundlePaths.Length];
                for (int i = 0; i < BundlePaths.Length; i++)
                {
                    _bundles[i] = new Bundle { Index = i, Name = Path.GetFileName(BundlePaths[i]) };
                }
                string path = Path.Combine(context.ModRootPath, BundlePaths[0]);
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
                for (int i = 0; i < BundlePaths.Length; i++)
                {
                    string file = Path.Combine(context.ModRootPath, BundlePaths[i]);
                    if (i > 0 && !File.Exists(file))
                    {
                        Later(i, "is not installed (an older copy of the mod?)");
                        continue;
                    }
                    int index = i;
                    AssetBundleCreateRequest opening = AssetBundle.LoadFromFileAsync(file);
                    opening.completed += Guard.Way<AsyncOperation>(_ => Opened(opening, index));
                }
            }
            catch (Exception e)
            {
                Fail("the asset bundle could not be opened", e);
            }
        }

        /// <summary>One of the bundles' other textures by name (road paint: "decal_dash"); null while loading or if none.</summary>
        public static Texture2D Texture(string name)
        {
            return _state == State.Ready && name != null && Pictures.TryGetValue(name, out Texture2D picture) ? picture : null;
        }

        /// <summary>The shared mesh and material for a prop id, from whichever bundle holds it; false if not ready or no such prop (logged once).</summary>
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
                    Logger?.Warn("Props: '" + id + "' is not in the asset bundles; its plain shape is used.");
                }
                return false;
            }
            material = Material(prop.Glows, prop.From);
            if (material == null)
            {
                return false;
            }
            mesh = prop.Mesh;
            return true;
        }

        /// <summary>Whether the bundles are loaded and one has this prop; unlike <see cref="TryGet"/>, never logs.</summary>
        public static bool Has(string id)
        {
            return _state == State.Ready && id != null && Props.ContainsKey(id);
        }

        /// <summary>The lit material of a prop's own atlas, tinted: one shared per atlas and colour until <see cref="Dispose"/>; null if it cannot be made.</summary>
        public static Material Tinted(string id, Color tint)
        {
            if (_state != State.Ready || id == null || !Props.TryGetValue(id, out Prop prop))
            {
                return null;
            }
            Material lit = Material(false, prop.From);
            if (lit == null)
            {
                return null;
            }
            Color32 packed = tint;
            int key = packed.r | packed.g << 8 | packed.b << 16 | prop.From.Index << 24;
            if (!Tints.TryGetValue(key, out Material made) || made == null)
            {
                made = new Material(lit) { name = "RealLifeOverhaul Props (tinted)" };
                made.SetColor(BaseColorId, new Color(tint.r, tint.g, tint.b, 1f));
                made.enableInstancing = true;
                Tints[key] = made;
            }
            return made;
        }

        /// <summary>Size of the prop's bounds in metres; zero if unknown.</summary>
        public static Vector3 SizeOf(string id)
        {
            return _state == State.Ready && id != null && Props.TryGetValue(id, out Prop prop) ? prop.Size : Vector3.zero;
        }

        /// <summary>Destroys the materials when the city ends; the bundles stay loaded for the session.</summary>
        public static void Dispose()
        {
            foreach (Bundle bundle in _bundles)
            {
                if (bundle.Lit != null)
                {
                    UnityEngine.Object.Destroy(bundle.Lit);
                }
                if (bundle.Glow != null)
                {
                    UnityEngine.Object.Destroy(bundle.Glow);
                }
                bundle.Lit = null;
                bundle.Glow = null;
            }
            foreach (Material tinted in Tints.Values)
            {
                if (tinted != null)
                {
                    UnityEngine.Object.Destroy(tinted);
                }
            }
            Tints.Clear();
        }

        // One material per kind and atlas, made when first asked for.
        private static Material Material(bool glows, Bundle from)
        {
            Material made = glows ? from.Glow : from.Lit;
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
            string name = (glows ? "RealLifeOverhaul Props (glow)" : "RealLifeOverhaul Props") + (from.Index > 0 ? " " + from.Name : string.Empty);
            made = new Material(shader) { name = name, renderQueue = Shapes.TakesDecalsQueue };   // Tinted's copies keep it
            made.SetColor(BaseColorId, Color.white);
            made.SetTexture(BaseColorMapId, from.Base);
            made.SetFloat(MetallicId, 0f);
            made.SetFloat(SmoothnessId, PlainSmoothness);
            if (from.Mask != null)
            {
                made.SetTexture(MaskMapId, from.Mask);
                made.EnableKeyword("_MASKMAP");
            }
            if (glows)
            {
                // Lit in its own colours: white bands show, black rubber stays black.
                made.SetFloat(AlbedoAffectEmissiveId, 1f);
                made.SetColor(EmissiveColorId, Color.white * Glow);
            }
            made.enableInstancing = true;
            if (glows)
            {
                from.Glow = made;
            }
            else
            {
                from.Lit = made;
            }
            return made;
        }

        private static void Opened(AssetBundleCreateRequest opening, int index)
        {
            try
            {
                AssetBundle bundle = opening.assetBundle;
                if (bundle == null)
                {
                    Unread(index, "could not be read (built for another Unity version?)", null);
                    return;
                }
                AssetBundleRequest reading = bundle.LoadAllAssetsAsync();
                reading.completed += Guard.Way<AsyncOperation>(_ => Loaded(reading, index));
            }
            catch (Exception e)
            {
                Unread(index, "could not be read", e);
            }
        }

        private static void Loaded(AssetBundleRequest reading, int index)
        {
            try
            {
                Bundle into = _bundles[index];
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
                            into.Base = texture;
                        }
                        else if (texture.name == MaskTexture)
                        {
                            into.Mask = texture;
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
                // Not JsonUtility: it leaves the arrays of a class from an assembly loaded at run time empty.
                CatalogueFile catalogue = catalogueText != null ? Newtonsoft.Json.JsonConvert.DeserializeObject<CatalogueFile>(catalogueText.text) : null;
                if (catalogue == null || catalogue.version != SupportedVersion || catalogue.items == null)
                {
                    Unread(index, "has a prop catalogue of version " + (catalogue != null ? catalogue.version : 0) + " with "
                                  + (catalogue != null && catalogue.items != null ? catalogue.items.Length : 0) + " items; this build reads version " + SupportedVersion, null);
                    return;
                }
                if (into.Base == null && catalogue.items.Length > 0)
                {
                    Unread(index, "has no texture atlas", null);
                    return;
                }
                into.Pending = catalogue.items;
                into.Meshes = meshes;
                into.Settled = true;
                TryReady();
            }
            catch (Exception e)
            {
                Unread(index, "has a prop catalogue that could not be read", e);
            }
        }

        // A bundle that could not be used: the first switches props off, a later one only leaves its own out.
        private static void Unread(int index, string problem, Exception error)
        {
            if (index == 0)
            {
                Fail("the asset bundle " + problem, error);
                return;
            }
            if (error != null)
            {
                Logger?.Error(error);
            }
            Later(index, problem);
        }

        private static void Later(int index, string problem)
        {
            Logger?.Warn("Props: " + BundlePaths[index] + " " + problem + "; the props in it are left out and their plain shapes are used.");
            _bundles[index].Settled = true;
            TryReady();
        }

        // Once every bundle has settled, their props in the order of the bundles, then the newer props: a later bundle
        // whose read ends first still never stands in front of an earlier one.
        private static void TryReady()
        {
            if (_state != State.Loading)
            {
                return;
            }
            foreach (Bundle bundle in _bundles)
            {
                if (!bundle.Settled)
                {
                    return;
                }
            }
            var said = new System.Text.StringBuilder(160);
            int triangles = 0;
            foreach (Bundle bundle in _bundles)
            {
                if (bundle.Pending == null)
                {
                    continue;
                }
                int added = 0;
                foreach (ItemFile file in bundle.Pending)
                {
                    if (string.IsNullOrEmpty(file.id) || string.IsNullOrEmpty(file.mesh) || !bundle.Meshes.TryGetValue(file.mesh, out Mesh mesh))
                    {
                        Logger?.Warn("Props: '" + file.id + "' is left out, its mesh is not in " + bundle.Name + ".");
                        continue;
                    }
                    if (Props.TryGetValue(file.id, out Prop held) && held.From != bundle)
                    {
                        Logger?.Warn("Props: '" + file.id + "' of " + bundle.Name + " is left out, " + held.From.Name + " has it.");
                        continue;
                    }
                    Props[file.id] = MakeProp(file, mesh, bundle);
                    triangles += (int)mesh.GetIndexCount(0) / 3;
                    added++;
                }
                said.Append(said.Length > 0 ? ", " : string.Empty).Append(bundle.Name).Append(' ').Append(added)
                    .Append(bundle.Base != null ? " on a " + bundle.Base.width + " x " + bundle.Base.height + " atlas" + (bundle.Mask != null ? " with a mask map" : string.Empty) : " (no atlas)");
                bundle.Pending = null;
                bundle.Meshes = null;
            }
            _state = State.Ready;
            Logger?.Info("Props: " + Props.Count + " loaded from the asset bundles (" + said + "; " + triangles + " triangles in all"
                         + (Pictures.Count > 0 ? ", and " + Pictures.Count + " picture(s) of road paint" : "") + ").");
            UseNewer();
        }

        private static Prop MakeProp(ItemFile file, Mesh mesh, Bundle from)
        {
            return new Prop
            {
                Mesh = mesh,
                Glows = file.material == GlowKind,
                Size = file.size != null && file.size.Length == 3 ? new Vector3(file.size[0], file.size[1], file.size[2]) : mesh.bounds.size,
                From = from
            };
        }

        // Off the main thread, nothing of Unity's touched: the newer props' files, read and checked.
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

        // The SHA-256 of a file in lower-case hex, as newer_props.py writes it.
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

        // What is wrong with a mesh file, or null: the checks of PropBundleBuilder.BuildMesh.
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

        // On the main thread once the bundle is in: the newer props in place of the bundle's own, all or none.
        private static void UseNewer()
        {
            if (_newer == null)
            {
                return;
            }
            Newer newer;
            try
            {
                newer = _newer.Result;   // read long before the bundle loads; at worst this waits once
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
                Props[file.id] = MakeProp(file, mesh, _bundles[0]);   // the newer props draw with the first bundle's atlas
                MissingLogged.Remove(file.id);
                names.Add(file.id);
            }
            Logger?.Info("Props: " + names.Count + " newer than the asset bundle read from " + NewerPath + " (" + string.Join(", ", names) + "), " + replaced
                         + " of them in place of the bundle's own; they draw with its atlas.");
        }

        // A mesh as PropBundleBuilder.BuildMesh makes one; the file is in Unity's space already.
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
