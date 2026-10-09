using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Turns the interchange folder written by the Blender scripts (catalogue.json, meshes/*.json,
/// textures/*.png) into the one asset bundle the mod ships. The bundle carries meshes, textures
/// and the catalogue only: no materials, shaders or scripts, so this project needs nothing of
/// the game's. Run from the command line (see build_bundle.ps1):
///   Unity.exe -batchmode -quit -projectPath . -executeMethod CharacterBundleBuilder.Build
///             [-buildFolder path] [-outFolder path]
///   Unity.exe -batchmode -quit -projectPath . -executeMethod CharacterBundleBuilder.Verify [-outFolder path]
/// </summary>
public static class CharacterBundleBuilder
{
    public const string BundleName = "reallife_character";
    public const string CatalogueAsset = "catalogue";

    private const string Generated = "Assets/Generated";
    private const int BoneCount = 55;
    private const int WeightsPerVertex = 4;

    // ---------------------------------------------------------------- the interchange format

    [Serializable]
    public class Catalogue
    {
        public int version;
        public string[] bones;
        public TextureEntry[] textures;
        public Item[] items;
    }

    [Serializable]
    public class TextureEntry
    {
        public string name;
        public string file;
        public string type;     // color | normal | mask | tint (a colour mask: linear, pure channels)
        public int maxSize;
    }

    [Serializable]
    public class Item
    {
        public string id;
        public string kind;
        public string sex;
        public string mesh;
        public Layer[] layers;
        public string skinFrom;
    }

    [Serializable]
    public class Layer
    {
        public string role;
        public string texture;
        public string normal;
        public string mask;
        public string tintMask;
    }

    [Serializable]
    public class MeshFile
    {
        public string name;
        public float[] vertices;
        public float[] normals;
        public float[] uv;
        public int[] boneIndices;
        public float[] boneWeights;
        public Submesh[] submeshes;
        public Shape[] shapes;
    }

    [Serializable]
    public class Submesh
    {
        public int[] triangles;
    }

    [Serializable]
    public class Shape
    {
        public string name;
        public float[] deltas;
    }

    // ---------------------------------------------------------------- entry points

    public static void Build()
    {
        Run(() =>
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string buildFolder = Path.GetFullPath(Argument("-buildFolder", Path.Combine(project, "..", "build")));
            string outFolder = OutFolder(project);
            string cataloguePath = Path.Combine(buildFolder, "catalogue.json");
            if (!File.Exists(cataloguePath))
            {
                throw new Exception("No catalogue.json in " + buildFolder + ". Run the Blender scripts first.");
            }
            string catalogueText = File.ReadAllText(cataloguePath);
            Catalogue catalogue = JsonUtility.FromJson<Catalogue>(catalogueText);
            Check(catalogue.bones != null && catalogue.bones.Length == BoneCount, "the catalogue lists " + (catalogue.bones?.Length ?? 0) + " bones, expected " + BoneCount);
            Check(catalogue.items != null && catalogue.items.Length > 0, "the catalogue has no items");

            if (AssetDatabase.IsValidFolder(Generated))
            {
                AssetDatabase.DeleteAsset(Generated);
            }
            Directory.CreateDirectory(Generated + "/Meshes");
            Directory.CreateDirectory(Generated + "/Textures");
            AssetDatabase.Refresh();

            var paths = new List<string>();
            var names = new List<string>();

            // Textures: copied in, then imported with the settings the game's own character textures use.
            var textureNames = new HashSet<string>();
            foreach (TextureEntry texture in catalogue.textures ?? new TextureEntry[0])
            {
                Check(textureNames.Add(texture.name), "texture '" + texture.name + "' is listed twice");
                string source = Path.Combine(buildFolder, texture.file);
                Check(File.Exists(source), "texture file missing: " + source);
                string target = Generated + "/Textures/" + texture.name + Path.GetExtension(texture.file);
                File.Copy(source, target, true);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                ConfigureTexture(target, texture);
                paths.Add(target);
                names.Add(texture.name);
            }

            // Meshes: one per distinct mesh name.
            var meshSubmeshes = new Dictionary<string, int>();
            foreach (Item item in catalogue.items)
            {
                Check(!string.IsNullOrEmpty(item.id) && !string.IsNullOrEmpty(item.mesh), "an item has no id or mesh");
                if (!meshSubmeshes.ContainsKey(item.mesh))
                {
                    string source = Path.Combine(buildFolder, "meshes", item.mesh + ".json");
                    Check(File.Exists(source), "mesh file missing: " + source);
                    Mesh mesh = BuildMesh(JsonUtility.FromJson<MeshFile>(File.ReadAllText(source)), item.mesh);
                    string target = Generated + "/Meshes/" + item.mesh + ".asset";
                    AssetDatabase.CreateAsset(mesh, target);
                    meshSubmeshes.Add(item.mesh, mesh.subMeshCount);
                    paths.Add(target);
                    names.Add(item.mesh);
                }
                CheckItem(item, meshSubmeshes[item.mesh], textureNames);
            }

            string catalogueTarget = Generated + "/" + CatalogueAsset + ".json";
            File.WriteAllText(catalogueTarget, catalogueText);
            AssetDatabase.ImportAsset(catalogueTarget, ImportAssetOptions.ForceSynchronousImport);
            paths.Add(catalogueTarget);
            names.Add(CatalogueAsset);
            AssetDatabase.SaveAssets();

            string staging = Path.Combine(project, "BundleOutput");
            Directory.CreateDirectory(staging);
            var bundle = new AssetBundleBuild
            {
                assetBundleName = BundleName,
                assetNames = paths.ToArray(),
                addressableNames = names.ToArray()
            };
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(staging, new[] { bundle },
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode | BuildAssetBundleOptions.ForceRebuildAssetBundle,
                BuildTarget.StandaloneWindows64);
            Check(manifest != null, "BuildAssetBundles returned nothing");

            // Only the bundle itself ships; Unity's manifest files stay in the staging folder.
            Directory.CreateDirectory(outFolder);
            string shipped = Path.Combine(outFolder, BundleName);
            File.Copy(Path.Combine(staging, BundleName), shipped, true);
            Debug.Log("[CharacterBundle] BUILT " + shipped + "  " + (new FileInfo(shipped).Length / 1024) + " KB, " + meshSubmeshes.Count + " meshes, "
                      + textureNames.Count + " textures, " + catalogue.items.Length + " items");
        });
    }

    /// <summary>
    /// Reads the shipped bundle the way the mod does (the catalogue first; a look's mesh and textures when it is worn;
    /// let go and read again) and lists what is in it; fails on anything the mod would trip over. Also adds up the
    /// texture memory: everything at once (what the mod held before it read looks on demand) against one outfit.
    /// </summary>
    public static void Verify()
    {
        Run(() =>
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string shipped = Path.Combine(OutFolder(project), BundleName);
            Check(File.Exists(shipped), "no bundle at " + shipped);
            AssetBundle bundle = AssetBundle.LoadFromFile(shipped);
            Check(bundle != null, "AssetBundle.LoadFromFile failed for " + shipped);
            try
            {
                var catalogueAsset = bundle.LoadAsset<TextAsset>(CatalogueAsset);
                Check(catalogueAsset != null, "the bundle has no catalogue");
                Catalogue catalogue = JsonUtility.FromJson<Catalogue>(catalogueAsset.text);
                Check(catalogue.version == 2, "the catalogue is version " + catalogue.version);

                // Every asset by name and type, one at a time; sizes are taken from the dimensions (both formats used are
                // one byte a texel, plus a third for the mips), because nothing reaches a graphics card in batch mode.
                var bytes = new Dictionary<string, long>();
                var meshes = new Dictionary<string, int>();
                int triangles = 0;
                long everything = 0;
                var listed = new HashSet<string> { CatalogueAsset };
                foreach (TextureEntry entry in catalogue.textures)
                {
                    listed.Add(entry.name);
                    var texture = bundle.LoadAsset<Texture2D>(entry.name);
                    Check(texture != null, "texture '" + entry.name + "' is not in the bundle");
                    long size = (long)texture.width * texture.height * 4 / 3;
                    bytes[entry.name] = size;
                    everything += size;
                    Debug.Log("[CharacterBundle]   texture " + texture.name + ": " + texture.width + "x" + texture.height + " " + texture.format + ", " + texture.mipmapCount
                              + " mips, " + (size / 1024) + " KB");
                    Resources.UnloadAsset(texture);
                }
                foreach (Item item in catalogue.items)
                {
                    listed.Add(item.mesh);
                    if (meshes.ContainsKey(item.mesh))
                    {
                        continue;
                    }
                    var mesh = bundle.LoadAsset<Mesh>(item.mesh);
                    Check(mesh != null, "item '" + item.id + "': mesh '" + item.mesh + "' is not in the bundle");
                    int count = 0;
                    for (int s = 0; s < mesh.subMeshCount; s++)
                    {
                        count += (int)mesh.GetIndexCount(s) / 3;
                    }
                    triangles += count;
                    Check(mesh.isReadable, "mesh '" + mesh.name + "' is not readable");
                    Check(mesh.bindposes.Length == BoneCount, "mesh '" + mesh.name + "' has " + mesh.bindposes.Length + " bindposes");
                    Debug.Log("[CharacterBundle]   mesh " + mesh.name + ": " + mesh.vertexCount + " vertices, " + count + " triangles, " + mesh.subMeshCount
                              + " submeshes, " + mesh.blendShapeCount + " shapes" + ShapeNames(mesh));
                    meshes[item.mesh] = mesh.subMeshCount;
                    Resources.UnloadAsset(mesh);
                }
                var listedLower = new HashSet<string>();
                foreach (string known in listed)
                {
                    listedLower.Add(known.ToLowerInvariant());
                }
                foreach (string name in bundle.GetAllAssetNames())
                {
                    string plain = Path.GetFileNameWithoutExtension(name);
                    Check(listedLower.Contains(plain.ToLowerInvariant()), "unexpected asset in the bundle: " + name);
                }

                // What one look costs, and the dearest outfit: top, legs and shoes (a dress is a top and legs), the scalp
                // underlay and the shared strand maps.
                var used = new HashSet<string>();
                var dearest = new Dictionary<string, long>();
                var dearestId = new Dictionary<string, string>();
                foreach (Item item in catalogue.items)
                {
                    CheckItem(item, meshes[item.mesh], new HashSet<string>(bytes.Keys));
                    long cost = 0;
                    var mine = new HashSet<string>();
                    foreach (Layer layer in item.layers)
                    {
                        foreach (string name in new[] { layer.texture, layer.normal, layer.mask, layer.tintMask })
                        {
                            if (!string.IsNullOrEmpty(name) && mine.Add(name))
                            {
                                used.Add(name);
                                cost += bytes[name];
                            }
                        }
                    }
                    string slot = item.sex + " " + item.kind;
                    if (!dearest.ContainsKey(slot) || cost > dearest[slot])
                    {
                        dearest[slot] = cost;
                        dearestId[slot] = item.id;
                    }
                }
                long shared = 0;
                foreach (KeyValuePair<string, long> pair in bytes)
                {
                    if (!used.Contains(pair.Key))
                    {
                        shared += pair.Value;
                    }
                }
                long outfit = 0;
                string outfitText = "";
                foreach (string sex in new[] { "male", "female" })
                {
                    long total = shared;
                    string text = "";
                    foreach (string kind in new[] { "hair", "top", "legs", "feet" })
                    {
                        if (dearest.ContainsKey(sex + " " + kind))
                        {
                            total += dearest[sex + " " + kind];
                            text += (text.Length > 0 ? ", " : "") + dearestId[sex + " " + kind] + " " + (dearest[sex + " " + kind] / 1024) + " KB";
                        }
                    }
                    if (total > outfit)
                    {
                        outfit = total;
                        outfitText = text;
                    }
                }

                // The mod's cycle on one look: read, let go, read again, and get a usable object both times.
                Item probe = catalogue.items[catalogue.items.Length - 1];
                for (int round = 0; round < 2; round++)
                {
                    var mesh = bundle.LoadAsset<Mesh>(probe.mesh);
                    var texture = bundle.LoadAsset<Texture2D>(probe.layers[0].texture);
                    Check(mesh != null && mesh.vertexCount > 0 && texture != null && texture.width > 0, "round " + round + ": '" + probe.id + "' did not come back after being let go");
                    Resources.UnloadAsset(mesh);
                    Resources.UnloadAsset(texture);
                }

                Debug.Log("[CharacterBundle] MEMORY textures: all " + bytes.Count + " at once " + (everything / 1048576f).ToString("0.0") + " MB; read on demand: "
                          + (shared / 1048576f).ToString("0.0") + " MB shared at start, " + (outfit / 1048576f).ToString("0.0") + " MB with the dearest outfit worn ("
                          + outfitText + ")");
                Debug.Log("[CharacterBundle] VERIFIED " + shipped + "  " + (new FileInfo(shipped).Length / 1024) + " KB on disk: " + catalogue.items.Length + " items, "
                          + meshes.Count + " meshes (" + triangles + " triangles), " + bytes.Count + " textures; read, let go and read again: ok");
            }
            finally
            {
                bundle.Unload(true);
            }
        });
    }

    // ---------------------------------------------------------------- meshes

    private static Mesh BuildMesh(MeshFile file, string name)
    {
        Check(file != null && file.vertices != null && file.vertices.Length >= 9 && file.vertices.Length % 3 == 0, name + ": no vertices");
        int count = file.vertices.Length / 3;
        Check(file.normals != null && file.normals.Length == count * 3, name + ": normals do not match the vertices");
        Check(file.uv != null && file.uv.Length == count * 2, name + ": uv does not match the vertices");
        Check(file.boneIndices != null && file.boneIndices.Length == count * WeightsPerVertex, name + ": boneIndices must be 4 per vertex");
        Check(file.boneWeights != null && file.boneWeights.Length == count * WeightsPerVertex, name + ": boneWeights must be 4 per vertex");
        Check(file.submeshes != null && file.submeshes.Length > 0, name + ": no submeshes");

        var mesh = new Mesh { name = name, indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        var vertices = new Vector3[count];
        var normals = new Vector3[count];
        var uv = new Vector2[count];
        var weights = new BoneWeight[count];
        for (int i = 0; i < count; i++)
        {
            vertices[i] = new Vector3(file.vertices[i * 3], file.vertices[i * 3 + 1], file.vertices[i * 3 + 2]);
            normals[i] = new Vector3(file.normals[i * 3], file.normals[i * 3 + 1], file.normals[i * 3 + 2]).normalized;
            uv[i] = new Vector2(file.uv[i * 2], file.uv[i * 2 + 1]);
            weights[i] = Weight(file, i, name);
        }
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uv;
        mesh.boneWeights = weights;

        // Placeholders: the mod copies the real ones from the game's own head mesh when it first uses this mesh.
        var bindposes = new Matrix4x4[BoneCount];
        for (int i = 0; i < BoneCount; i++)
        {
            bindposes[i] = Matrix4x4.identity;
        }
        mesh.bindposes = bindposes;

        mesh.subMeshCount = file.submeshes.Length;
        for (int s = 0; s < file.submeshes.Length; s++)
        {
            int[] triangles = file.submeshes[s].triangles;
            Check(triangles != null && triangles.Length >= 3 && triangles.Length % 3 == 0, name + ": submesh " + s + " has no triangles");
            for (int t = 0; t < triangles.Length; t++)
            {
                Check(triangles[t] >= 0 && triangles[t] < count, name + ": submesh " + s + " points outside the vertices");
            }
            mesh.SetTriangles(triangles, s, false);
        }

        if (file.shapes != null)
        {
            var deltas = new Vector3[count];
            foreach (Shape shape in file.shapes)
            {
                Check(shape.deltas != null && shape.deltas.Length == count * 3, name + ": shape '" + shape.name + "' does not match the vertices");
                for (int i = 0; i < count; i++)
                {
                    deltas[i] = new Vector3(shape.deltas[i * 3], shape.deltas[i * 3 + 1], shape.deltas[i * 3 + 2]);
                }
                mesh.AddBlendShapeFrame(shape.name, 100f, deltas, null, null);
            }
        }

        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    private static BoneWeight Weight(MeshFile file, int vertex, string name)
    {
        int o = vertex * WeightsPerVertex;
        float total = 0f;
        for (int k = 0; k < WeightsPerVertex; k++)
        {
            Check(file.boneIndices[o + k] >= 0 && file.boneIndices[o + k] < BoneCount, name + ": vertex " + vertex + " names bone " + file.boneIndices[o + k]);
            total += Mathf.Max(0f, file.boneWeights[o + k]);
        }
        Check(total > 1e-4f, name + ": vertex " + vertex + " has no bone weights");
        return new BoneWeight
        {
            boneIndex0 = file.boneIndices[o], weight0 = Mathf.Max(0f, file.boneWeights[o]) / total,
            boneIndex1 = file.boneIndices[o + 1], weight1 = Mathf.Max(0f, file.boneWeights[o + 1]) / total,
            boneIndex2 = file.boneIndices[o + 2], weight2 = Mathf.Max(0f, file.boneWeights[o + 2]) / total,
            boneIndex3 = file.boneIndices[o + 3], weight3 = Mathf.Max(0f, file.boneWeights[o + 3]) / total
        };
    }

    private static string ShapeNames(Mesh mesh)
    {
        if (mesh.blendShapeCount == 0)
        {
            return "";
        }
        string names = " [";
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            names += (i > 0 ? " " : "") + mesh.GetBlendShapeName(i);
        }
        return names + "]";
    }

    // ---------------------------------------------------------------- textures

    private static void ConfigureTexture(string path, TextureEntry entry)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        bool normal = entry.type == "normal";
        bool mask = entry.type == "mask" || entry.type == "tint";
        Check(normal || mask || entry.type == "color", "texture '" + entry.name + "' has unknown type '" + entry.type + "'");
        int size = entry.maxSize > 0 ? entry.maxSize : 1024;

        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !normal && !mask;
        importer.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = !normal && !mask;      // bleeds colour under the transparent parts: no dark fringes on strands
        importer.mipmapEnabled = true;
        importer.mipMapsPreserveCoverage = !normal && !mask;  // strands keep their width in the smaller mips
        importer.alphaTestReferenceValue = 0.4f;
        importer.streamingMipmaps = false;
        importer.isReadable = false;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.npotScale = TextureImporterNPOTScale.ToNearest;
        importer.maxTextureSize = size;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {
            name = "Standalone",
            overridden = true,
            maxTextureSize = size,
            format = normal ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7,
            textureCompression = TextureImporterCompression.CompressedHQ
        });
        importer.SaveAndReimport();
    }

    // ---------------------------------------------------------------- shared checks

    private static void CheckItem(Item item, int submeshes, HashSet<string> textures)
    {
        Check(item.layers != null && item.layers.Length == submeshes, "item '" + item.id + "' has " + (item.layers?.Length ?? 0) + " layers for " + submeshes + " submeshes");
        foreach (Layer layer in item.layers)
        {
            Check(!string.IsNullOrEmpty(layer.texture) && textures.Contains(layer.texture), "item '" + item.id + "': texture '" + layer.texture + "' is not in the catalogue");
            Check(string.IsNullOrEmpty(layer.normal) || textures.Contains(layer.normal), "item '" + item.id + "': normal '" + layer.normal + "' is not in the catalogue");
            Check(string.IsNullOrEmpty(layer.mask) || textures.Contains(layer.mask), "item '" + item.id + "': mask '" + layer.mask + "' is not in the catalogue");
            Check(string.IsNullOrEmpty(layer.tintMask) || textures.Contains(layer.tintMask), "item '" + item.id + "': tint mask '" + layer.tintMask + "' is not in the catalogue");
        }
    }

    private static void Check(bool condition, string problem)
    {
        if (!condition)
        {
            throw new Exception(problem);
        }
    }

    private static string OutFolder(string project)
    {
        return Path.GetFullPath(Argument("-outFolder", Path.Combine(project, "..", "..", "Assets", "Mods", "RealLifeOverhaul", "Bundles")));
    }

    private static string Argument(string name, string fallback)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int i = 0; i < arguments.Length - 1; i++)
        {
            if (arguments[i] == name)
            {
                return arguments[i + 1];
            }
        }
        return fallback;
    }

    // Batch mode: a failure must end the process with a non-zero exit code, not leave the editor open.
    private static void Run(Action work)
    {
        try
        {
            work();
        }
        catch (Exception e)
        {
            Debug.LogError("[CharacterBundle] FAILED: " + e.Message + "\n" + e.StackTrace);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
            return;
        }
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(0);
        }
    }
}
