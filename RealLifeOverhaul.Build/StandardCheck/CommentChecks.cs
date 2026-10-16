using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// docs/standards/comments.md: a summary on every public and internal type, summaries kept short, and no notes of work
// left to do.
internal static partial class Program
{
    private const int MostMemberSummary = 8;   // /// lines: <summary>, six lines of text, </summary>
    private const int MostTypeSummary = 40;    // /// lines: a longer account belongs in docs/features

    private static readonly Regex LeftToDo = new Regex(@"\b(?:TODO|FIXME|HACK|XXX)\b", RegexOptions.Compiled);

    private static void CommentChecks()
    {
        var types = new Dictionary<string, List<(SourceFile File, SourceType Type)>>(StringComparer.Ordinal);
        foreach (SourceFile f in Scripts)
        {
            Examine("RLO-DOC-004", f.Rel);
            Examine("RLO-DOC-005", f.Rel);
            Examine("RLO-DOC-009", f.Rel);
            var typeLines = new HashSet<int>();
            foreach (SourceType t in f.Types)
            {
                typeLines.Add(t.Line);
                if (t.TopLevel && t.Public)
                {
                    string key = t.Namespace + "." + t.Name;
                    if (!types.TryGetValue(key, out List<(SourceFile, SourceType)> parts))
                    {
                        parts = new List<(SourceFile, SourceType)>(1);
                        types[key] = parts;
                    }
                    parts.Add((f, t));
                }
            }
            SummaryLengths(f, typeLines);
            foreach (SourceComment c in f.Comments)
            {
                if (LeftToDo.IsMatch(c.Text))
                {
                    Hit("RLO-DOC-009", f.Rel, c.Line + 1, c.Text);
                }
            }
        }
        // RLO-DOC-002: a public or internal type has a summary on at least one of its parts.
        foreach (KeyValuePair<string, List<(SourceFile File, SourceType Type)>> kv in types)
        {
            (SourceFile file, SourceType type) = kv.Value[0];
            Examine("RLO-DOC-002", file.Rel);
            bool any = false;
            foreach ((SourceFile File, SourceType Type) part in kv.Value)
            {
                any |= part.Type.SummaryLines > 0;
            }
            if (!any)
            {
                Hit("RLO-DOC-002", file.Rel, type.Line + 1, type.Kind + " " + type.Name + " has no summary");
            }
        }
    }

    // RLO-DOC-004 and RLO-DOC-005: how long a run of /// lines may be, above a member and above a type.
    private static void SummaryLengths(SourceFile f, HashSet<int> typeLines)
    {
        int i = 0;
        while (i < f.Lines.Length)
        {
            if (!f.Lines[i].TrimStart().StartsWith("///", StringComparison.Ordinal))
            {
                i++;
                continue;
            }
            int start = i;
            while (i < f.Lines.Length && f.Lines[i].TrimStart().StartsWith("///", StringComparison.Ordinal))
            {
                i++;
            }
            int n = i - start;
            int next = i;
            while (next < f.Lines.Length && f.Lines[next].TrimStart().StartsWith("[", StringComparison.Ordinal))
            {
                next++;
            }
            bool onType = typeLines.Contains(next);
            if (onType && n > MostTypeSummary)
            {
                Hit("RLO-DOC-005", f.Rel, start + 1, n + " lines above a type");
            }
            else if (!onType && n > MostMemberSummary)
            {
                Hit("RLO-DOC-004", f.Rel, start + 1, n + " lines above a member");
            }
        }
    }
}
