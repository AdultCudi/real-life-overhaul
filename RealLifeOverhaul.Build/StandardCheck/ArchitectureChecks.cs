using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

// docs/standards/architecture.md: rules classes free of the game, no Harmony, no dependency beyond the game's own
// assemblies, and every key the mod writes into a save under reallife:.
internal static partial class Program
{
    private static readonly Regex RulesFile = new Regex(@"Rules(\.[A-Za-z]+)?\.cs$", RegexOptions.Compiled);
    private static readonly Regex GameToken = new Regex(@"\b(UnityEngine|BAModAPI|BigAmbitions|Entities|Localizor|TMPro|Cinemachine|HarmonyLib)\.", RegexOptions.Compiled);
    private static readonly Regex AnyUsing = new Regex(@"^using\s+(?:static\s+)?(?:\w+\s*=\s*)?([A-Za-z_][\w.]*)", RegexOptions.Compiled);
    private static readonly Regex HarmonyUse = new Regex(@"\bHarmonyLib\b|\[\s*HarmonyPatch\b|\bHarmonyInstance\b", RegexOptions.Compiled);
    private static readonly Regex DataCall = new Regex(@"\.Data\s*\(([^;]*?)\)\s*(?:\.|;|$)", RegexOptions.Compiled);
    private static readonly Regex Literal = new Regex("\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);
    private static readonly Regex ModDataLiteral = new Regex("\\bmodData\\s*(?:\\[|\\.(?:TryGetValue|Remove|ContainsKey|Add)\\s*\\()\\s*\"([^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex ModDataName = new Regex(@"\bmodData\s*(?:\[|\.(?:TryGetValue|Remove|ContainsKey|Add)\s*\()\s*([A-Za-z_][\w.]*)\s*[\],)]", RegexOptions.Compiled);
    private static readonly Regex StringConstant = new Regex("\\b(?:const|static\\s+readonly)\\s+string\\s+([A-Za-z_]\\w*)\\s*=\\s*\"([^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex KeyConstant = new Regex("\\b(?:const|static\\s+readonly)\\s+string\\s+(\\w*Key\\w*)\\s*=\\s*\"([^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex StoreFile = new Regex(@"(Store|State|Save|Saves)(\.[A-Za-z]+)?\.cs$", RegexOptions.Compiled);

    private static void ArchitectureChecks()
    {
        var constants = new Dictionary<string, List<(SourceFile File, string Value)>>(StringComparer.Ordinal);
        foreach (SourceFile f in Scripts)
        {
            for (int i = 0; i < f.Bare.Length; i++)
            {
                foreach (Match m in StringConstant.Matches(f.Bare[i]))
                {
                    if (!constants.TryGetValue(m.Groups[1].Value, out List<(SourceFile, string)> values))
                    {
                        values = new List<(SourceFile, string)>(1);
                        constants[m.Groups[1].Value] = values;
                    }
                    values.Add((f, m.Groups[2].Value));
                }
            }
        }
        foreach (SourceFile f in Scripts)
        {
            GameFree(f);
            NoHarmony(f);
            SaveKeys(f, constants);
        }
        Dependencies();
    }

    // RLO-ARC-002: a rules file names nothing but System and the mod's own namespaces.
    private static void GameFree(SourceFile f)
    {
        if (!RulesFile.IsMatch(f.Rel))
        {
            return;
        }
        Examine("RLO-ARC-002", f.Rel);
        for (int i = 0; i < f.Code.Length; i++)
        {
            string code = f.Code[i].Trim();
            Match u = AnyUsing.Match(code);
            if (u.Success && f.Context[i] != "body" && code.EndsWith(";", StringComparison.Ordinal))
            {
                string ns = u.Groups[1].Value;
                bool own = ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal) || ns == "RealLifeOverhaul" || ns.StartsWith("RealLifeOverhaul.", StringComparison.Ordinal);
                if (!own)
                {
                    Hit("RLO-ARC-002", f.Rel, i + 1, code);
                }
            }
            Match g = GameToken.Match(code);
            if (g.Success)
            {
                Hit("RLO-ARC-002", f.Rel, i + 1, code);
            }
        }
    }

    // RLO-ARC-011: no Harmony; the baseline holds the one place that reaches Going Public's own copy by name.
    private static void NoHarmony(SourceFile f)
    {
        Examine("RLO-ARC-011", f.Rel);
        for (int i = 0; i < f.Bare.Length; i++)
        {
            foreach (Match m in HarmonyUse.Matches(f.Bare[i]))
            {
                Hit("RLO-ARC-011", f.Rel, i + 1, f.Bare[i]);
            }
        }
    }

    // RLO-ARC-012: the mod's build references the game's own assemblies and nothing else.
    private static void Dependencies()
    {
        string rel = "RealLifeOverhaul.Build/RealLifeOverhaul.csproj";
        string path = Path.Combine(_root, rel);
        Examine("RLO-ARC-011", rel);
        Examine("RLO-ARC-012", rel);
        if (!File.Exists(path))
        {
            Hit("RLO-ARC-012", rel, 0, "the mod's project file is missing");
            return;
        }
        string[] lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.IndexOf("Harmony", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Hit("RLO-ARC-011", rel, i + 1, line);
            }
            if (Regex.IsMatch(line, @"<(PackageReference|ProjectReference|FrameworkReference)\b"))
            {
                Hit("RLO-ARC-012", rel, i + 1, line);
            }
            Match r = Regex.Match(line, "<Reference\\s+Include=\"([^\"]*)\"");
            if (r.Success && !r.Groups[1].Value.StartsWith("$(GameManaged)", StringComparison.Ordinal))
            {
                Hit("RLO-ARC-012", rel, i + 1, line);
            }
        }
    }

    // RLO-ARC-014: every key the mod writes into the save's modData is reallife:<feature>.
    private static void SaveKeys(SourceFile f, Dictionary<string, List<(SourceFile File, string Value)>> constants)
    {
        Examine("RLO-ARC-014", f.Rel);
        bool featureList = f.Rel.Contains("/Meta/FeatureList.", StringComparison.Ordinal);
        bool store = StoreFile.IsMatch(f.Rel);
        for (int i = 0; i < f.Bare.Length; i++)
        {
            string line = f.Bare[i];
            foreach (Match m in ModDataLiteral.Matches(line))
            {
                if (!SaveKey(m.Groups[1].Value))
                {
                    Hit("RLO-ARC-014", f.Rel, i + 1, "modData key \"" + m.Groups[1].Value + "\"");
                }
            }
            foreach (Match m in ModDataName.Matches(line))
            {
                string name = m.Groups[1].Value;
                string v = Resolve(f, name, constants);
                if (v != null && !SaveKey(v))
                {
                    Hit("RLO-ARC-014", f.Rel, i + 1, "modData key " + name + " = \"" + v + "\"");
                }
            }
            if (featureList)
            {
                foreach (Match d in DataCall.Matches(line))
                {
                    foreach (Match lit in Literal.Matches(d.Groups[1].Value))
                    {
                        if (!SaveKey(lit.Groups[1].Value))
                        {
                            Hit("RLO-ARC-014", f.Rel, i + 1, ".Data(\"" + lit.Groups[1].Value + "\")");
                        }
                    }
                }
            }
            if (store)
            {
                foreach (Match k in KeyConstant.Matches(line))
                {
                    string v = k.Groups[2].Value;
                    if (v.Length > 0 && !SaveKey(v) && !v.StartsWith("reallife_", StringComparison.Ordinal))
                    {
                        Hit("RLO-ARC-014", f.Rel, i + 1, k.Groups[1].Value + " = \"" + v + "\"");
                    }
                }
            }
        }
    }

    /// <summary>
    /// The value of the string constant a name stands for: Type.Name among the files that declare Type, a plain Name in
    /// this file, else the one constant of that name in the mod; null when it cannot be told.
    /// </summary>
    private static string Resolve(SourceFile f, string name, Dictionary<string, List<(SourceFile File, string Value)>> constants)
    {
        string[] parts = name.Split('.');
        if (!constants.TryGetValue(parts[parts.Length - 1], out List<(SourceFile File, string Value)> all))
        {
            return null;
        }
        string owner = parts.Length > 1 ? parts[parts.Length - 2] : null;
        string found = null;
        int count = 0;
        foreach ((SourceFile File, string Value) c in all)
        {
            bool fits = owner == null ? c.File == f : c.File.Types.Exists(t => t.Name == owner);
            if (fits)
            {
                found = c.Value;
                count++;
            }
        }
        if (count == 1)
        {
            return found;
        }
        return count == 0 && all.Count == 1 ? all[0].Value : null;
    }

    private static bool SaveKey(string key) => key.StartsWith("reallife:", StringComparison.Ordinal) && key.Length > "reallife:".Length;
}
