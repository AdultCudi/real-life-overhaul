using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

// docs/standards/platforms.md: the game is sold for Windows and macOS and the mod is built on Windows alone, so what
// only Windows has must never be reached on another system, and what the mod ships must be the same file everywhere.
// RLO-PLT-001: a P/Invoke, or a program started, only where HostSystem.Windows was asked. RLO-PLT-002: no path written
// with Windows' own separator or folders. RLO-PLT-003: every asset bundle the mod opens is one of its list, named as its
// file is, and has its shaders checked as it is read. RLO-PLT-004: a shipped bundle holds no shader the game has not
// got, read from the bundle's file itself. RLO-PLT-005: the texts that say where the log is say it for a Mac too.
internal static partial class Program
{
    private static readonly Regex ExternName = new Regex(@"\bstatic\s+extern\s+[\w\.\[\]<>]+\s+(\w+)\s*\(", RegexOptions.Compiled);
    private static readonly Regex StartsProgram = new Regex(@"\bProcess\.Start\s*\(", RegexOptions.Compiled);
    private static readonly Regex AsksSystem = new Regex(@"\bHostSystem\.Windows\b|\bRuntimePlatform\.Windows", RegexOptions.Compiled);
    private static readonly Regex OpensBundle = new Regex(@"\bAssetBundle\.LoadFrom(?:File|Memory|Stream)(?:Async)?\s*\(", RegexOptions.Compiled);
    // In a literal as it is written: a backslash (escaped, so two) before a part of a path or after one, or a drive. In a
    // verbatim one the backslash stands once. A part is two letters at least, so "\\n" and "\\u" written for JSON are none.
    private static readonly Regex WindowsPath = new Regex(@"[A-Za-z0-9_ ]\\\\[A-Za-z]{2}|^\\\\[A-Za-z]{2}|[A-Za-z0-9_]\\\\$|^[A-Za-z]:\\\\|^[A-Za-z]:/", RegexOptions.Compiled);
    private static readonly Regex WindowsPathVerbatim = new Regex(@"[A-Za-z0-9_ ]\\[A-Za-z]{2}|^\\[A-Za-z]{2}|[A-Za-z0-9_]\\$|^[A-Za-z]:[\\/]", RegexOptions.Compiled);
    private static readonly Regex NumberedLetter = new Regex(@"\\{1,2}[ux][0-9A-Fa-f]{2,4}", RegexOptions.Compiled);   // a letter by its number: no part of a path
    private static readonly Regex NoFileName = new Regex(@"[<>|?*]", RegexOptions.Compiled);                           // what no file's name may hold: a pattern's signs
    // Two parts joined with a backslash written by itself: folder + "\\" + name, string.Join("\\", parts).
    private static readonly Regex JoinedBySeparator = new Regex(@"(?:\+\s*|\b(?:Join|Concat)\(\s*)(?:""\\\\""|@""\\""|'\\\\')|(?:""\\\\""|@""\\""|'\\\\')\s*\+", RegexOptions.Compiled);
    private static readonly Regex WindowsFolder = new Regex(@"\bAppData\b|\bLocalLow\b|%[A-Z_]{3,}%|\bProgram Files\b", RegexOptions.Compiled);
    private static readonly Regex BundleList = new Regex(@"\bBundles\s*=\s*\{([^}]*)\}", RegexOptions.Compiled);
    private static readonly Regex FallbackList = new Regex(@"\bFallbacks\s*=\s*\{([^}]*)\}", RegexOptions.Compiled);
    private static readonly Regex Quoted = new Regex(@"""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex TwoParts = new Regex(@"""([^""]+)""\s*\+\s*""([^""]+)""", RegexOptions.Compiled);

    // A literal that names a Windows folder or separator on purpose: the file under Scripts, the literal, and why.
    private static readonly (string File, string Text, string Why)[] WindowsWords =
    {
        ("Meta/BugReportRules.cs", "%USERPROFILE%", "what a bug report writes in place of the player's user folder, on every system: a mark, not a path"),
        ("Texts/TextPacks.cs", "LocalLow", "the folder each system keeps games' data in, by name, to shorten a path for a notice"),
    };

    private static void PlatformChecks()
    {
        PlatformCalls();
        PlatformPaths();
        PlatformBundles();
        PlatformTexts();
    }

    // RLO-PLT-001: what only Windows has is reached only where the system was asked, in the same member.
    private static void PlatformCalls()
    {
        foreach (SourceFile f in Scripts)
        {
            Examine("RLO-PLT-001", f.Rel);
            var names = new List<string>(4);
            foreach (Match m in ExternName.Matches(f.CodeText))
            {
                names.Add(m.Groups[1].Value);
            }
            for (int i = 0; i < f.Code.Length; i++)
            {
                string line = f.Code[i];
                string called = null;
                if (StartsProgram.IsMatch(line))
                {
                    called = "a program is started";
                }
                for (int n = 0; called == null && n < names.Count; n++)
                {
                    if (Regex.IsMatch(line, @"\b" + names[n] + @"\s*\(") && !Regex.IsMatch(line, @"\bextern\b"))
                    {
                        called = "'" + names[n] + "', a call of Windows' own, is made";
                    }
                }
                if (called != null && !AsksSystem.IsMatch(MemberAround(f, i)))
                {
                    Hit("RLO-PLT-001", f.Rel, i + 1, called + " where HostSystem.Windows is not asked: " + line.Trim());
                }
            }
        }
    }

    // The member a line stands in, as code: from its first line (a class's members begin eight spaces in) to its closing
    // brace; a member of one line is that line.
    private static string MemberAround(SourceFile f, int line)
    {
        int head = line;
        while (head > 0 && !MemberHead(f.Code[head]))
        {
            head--;
        }
        int end = head;
        if (!f.Code[head].Contains("=>") || !f.Code[head].TrimEnd().EndsWith(";", StringComparison.Ordinal))
        {
            end = head + 1;
            while (end < f.Code.Length - 1 && f.Code[end] != "        }")
            {
                end++;
            }
        }
        end = Math.Max(end, line);
        return string.Join("\n", f.Code, head, end - head + 1);
    }

    private static bool MemberHead(string code)
    {
        return code.Length > 8 && code.StartsWith("        ", StringComparison.Ordinal) && code[8] != ' ' && code[8] != '{' && code[8] != '}' && code[8] != '[';
    }

    // RLO-PLT-002: no path written with Windows' separator, a drive or one of Windows' own folders.
    private static void PlatformPaths()
    {
        // The rule's own patterns first, held to lines they must find and lines they must leave.
        Check(PathOn(@"string path = folder + ""\\"" + name;") && PathOn(@"string.Join(""\\"", parts)") && PathOn(@"folder + '\\' + name") && PathOn(@"x = @""\"" + y;")
              && PathOn(@"Say(""ModsLocal\\RealLifeOverhaul"");") && PathOn(@"Open(@""C:\Users\Public"");") && PathOn(@"Open(""D:/Games"");") && PathOn(@"x = ""AppData/LocalLow"";")
              && PathOn(@"x = folder + ""\\Reports"";") && PathOn(@"x = ""%APPDATA%"";"),
            "RLO-PLT-002: the rule finds parts joined with a backslash, a path written with one, a drive, and Windows' own folders");
        Check(!PathOn(@"faults.Add(""\"""" + phrase);") && !PathOn(@"back.Replace(""\\"", ""\\\\"");") && !PathOn(@"t.Append(""\\n"").Append(""\\u"");")
              && !PathOn(@"Path.Combine(folder, ""Reports"", name);") && !PathOn(@"const string Never = @""\x00-\x1F\uFF01"";") && !PathOn(@"x = @""\b(Get|Set)\s*\("";")
              && !PathOn(@"x = ""Library/Application Support"";") && !PathOn(@"c == '""' || c == '\\'"),
            "RLO-PLT-002: and leaves a quote, an escape for JSON, a letter by its number, a pattern's signs and a Mac's folders");
        var used = new bool[WindowsWords.Length];
        foreach (SourceFile f in Scripts)
        {
            Examine("RLO-PLT-002", f.Rel);
            for (int i = 0; i < f.Bare.Length; i++)
            {
                if (JoinedBySeparator.IsMatch(f.Bare[i]))
                {
                    Hit("RLO-PLT-002", f.Rel, i + 1, "two parts joined with a backslash written by itself (join paths with Path.Combine): " + f.Bare[i].Trim());
                }
                if (f.Bare[i].IndexOf('"') < 0 || f.Bare[i].Contains("Regex"))
                {
                    continue;   // no text on the line, or a pattern, whose backslashes are its own
                }
                foreach ((string text, bool verbatim) in LiteralsOn(f.Bare[i]))
                {
                    string what = PathIn(text, verbatim);
                    if (what == null)
                    {
                        continue;
                    }
                    int excused = Array.FindIndex(WindowsWords, w => f.Rel.EndsWith("/" + w.File, StringComparison.Ordinal) && text.Contains(w.Text));
                    if (excused >= 0)
                    {
                        used[excused] = true;
                        continue;
                    }
                    Hit("RLO-PLT-002", f.Rel, i + 1, what + " (join paths with Path.Combine from a folder the engine gives): \"" + text + "\"");
                }
            }
        }
        for (int k = 0; k < WindowsWords.Length; k++)
        {
            Check(used[k], "RLO-PLT-002 allowed words: " + WindowsWords[k].File + " no longer writes \"" + WindowsWords[k].Text + "\" (" + WindowsWords[k].Why + "): take the entry out");
        }
    }

    // What of Windows' a literal holds, as it is written between its quotes: a path, one of Windows' folders, or null.
    private static string PathIn(string text, bool verbatim)
    {
        string parts = NumberedLetter.Replace(text, string.Empty);
        if (!NoFileName.IsMatch(parts) && (verbatim ? WindowsPathVerbatim : WindowsPath).IsMatch(parts))
        {
            return "a path written with Windows' separator or a drive";
        }
        return WindowsFolder.IsMatch(text) ? "one of Windows' own folders" : null;
    }

    // Whether the rule finds anything on one line of code.
    private static bool PathOn(string line)
    {
        if (JoinedBySeparator.IsMatch(line))
        {
            return true;
        }
        foreach ((string text, bool verbatim) in LiteralsOn(line))
        {
            if (PathIn(text, verbatim) != null)
            {
                return true;
            }
        }
        return false;
    }

    // The string literals written on one line of code, each as it is written between its quotes, and whether it is a
    // verbatim one (@"..."), in which a backslash stands for itself.
    private static List<(string Text, bool Verbatim)> LiteralsOn(string line)
    {
        var found = new List<(string, bool)>(2);
        int i = 0;
        while (i < line.Length)
        {
            if (line[i] == '\'')
            {
                i += i + 1 < line.Length && line[i + 1] == '\\' ? 4 : 3;   // a character: passed over, so '"' opens nothing
                continue;
            }
            if (line[i] != '"')
            {
                i++;
                continue;
            }
            bool verbatim = (i > 0 && line[i - 1] == '@') || (i > 1 && line[i - 1] == '$' && line[i - 2] == '@');
            int from = ++i;
            while (i < line.Length)
            {
                if (verbatim && line[i] == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    i += 2;   // a quote written twice inside a verbatim literal
                    continue;
                }
                if (line[i] == '"')
                {
                    break;
                }
                i += !verbatim && line[i] == '\\' ? 2 : 1;
            }
            found.Add((line.Substring(from, Math.Min(i, line.Length) - from), verbatim));
            i++;
        }
        return found;
    }

    // RLO-PLT-003 and RLO-PLT-004: the bundles' list, their files, their loaders and what the files hold.
    private static void PlatformBundles()
    {
        string rules = ScriptText("Core/BundleShaderRules.cs");
        var listed = new List<string>(6);
        foreach (Match m in TwoParts.Matches(BundleList.Match(rules).Groups[1].Value))
        {
            listed.Add(m.Groups[1].Value + m.Groups[2].Value);   // a name is written in two parts there, so the locale check does not take it for a key
        }
        var fallbacks = new List<string>(2);
        foreach (Match m in Quoted.Matches(FallbackList.Match(rules).Groups[1].Value))
        {
            fallbacks.Add(m.Groups[1].Value);
        }
        Check(listed.Count >= 6 && fallbacks.Count % 2 == 0, "RLO-PLT-003: Core/BundleShaderRules.cs lists the mod's bundles and its pairs of shaders (" + listed.Count + " bundles read)");

        string folder = Path.Combine(_root, BundlesDir);
        var files = new List<string>(6);
        foreach (string path in Directory.Exists(folder) ? Directory.GetFiles(folder) : new string[0])
        {
            string name = Path.GetFileName(path);
            if (!name.EndsWith(".meta", StringComparison.Ordinal) && !name.EndsWith(".manifest", StringComparison.Ordinal))
            {
                files.Add(name);
            }
        }
        files.Sort(StringComparer.Ordinal);
        var sorted = new List<string>(listed);
        sorted.Sort(StringComparer.Ordinal);
        // By the letters' own case: a Mac's disk may tell reallife_ui from Reallife_UI, and Windows never does.
        Check(string.Join(",", files) == string.Join(",", sorted),
            "RLO-PLT-003: the bundles shipped (" + string.Join(", ", files) + ") are the bundles listed in BundleShaderRules.Bundles (" + string.Join(", ", sorted) + "), letter for letter");

        var all = new System.Text.StringBuilder(1 << 20);
        foreach (SourceFile f in Scripts)
        {
            Examine("RLO-PLT-003", f.Rel);
            all.Append(f.Bare.Length > 0 ? string.Join("\n", f.Bare) : string.Empty).Append('\n');
            if (!OpensBundle.IsMatch(f.CodeText))
            {
                continue;
            }
            // Its own file or one beside it of the same class has the bundle checked: the props' and the clips' through
            // BundleShaders.Check, the fonts' by their own mend and line.
            string group = GroupText(f.Rel);
            if (!group.Contains("BundleShaders.Check(") && !(group.Contains("ShadersLine(") && group.Contains("Mend();")))
            {
                Hit("RLO-PLT-003", f.Rel, f.LineAt(OpensBundle.Match(f.CodeText).Index) + 1, "a bundle is opened here and its shaders are not checked (BundleShaders.Check as it is read)");
            }
        }
        string code = all.ToString();
        foreach (string bundle in listed)
        {
            Check(code.Contains("\"Bundles/" + bundle + "\""), "RLO-PLT-003: the bundle " + bundle + " is read by a loader under that very name (\"Bundles/" + bundle + "\")");
        }
        // Each language's and each sound's file is named in small letters, as the game and the mod ask for them.
        foreach (string dir in new[] { LocalesDir, "Assets/Mods/RealLifeOverhaul/Sounds" })
        {
            foreach (string path in Directory.Exists(Path.Combine(_root, dir)) ? Directory.GetFiles(Path.Combine(_root, dir)) : new string[0])
            {
                string name = Path.GetFileName(path);
                bool data = name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase);
                Check(!data || name == name.ToLowerInvariant(), "RLO-PLT-003: " + dir + "/" + name + " is named in small letters, so a Mac's disk that tells letters' case apart finds it");
            }
        }
        // And each recorded sound is asked for under its file's very name (an off-throttle layer under its loop's).
        string soundRules = ScriptText("Driving/Sound/CarSoundRules.cs");
        string sounds = Path.Combine(_root, "Assets/Mods/RealLifeOverhaul/Sounds");
        foreach (string path in Directory.Exists(sounds) ? Directory.GetFiles(sounds, "*.ogg") : new string[0])
        {
            string stem = Path.GetFileNameWithoutExtension(path);
            string loop = stem.EndsWith("_off", StringComparison.Ordinal) ? stem.Substring(0, stem.Length - 4) : stem;
            Check(soundRules.Contains("\"" + stem + "\"") || soundRules.Contains("\"" + loop + "\""),
                "RLO-PLT-003: the sound " + stem + ".ogg is asked for under that very name, letter for letter, in Driving/Sound/CarSoundRules.cs");
        }

        var game = new HashSet<string>(StringComparer.Ordinal);
        string gameList = Path.Combine(_root, "RealLifeOverhaul.Build", "StandardCheck", "game-shaders.txt");
        foreach (string raw in File.Exists(gameList) ? File.ReadAllLines(gameList) : new string[0])
        {
            if (raw.Length > 0 && raw[0] != '#')
            {
                game.Add(raw.Split('\t')[0]);
            }
        }
        Check(game.Count > 100 && game.Contains("HDRP/Lit") && game.Contains("TextMeshPro/Distance Field"),
            "RLO-PLT-004: game-shaders.txt lists the game's own shaders (" + game.Count + " read; written by Census/game_shaders.py)");
        for (int i = 0; i + 1 < fallbacks.Count; i += 2)
        {
            Check(game.Contains(fallbacks[i + 1]), "RLO-PLT-004: the shader written down to stand in for " + fallbacks[i] + ", " + fallbacks[i + 1] + ", is one the game has");
        }
        foreach (string name in files)
        {
            BundleContents held = BundleFile.Read(Path.Combine(folder, name));
            Check(held.Problem == null, "RLO-PLT-004: the bundle " + name + " is read from its file" + (held.Problem != null ? " (" + held.Problem + ")" : string.Empty));
            if (held.Problem != null)
            {
                continue;
            }
            var own = new Dictionary<long, string>();
            foreach (BundleShader shader in held.Shaders)
            {
                own[shader.PathId] = shader.Name;
                int stands = fallbacks.IndexOf(shader.Name);
                bool known = game.Contains(shader.Name ?? string.Empty) || (stands >= 0 && stands % 2 == 0 && game.Contains(fallbacks[stands + 1]));
                Check(known, "RLO-PLT-004: the bundle " + name + " brings the shader '" + shader.Name + "', which is one the game has (so the game's own of that name is drawn with on every system)"
                             + (known ? string.Empty : ": it is not in game-shaders.txt and has no stand-in written down in BundleShaderRules"));
            }
            foreach (BundleMaterial material in held.Materials)
            {
                bool inside = material.ShaderFile == 0 && own.ContainsKey(material.ShaderPath);
                Check(inside, "RLO-PLT-004: the material '" + material.Name + "' of " + name + " names a shader of the bundle's own, which the check at load replaces by the game's"
                              + (inside ? string.Empty : ": it points outside the bundle, at nothing the game can be asked for by name"));
            }
            Console.WriteLine("   " + name + ": built for target " + held.Target + " with Unity " + held.Engine + "; " + held.Materials.Count + " material(s), " + held.Shaders.Count + " shader(s)"
                              + (held.Shaders.Count > 0 ? " (" + string.Join(", ", held.Shaders.ConvertAll(s => s.Name + " for API " + string.Join("+", s.Platforms))) + ")" : string.Empty));
        }
    }

    // RLO-PLT-005: the words that say where the log and the mod's folders are say it for a Mac too, and the setup says it is for Windows.
    private static void PlatformTexts()
    {
        string en = File.Exists(Path.Combine(_root, LocalesDir, "en.json")) ? File.ReadAllText(Path.Combine(_root, LocalesDir, "en.json")) : string.Empty;
        Match trouble = Regex.Match(en, @"""reallife_guide_trouble"":\s*""((?:[^""\\]|\\.)*)""");
        Check(trouble.Success && trouble.Groups[1].Value.Contains("Player.log in {data}") && trouble.Groups[1].Value.Contains("A Mac keeps the log apart")
              && trouble.Groups[1].Value.Contains("Library, Logs") && !trouble.Groups[1].Value.Contains("ModsLocal\\\\RealLifeOverhaul"),
            "RLO-PLT-005: the Guide's \"Where the files are\" says where a Mac keeps Player.log, and names no folder with Windows' separator");
        string guide = ScriptText("Meta/Guide.cs");
        Check(guide.Contains("case \"data\": return LogPlaceRules.Folder(Application.consoleLogPath, Application.persistentDataPath);"),
            "RLO-PLT-005: the Guide's {data} is the folder the engine says the log is in, not the data folder taken for it");
        foreach (string file in new[] { "Performance/PerfProbe.cs", "Performance/PerfBench.cs", "Diagnostics/CharacterLab.cs" })
        {
            Check(ScriptText(file).Contains("LogPlaceRules.Folder(Application.consoleLogPath, Application.persistentDataPath)"),
                "RLO-PLT-005: " + file + " writes its report next to Player.log, as its notice says, wherever the system keeps the log");
        }
        string install = File.Exists(Path.Combine(_root, "INSTALL.md")) ? File.ReadAllText(Path.Combine(_root, "INSTALL.md")) : string.Empty;
        Check(install.Contains("## On a Mac") && install.Contains("Library/Application Support") && install.Contains("Library/Logs") && install.Contains("The setup program is for Windows"),
            "RLO-PLT-005: INSTALL.md says the setup is for Windows and gives a Mac's folders for the mod and the log");
    }

    private static string ScriptText(string underScripts)
    {
        string rel = ScriptsDir + "/" + underScripts;
        foreach (SourceFile f in Scripts)
        {
            if (f.Rel == rel)
            {
                return string.Join("\n", f.Bare);
            }
        }
        return string.Empty;
    }

    // The code of a file's class with its other parts (UiFonts.cs alone; TextServer.cs with TextServer.Start.cs).
    private static string GroupText(string rel)
    {
        string key = GroupKey(rel);
        var text = new System.Text.StringBuilder(1 << 14);
        foreach (SourceFile f in Scripts)
        {
            if (GroupKey(f.Rel) == key)
            {
                text.Append(f.CodeText).Append('\n');
            }
        }
        return text.ToString();
    }
}
