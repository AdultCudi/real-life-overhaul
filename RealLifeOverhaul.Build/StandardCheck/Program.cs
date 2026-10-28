using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

// Checks the rules of docs/standards that a program can check, by reading the repository's files as text. Each finding
// is counted against its rule and its subject (a file, a locale key, a commit); a subject may have as many findings as
// baseline.txt allows it, and no more, so that what the code held when the standard was set passes and anything new
// fails. A check that fails prints one line; the last line is ALL CHECKS PASSED or the count that failed.
//
//   dotnet StandardCheck.dll [repository root] [-write-baseline]
//
// With -write-baseline it writes baseline.txt anew from what it finds, and passes; see docs/standards/testing.md for
// when that may be done.
internal static partial class Program
{
    private const int MostListed = 12;   // findings listed under one failing subject

    private static int _failed, _run;
    private static string _root;
    private static readonly List<SourceFile> Scripts = new List<SourceFile>(1400);
    private static readonly Dictionary<string, int> Allowed = new Dictionary<string, int>(StringComparer.Ordinal);
    private static readonly Dictionary<string, List<string>> Found = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    private static readonly List<string> Examined = new List<string>(20000);
    private static readonly HashSet<string> ExaminedSet = new HashSet<string>(StringComparer.Ordinal);
    private static readonly List<string> Notes = new List<string>();

    private const string ScriptsDir = "Assets/Mods/RealLifeOverhaul/Scripts";
    private const string LocalesDir = "Assets/Mods/RealLifeOverhaul/Locales";

    private static string BaselinePath => Path.Combine(_root, "RealLifeOverhaul.Build", "StandardCheck", "baseline.txt");

    private static int Main(string[] args)
    {
        bool write = false;
        Console.OutputEncoding = new UTF8Encoding(false);
        _root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        foreach (string a in args)
        {
            if (a == "-write-baseline")
            {
                write = true;
            }
            else
            {
                _root = Path.GetFullPath(a);
            }
        }
        if (!Directory.Exists(Path.Combine(_root, ScriptsDir)))
        {
            Console.WriteLine("FAIL  no " + ScriptsDir + " under " + _root);
            Console.WriteLine("1 of 1 CHECKS FAILED");
            return 1;
        }
        Console.WriteLine("Repository: " + _root);
        ReadScripts();
        ReadBaseline();

        StyleChecks();
        ArchitectureChecks();
        PerformanceChecks();
        AssetChecks();
        PropSetChecks();
        MultiplayerChecks();
        TestingChecks();
        TextChecks();
        GuideChecks();
        CommentChecks();
        LoggerChecks();
        GitChecks();

        if (write)
        {
            WriteBaseline();
            Console.WriteLine("Baseline written: " + BaselinePath);
        }
        Compare(write);
        foreach (string note in Notes)
        {
            Console.WriteLine("NOTE  " + note);
        }
        Console.WriteLine(_failed == 0 ? "ALL CHECKS PASSED (" + _run + ")" : _failed + " of " + _run + " CHECKS FAILED");
        return _failed == 0 ? 0 : 1;
    }

    private static void Check(bool ok, string what)
    {
        _run++;
        if (!ok)
        {
            _failed++;
            Console.WriteLine("FAIL  " + what);
        }
    }

    // ---------------------------------------------------------------- findings and the baseline

    private static string Key(string rule, string subject) => rule + "\t" + subject;

    /// <summary>Says that a rule was applied to a subject, so that it counts as one check whether or not it found anything.</summary>
    private static void Examine(string rule, string subject)
    {
        string key = Key(rule, subject);
        if (ExaminedSet.Add(key))
        {
            Examined.Add(key);
        }
    }

    /// <summary>One break of a rule in a subject; where is a line number (1-based) or 0, what a short excerpt.</summary>
    private static void Hit(string rule, string subject, int where, string what)
    {
        Examine(rule, subject);
        string key = Key(rule, subject);
        if (!Found.TryGetValue(key, out List<string> list))
        {
            list = new List<string>(2);
            Found[key] = list;
        }
        string text = what.Trim();
        if (text.Length > 110)
        {
            text = text.Substring(0, 107) + "...";
        }
        list.Add(where > 0 ? "line " + where + ": " + text : text);
    }

    private static void ReadBaseline()
    {
        if (!File.Exists(BaselinePath))
        {
            Notes.Add("no baseline.txt: every finding fails");
            return;
        }
        foreach (string raw in File.ReadAllLines(BaselinePath, Encoding.UTF8))
        {
            string line = raw.TrimEnd();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }
            string[] parts = line.Split('\t');
            if (parts.Length == 3 && int.TryParse(parts[2], out int n) && n > 0)
            {
                Allowed[Key(parts[0], parts[1])] = n;
            }
            else
            {
                Check(false, "baseline.txt: a line is not rule, subject and count separated by tabs: " + line);
            }
        }
    }

    private static void Compare(bool written)
    {
        int lower = 0;
        foreach (string key in Examined)
        {
            int found = Found.TryGetValue(key, out List<string> list) ? list.Count : 0;
            int allowed = written ? found : (Allowed.TryGetValue(key, out int n) ? n : 0);
            string[] parts = key.Split('\t');
            if (found > allowed)
            {
                Check(false, parts[0] + " " + parts[1] + ": " + found + " found, the baseline allows " + allowed + " (docs/standards, " + parts[0] + ")");
                for (int i = 0; i < list.Count && i < MostListed; i++)
                {
                    Console.WriteLine("        " + list[i]);
                }
                if (list.Count > MostListed)
                {
                    Console.WriteLine("        and " + (list.Count - MostListed) + " more");
                }
            }
            else
            {
                Check(true, "");
                if (found < allowed)
                {
                    lower++;
                }
            }
        }
        int gone = 0;
        foreach (string key in Allowed.Keys)
        {
            if (!ExaminedSet.Contains(key))
            {
                gone++;
            }
        }
        if (lower + gone > 0 && !written)
        {
            Notes.Add(lower + " baseline entries allow more than is found and " + gone + " name a subject no longer there: the baseline can shrink (-write-baseline)");
        }
    }

    private static void WriteBaseline()
    {
        var keys = new List<string>(Found.Keys);
        keys.Sort(StringComparer.Ordinal);
        var sb = new StringBuilder();
        sb.Append("# StandardCheck baseline: the breaks of docs/standards that the code held when the standard was set (17 Oct 2026).\n");
        sb.Append("# One line per rule and subject: the rule, the file or locale key, how many findings it may have. A subject\n");
        sb.Append("# not listed may have none. The list only shrinks: fix a finding and lower its count; never raise one or add\n");
        sb.Append("# a line in the same change as new code (docs/standards/testing.md, RLO-TST-011).\n");
        foreach (string key in keys)
        {
            sb.Append(key).Append('\t').Append(Found[key].Count).Append('\n');
        }
        File.WriteAllText(BaselinePath, sb.ToString(), new UTF8Encoding(false));
    }

    // ---------------------------------------------------------------- files

    private static void ReadScripts()
    {
        string dir = Path.Combine(_root, ScriptsDir);
        var paths = new List<string>(Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories));
        paths.Sort(StringComparer.Ordinal);
        foreach (string path in paths)
        {
            Scripts.Add(SourceFile.Read(path, Rel(path)));
        }
        Console.WriteLine("  " + Scripts.Count + " source files under " + ScriptsDir);
    }

    private static string Rel(string path) => Path.GetRelativePath(_root, path).Replace('\\', '/');

    /// <summary>The file's name without its folder and without everything from its first dot.</summary>
    private static string BaseName(string rel)
    {
        string name = rel.Substring(rel.LastIndexOf('/') + 1);
        int dot = name.IndexOf('.');
        return dot < 0 ? name : name.Substring(0, dot);
    }

    private static string FileName(string rel) => rel.Substring(rel.LastIndexOf('/') + 1);

    private static string Folder(string rel) => rel.Substring(0, rel.LastIndexOf('/'));

    // ---------------------------------------------------------------- git

    /// <summary>What git prints for the arguments in the repository, or null if git is not there or fails.</summary>
    private static string Git(string arguments)
    {
        try
        {
            var start = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = _root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8
            };
            using Process p = Process.Start(start);
            if (p == null)
            {
                return null;
            }
            string output = p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            if (!p.WaitForExit(60000) || p.ExitCode != 0)
            {
                return null;
            }
            return output;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string _base;
    private static bool _baseRead;

    /// <summary>The commit this branch left main at (main, else origin/main, else HEAD); null without git.</summary>
    private static string MergeBase()
    {
        if (_baseRead)
        {
            return _base;
        }
        _baseRead = true;
        foreach (string main in new[] { "main", "origin/main", "upstream/main" })
        {
            string b = Git("merge-base HEAD " + main);
            if (!string.IsNullOrWhiteSpace(b))
            {
                _base = b.Trim();
                return _base;
            }
        }
        string head = Git("rev-parse HEAD");
        _base = string.IsNullOrWhiteSpace(head) ? null : head.Trim();
        return _base;
    }
}
