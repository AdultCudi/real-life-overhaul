using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

// docs/standards/testing.md: every rules file and every save store of the mod is compiled into at least one of the
// offline checkers, so that what ships is what was checked.
internal static partial class Program
{
    private static readonly Regex CheckedFile = new Regex(@"(Rules|Store)(\.[A-Za-z]+)?\.cs$", RegexOptions.Compiled);
    private static readonly Regex CompileItem = new Regex("<Compile\\s+Include=\"([^\"]+)\"", RegexOptions.Compiled);

    private static void TestingChecks()
    {
        var patterns = new List<Regex>();
        string build = Path.Combine(_root, "RealLifeOverhaul.Build");
        foreach (string project in Directory.GetFiles(build, "*.csproj", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(project) == "RealLifeOverhaul.csproj")
            {
                continue;   // the mod itself compiles everything
            }
            foreach (Match m in CompileItem.Matches(File.ReadAllText(project)))
            {
                string include = m.Groups[1].Value.Replace('\\', '/');
                int at = include.IndexOf("RealLifeOverhaul/Scripts/", StringComparison.Ordinal);
                if (at >= 0)
                {
                    patterns.Add(Glob(include.Substring(at + "RealLifeOverhaul/Scripts/".Length)));
                }
            }
        }
        foreach (SourceFile f in Scripts)
        {
            if (!CheckedFile.IsMatch(f.Rel))
            {
                continue;
            }
            Examine("RLO-TST-001", f.Rel);
            string inScripts = f.Rel.Substring(ScriptsDir.Length + 1);
            bool compiled = false;
            foreach (Regex p in patterns)
            {
                if (p.IsMatch(inScripts))
                {
                    compiled = true;
                    break;
                }
            }
            if (!compiled)
            {
                Hit("RLO-TST-001", f.Rel, 0, "compiled into no checker under RealLifeOverhaul.Build");
            }
        }
    }

    /// <summary>An MSBuild item pattern (with * and **) as a regular expression over a path with forward slashes.</summary>
    private static Regex Glob(string pattern)
    {
        var sb = new StringBuilder("^");
        for (int i = 0; i < pattern.Length; i++)
        {
            char c = pattern[i];
            if (c == '*' && i + 1 < pattern.Length && pattern[i + 1] == '*')
            {
                sb.Append("(?:.*/)?");
                i += pattern.Length > i + 2 && pattern[i + 2] == '/' ? 2 : 1;
            }
            else if (c == '*')
            {
                sb.Append("[^/]*");
            }
            else if (c == '?')
            {
                sb.Append("[^/]");
            }
            else
            {
                sb.Append(Regex.Escape(c.ToString()));
            }
        }
        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.IgnoreCase);
    }
}
