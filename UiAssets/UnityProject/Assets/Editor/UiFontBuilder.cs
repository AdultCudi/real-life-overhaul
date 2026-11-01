using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Turns the static fonts cut by UiAssets/scripts/fonts.py into the interface's TextMeshPro font assets and the bundle the
/// mod ships (reallife_ui): one signed distance field atlas a weight, Sora's falling back to Manrope of the same weight for
/// the Cyrillic and Greek Sora does not draw, each family's weights in each other's weight table, the faces' list and the
/// two licences as text assets. Static atlases: the game never rasterises a glyph while it is played. Built with the
/// game's TextMeshPro (3.2.0-pre.10) so the assets read as the game's own. Run from the command line (see build.ps1):
///   Unity.exe -batchmode -quit -projectPath . -executeMethod UiFontBuilder.Prepare
///   Unity.exe -batchmode -quit -projectPath . -executeMethod UiFontBuilder.Build -fontsFolder path -outFolder path
///   Unity.exe -batchmode -quit -projectPath . -executeMethod UiFontBuilder.Verify -outFolder path -contents path
/// </summary>
public static class UiFontBuilder
{
    public const string BundleName = "reallife_ui";
    public const string FacesAsset = "faces";

    private const string Generated = "Assets/Generated";
    private const string Tag = "[UiBundle] ";
    private const int SamplingSize = 48;   // px: the atlas's em; at the interface's largest size (88 px at 4K) still a sharp edge
    private const int Padding = 5;         // px of distance field around a glyph, a tenth of the em as TextMeshPro's own default
    private const int Version = 1;

    // The City Ledger's faces: Source Serif 4's display cut for heads, its text cut for reading, Libre Franklin for the
    // paper's labels and figures (UiAssets/scripts/fonts.py cuts them; UiTokens names them by family and weight).
    private static readonly string[] Families = { "SerifDisplay", "SerifDisplay", "Serif", "Serif", "Franklin", "Franklin", "Franklin" };
    private static readonly string[] Styles = { "SemiBold", "Bold", "Medium", "Bold", "SemiBold", "Bold", "ExtraBold" };
    private static readonly int[] Weights = { 600, 700, 500, 700, 600, 700, 800 };
    private static readonly string[] Licences = { "OFL-SourceSerif4", "OFL-LibreFranklin" };
    private static readonly Vector2Int[] AtlasSizes = { new Vector2Int(1024, 1024), new Vector2Int(2048, 1024), new Vector2Int(2048, 2048) };

    [Serializable]
    public class FaceEntry
    {
        public string asset;
        public string family;
        public int weight;
        public int characters;
        public int atlasWidth;
        public int atlasHeight;
        public long bytes;
    }

    [Serializable]
    public class FaceList
    {
        public int version;
        public int samplingSize;
        public FaceEntry[] faces;
        public long totalBytes;
    }

    /// <summary>
    /// Opens the project and nothing more, so Unity resolves its packages into Library/PackageCache; build.ps1 then
    /// unpacks TextMeshPro's essential resources from there (scripts/tmp_essentials.py), as ImportPackage returns before
    /// its import is done in batch mode.
    /// </summary>
    public static void Prepare()
    {
        Debug.Log(Tag + "the project is open and its packages resolved");
    }

    public static void Build()
    {
        try
        {
            string fonts = Argument("-fontsFolder", Path.GetFullPath("../build/fonts"));
            string outFolder = Argument("-outFolder", Path.GetFullPath("../../Assets/Mods/RealLifeOverhaul/Bundles"));
            string licences = Argument("-licences", Path.GetFullPath("../fonts"));
            if (Shader.Find("TextMeshPro/Distance Field") == null)
            {
                Fail("TextMeshPro's Distance Field shader is missing: run Prepare first");
            }
            if (AssetDatabase.IsValidFolder(Generated))
            {
                AssetDatabase.DeleteAsset(Generated);
            }
            AssetDatabase.CreateFolder("Assets", "Generated");
            var made = new Dictionary<string, TMP_FontAsset>();
            var entries = new List<FaceEntry>();
            var names = new List<string>();
            for (int f = 0; f < Families.Length; f++)
            {
                uint[] codes = Characters(Path.Combine(fonts, Families[f] + ".chars.txt"));
                string name = Families[f] + "-" + Styles[f];
                TMP_FontAsset asset = Face(Path.Combine(fonts, name + ".ttf"), name, codes, out Vector2Int size);
                made[name] = asset;
                names.Add(Generated + "/" + name + " SDF.asset");
                entries.Add(new FaceEntry { asset = name + " SDF", family = Families[f], weight = Weights[f], characters = asset.characterTable.Count, atlasWidth = size.x, atlasHeight = size.y });
                Debug.Log(Tag + name + ": " + asset.characterTable.Count + " characters on a " + size.x + " x " + size.y + " atlas");
            }
            Tie(made);
            var list = new FaceList { version = Version, samplingSize = SamplingSize, faces = entries.ToArray() };
            File.WriteAllText(Generated + "/" + FacesAsset + ".json", JsonUtility.ToJson(list, true), new UTF8Encoding(false));
            names.Add(Generated + "/" + FacesAsset + ".json");
            foreach (string licence in Licences)
            {
                File.Copy(Path.Combine(licences, licence + ".txt"), Generated + "/" + licence + ".txt", true);
                names.Add(Generated + "/" + licence + ".txt");
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            string staging = Path.GetFullPath("BundleOutput");
            Directory.CreateDirectory(staging);
            var bundle = new AssetBundleBuild { assetBundleName = BundleName, assetNames = names.ToArray() };
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(staging, new[] { bundle },
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode | BuildAssetBundleOptions.ForceRebuildAssetBundle,
                BuildTarget.StandaloneWindows64);
            if (manifest == null)
            {
                Fail("BuildAssetBundles returned nothing");
            }
            Directory.CreateDirectory(outFolder);
            File.Copy(Path.Combine(staging, BundleName), Path.Combine(outFolder, BundleName), true);
            Debug.Log(Tag + "built " + Path.Combine(outFolder, BundleName) + " (" + new FileInfo(Path.Combine(outFolder, BundleName)).Length + " bytes)");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + e);
            EditorApplication.Exit(1);
        }
    }

    // One weight: a dynamic asset filled with every listed character on the smallest atlas that takes them all, then made
    // static (no source font kept) and saved with its atlas and material inside it.
    private static TMP_FontAsset Face(string ttf, string name, uint[] codes, out Vector2Int size)
    {
        string fontPath = Generated + "/" + name + ".ttf";
        File.Copy(ttf, fontPath, true);
        AssetDatabase.ImportAsset(fontPath, ImportAssetOptions.ForceSynchronousImport);
        var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        if (font == null)
        {
            Fail(name + ": the font did not import");
        }
        foreach (Vector2Int candidate in AtlasSizes)
        {
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, SamplingSize, Padding, GlyphRenderMode.SDFAA, candidate.x, candidate.y, AtlasPopulationMode.Dynamic, false);
            if (asset == null)
            {
                Fail(name + ": TextMeshPro could not make a font asset of it");
            }
            asset.TryAddCharacters(codes, out uint[] missing, true);
            if (missing != null && missing.Length > 0)
            {
                Debug.Log(Tag + name + ": " + missing.Length + " characters left over on " + candidate.x + " x " + candidate.y + ", a larger atlas is tried");
                UnityEngine.Object.DestroyImmediate(asset.atlasTexture);
                UnityEngine.Object.DestroyImmediate(asset.material);
                UnityEngine.Object.DestroyImmediate(asset);
                continue;
            }
            asset.name = name + " SDF";
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            Material material = asset.material;
            material.shader = Shader.Find("TextMeshPro/Distance Field");
            material.name = name + " SDF Material";
            Texture2D atlas = asset.atlasTexture;
            atlas.name = name + " SDF Atlas";
            string path = Generated + "/" + name + " SDF.asset";
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.AddObjectToAsset(atlas, asset);
            AssetDatabase.AddObjectToAsset(material, asset);
            EditorUtility.SetDirty(asset);
            size = candidate;
            return asset;
        }
        Fail(name + ": the characters do not fit the largest atlas");
        size = default;
        return null;
    }

    // Sora's weights fall back to Manrope's of the same weight (Cyrillic, Greek); each family's weights stand in each
    // other's weight table, so <b> and a text's weight pick the family's own cut, not a thickened copy.
    private static void Tie(Dictionary<string, TMP_FontAsset> made)
    {
        for (int f = 0; f < Families.Length; f++)
        {
            TMP_FontAsset asset = made[Families[f] + "-" + Styles[f]];
            TMP_FontWeightPair[] table = asset.fontWeightTable;   // the asset's own array: its setter is the package's alone
            for (int w = 0; w < Families.Length; w++)
            {
                if (Families[w] == Families[f])
                {
                    table[Weights[w] / 100 - 1].regularTypeface = made[Families[w] + "-" + Styles[w]];
                }
            }
            EditorUtility.SetDirty(asset);
        }
    }

    /// <summary>Loads the built bundle back as the mod will, checks every face, measures what it costs and writes the contents list.</summary>
    public static void Verify()
    {
        AssetBundle bundle = null;
        try
        {
            string outFolder = Argument("-outFolder", Path.GetFullPath("../../Assets/Mods/RealLifeOverhaul/Bundles"));
            string contents = Argument("-contents", Path.GetFullPath("../contents/" + BundleName + ".json"));
            string path = Path.Combine(outFolder, BundleName);
            bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
            {
                Fail("the bundle does not load: " + path);
            }
            var list = JsonUtility.FromJson<FaceList>(bundle.LoadAsset<TextAsset>(FacesAsset).text);
            if (list == null || list.version != Version || list.faces == null || list.faces.Length != Families.Length)
            {
                Fail("the faces list is not what this builder writes");
            }
            long total = 0;
            foreach (FaceEntry entry in list.faces)
            {
                var asset = bundle.LoadAsset<TMP_FontAsset>(entry.asset);
                if (asset == null || asset.characterTable.Count != entry.characters || asset.atlasTexture == null || asset.material == null)
                {
                    Fail(entry.asset + " is missing or incomplete in the bundle");
                }
                if (asset.atlasPopulationMode != AtlasPopulationMode.Static || asset.sourceFontFile != null)
                {
                    Fail(entry.asset + " is not static, or keeps its source font");
                }
                if (entry.family == "Sora" && (asset.fallbackFontAssetTable == null || asset.fallbackFontAssetTable.Count != 1))
                {
                    Fail(entry.asset + " does not fall back to Manrope");
                }
                entry.bytes = Profiler.GetRuntimeMemorySizeLong(asset.atlasTexture);
                total += entry.bytes;
            }
            if (bundle.LoadAsset<TextAsset>("OFL-SourceSerif4") == null || bundle.LoadAsset<TextAsset>("OFL-LibreFranklin") == null)
            {
                Fail("a licence is missing from the bundle");
            }
            list.totalBytes = total;
            Directory.CreateDirectory(Path.GetDirectoryName(contents));
            File.WriteAllText(contents, JsonUtility.ToJson(list, true) + "\n", new UTF8Encoding(false));
            Debug.Log(Tag + "verified: " + list.faces.Length + " faces, atlases " + (total / 1048576f).ToString("0.0", CultureInfo.InvariantCulture)
                      + " MB in memory while loaded (readable; the mod frees each atlas's CPU copy as it loads it)");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + e);
            EditorApplication.Exit(1);
        }
        finally
        {
            if (bundle != null)
            {
                bundle.Unload(true);
            }
        }
    }

    private static uint[] Characters(string path)
    {
        if (!File.Exists(path))
        {
            Fail("no character list at " + path + ": run UiAssets/scripts/fonts.py first");
        }
        string[] parts = File.ReadAllText(path).Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var codes = new uint[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            codes[i] = uint.Parse(parts[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        return codes;
    }

    private static void Fail(string why)
    {
        throw new InvalidOperationException(why);
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
}
