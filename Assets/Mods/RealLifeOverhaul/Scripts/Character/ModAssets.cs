using System;
using System.Collections.Generic;
using System.IO;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul
{
    internal enum LayerRole { Scalp, Bulk, Wisps, Cloth }

    /// <summary>One look from the bundle: a mesh, and per submesh what kind of material it wants and which textures.</summary>
    internal sealed class LookItem
    {
        public sealed class Layer
        {
            public LayerRole Role;
            public string TextureName;
            public string NormalName;
            public string MaskName;
            public string TintMaskName;  // a colour mask in the game's own convention: pure channels, each tinted by one colour
            public Color[] Tints;        // red, green, blue channel of the tint mask (as picked: sRGB)
            // In memory only while the look is worn (ModAssets.Request).
            public Texture2D Texture;
            public Texture2D Normal;
            public Texture2D Mask;
            public Texture2D TintMask;
        }

        public string Id;
        public LookKind Kind;
        public string MeshName;
        public Mesh Mesh;            // in memory only while the look is worn
        public Layer[] Layers;
        public string SkinFrom;      // clothes: the game's variant whose skin part shows what this garment leaves bare
        public string Under;         // an underlay: worn beneath the game's own hair mesh of this name instead of replacing it
        public int SkinTriangles;    // clothes over bare skin: how many triangles the skin part must have for SkinHidden to apply
        public int[] SkinHidden;     // and which of them lie under the cloth and are not drawn
        public bool Female;
        public int Vertices;
        public int Triangles;
        public bool Prepared;        // the game's bind poses have been written into the mesh
        public int Wearers;          // pieces on characters that are drawn with it now
        public bool Loaded;
        public bool Loading;
        public bool Broken;          // an asset of it could not be read: the game's own piece stays
    }

    /// <summary>
    /// The meshes and textures the mod ships for the player's looks.
    ///
    /// They live in one asset bundle next to the mod's DLL (Bundles/reallife_character) that holds
    /// meshes, textures and a catalogue, and no materials or shaders: materials are always copies
    /// of ones the game is drawing with (see <see cref="ModLooks"/>).
    ///
    /// The bundle is opened once per game session, in the background, and only its catalogue and
    /// the few textures every character shares (the hair strand maps) are read then. A look's mesh
    /// and textures are read when it is first worn (<see cref="Request"/>, in the background: the
    /// game's own piece shows until they are there) and let go again when nobody wears it
    /// (<see cref="Sweep"/>), so what is in memory is what is on screen, not everything on offer.
    /// Nothing here runs per frame. If the file is missing or not as expected, this logs once and
    /// the game's own look stays.
    ///
    /// The meshes are skinned to the game's detailed skeleton by bone order, and are modelled in
    /// the space of the game's male head mesh. Their bind poses are therefore not shipped but
    /// copied from that head mesh each time a mesh is brought in, after checking the bone names.
    /// </summary>
    internal static class ModAssets
    {
        private const string BundlePath = "Bundles/reallife_character";
        private const string CatalogueName = "catalogue";
        private const int SupportedVersion = 2;

        // ---------------------------------------------------------------- the catalogue as written by the asset pipeline

#pragma warning disable 0649   // filled in from the catalogue file
        [Serializable]
        private class CatalogueFile
        {
            public int version;
            public string[] bones;
            public TextureFile[] textures;
            public ItemFile[] items;
            public PhotoFile[] photos;
        }

        // A garment's photograph for the game's screens: where it stands on a sheet of them (a texture no look names).
        [Serializable]
        private class PhotoFile
        {
            public string garment;   // the garment's mesh: every colourway of it shows this one
            public string texture;
            public int x, y, w, h;   // in pixels, from the sheet's lower left corner
        }

        [Serializable]
        private class TextureFile
        {
            public string name;
        }

        [Serializable]
        private class ItemFile
        {
            public string id;
            public string kind;
            public string mesh;
            public LayerFile[] layers;
            public string skinFrom;
            public string under;
            public string sex;
            public StatsFile stats;
            public SkinHideFile skinHide;
        }

        [Serializable]
        private class SkinHideFile
        {
            public int triangles;
            public int[] hidden;
        }

        [Serializable]
        private class StatsFile
        {
            public int vertices;
            public int triangles;
        }

        [Serializable]
        private class LayerFile
        {
            public string role;
            public string texture;
            public string normal;
            public string mask;
            public string tintMask;
            public float[][] tints;
        }

#pragma warning restore 0649

        private enum State { NotStarted, Loading, Ready, Failed }

        public static IModLogger Logger;

        /// <summary>Raised (on the main thread, in a later frame than the request) when a requested look has come in or failed.</summary>
        public static Action Arrived;

        private static readonly Dictionary<string, LookItem> Items = new Dictionary<string, LookItem>(128);
        private static readonly Dictionary<string, PhotoFile> Photos = new Dictionary<string, PhotoFile>(24);
        private static readonly Dictionary<string, Sprite> PhotoSprites = new Dictionary<string, Sprite>(24);
        private static readonly HashSet<string> MissingLogged = new HashSet<string>();
        private static readonly Dictionary<string, Texture2D> Shared = new Dictionary<string, Texture2D>(4);
        private static readonly Dictionary<string, UnityEngine.Object> Held = new Dictionary<string, UnityEngine.Object>(16);   // by "m:" or "t:" + name
        private static readonly HashSet<string> Pending = new HashSet<string>();
        private static readonly HashSet<string> Needed = new HashSet<string>();
        private static readonly List<string> Scratch = new List<string>(16);
        private static State _state = State.NotStarted;
        private static string _problem;
        private static AssetBundle _bundle;
        private static string[] _bones = Array.Empty<string>();
        private static Matrix4x4[] _bindposes;
        private static int _triangles;
        private static int _sharedLeft;
        private static bool _dirty;
        private static int _loads;
        private static int _unloads;

        public static bool Ready => _state == State.Ready;

        /// <summary>True once the bundle has either opened or failed: nothing more will change about what is on offer.</summary>
        public static bool Settled => _state == State.Ready || _state == State.Failed;

        /// <summary>A texture of the bundle that belongs to no single look (the hair strand maps), or null.</summary>
        public static Texture2D Texture(string name)
        {
            return _state == State.Ready && Shared.TryGetValue(name, out Texture2D texture) ? texture : null;
        }

        /// <summary>
        /// A garment's photograph, by its mesh as the catalogue lists it (the game's own character wearing it, framed as
        /// the game frames its own pieces in its screens), or null if the bundle has none for it. Made once a session.
        /// </summary>
        public static Sprite Photo(string garment)
        {
            if (_state != State.Ready || garment == null || !Photos.TryGetValue(garment, out PhotoFile photo))
            {
                return null;
            }
            if (PhotoSprites.TryGetValue(garment, out Sprite made) && made != null)
            {
                return made;
            }
            if (!Shared.TryGetValue(photo.texture, out Texture2D sheet) || sheet == null || photo.x + photo.w > sheet.width || photo.y + photo.h > sheet.height)
            {
                return null;
            }
            made = Sprite.Create(sheet, new Rect(photo.x, photo.y, photo.w, photo.h), new Vector2(0.5f, 0.5f), 100f);
            made.name = "RealLifeOverhaul photo " + garment;
            PhotoSprites[garment] = made;
            return made;
        }

        /// <summary>Starts opening the bundle, the first time it is called in a game session.</summary>
        public static void Begin(ModContext context)
        {
            if (_state != State.NotStarted)
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

        private static void Opened(AssetBundleCreateRequest opening)
        {
            try
            {
                _bundle = opening.assetBundle;
                if (_bundle == null)
                {
                    Fail("the asset bundle could not be read (built for another Unity version?)");
                    return;
                }
                AssetBundleRequest reading = _bundle.LoadAssetAsync<TextAsset>(CatalogueName);
                reading.completed += _ => Catalogued(reading);
            }
            catch (Exception e)
            {
                Fail("the asset bundle could not be read", e);
            }
        }

        private static void Catalogued(AssetBundleRequest reading)
        {
            try
            {
                var catalogueText = reading.asset as TextAsset;
                if (catalogueText == null)
                {
                    Fail("the asset bundle has no catalogue");
                    return;
                }
                // Not JsonUtility: Unity's serializer does not know classes from an assembly loaded at run time,
                // and silently leaves their arrays empty.
                CatalogueFile catalogue = Newtonsoft.Json.JsonConvert.DeserializeObject<CatalogueFile>(catalogueText.text);
                if (catalogue == null || catalogue.version != SupportedVersion || catalogue.bones == null || catalogue.items == null)
                {
                    Fail("the asset catalogue is version " + (catalogue != null ? catalogue.version : 0) + " with "
                         + (catalogue != null && catalogue.bones != null ? catalogue.bones.Length : 0) + " bones and "
                         + (catalogue != null && catalogue.items != null ? catalogue.items.Length : 0) + " items; this build reads version " + SupportedVersion);
                    return;
                }
                _bones = catalogue.bones;
                var used = new HashSet<string>();
                foreach (ItemFile file in catalogue.items)
                {
                    LookItem item = Read(file, out string problem);
                    if (item == null)
                    {
                        Logger?.Warn("New looks: '" + file.id + "' is left out, " + problem + ".");
                        continue;
                    }
                    Items[item.Id] = item;
                    _triangles += item.Triangles;
                    foreach (LookItem.Layer layer in item.Layers)
                    {
                        used.Add(layer.TextureName);
                        used.Add(layer.NormalName ?? "");
                        used.Add(layer.MaskName ?? "");
                        used.Add(layer.TintMaskName ?? "");
                    }
                }

                foreach (PhotoFile photo in catalogue.photos ?? Array.Empty<PhotoFile>())
                {
                    if (photo != null && !string.IsNullOrEmpty(photo.garment) && !string.IsNullOrEmpty(photo.texture) && photo.w > 0 && photo.h > 0)
                    {
                        Photos[photo.garment] = photo;
                    }
                }

                // Textures no look names are everybody's (the hair strand maps, the sheet of photographs): read now and kept.
                var shared = new List<string>(4);
                foreach (TextureFile texture in catalogue.textures ?? Array.Empty<TextureFile>())
                {
                    if (texture != null && !string.IsNullOrEmpty(texture.name) && !used.Contains(texture.name) && _bundle.Contains(texture.name))
                    {
                        shared.Add(texture.name);
                    }
                }
                _sharedLeft = shared.Count;
                if (_sharedLeft == 0)
                {
                    Settle();
                }
                foreach (string name in shared)
                {
                    string captured = name;
                    AssetBundleRequest request = _bundle.LoadAssetAsync<Texture2D>(name);
                    request.completed += _ => SharedArrived(captured, request);
                }
            }
            catch (Exception e)
            {
                Fail("the asset catalogue could not be read", e);
            }
        }

        private static void SharedArrived(string name, AssetBundleRequest request)
        {
            try
            {
                if (request.asset is Texture2D texture)
                {
                    Shared[name] = texture;
                }
                if (--_sharedLeft <= 0 && _state == State.Loading)
                {
                    Settle();
                }
            }
            catch (Exception e)
            {
                Fail("a shared texture could not be read", e);
            }
        }

        private static void Settle()
        {
            _state = State.Ready;
            Logger?.Info("New looks: " + Items.Count + " on offer in the asset bundle (" + _triangles + " triangles in all, " + Shared.Count
                         + " shared textures in memory, " + Megabytes(SharedBytes()) + " MB, " + Photos.Count + " garment photograph(s)); each look's mesh and textures are read when it is worn.");
        }

        private static LookItem Read(ItemFile file, out string problem)
        {
            problem = null;
            if (string.IsNullOrEmpty(file.id) || string.IsNullOrEmpty(file.mesh) || !_bundle.Contains(file.mesh))
            {
                problem = "its mesh is not in the bundle";
                return null;
            }
            if (!TryKind(file.kind, out LookKind kind))
            {
                problem = "'" + file.kind + "' is not a kind of look this build knows";
                return null;
            }
            if (file.layers == null || file.layers.Length == 0)
            {
                problem = "it has no layers";
                return null;
            }
            var item = new LookItem
            {
                Id = file.id, Kind = kind, MeshName = file.mesh, SkinFrom = string.IsNullOrEmpty(file.skinFrom) ? null : file.skinFrom,
                Under = string.IsNullOrEmpty(file.under) ? null : file.under, Female = file.sex == "female",
                Vertices = file.stats != null ? file.stats.vertices : 0, Triangles = file.stats != null ? file.stats.triangles : 0,
                Layers = new LookItem.Layer[file.layers.Length]
            };
            if (file.skinHide != null && file.skinHide.hidden != null && file.skinHide.hidden.Length > 0)
            {
                item.SkinTriangles = file.skinHide.triangles;
                item.SkinHidden = file.skinHide.hidden;
            }
            for (int i = 0; i < file.layers.Length; i++)
            {
                LayerFile layer = file.layers[i];
                if (!TryRole(layer.role, out LayerRole role) || string.IsNullOrEmpty(layer.texture) || !_bundle.Contains(layer.texture))
                {
                    problem = "layer " + i + " has no usable role or texture";
                    return null;
                }
                var made = new LookItem.Layer
                {
                    Role = role, TextureName = layer.texture, NormalName = Optional(layer.normal), MaskName = Optional(layer.mask),
                    TintMaskName = Optional(layer.tintMask)
                };
                if (made.TintMaskName != null && layer.tints != null && layer.tints.Length > 0)
                {
                    made.Tints = new Color[Mathf.Min(layer.tints.Length, 3)];
                    for (int k = 0; k < made.Tints.Length; k++)
                    {
                        float[] c = layer.tints[k];
                        made.Tints[k] = c != null && c.Length >= 3 ? new Color(c[0], c[1], c[2], 1f) : Color.white;
                    }
                }
                else
                {
                    made.TintMaskName = null;
                }
                item.Layers[i] = made;
            }
            return item;
        }

        private static string Optional(string name)
        {
            return !string.IsNullOrEmpty(name) && _bundle.Contains(name) ? name : null;
        }

        private static bool TryKind(string text, out LookKind kind)
        {
            switch (text)
            {
                case "hair": kind = LookKind.Hair; return true;
                case "beard": kind = LookKind.Beard; return true;
                case "top": kind = LookKind.Top; return true;
                case "legs": kind = LookKind.Legs; return true;
                case "feet": kind = LookKind.Feet; return true;
                default: kind = LookKind.Hair; return false;
            }
        }

        private static bool TryRole(string text, out LayerRole role)
        {
            switch (text)
            {
                case "scalp": role = LayerRole.Scalp; return true;
                case "bulk": role = LayerRole.Bulk; return true;
                case "wisps": role = LayerRole.Wisps; return true;
                case "cloth": role = LayerRole.Cloth; return true;
                default: role = LayerRole.Cloth; return false;
            }
        }

        // ---------------------------------------------------------------- finding

        /// <summary>The scalp underlay made for one of the game's own hair meshes on a head of this gender, or null.</summary>
        public static LookItem FindUnderlay(string gameMesh, bool female)
        {
            if (gameMesh == null || _state != State.Ready)
            {
                return null;
            }
            foreach (LookItem item in Items.Values)
            {
                if (item.Under == gameMesh && item.Female == female && !item.Broken)
                {
                    return item;
                }
            }
            return null;
        }

        /// <summary>The look with this id (not yet in memory, perhaps), or null (logged once per id) if the bundle does not have it.</summary>
        public static LookItem Find(string id)
        {
            if (id == null || _state != State.Ready)
            {
                return null;
            }
            if (Items.TryGetValue(id, out LookItem item))
            {
                return item.Broken ? null : item;
            }
            if (MissingLogged.Add(id))
            {
                Logger?.Info("New looks: '" + id + "' is not in the asset bundle; the game's own is shown for it.");
            }
            return null;
        }

        // ---------------------------------------------------------------- reading a look in, and letting it go

        /// <summary>
        /// Starts reading this look's mesh and textures in the background, unless that is under way or done.
        /// <see cref="Arrived"/> is raised, in a later frame, when it is ready to wear (<see cref="LookItem.Loaded"/>)
        /// or has failed. Returns true if it is ready now (what it needs was in memory for another look).
        /// </summary>
        public static bool Request(LookItem item)
        {
            if (item == null || item.Broken || _state != State.Ready)
            {
                return false;
            }
            if (item.Loaded || item.Loading)
            {
                return item.Loaded;
            }
            try
            {
                item.Loading = true;
                var names = new List<string>(6);
                Names(item, names);
                for (int i = 0; i < names.Count; i++)
                {
                    if (!Held.ContainsKey(names[i]))
                    {
                        Fetch(names[i]);
                    }
                }
                Complete();   // ready at once if another colourway's mesh and maps are all it needs
                return item.Loaded;
            }
            catch (Exception e)
            {
                Break(item, "could not be requested");
                Logger?.Error(e);
                return false;
            }
        }

        private static void Fetch(string key)
        {
            if (!Pending.Add(key))
            {
                return;   // on its way for another look
            }
            bool mesh = key[0] == 'm';
            string name = key.Substring(2);
            AssetBundleRequest request = mesh ? _bundle.LoadAssetAsync<Mesh>(name) : _bundle.LoadAssetAsync<Texture2D>(name);
            request.completed += _ =>
            {
                try
                {
                    Pending.Remove(key);
                    UnityEngine.Object asset = request.asset;
                    if (asset != null)
                    {
                        Held[key] = asset;
                        _loads++;
                    }
                    else
                    {
                        foreach (LookItem item in Items.Values)
                        {
                            if (item.Loading && Uses(item, key))
                            {
                                Break(item, "its " + (mesh ? "mesh" : "texture") + " '" + name + "' could not be read from the bundle");
                            }
                        }
                    }
                    Complete();
                    _dirty = true;
                    Arrived?.Invoke();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            };
        }

        // Every look that was waiting and now has all it needs is ready to wear.
        private static void Complete()
        {
            foreach (LookItem item in Items.Values)
            {
                if (!item.Loading)
                {
                    continue;
                }
                Scratch.Clear();
                Names(item, Scratch);
                bool all = true;
                for (int i = 0; i < Scratch.Count && all; i++)
                {
                    all = Held.ContainsKey(Scratch[i]);
                }
                if (!all)
                {
                    continue;
                }
                item.Mesh = Held["m:" + item.MeshName] as Mesh;
                if (item.Mesh == null || !item.Mesh.isReadable || item.Mesh.subMeshCount != item.Layers.Length)
                {
                    Break(item, item.Mesh == null ? "its mesh is not a mesh" : !item.Mesh.isReadable ? "its mesh cannot be given the game's bind poses" : "its layers do not match its mesh");
                    continue;
                }
                foreach (LookItem.Layer layer in item.Layers)
                {
                    layer.Texture = Held["t:" + layer.TextureName] as Texture2D;
                    layer.Normal = layer.NormalName != null ? Held["t:" + layer.NormalName] as Texture2D : null;
                    layer.Mask = layer.MaskName != null ? Held["t:" + layer.MaskName] as Texture2D : null;
                    layer.TintMask = layer.TintMaskName != null ? Held["t:" + layer.TintMaskName] as Texture2D : null;
                }
                item.Loading = false;
                item.Loaded = true;
                item.Prepared = false;
            }
            Scratch.Clear();
        }

        private static void Names(LookItem item, List<string> into)
        {
            into.Add("m:" + item.MeshName);
            foreach (LookItem.Layer layer in item.Layers)
            {
                into.Add("t:" + layer.TextureName);
                if (layer.NormalName != null)
                {
                    into.Add("t:" + layer.NormalName);
                }
                if (layer.MaskName != null)
                {
                    into.Add("t:" + layer.MaskName);
                }
                if (layer.TintMaskName != null)
                {
                    into.Add("t:" + layer.TintMaskName);
                }
            }
        }

        private static bool Uses(LookItem item, string key)
        {
            var names = new List<string>(6);
            Names(item, names);
            return names.Contains(key);
        }

        private static void Break(LookItem item, string why)
        {
            item.Broken = true;
            item.Loading = false;
            Forget(item);
            Logger?.Warn("New looks: '" + item.Id + "' is left out, " + why + ". The game's own is shown for it.");
        }

        private static void Forget(LookItem item)
        {
            item.Loaded = false;
            item.Prepared = false;
            item.Mesh = null;
            foreach (LookItem.Layer layer in item.Layers)
            {
                layer.Texture = null;
                layer.Normal = null;
                layer.Mask = null;
                layer.TintMask = null;
            }
        }

        /// <summary>A piece drawn with this look has gone: at the next <see cref="Sweep"/> it may leave memory.</summary>
        public static void Released(LookItem item)
        {
            if (item != null && item.Wearers > 0)
            {
                item.Wearers--;
            }
            _dirty = true;
        }

        /// <summary>
        /// Lets go of every mesh and texture that no worn look, and no look on its way in, needs any more.
        /// Called after looks have changed (never per frame); does nothing unless something was released.
        /// </summary>
        public static void Sweep()
        {
            if (!_dirty || _state != State.Ready)
            {
                return;
            }
            _dirty = false;
            try
            {
                Needed.Clear();
                foreach (LookItem item in Items.Values)
                {
                    if (item.Wearers > 0 || item.Loading)
                    {
                        Scratch.Clear();
                        Names(item, Scratch);
                        for (int i = 0; i < Scratch.Count; i++)
                        {
                            Needed.Add(Scratch[i]);
                        }
                    }
                }
                foreach (LookItem item in Items.Values)
                {
                    if (item.Loaded && item.Wearers <= 0)
                    {
                        Forget(item);
                    }
                }
                Scratch.Clear();
                foreach (string key in Held.Keys)
                {
                    if (!Needed.Contains(key))
                    {
                        Scratch.Add(key);
                    }
                }
                for (int i = 0; i < Scratch.Count; i++)
                {
                    UnityEngine.Object asset = Held[Scratch[i]];
                    Held.Remove(Scratch[i]);
                    if (asset != null)
                    {
                        Resources.UnloadAsset(asset);   // the bundle reads it again from the file when it is next asked for
                        _unloads++;
                    }
                }
                Scratch.Clear();
                Needed.Clear();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>
        /// Makes a mesh ready to be skinned to this character: the first time, checks that the
        /// character's skeleton is the one the meshes were made for and takes the bind poses from the
        /// game's male head mesh. Returns null when all is well, else what is wrong.
        /// </summary>
        public static string Prepare(LookItem item, AppearanceSetter character)
        {
            if (item.Prepared)
            {
                return null;
            }
            if (item.Mesh == null)
            {
                return "'" + item.Id + "' is not in memory";
            }
            if (_bindposes == null)
            {
                SkinnedMeshRenderer head = character.maleHeadMesh;
                if (head == null || head.sharedMesh == null)
                {
                    return "the character has no male head mesh to take bind poses from";
                }
                Transform[] bones = head.bones;
                if (bones.Length != _bones.Length)
                {
                    return "the game's skeleton has " + bones.Length + " bones, the looks were made for " + _bones.Length;
                }
                for (int i = 0; i < bones.Length; i++)
                {
                    if (bones[i] == null || bones[i].name != _bones[i])
                    {
                        return "bone " + i + " of the game's skeleton is '" + (bones[i] != null ? bones[i].name : "none") + "', the looks expect '" + _bones[i] + "'";
                    }
                }
                Matrix4x4[] bindposes = head.sharedMesh.bindposes;
                if (bindposes.Length != _bones.Length)
                {
                    return "the game's head mesh has " + bindposes.Length + " bind poses";
                }
                _bindposes = bindposes;
            }
            item.Mesh.bindposes = _bindposes;
            item.Prepared = true;
            return null;
        }

        public static int BoneCount => _bones.Length;

        // ---------------------------------------------------------------- reporting

        // What a texture takes on the graphics card: both formats the bundle uses (BC7, BC5) are one byte a texel, plus a third for the mips.
        private static long Bytes(UnityEngine.Object asset)
        {
            return asset is Texture2D texture ? (long)texture.width * texture.height * (texture.mipmapCount > 1 ? 4 : 3) / 3 : 0;
        }

        private static long SharedBytes()
        {
            long bytes = 0;
            foreach (Texture2D texture in Shared.Values)
            {
                bytes += Bytes(texture);
            }
            return bytes;
        }

        private static string Megabytes(long bytes)
        {
            return (bytes / 1048576f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string Describe()
        {
            switch (_state)
            {
                case State.Ready:
                    long bytes = SharedBytes();
                    int meshes = 0, textures = Shared.Count;
                    foreach (KeyValuePair<string, UnityEngine.Object> held in Held)
                    {
                        bytes += Bytes(held.Value);
                        if (held.Key[0] == 'm')
                        {
                            meshes++;
                        }
                        else
                        {
                            textures++;
                        }
                    }
                    return "open, " + Items.Count + " looks on offer (" + _triangles + " triangles in all); in memory now " + meshes + " meshes and " + textures
                           + " textures, " + Megabytes(bytes) + " MB of textures (" + _loads + " read in and " + _unloads + " let go this session, " + Pending.Count
                           + " on their way), bind poses " + (_bindposes != null ? "taken from the game" : "not needed yet");
                case State.Loading:
                    return "still opening";
                case State.Failed:
                    return "FAILED: " + _problem;
                default:
                    return "not started";
            }
        }

        private static void Fail(string problem, Exception error = null)
        {
            _state = State.Failed;
            _problem = problem;
            Logger?.Warn("New looks: switched off, " + problem + ". The game's own hair and clothes are shown.");
            if (error != null)
            {
                Logger?.Error(error);
            }
        }
    }
}
