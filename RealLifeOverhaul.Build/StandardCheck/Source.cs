using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>One comment of a source file: where it starts and its text with the delimiters.</summary>
internal struct SourceComment
{
    public int Line;        // 0-based
    public string Text;
    public bool Doc;        // a /// line
    public bool Trailing;   // code stands before it on its line
}

/// <summary>One type declared in a source file.</summary>
internal sealed class SourceType
{
    public string Name;
    public string Kind;          // class, struct, interface, enum, record
    public string Header;        // the declaration up to its opening brace, comments and literals blanked
    public string Namespace;
    public int Line;             // 0-based line of the keyword
    public bool TopLevel;
    public bool Partial;
    public bool Public;          // public or internal
    public int SummaryLines;     // the /// lines just above it (attributes between are allowed); 0 for none
    public string Summary;       // their text, joined; "" for none
}

/// <summary>
/// One C# file read for the checks. <see cref="Code"/> is the file with every comment and the inside of every string and
/// character literal blanked to spaces, and <see cref="Bare"/> with only the comments blanked; both keep every line and
/// column of the file, so a match in either is at the same place in <see cref="Lines"/>. <see cref="Context"/> says, for
/// each line, what the innermost block open at its start is: "type" for the body of a class, struct, interface or
/// record, "enum", "ns" for a namespace, "body" for anything else, "" for none.
/// </summary>
internal sealed class SourceFile
{
    public string Rel;
    public string[] Lines;
    public string[] Code;
    public string[] Bare;
    public string[] Context;
    public bool[] InParens;   // the line starts inside an open parenthesis: a parameter or argument list goes on
    public string CodeText;
    public readonly List<SourceComment> Comments = new List<SourceComment>();
    public readonly List<SourceType> Types = new List<SourceType>();
    public readonly List<string> Namespaces = new List<string>();

    private static readonly Regex TypeDecl = new Regex(@"\b(class|struct|interface|enum|record)\s+([A-Za-z_]\w*)", RegexOptions.Compiled);
    private static readonly Regex NamespaceDecl = new Regex(@"\bnamespace\s+([\w.]+)", RegexOptions.Compiled);

    public static SourceFile Read(string path, string rel)
    {
        string text = File.ReadAllText(path, Encoding.UTF8);
        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text.Substring(1);
        }
        text = text.Replace("\r\n", "\n");
        var file = new SourceFile { Rel = rel };
        char[] code = text.ToCharArray();
        char[] bare = text.ToCharArray();
        file.Blank(text, code, bare);
        file.Lines = text.Split('\n');
        file.CodeText = new string(code);
        file.Code = file.CodeText.Split('\n');
        file.Bare = new string(bare).Split('\n');
        file.Structure();
        return file;
    }

    // ---------------------------------------------------------------- comments and literals

    private void Blank(string s, char[] code, char[] bare)
    {
        int line = 0;
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            char next = i + 1 < s.Length ? s[i + 1] : '\0';
            if (c == '\n')
            {
                line++;
                i++;
            }
            else if (c == '/' && next == '/')
            {
                int end = s.IndexOf('\n', i);
                if (end < 0)
                {
                    end = s.Length;
                }
                int lineStart = s.LastIndexOf('\n', Math.Max(0, i - 1)) + 1;
                if (i == 0)
                {
                    lineStart = 0;
                }
                string before = s.Substring(lineStart, i - lineStart);
                string body = s.Substring(i, end - i);
                Comments.Add(new SourceComment
                {
                    Line = line,
                    Text = body,
                    Doc = body.StartsWith("///", StringComparison.Ordinal) && !body.StartsWith("////", StringComparison.Ordinal),
                    Trailing = before.Trim().Length > 0
                });
                Fill(code, i, end);
                Fill(bare, i, end);
                i = end;
            }
            else if (c == '/' && next == '*')
            {
                int end = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? s.Length : end + 2;
                Comments.Add(new SourceComment { Line = line, Text = s.Substring(i, end - i) });
                Fill(code, i, end);
                Fill(bare, i, end);
                line += Count(s, i, end, '\n');
                i = end;
            }
            else if (c == '"' || ((c == '$' || c == '@') && (next == '"' || ((next == '$' || next == '@') && i + 2 < s.Length && s[i + 2] == '"'))))
            {
                int end = StringEnd(s, i);
                int open = s.IndexOf('"', i);
                Fill(code, open + 1, end - 1);
                line += Count(s, i, end, '\n');
                i = end;
            }
            else if (c == '\'')
            {
                int end = CharEnd(s, i);
                Fill(code, i + 1, end - 1);
                i = end;
            }
            else
            {
                i++;
            }
        }
    }

    /// <summary>The index just past the string literal that starts at i (with its $ or @ prefix).</summary>
    private static int StringEnd(string s, int i)
    {
        bool verbatim = false, interpolated = false;
        while (s[i] != '"')
        {
            verbatim |= s[i] == '@';
            interpolated |= s[i] == '$';
            i++;
        }
        i++;
        while (i < s.Length)
        {
            char c = s[i];
            if (verbatim && c == '"' && i + 1 < s.Length && s[i + 1] == '"')
            {
                i += 2;
            }
            else if (c == '"')
            {
                return i + 1;
            }
            else if (!verbatim && c == '\\')
            {
                i += 2;
            }
            else if (!verbatim && c == '\n')
            {
                return i;
            }
            else if (interpolated && c == '{' && i + 1 < s.Length && s[i + 1] == '{')
            {
                i += 2;
            }
            else if (interpolated && c == '{')
            {
                i = HoleEnd(s, i + 1);
            }
            else
            {
                i++;
            }
        }
        return s.Length;
    }

    /// <summary>The index just past the closing brace of an interpolation hole whose inside starts at i.</summary>
    private static int HoleEnd(string s, int i)
    {
        int depth = 1;
        while (i < s.Length)
        {
            char c = s[i];
            char next = i + 1 < s.Length ? s[i + 1] : '\0';
            if (c == '"' || ((c == '$' || c == '@') && (next == '"' || ((next == '$' || next == '@') && i + 2 < s.Length && s[i + 2] == '"'))))
            {
                i = StringEnd(s, i);
            }
            else if (c == '\'')
            {
                i = CharEnd(s, i);
            }
            else if (c == '{')
            {
                depth++;
                i++;
            }
            else if (c == '}')
            {
                depth--;
                i++;
                if (depth == 0)
                {
                    return i;
                }
            }
            else
            {
                i++;
            }
        }
        return s.Length;
    }

    private static int CharEnd(string s, int i)
    {
        int j = i + 1;
        while (j < s.Length && s[j] != '\'' && s[j] != '\n')
        {
            j += s[j] == '\\' ? 2 : 1;
        }
        return Math.Min(s.Length, j + 1);
    }

    private static void Fill(char[] a, int from, int to)
    {
        for (int k = Math.Max(0, from); k < to && k < a.Length; k++)
        {
            if (a[k] != '\n')
            {
                a[k] = ' ';
            }
        }
    }

    private static int Count(string s, int from, int to, char c)
    {
        int n = 0;
        for (int k = from; k < to && k < s.Length; k++)
        {
            if (s[k] == c)
            {
                n++;
            }
        }
        return n;
    }

    // ---------------------------------------------------------------- blocks and types

    private void Structure()
    {
        Context = new string[Code.Length];
        InParens = new bool[Code.Length];
        var stack = new List<string>(16);
        var namespaces = new List<string>(4);
        string text = CodeText;
        int segment = 0;
        int line = 0;
        int paren = 0;
        Context[0] = "";
        for (int k = 0; k < text.Length; k++)
        {
            char c = text[k];
            if (c == '\n')
            {
                line++;
                Context[line] = stack.Count > 0 ? stack[stack.Count - 1] : "";
                InParens[line] = paren > 0;
                continue;
            }
            if (c == '(')
            {
                paren++;
            }
            else if (c == ')')
            {
                paren = Math.Max(0, paren - 1);
            }
            else if (c == ';' && paren <= 0)
            {
                segment = k + 1;
            }
            else if (c == '{')
            {
                string header = text.Substring(segment, k - segment);
                string parent = stack.Count > 0 ? stack[stack.Count - 1] : "";
                Match ns = NamespaceDecl.Match(header);
                Match type = TypeDecl.Match(header);
                if (ns.Success && (parent == "" || parent == "ns"))
                {
                    namespaces.Add(ns.Groups[1].Value);
                    Namespaces.Add(ns.Groups[1].Value);
                    stack.Add("ns");
                }
                else if (type.Success && (parent == "" || parent == "ns" || parent == "type") && IsDeclaration(header, type.Index))
                {
                    int at = segment + type.Index;
                    int typeLine = LineOf(text, at);
                    Types.Add(new SourceType
                    {
                        Name = type.Groups[2].Value,
                        Kind = type.Groups[1].Value,
                        Header = header.Trim(),
                        Namespace = namespaces.Count > 0 ? namespaces[namespaces.Count - 1] : "",
                        Line = typeLine,
                        TopLevel = parent != "type",
                        Partial = Regex.IsMatch(header.Substring(0, type.Index), @"\bpartial\b"),
                        Public = Regex.IsMatch(header.Substring(0, type.Index), @"\b(public|internal)\b"),
                        SummaryLines = SummaryAbove(typeLine),
                        Summary = SummaryTextAbove(typeLine)
                    });
                    stack.Add(type.Groups[1].Value == "enum" ? "enum" : "type");
                }
                else
                {
                    stack.Add("body");
                }
                segment = k + 1;
                paren = 0;
            }
            else if (c == '}')
            {
                if (stack.Count > 0)
                {
                    if (stack[stack.Count - 1] == "ns" && namespaces.Count > 0)
                    {
                        namespaces.RemoveAt(namespaces.Count - 1);
                    }
                    stack.RemoveAt(stack.Count - 1);
                }
                segment = k + 1;
                paren = 0;
            }
        }
        for (int i = 0; i < Context.Length; i++)
        {
            Context[i] ??= "";
        }
    }

    /// <summary>False for "new class", a generic constraint "where T : class" and the like.</summary>
    private static bool IsDeclaration(string header, int at)
    {
        string before = Regex.Replace(header.Substring(0, at), @"\[[^\[\]]*\]", " ");
        if (Regex.IsMatch(before, @"\bwhere\b") || Regex.IsMatch(before, @"(=|\bnew|\(|,|=>)\s*$"))
        {
            return false;
        }
        return !before.Contains("=");
    }

    private static int LineOf(string text, int index)
    {
        int n = 0;
        for (int k = 0; k < index; k++)
        {
            if (text[k] == '\n')
            {
                n++;
            }
        }
        return n;
    }

    /// <summary>The /// lines just above line (attribute lines between are passed over); 0 for none.</summary>
    public int SummaryAbove(int line)
    {
        int j = line - 1;
        while (j >= 0 && Lines[j].TrimStart().StartsWith("[", StringComparison.Ordinal))
        {
            j--;
        }
        int n = 0;
        while (j >= 0 && Lines[j].TrimStart().StartsWith("///", StringComparison.Ordinal))
        {
            n++;
            j--;
        }
        return n;
    }

    /// <summary>The text of the /// lines just above line, joined with spaces; "" for none.</summary>
    public string SummaryTextAbove(int line)
    {
        int n = SummaryAbove(line);
        if (n == 0)
        {
            return "";
        }
        int j = line - 1;
        while (j >= 0 && Lines[j].TrimStart().StartsWith("[", StringComparison.Ordinal))
        {
            j--;
        }
        var parts = new List<string>(n);
        for (int k = j - n + 1; k <= j; k++)
        {
            parts.Add(Lines[k].Trim().Substring(3).Trim());
        }
        return string.Join(" ", parts);
    }

    /// <summary>The body of the block whose opening brace is the first at or after index, as an index range of CodeText.</summary>
    public bool Block(int index, out int open, out int close)
    {
        open = CodeText.IndexOf('{', index);
        close = -1;
        if (open < 0)
        {
            return false;
        }
        int depth = 0;
        for (int k = open; k < CodeText.Length; k++)
        {
            if (CodeText[k] == '{')
            {
                depth++;
            }
            else if (CodeText[k] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    close = k;
                    return true;
                }
            }
        }
        return false;
    }

    public int LineAt(int index) => LineOf(CodeText, index);
}
