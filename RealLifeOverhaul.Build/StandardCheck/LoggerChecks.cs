using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// docs/standards/architecture.md, RLO-ARC-007: a fault is written to the mod's log. A class's own log is a static
// IModLogger field that the mod's start (Core/RealLifeOverhaulMod.cs) or its owner sets; one nothing sets writes nothing,
// silently, and one nothing reads is set for no one. Every such field in Scripts is set somewhere and read somewhere.
internal static partial class Program
{
    private static readonly Regex LoggerField = new Regex(@"\bstatic\s+IModLogger\s+([A-Za-z_]\w*)\s*;", RegexOptions.Compiled);

    private static void LoggerChecks()
    {
        var fields = new List<(SourceFile File, string Owner, string Name, int Line)>(256);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (SourceFile f in Scripts)
        {
            for (int i = 0; i < f.Code.Length; i++)
            {
                Match m = LoggerField.Match(f.Code[i]);
                if (m.Success)
                {
                    fields.Add((f, OwnerAt(f, i), m.Groups[1].Value, i + 1));
                    names.Add(m.Groups[1].Value);
                }
            }
        }

        // One pass over every file: each Owner.Name that is set, and each that is read.
        var set = new HashSet<string>(StringComparer.Ordinal);
        var read = new HashSet<string>(StringComparer.Ordinal);
        var qualified = new Regex(@"\b([A-Za-z_]\w*)\s*\.\s*(" + string.Join("|", names) + @")\b(\s*=(?!=|>))?", RegexOptions.Compiled);
        var bare = new Regex(@"(?<![\w.])(" + string.Join("|", names) + @")\b(\s*=(?!=|>))?", RegexOptions.Compiled);
        var byType = new Dictionary<string, List<SourceFile>>(StringComparer.Ordinal);
        foreach (SourceFile f in Scripts)
        {
            foreach (Match m in qualified.Matches(f.CodeText))
            {
                (m.Groups[3].Success ? set : read).Add(m.Groups[1].Value + "." + m.Groups[2].Value);
            }
            foreach (SourceType t in f.Types)
            {
                if (!byType.TryGetValue(t.Name, out List<SourceFile> list))
                {
                    list = new List<SourceFile>(2);
                    byType[t.Name] = list;
                }
                if (!list.Contains(f))
                {
                    list.Add(f);
                }
            }
        }

        // Inside the owner's own files (each part of a partial class) the field is named without its class.
        foreach ((SourceFile file, string owner, string name, int line) in fields)
        {
            string key = owner + "." + name;
            if (byType.TryGetValue(owner, out List<SourceFile> parts))
            {
                foreach (SourceFile part in parts)
                {
                    foreach (Match m in bare.Matches(part.CodeText))
                    {
                        if (m.Groups[1].Value != name || IsLoggerDeclaration(part.CodeText, m.Index))
                        {
                            continue;
                        }
                        (m.Groups[2].Success ? set : read).Add(key);
                    }
                }
            }
            Check(set.Contains(key), "logger " + key + " (" + file.Rel + " line " + line + ") is set somewhere in Scripts: nothing sets it, so its lines never reach the log");
            Check(read.Contains(key), "logger " + key + " (" + file.Rel + " line " + line + ") is read somewhere in Scripts: it is set and nothing writes through it");
        }
        Console.WriteLine("  " + fields.Count + " static loggers in Scripts, each set and read");
    }

    /// <summary>The innermost type whose body holds the line; "" outside every type.</summary>
    private static string OwnerAt(SourceFile f, int line)
    {
        int at = IndexOfLine(f, line);
        string owner = "";
        int narrowest = int.MaxValue;
        foreach (SourceType t in f.Types)
        {
            if (t.Line > line || !f.Block(IndexOfLine(f, t.Line), out int open, out int close))
            {
                continue;
            }
            if (open < at && at < close && close - open < narrowest)
            {
                narrowest = close - open;
                owner = t.Name;
            }
        }
        return owner;
    }

    private static int IndexOfLine(SourceFile f, int line)
    {
        int index = 0;
        for (int i = 0; i < line; i++)
        {
            index += f.Code[i].Length + 1;
        }
        return index;
    }

    private static bool IsLoggerDeclaration(string text, int index)
    {
        int from = Math.Max(0, index - 40);
        return text.Substring(from, index - from).TrimEnd().EndsWith("IModLogger", StringComparison.Ordinal);
    }
}
