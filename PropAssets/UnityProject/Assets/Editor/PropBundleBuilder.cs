using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Turns the interchange folder written by the Blender scripts (catalogue.json, meshes/*.json,
/// textures/*.png) into the asset bundle of street props the mod ships. The bundle carries
/// static meshes, the shared atlas textures and the catalogue only: no materials, shaders,
/// colliders or scripts, so this project needs nothing of the game's. Run from the command
/// line (see build_bundle.ps1):
///   Unity.exe -batchmode -quit -projectPath . -executeMethod PropBundleBuilder.Build
///             [-buildFolder path] [-outFolder path]
///   Unity.exe -batchmode -quit -projectPath . -executeMethod PropBundleBuilder.Verify [-outFolder path]
/// </summary>
public static class PropBundleBuilder
{
    public const string FirstBundle = "reallife_props";

    // -bundleName names the bundle built and checked: the first, or the second (README, "The second bundle").
    private static string BundleName => Argument("-bundleName", FirstBundle);
    public const string CatalogueAsset = "catalogue";

    private const string Generated = "Assets/Generated";
    private const string Tag = "[PropBundle] ";

    // ---------------------------------------------------------------- the interchange format

    [Serializable]
    public class Catalogue
    {
        public int version;
        public TextureEntry[] textures;
        public Item[] items;
    }

    [Serializable]
    public class TextureEntry
    {
        public string name;
        public string file;
        public string type;     // color | mask | decal (colour with its alpha: road paint laid by a decal projector)
        public int maxSize;
    }

    [Serializable]
    public class Item
    {
        public string id;
        public string mesh;
        public string material;     // lit | glow
        public int triangles;
        public float[] size;
    }

    [Serializable]
    public class MeshFile
    {
        public string name;
        public float[] vertices;
        public float[] normals;
        public float[] uv;
        public int[] triangles;
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
            Check(File.Exists(cataloguePath), "no catalogue.json in " + buildFolder + ". Run the Blender scripts first.");
            string catalogueText = File.ReadAllText(cataloguePath);
            Catalogue catalogue = JsonUtility.FromJson<Catalogue>(catalogueText);
            Check(catalogue.items != null && catalogue.items.Length > 0, "the catalogue has no items");
            Check(catalogue.textures != null && catalogue.textures.Length > 0, "the catalogue has no textures");

            if (AssetDatabase.IsValidFolder(Generated))
            {
                AssetDatabase.DeleteAsset(Generated);
            }
            Directory.CreateDirectory(Generated + "/Meshes");
            Directory.CreateDirectory(Generated + "/Textures");
            AssetDatabase.Refresh();

            var paths = new List<string>();
            var names = new List<string>();

            var textureNames = new HashSet<string>();
            foreach (TextureEntry texture in catalogue.textures)
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

            var ids = new HashSet<string>();
            var meshes = new HashSet<string>();
            int triangles = 0;
            foreach (Item item in catalogue.items)
            {
                Check(!string.IsNullOrEmpty(item.id) && !string.IsNullOrEmpty(item.mesh), "an item has no id or mesh");
                Check(ids.Add(item.id), "prop '" + item.id + "' is listed twice");
                Check(item.material == "lit" || item.material == "glow", "prop '" + item.id + "' wants unknown material '" + item.material + "'");
                if (meshes.Add(item.mesh))
                {
                    string source = Path.Combine(buildFolder, "meshes", item.mesh + ".json");
                    Check(File.Exists(source), "mesh file missing: " + source);
                    Mesh mesh = BuildMesh(JsonUtility.FromJson<MeshFile>(File.ReadAllText(source)), item.mesh);
                    triangles += (int)mesh.GetIndexCount(0) / 3;
                    string target = Generated + "/Meshes/" + item.mesh + ".asset";
                    AssetDatabase.CreateAsset(mesh, target);
                    paths.Add(target);
                    names.Add(item.mesh);
                }
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
            Debug.Log(Tag + "BUILT " + shipped + "  " + (new FileInfo(shipped).Length / 1024) + " KB, " + meshes.Count + " meshes (" + triangles + " triangles), "
                      + textureNames.Count + " textures, " + catalogue.items.Length + " props");
        });
    }

    /// <summary>Loads the shipped bundle the way the mod does and lists what is in it; fails on anything the mod would trip over.</summary>
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
                var meshes = new Dictionary<string, Mesh>();
                var textures = new Dictionary<string, Texture2D>();
                TextAsset catalogueAsset = null;
                int triangles = 0;
                long textureBytes = 0;
                long meshBytes = 0;
                foreach (UnityEngine.Object asset in bundle.LoadAllAssets())
                {
                    if (asset is Mesh mesh)
                    {
                        meshes[mesh.name] = mesh;
                        Check(mesh.subMeshCount == 1, "mesh '" + mesh.name + "' has " + mesh.subMeshCount + " submeshes; a prop is drawn with one material");
                        triangles += (int)mesh.GetIndexCount(0) / 3;
                        meshBytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh);
                    }
                    else if (asset is Texture2D texture)
                    {
                        textures[texture.name] = texture;
                        long bytes = (long)texture.width * texture.height * 4 / 3;      // BC7 is one byte a pixel; a third more for the mipmaps
                        textureBytes += bytes;
                        Check(texture.mipmapCount > 1, "texture '" + texture.name + "' has no mipmaps");
                        // Unity quietly keeps a texture whose sides are not powers of two as RGBA32: four times the memory.
                        Check(texture.format == TextureFormat.BC7, "texture '" + texture.name + "' is " + texture.format + ", not BC7: are its sides powers of two?");
                        Debug.Log(Tag + "  texture " + texture.name + ": " + texture.width + "x" + texture.height + " " + texture.format + ", " + texture.mipmapCount
                                  + " mips, " + texture.filterMode + ", aniso " + texture.anisoLevel + ", " + (bytes / 1024) + " KB");
                    }
                    else if (asset is TextAsset text && text.name == CatalogueAsset)
                    {
                        catalogueAsset = text;
                    }
                    else
                    {
                        throw new Exception("unexpected asset in the bundle: " + asset.name + " (" + asset.GetType().Name + ")");
                    }
                }
                Check(catalogueAsset != null, "the bundle has no catalogue");
                Catalogue catalogue = JsonUtility.FromJson<Catalogue>(catalogueAsset.text);
                foreach (TextureEntry texture in catalogue.textures)
                {
                    Check(textures.ContainsKey(texture.name), "texture '" + texture.name + "' is not in the bundle");
                }
                foreach (Item item in catalogue.items)
                {
                    Check(meshes.TryGetValue(item.mesh, out Mesh mesh), "prop '" + item.id + "': mesh '" + item.mesh + "' is not in the bundle");
                    Vector3 size = mesh.bounds.size;
                    Check(item.size != null && item.size.Length == 3, "prop '" + item.id + "' has no size");
                    Check((size - new Vector3(item.size[0], item.size[1], item.size[2])).magnitude < 0.002f, "prop '" + item.id + "': the mesh is " + size.ToString("F3") + ", the catalogue says otherwise");
                    Check((int)mesh.GetIndexCount(0) / 3 == item.triangles, "prop '" + item.id + "': triangle count differs from the catalogue");
                    Debug.Log(Tag + "  prop " + item.id + ": " + mesh.vertexCount + " vertices, " + item.triangles + " triangles, " + size.x.ToString("F2") + " x " + size.y.ToString("F2")
                              + " x " + size.z.ToString("F2") + " m, " + item.material);
                }
                Debug.Log(Tag + "VERIFIED " + shipped + "  " + (new FileInfo(shipped).Length / 1024) + " KB on disk: " + catalogue.items.Length + " props, "
                          + meshes.Count + " meshes (" + triangles + " triangles, " + (meshBytes / 1024) + " KB in memory), " + textures.Count + " textures (" + (textureBytes / 1024) + " KB in memory)");
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
        Check(count <= 65535, name + ": more than 65535 vertices");
        Check(file.normals != null && file.normals.Length == count * 3, name + ": normals do not match the vertices");
        Check(file.uv != null && file.uv.Length == count * 2, name + ": uv does not match the vertices");
        Check(file.triangles != null && file.triangles.Length >= 3 && file.triangles.Length % 3 == 0, name + ": no triangles");

        var vertices = new Vector3[count];
        var normals = new Vector3[count];
        var uv = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            vertices[i] = new Vector3(file.vertices[i * 3], file.vertices[i * 3 + 1], file.vertices[i * 3 + 2]);
            normals[i] = new Vector3(file.normals[i * 3], file.normals[i * 3 + 1], file.normals[i * 3 + 2]).normalized;
            uv[i] = new Vector2(file.uv[i * 2], file.uv[i * 2 + 1]);
        }
        for (int t = 0; t < file.triangles.Length; t++)
        {
            Check(file.triangles[t] >= 0 && file.triangles[t] < count, name + ": a triangle points outside the vertices");
        }

        var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt16 };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uv;
        mesh.SetTriangles(file.triangles, 0, false);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        mesh.Optimize();
        mesh.UploadMeshData(true);      // static: no copy kept in main memory
        return mesh;
    }

    // ---------------------------------------------------------------- textures

    private static void ConfigureTexture(string path, TextureEntry entry)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        bool mask = entry.type == "mask", decal = entry.type == "decal";
        Check(mask || decal || entry.type == "color", "texture '" + entry.name + "' has unknown type '" + entry.type + "'");
        int size = entry.maxSize > 0 ? entry.maxSize : 1024;

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = !mask;
        importer.alphaSource = mask || decal ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = decal;          // (the paint's edges keep its colour where it thins out)
        importer.mipmapEnabled = true;
        importer.streamingMipmaps = false;
        importer.isReadable = false;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.wrapMode = TextureWrapMode.Repeat;       // the tape's row tiles along U
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = size;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {
            name = "Standalone",
            overridden = true,
            maxTextureSize = size,
            format = TextureImporterFormat.BC7,
            textureCompression = TextureImporterCompression.CompressedHQ
        });
        importer.SaveAndReimport();
    }

    // ---------------------------------------------------------------- shared

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
            Debug.LogError(Tag + "FAILED: " + e.Message + "\n" + e.StackTrace);
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
