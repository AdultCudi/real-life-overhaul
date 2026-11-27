using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Turns the cars' interchange folders (GarageAssets/cars/&lt;id&gt;/car.json with its meshes/*.json and textures/*.png,
/// written from Blender) into the bundle of the mod's own cars, reallife_cars. The bundle carries meshes and textures
/// only: no materials, shaders, colliders or scripts, so this project needs nothing of the game's. The mod makes each
/// car's prefab while the game runs, from the game car it is built on (Scripts/Garage/ModCars.cs). Meshes stay readable:
/// the game dents a car's body on a crash by moving its vertices, and builds a convex collider from the hull.
///   Unity.exe -batchmode -quit -projectPath . -executeMethod CarBundleBuilder.Build  [-buildFolder path] [-outFolder path]
///   Unity.exe -batchmode -quit -projectPath . -executeMethod CarBundleBuilder.Verify [-outFolder path] [-contents file]
/// </summary>
public static class CarBundleBuilder
{
    public const string BundleName = "reallife_cars";
    private const string Generated = "Assets/Generated";
    private const string Tag = "[CarBundle] ";

    [Serializable]
    public class Car
    {
        public string id;
        public string[] meshes;          // names; each is meshes/<name>.json
        public TextureEntry[] textures;
    }

    [Serializable]
    public class TextureEntry
    {
        public string name;
        public string type;      // color (sRGB, its alpha kept: the paint) | data (linear, alpha kept) | normal | glow (sRGB, no alpha) | switch (linear, no alpha)
        public int size;
    }

    [Serializable]
    public class MeshFile
    {
        public string name;
        public float[] vertices;
        public float[] normals;
        public float[] tangents;
        public float[] uv;
        public int[] triangles;
    }

    public static void Build()
    {
        Run(() =>
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string buildFolder = Path.GetFullPath(Argument("-buildFolder", Path.Combine(project, "..", "cars")));
            string outFolder = OutFolder(project);
            if (AssetDatabase.IsValidFolder(Generated))
            {
                AssetDatabase.DeleteAsset(Generated);
            }
            Directory.CreateDirectory(Generated + "/Meshes");
            Directory.CreateDirectory(Generated + "/Textures");
            AssetDatabase.Refresh();
            var paths = new List<string>();
            var names = new List<string>();
            var seen = new HashSet<string>();
            int cars = 0, triangles = 0;
            string[] folders = Directory.GetDirectories(buildFolder);
            Array.Sort(folders, StringComparer.Ordinal);
            foreach (string folder in folders)
            {
                string file = Path.Combine(folder, "car.json");
                if (!File.Exists(file))
                {
                    continue;
                }
                Car car = JsonUtility.FromJson<Car>(File.ReadAllText(file));
                Check(car != null && !string.IsNullOrEmpty(car.id) && car.id == Path.GetFileName(folder), "car.json in " + folder + " does not name its folder");
                Check(car.meshes != null && car.meshes.Length > 0 && car.textures != null && car.textures.Length > 0, car.id + " lists no meshes or no textures");
                foreach (string meshName in car.meshes)
                {
                    Check(meshName.StartsWith(car.id + "_", StringComparison.Ordinal) && seen.Add(meshName), "mesh " + meshName + " is not this car's, or is listed twice");
                    string source = Path.Combine(folder, "meshes", meshName + ".json");
                    Check(File.Exists(source), "mesh file missing: " + source);
                    Mesh mesh = BuildMesh(JsonUtility.FromJson<MeshFile>(File.ReadAllText(source)), meshName);
                    triangles += mesh.triangles.Length / 3;
                    string target = Generated + "/Meshes/" + meshName + ".asset";
                    AssetDatabase.CreateAsset(mesh, target);
                    paths.Add(target);
                    names.Add(meshName);
                }
                foreach (TextureEntry texture in car.textures)
                {
                    Check(texture.name.StartsWith(car.id + "_", StringComparison.Ordinal) && seen.Add(texture.name), "texture " + texture.name + " is not this car's, or is listed twice");
                    string source = Path.Combine(folder, "textures", texture.name + ".png");
                    Check(File.Exists(source), "texture file missing: " + source);
                    string target = Generated + "/Textures/" + texture.name + ".png";
                    File.Copy(source, target, true);
                    AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                    ConfigureTexture(target, texture);
                    paths.Add(target);
                    names.Add(texture.name);
                }
                cars++;
            }
            Check(cars > 0, "no car.json under " + buildFolder);
            AssetDatabase.SaveAssets();
            string staging = Path.Combine(project, "BundleOutput");
            Directory.CreateDirectory(staging);
            var bundle = new AssetBundleBuild { assetBundleName = BundleName, assetNames = paths.ToArray(), addressableNames = names.ToArray() };
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(staging, new[] { bundle },
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode | BuildAssetBundleOptions.ForceRebuildAssetBundle,
                BuildTarget.StandaloneWindows64);
            Check(manifest != null, "BuildAssetBundles returned nothing");
            Directory.CreateDirectory(outFolder);
            string shipped = Path.Combine(outFolder, BundleName);
            File.Copy(Path.Combine(staging, BundleName), shipped, true);
            Debug.Log(Tag + "BUILT " + shipped + "  " + (new FileInfo(shipped).Length / 1024) + " KB, " + cars + " car(s), " + triangles + " triangles, " + names.Count + " assets");
        });
    }

    /// <summary>Loads the shipped bundle the way the mod does and writes what it holds; fails on anything the mod would trip over.</summary>
    public static void Verify()
    {
        Run(() =>
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string shipped = Path.Combine(OutFolder(project), BundleName);
            Check(File.Exists(shipped), "no bundle at " + shipped);
            AssetBundle bundle = AssetBundle.LoadFromFile(shipped);
            Check(bundle != null, "AssetBundle.LoadFromFile failed for " + shipped);
            var rows = new List<string>();
            var textureRows = new List<string>();
            long textureBytes = 0;
            try
            {
                foreach (UnityEngine.Object asset in bundle.LoadAllAssets())
                {
                    if (asset is Mesh mesh)
                    {
                        Check(mesh.isReadable, "mesh " + mesh.name + " is not readable: the game could not dent it or cook its collider");
                        Check(mesh.vertexCount > 0 && mesh.normals.Length == mesh.vertexCount && mesh.uv.Length == mesh.vertexCount && mesh.tangents.Length == mesh.vertexCount,
                            "mesh " + mesh.name + " lacks normals, tangents or UVs");
                        Bounds b = mesh.bounds;
                        rows.Add("\n  {\"name\": \"" + mesh.name + "\", \"vertices\": " + mesh.vertexCount + ", \"triangles\": " + mesh.triangles.Length / 3
                                 + ", \"size\": [" + F(b.size.x) + ", " + F(b.size.y) + ", " + F(b.size.z) + "], \"centre\": [" + F(b.center.x) + ", " + F(b.center.y) + ", " + F(b.center.z) + "]}");
                    }
                    else if (asset is Texture2D texture)
                    {
                        textureBytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
                        textureRows.Add("\n  {\"name\": \"" + texture.name + "\", \"width\": " + texture.width + ", \"height\": " + texture.height + ", \"format\": \"" + texture.format
                                        + "\", \"mips\": " + texture.mipmapCount + "}");
                    }
                }
            }
            finally
            {
                bundle.Unload(true);
            }
            Check(rows.Count > 0 && textureRows.Count > 0, "the bundle holds no meshes or no textures");
            rows.Sort(StringComparer.Ordinal);
            textureRows.Sort(StringComparer.Ordinal);
            var text = new StringBuilder(2048);
            text.Append("{\n \"bundle\": \"").Append(BundleName).Append("\",\n \"bytes\": ").Append(new FileInfo(shipped).Length).Append(",\n \"meshes\": [");
            text.Append(string.Join(",", rows)).Append("\n ],\n \"textures\": [").Append(string.Join(",", textureRows)).Append("\n ]\n}\n");
            string contents = Path.GetFullPath(Argument("-contents", Path.Combine(project, "..", "contents", BundleName + ".json")));
            Directory.CreateDirectory(Path.GetDirectoryName(contents));
            File.WriteAllText(contents, text.ToString());
            Debug.Log(Tag + "VERIFIED " + shipped + ": " + rows.Count + " meshes, " + textureRows.Count + " textures (" + (textureBytes / 1024) + " KB in video memory); list at " + contents);
        });
    }

    private static string F(float value)
    {
        return value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static Mesh BuildMesh(MeshFile file, string name)
    {
        Check(file != null && file.vertices != null && file.vertices.Length >= 9 && file.vertices.Length % 3 == 0, "mesh " + name + " has no vertices");
        int count = file.vertices.Length / 3;
        Check(file.normals != null && file.normals.Length == count * 3 && file.uv != null && file.uv.Length == count * 2
              && file.tangents != null && file.tangents.Length == count * 4, "mesh " + name + ": normals, tangents and UVs must match the vertices");
        Check(count < 65535, "mesh " + name + " needs 32-bit indices");
        var vertices = new Vector3[count];
        var normals = new Vector3[count];
        var tangents = new Vector4[count];
        var uv = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            vertices[i] = new Vector3(file.vertices[i * 3], file.vertices[i * 3 + 1], file.vertices[i * 3 + 2]);
            normals[i] = new Vector3(file.normals[i * 3], file.normals[i * 3 + 1], file.normals[i * 3 + 2]);
            tangents[i] = new Vector4(file.tangents[i * 4], file.tangents[i * 4 + 1], file.tangents[i * 4 + 2], file.tangents[i * 4 + 3]);
            uv[i] = new Vector2(file.uv[i * 2], file.uv[i * 2 + 1]);
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTangents(tangents);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(file.triangles, 0, false);
        mesh.RecalculateBounds();
        return mesh;   // left readable (see the summary)
    }

    private static void ConfigureTexture(string path, TextureEntry entry)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        bool normal = entry.type == "normal", colour = entry.type == "color", data = entry.type == "data", glow = entry.type == "glow", flag = entry.type == "switch";
        Check(normal || colour || data || glow || flag, "texture " + entry.name + " has unknown type " + entry.type);
        int size = entry.size > 0 ? entry.size : 1024;
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = colour || glow;
        importer.alphaSource = colour || data ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = false;      // the alpha is data (where the paint goes, how smooth): the colour under a zero is kept
        importer.mipmapEnabled = true;
        importer.streamingMipmaps = false;
        importer.isReadable = false;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = size;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {
            name = "Standalone",
            overridden = true,
            maxTextureSize = size,
            format = normal ? TextureImporterFormat.BC5 : colour || data ? TextureImporterFormat.BC7 : TextureImporterFormat.DXT1,
            textureCompression = TextureImporterCompression.CompressedHQ
        });
        importer.SaveAndReimport();
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
