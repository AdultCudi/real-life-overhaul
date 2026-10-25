using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

// docs/standards/performance.md, RLO-PERF-016: the props bundles are the rows of PropAssets/sets.json. Each shipped
// bundle is the one its committed list (PropAssets/contents/<bundle>.json) describes, held to it by SHA-256, and keeps
// its set's limits; a prop id is in one bundle only; the mod reads every bundle and nothing else reads them; every file
// that ships or lists the mod's files carries every bundle; every prop the code asks for by name is in a bundle; and a
// prop taken out of a bundle is named by no code again, unless it is modelled anew in another.
internal static partial class Program
{
    private const string Rule = "RLO-PERF-016";
    private const string SetsFile = "PropAssets/sets.json";
    private const string ContentsDir = "PropAssets/contents";
    private const string BundlesDir = "Assets/Mods/RealLifeOverhaul/Bundles";

    // The files that list the mod's files by name, each of which must name every bundle.
    private static readonly string[] ListingFiles =
    {
        "INSTALL.md", "RealLifeOverhaul.Build/Workshop/UPLOAD.md", "RealLifeOverhaul.Build/Workshop/CREDITS.md", "docs/reference/folder-map.md",
    };

    // A prop asked for by its name: the library's own lookups, the stand-in makers that go to it first, and a clip's tool.
    private static readonly Regex AskedFor = new Regex(
        @"(?:PropLibrary\.(?:TryGet|Has|SizeOf|Tinted)|Shapes\.Prop|\.Take|\bModelled|PropLibraryHas|JobProps\.Has)\(\s*""([a-z0-9_]+)""", RegexOptions.Compiled);
    private static readonly Regex ClipTool = new Regex(@"\btool=""([a-z0-9_]+)""", RegexOptions.Compiled);
    private static readonly Regex Paths = new Regex(@"BundlePaths\s*=\s*\{([^}]*)\}", RegexOptions.Compiled);

    private static void PropSetChecks()
    {
        string setsPath = Path.Combine(_root, SetsFile);
        Examine(Rule, SetsFile);
        if (!File.Exists(setsPath))
        {
            Hit(Rule, SetsFile, 0, "no " + SetsFile);
            return;
        }
        var sets = new List<JsonElement>();
        using JsonDocument setsDoc = JsonDocument.Parse(File.ReadAllText(setsPath));
        foreach (JsonElement s in setsDoc.RootElement.GetProperty("sets").EnumerateArray())
        {
            sets.Add(s);
        }
        SetRows(sets);

        var heldBy = new Dictionary<string, string>(StringComparer.Ordinal);
        var removed = new Dictionary<string, string>(StringComparer.Ordinal);   // id -> the bundle it was taken out of
        var bundles = new List<string>();
        foreach (JsonElement set in sets)
        {
            string bundle = set.GetProperty("bundle").GetString();
            bundles.Add(bundle);
            if (set.TryGetProperty("removed", out JsonElement gone))
            {
                foreach (JsonElement r in gone.EnumerateArray())
                {
                    removed[r.GetString()] = bundle;
                }
            }
            Bundle(set, bundle, heldBy);
        }
        Loader(bundles);
        Listings(bundles);
        Asked(heldBy, removed);
    }

    // The table itself: names, bundles and folders each once, the first set closed and some set open, atlas sides powers of two.
    private static void SetRows(List<JsonElement> sets)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        bool open = false;
        for (int i = 0; i < sets.Count; i++)
        {
            JsonElement s = sets[i];
            string name = s.GetProperty("name").GetString();
            foreach (string key in new[] { "name", "bundle", "build" })
            {
                if (!seen.Add(key + ":" + s.GetProperty(key).GetString()))
                {
                    Hit(Rule, SetsFile, 0, "the set " + name + " has the " + key + " of another set");
                }
            }
            bool isOpen = s.GetProperty("open").GetBoolean();
            open |= isOpen;
            if (i == 0 && isOpen)
            {
                Hit(Rule, SetsFile, 0, "the first set is open: it is closed, new props go in a later one");
            }
            JsonElement atlas = s.GetProperty("atlas");
            if (!PowerOfTwo(atlas.GetProperty("width").GetInt32()) || !PowerOfTwo(atlas.GetProperty("height").GetInt32()) || !PowerOfTwo(s.GetProperty("mask_divisor").GetInt32()))
            {
                Hit(Rule, SetsFile, 0, "the set " + name + "'s atlas is not powers of two: Unity keeps it uncompressed");
            }
        }
        if (!open)
        {
            Hit(Rule, SetsFile, 0, "no set is open: a new prop has nowhere to go");
        }
    }

    // One bundle: shipped, described by its list (same SHA-256), within its set's limits, its ids new to the others.
    private static void Bundle(JsonElement set, string bundle, Dictionary<string, string> heldBy)
    {
        string subject = BundlesDir + "/" + bundle;
        Examine(Rule, subject);
        string file = Path.Combine(_root, BundlesDir, bundle);
        string listPath = Path.Combine(_root, ContentsDir, bundle + ".json");
        if (!File.Exists(file) || new FileInfo(file).Length == 0)
        {
            Hit(Rule, subject, 0, "not built: run PropAssets/build.ps1 -Set " + set.GetProperty("name").GetString());
            return;
        }
        if (!File.Exists(listPath))
        {
            Hit(Rule, subject, 0, "no " + ContentsDir + "/" + bundle + ".json: build it with build_bundle.ps1, which writes it");
            return;
        }
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(listPath));
        JsonElement list = doc.RootElement;
        string hash;
        using (FileStream stream = File.OpenRead(file))
        using (SHA256 sha = SHA256.Create())
        {
            hash = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        }
        if (list.GetProperty("sha256").GetString() != hash)
        {
            Hit(Rule, subject, 0, "is not the bundle its list describes (SHA-256 " + hash.Substring(0, 12) + "): build it again with build_bundle.ps1");
        }
        if (list.GetProperty("set").GetString() != set.GetProperty("name").GetString())
        {
            Hit(Rule, subject, 0, "its list says it is of the set " + list.GetProperty("set").GetString());
        }
        long bytes = new FileInfo(file).Length;
        if (bytes > set.GetProperty("disk_kb").GetInt64() * 1024)
        {
            Hit(Rule, subject, 0, bytes / 1024 + " KB on disk, over its set's " + set.GetProperty("disk_kb").GetInt64() + " KB");
        }
        var meshes = new Dictionary<string, int>(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int props = 0;
        foreach (JsonElement prop in list.GetProperty("props").EnumerateArray())
        {
            props++;
            string id = prop.GetProperty("id").GetString();
            meshes[prop.GetProperty("mesh").GetString()] = prop.GetProperty("triangles").GetInt32();
            ids.Add(id);
            if (heldBy.TryGetValue(id, out string other))
            {
                Hit(Rule, subject, 0, "'" + id + "' is in " + other + " too: a prop id is in one bundle only");
            }
            else
            {
                heldBy[id] = bundle;
            }
        }
        foreach (JsonElement prop in list.GetProperty("props").EnumerateArray())
        {
            if (prop.TryGetProperty("alias_of", out JsonElement of) && !ids.Contains(of.GetString()))
            {
                Hit(Rule, subject, 0, "'" + prop.GetProperty("id").GetString() + "' is drawn as '" + of.GetString() + "', which the bundle does not hold");
            }
        }
        int triangles = 0;
        foreach (int t in meshes.Values)
        {
            triangles += t;
        }
        int limit = set.GetProperty("triangles").GetInt32();
        if (triangles != list.GetProperty("triangles").GetInt32() || triangles > limit)
        {
            Hit(Rule, subject, 0, triangles + " triangles in its meshes (its list says " + list.GetProperty("triangles").GetInt32() + "), its set allows " + limit);
        }
        JsonElement atlas = list.GetProperty("atlas"), most = set.GetProperty("atlas");
        int width = atlas.GetProperty("width").GetInt32(), height = atlas.GetProperty("height").GetInt32();
        bool none = width == 0 && height == 0;
        if (none != (props == 0) || (!none && (!PowerOfTwo(width) || !PowerOfTwo(height)))
            || width > most.GetProperty("width").GetInt32() || height > most.GetProperty("height").GetInt32() || atlas.GetProperty("used").GetInt32() > 100)
        {
            Hit(Rule, subject, 0, "its atlas is " + width + " x " + height + " (" + atlas.GetProperty("used").GetInt32() + " % used) for " + props
                                  + " props; its set allows " + most.GetProperty("width").GetInt32() + " x " + most.GetProperty("height").GetInt32() + ", and none only with no props");
        }
    }

    // The mod reads every bundle, in the table's order, and is the only code that names one.
    private static void Loader(List<string> bundles)
    {
        const string library = ScriptsDir + "/Core/PropLibrary.cs";
        Examine(Rule, library);
        var wanted = new List<string>();
        foreach (string b in bundles)
        {
            wanted.Add("\"Bundles/" + b + "\"");
        }
        foreach (SourceFile f in Scripts)
        {
            string all = string.Join("\n", f.Lines);
            if (f.Rel == library)
            {
                Match m = Paths.Match(all);
                var found = new List<string>();
                if (m.Success)
                {
                    foreach (string part in m.Groups[1].Value.Split(','))
                    {
                        if (part.Trim().Length > 0)
                        {
                            found.Add(part.Trim());
                        }
                    }
                }
                if (string.Join(",", found) != string.Join(",", wanted))
                {
                    Hit(Rule, library, 0, "BundlePaths is {" + string.Join(", ", found) + "}, sets.json wants {" + string.Join(", ", wanted) + "}");
                }
                continue;
            }
            foreach (string b in bundles)
            {
                if (all.Contains("\"Bundles/" + b + "\"", StringComparison.Ordinal) || all.Contains("\"" + b + "\"", StringComparison.Ordinal))
                {
                    Hit(Rule, f.Rel, 0, "names the bundle " + b + ": only PropLibrary reads the props bundles; ask it by prop id");
                }
            }
        }
    }

    // What ships: the build copies the whole Bundles folder, the installer and the zip take the whole mod folder, and the
    // files that list the mod's files name every bundle.
    private static void Listings(List<string> bundles)
    {
        Contains("RealLifeOverhaul.Build/RealLifeOverhaul.csproj", @"<BundleFiles Include=""$(ModSource)\Bundles\**\*""", "copies every file of Bundles (the build's package)");
        Contains("RealLifeOverhaul.Build/Installer/RealLifeOverhaul.iss", @"Source: ""{#SourceDir}\RealLifeOverhaul\*""; DestDir: ""{app}""; Flags: recursesubdirs", "installs the whole mod folder");
        Contains("RealLifeOverhaul.Build/Installer/build-installer.ps1", "Compress-Archive -Path $mod ", "zips the whole mod folder");
        string csproj = Read("RealLifeOverhaul.Build/RealLifeOverhaul.csproj");
        foreach (string b in bundles)
        {
            if (csproj != null && Regex.IsMatch(csproj, @"<BundleFiles[^>]*Exclude=""[^""]*" + Regex.Escape(b)))
            {
                Hit(Rule, "RealLifeOverhaul.Build/RealLifeOverhaul.csproj", 0, "leaves the bundle " + b + " out of the package");
            }
        }
        foreach (string rel in ListingFiles)
        {
            string text = Read(rel);
            Examine(Rule, rel);
            foreach (string b in bundles)
            {
                if (text == null || !Regex.IsMatch(text, @"(?<![A-Za-z0-9_])" + Regex.Escape(b) + @"(?![A-Za-z0-9_])"))
                {
                    Hit(Rule, rel, 0, "does not name the bundle " + b);
                }
            }
        }
    }

    // Every prop the code asks for by a whole name is in a bundle (a name ending in _ is the start of names built from
    // parts, and some prop must start so), and no code names a prop taken out of the bundles.
    private static void Asked(Dictionary<string, string> heldBy, Dictionary<string, string> removed)
    {
        var sources = new List<(string Rel, string Text, Regex Pattern)>();
        foreach (SourceFile f in Scripts)
        {
            sources.Add((f.Rel, string.Join("\n", f.Lines), AskedFor));
        }
        string clips = Path.Combine(_root, "CityAssets", "scripts");
        if (Directory.Exists(clips))
        {
            foreach (string file in Directory.GetFiles(clips, "clips*.py"))
            {
                sources.Add(("CityAssets/scripts/" + Path.GetFileName(file), File.ReadAllText(file), ClipTool));
            }
        }
        foreach ((string rel, string text, Regex pattern) in sources)
        {
            foreach (Match m in pattern.Matches(text))
            {
                string id = m.Groups[1].Value;
                string subject = "prop " + id;
                Examine(Rule, subject);
                if (id.EndsWith("_", StringComparison.Ordinal) ? StartsAny(heldBy, id) : heldBy.ContainsKey(id))
                {
                    continue;
                }
                Hit(Rule, subject, 0, rel + " asks for it, and no bundle holds it" + (removed.ContainsKey(id) ? ": it was taken out (sets.json, removed); model it anew in the open set" : " (PropAssets/contents)"));
            }
            foreach (string id in removed.Keys)
            {
                if (!heldBy.ContainsKey(id) && Regex.IsMatch(text, "\"" + Regex.Escape(id) + "\""))
                {
                    Hit(Rule, "prop " + id, 0, rel + " names it, and it was taken out of the bundles (sets.json, removed)");
                }
            }
        }
        foreach (KeyValuePair<string, string> gone in removed)
        {
            Examine(Rule, "prop " + gone.Key);
            if (heldBy.TryGetValue(gone.Key, out string bundle) && bundle == gone.Value)
            {
                Hit(Rule, "prop " + gone.Key, 0, "is listed as taken out of " + bundle + ", which still holds it");
            }
        }
    }

    private static bool StartsAny(Dictionary<string, string> heldBy, string prefix)
    {
        foreach (string id in heldBy.Keys)
        {
            if (id.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static void Contains(string rel, string text, string why)
    {
        Examine(Rule, rel);
        string all = Read(rel);
        if (all == null || !all.Contains(text, StringComparison.Ordinal))
        {
            Hit(Rule, rel, 0, "no longer " + why + ": every props bundle must ship");
        }
    }

    private static string Read(string rel)
    {
        string path = Path.Combine(_root, rel);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static bool PowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;
}
