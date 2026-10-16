using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// docs/standards/code-style.md: naming, files, namespaces, layout and the language features the mod's code keeps to.
// Every rule here reads the mod's sources (Scripts) only; the checkers' own code is held to the standard by review.
internal static partial class Program
{
    private static readonly Regex Pascal = new Regex(@"^[A-Z][A-Za-z0-9]*$", RegexOptions.Compiled);
    private static readonly Regex UnderCamel = new Regex(@"^_[a-z][A-Za-z0-9]*$", RegexOptions.Compiled);
    private static readonly Regex InterfaceName = new Regex(@"^I[A-Z][A-Za-z0-9]*$", RegexOptions.Compiled);

    /// <summary>A member declared on one line at a type's level: its modifiers, its type, its name and what follows.</summary>
    private static readonly Regex Member = new Regex(
        @"^(?<mods>(?:(?:public|private|protected|internal|static|readonly|const|volatile|new|override|virtual|abstract|sealed|extern|unsafe|async|partial|event|fixed)\s+)*)"
        + @"(?<type>(?:\((?:[^()]|\([^()]*\))*\)|[A-Za-z_][\w.]*(?:<(?:[^<>]|<(?:[^<>]|<[^<>]*>)*>)*>)?)(?:\[[\s,]*\])*\??(?:\[[\s,]*\])*)\s+"
        + @"(?<name>[A-Za-z_]\w*)\s*(?<after>=>|[=;,({<]|$)",
        RegexOptions.Compiled);

    private static readonly Regex LeadingAttributes = new Regex(@"^(?:\[[^\[\]]*(?:\[[^\[\]]*\][^\[\]]*)*\]\s*)+", RegexOptions.Compiled);
    private static readonly Regex EnumMember = new Regex(@"^(?<name>[A-Za-z_]\w*)\s*(?:=|,|$)", RegexOptions.Compiled);
    private static readonly Regex UsingLine = new Regex(@"^using\s+([A-Za-z_][\w.]*)\s*;", RegexOptions.Compiled);
    private static readonly Regex BraceAtEnd = new Regex(
        @"^(?:\}\s*)?(?:(?:if|else|for|foreach|while|switch|try|catch|finally|do|lock|using|namespace)\b.*|.*\b(?:class|struct|interface|enum)\s+\w.*)\{$",
        RegexOptions.Compiled);
    private static readonly Regex ControlStart = new Regex(@"^(?:else\s+)?(?:if|for|foreach|while)\s*\(", RegexOptions.Compiled);
    private static readonly Regex Linq = new Regex(@"\busing\s+System\.Linq\s*;|\bSystem\.Linq\.", RegexOptions.Compiled);

    private static readonly HashSet<string> NotTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "return", "throw", "using", "get", "set", "init", "add", "remove", "case", "goto", "else", "await", "yield", "var",
        "implicit", "explicit", "delegate", "operator", "namespace", "class", "struct", "interface", "enum", "record", "where"
    };

    private static void StyleChecks()
    {
        var namespacesByFolder = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        foreach (SourceFile f in Scripts)
        {
            Names(f);
            FileAndType(f);
            Usings(f);
            Layout(f);
            string folder = Folder(f.Rel);
            if (!namespacesByFolder.TryGetValue(folder, out Dictionary<string, int> counts))
            {
                counts = new Dictionary<string, int>(StringComparer.Ordinal);
                namespacesByFolder[folder] = counts;
            }
            string ns = f.Namespaces.Count > 0 ? f.Namespaces[0] : "";
            counts[ns] = counts.TryGetValue(ns, out int n) ? n + 1 : 1;
        }
        // RLO-STY-009: a file declares the namespace most files of its folder declare.
        foreach (SourceFile f in Scripts)
        {
            Examine("RLO-STY-009", f.Rel);
            Dictionary<string, int> counts = namespacesByFolder[Folder(f.Rel)];
            string most = null;
            foreach (KeyValuePair<string, int> kv in counts)
            {
                if (most == null || kv.Value > counts[most] || (kv.Value == counts[most] && string.CompareOrdinal(kv.Key, most) < 0))
                {
                    most = kv.Key;
                }
            }
            string ns = f.Namespaces.Count > 0 ? f.Namespaces[0] : "";
            if (ns != most)
            {
                Hit("RLO-STY-009", f.Rel, 0, "namespace " + (ns.Length > 0 ? ns : "(none)") + ", the folder's is " + most);
            }
        }
    }

    // RLO-STY-001 to RLO-STY-004: the case of every name.
    private static void Names(SourceFile f)
    {
        foreach (string rule in new[] { "RLO-STY-001", "RLO-STY-002", "RLO-STY-003", "RLO-STY-004" })
        {
            Examine(rule, f.Rel);
        }
        foreach (SourceType t in f.Types)
        {
            if (!Pascal.IsMatch(t.Name))
            {
                Hit("RLO-STY-001", f.Rel, t.Line + 1, t.Kind + " " + t.Name);
            }
            if (t.Kind == "interface" && !InterfaceName.IsMatch(t.Name))
            {
                Hit("RLO-STY-002", f.Rel, t.Line + 1, "interface " + t.Name);
            }
        }
        for (int i = 0; i < f.Code.Length; i++)
        {
            string context = f.Context[i];
            string line = f.Code[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }
            if (context == "enum")
            {
                Match e = EnumMember.Match(line);
                if (e.Success && !Pascal.IsMatch(e.Groups["name"].Value))
                {
                    Hit("RLO-STY-001", f.Rel, i + 1, "enum member " + e.Groups["name"].Value);
                }
                continue;
            }
            if (context != "type" || f.InParens[i])
            {
                continue;
            }
            line = LeadingAttributes.Replace(line, "");
            Match m = Member.Match(line);
            if (!m.Success)
            {
                continue;
            }
            string mods = " " + m.Groups["mods"].Value;
            string type = m.Groups["type"].Value;
            string name = m.Groups["name"].Value;
            string after = m.Groups["after"].Value;
            if (NotTypes.Contains(type) || name == "operator" || mods.Contains(" event "))
            {
                continue;
            }
            if (after == "" && NextCode(f, i).StartsWith("{", StringComparison.Ordinal))
            {
                after = "{";
            }
            if (after == "(" || after == "<" || after == "{" || after == "=>")
            {
                if (!Pascal.IsMatch(name))
                {
                    Hit("RLO-STY-001", f.Rel, i + 1, (after == "(" || after == "<" ? "method " : "property ") + name);
                }
                continue;
            }
            bool isConst = mods.Contains(" const ");
            bool isStatic = mods.Contains(" static ");
            bool isReadonly = mods.Contains(" readonly ");
            bool isPrivate = !mods.Contains(" public ") && !mods.Contains(" protected ") && !mods.Contains(" internal ");
            if (isConst || (isStatic && isReadonly))
            {
                if (!Pascal.IsMatch(name))
                {
                    Hit("RLO-STY-004", f.Rel, i + 1, (isConst ? "constant " : "static readonly field ") + name);
                }
            }
            else if (isPrivate)
            {
                if (!UnderCamel.IsMatch(name))
                {
                    Hit("RLO-STY-003", f.Rel, i + 1, "private field " + name);
                }
            }
            else if (!Pascal.IsMatch(name))
            {
                Hit("RLO-STY-001", f.Rel, i + 1, "field " + name);
            }
        }
    }

    private static string NextCode(SourceFile f, int i)
    {
        for (int j = i + 1; j < f.Code.Length; j++)
        {
            string t = f.Code[j].Trim();
            if (t.Length > 0)
            {
                return t;
            }
        }
        return "";
    }

    // RLO-STY-006 and RLO-STY-007: a file is named after a type it declares; a file Type.Part.cs holds a part of Type.
    private static void FileAndType(SourceFile f)
    {
        Examine("RLO-STY-006", f.Rel);
        Examine("RLO-STY-007", f.Rel);
        string name = BaseName(f.Rel);
        SourceType own = null;
        foreach (SourceType t in f.Types)
        {
            if (t.TopLevel && t.Name == name)
            {
                own = t;
                break;
            }
        }
        if (own == null)
        {
            var names = new List<string>();
            foreach (SourceType t in f.Types)
            {
                if (t.TopLevel)
                {
                    names.Add(t.Name);
                }
            }
            Hit("RLO-STY-006", f.Rel, 0, "no type " + name + " here (" + (names.Count > 0 ? string.Join(", ", names) : "no type") + ")");
        }
        else if (FileName(f.Rel).Length > name.Length + 3 && !own.Partial)
        {
            Hit("RLO-STY-007", f.Rel, own.Line + 1, own.Name + " is not partial in a part file");
        }
    }

    // RLO-STY-010: using directives at the top, System first, then the rest, each group in alphabetical order.
    private static void Usings(SourceFile f)
    {
        Examine("RLO-STY-010", f.Rel);
        var names = new List<string>();
        int first = -1;
        for (int i = 0; i < f.Code.Length; i++)
        {
            Match m = UsingLine.Match(f.Code[i].Trim());
            if (m.Success && f.Context[i] == "")
            {
                names.Add(m.Groups[1].Value);
                first = first < 0 ? i : first;
            }
        }
        if (names.Count < 2)
        {
            return;
        }
        if (!Ordered(names, StringComparer.Ordinal) && !Ordered(names, StringComparer.OrdinalIgnoreCase))
        {
            Hit("RLO-STY-010", f.Rel, first + 1, "using directives out of order: " + string.Join(", ", names));
        }
    }

    private static bool Ordered(List<string> names, StringComparer comparer)
    {
        var sorted = new List<string>(names);
        sorted.Sort((a, b) =>
        {
            bool sa = a == "System" || a.StartsWith("System.", StringComparison.Ordinal);
            bool sb = b == "System" || b.StartsWith("System.", StringComparison.Ordinal);
            if (sa != sb)
            {
                return sa ? -1 : 1;
            }
            return comparer.Compare(a, b);
        });
        for (int i = 0; i < names.Count; i++)
        {
            if (names[i] != sorted[i])
            {
                return false;
            }
        }
        return true;
    }

    // RLO-STY-011 to RLO-STY-015 and RLO-STY-017: indentation, braces, line length, whitespace and LINQ.
    private static void Layout(SourceFile f)
    {
        foreach (string rule in new[] { "RLO-STY-011", "RLO-STY-012", "RLO-STY-013", "RLO-STY-014", "RLO-STY-015", "RLO-STY-017" })
        {
            Examine(rule, f.Rel);
        }
        for (int i = 0; i < f.Lines.Length; i++)
        {
            string raw = f.Lines[i];
            string lead = raw.Substring(0, raw.Length - raw.TrimStart().Length);
            if (lead.IndexOf('\t') >= 0)
            {
                Hit("RLO-STY-011", f.Rel, i + 1, "a tab in the indentation");
            }
            if (raw.Length > 200)
            {
                Hit("RLO-STY-014", f.Rel, i + 1, raw.Length + " columns");
            }
            if (raw.Length > 0 && (raw[raw.Length - 1] == ' ' || raw[raw.Length - 1] == '\t'))
            {
                Hit("RLO-STY-015", f.Rel, i + 1, "trailing whitespace");
            }
            string code = f.Code[i].Trim();
            if (code.Length > 1 && BraceAtEnd.IsMatch(code))
            {
                Hit("RLO-STY-012", f.Rel, i + 1, code);
            }
            if (ControlStart.IsMatch(code))
            {
                Braced(f, i, code);
            }
            if (code == "else" && !NextCode(f, i).StartsWith("{", StringComparison.Ordinal))
            {
                Hit("RLO-STY-013", f.Rel, i + 1, "else without braces");
            }
            if (Linq.IsMatch(code))
            {
                Hit("RLO-STY-017", f.Rel, i + 1, code);
            }
        }
    }

    /// <summary>RLO-STY-013: the body of an if, for, foreach or while on line i is a block in braces.</summary>
    private static void Braced(SourceFile f, int i, string code)
    {
        int open = code.IndexOf('(');
        int depth = 0;
        int close = -1;
        for (int k = open; k < code.Length; k++)
        {
            if (code[k] == '(')
            {
                depth++;
            }
            else if (code[k] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    close = k;
                    break;
                }
            }
        }
        if (close < 0)
        {
            return;   // the condition goes on over the next lines
        }
        string rest = code.Substring(close + 1).Trim();
        if (rest.Length == 0)
        {
            if (!NextCode(f, i).StartsWith("{", StringComparison.Ordinal))
            {
                Hit("RLO-STY-013", f.Rel, i + 1, code);
            }
        }
        else if (!rest.StartsWith("{", StringComparison.Ordinal) && rest != ";" && !rest.StartsWith("&&", StringComparison.Ordinal) && !rest.StartsWith("||", StringComparison.Ordinal))
        {
            Hit("RLO-STY-013", f.Rel, i + 1, code);
        }
    }
}
