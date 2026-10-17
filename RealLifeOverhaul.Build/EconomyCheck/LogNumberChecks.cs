using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

// Log lines write numbers the same on every machine: inside a call to the mod's logger no number is formatted with a
// decimal or a grouping in the current culture, which the game sets from its own number format option.
internal static partial class Program
{
    private static readonly Regex LogCall = new Regex(@"\b(?:Logger|_logger|logger|MetaLog|log|Sink|Inner|_inner|Log)\s*\??\.(?:Info|Warn|Error|Debug)\s*\(");
    private const string LogFormat = @"(?:F[1-9]|N[0-9]|P[0-9]?|0\.0+|0\.#+|0\.0+#+|#,0[^""]*|\+0\.0[^""]*)";
    private static readonly Regex CultureToString = new Regex(@"\.ToString\(""" + LogFormat + @"""\)");
    private static readonly Regex CultureHole = new Regex(@"\{[^{}""]*?:" + LogFormat + @"\}");
    private const string InvariantWrap = "FormattableString.Invariant(";

    private static void LogNumbers()
    {
        int calls = 0;
        var found = new List<string>();
        foreach (string file in Directory.GetFiles(PhotoScripts, "*.cs", SearchOption.AllDirectories))
        {
            string code = LogCode(File.ReadAllText(file));
            foreach (Match m in LogCall.Matches(code))
            {
                calls++;
                int open = m.Index + m.Length - 1;
                string args = code.Substring(open, LogCallEnd(code, open) - open + 1);
                foreach (Match t in CultureToString.Matches(args))
                {
                    found.Add(System.IO.Path.GetFileName(file) + " " + t.Value);
                }
                foreach (Match h in CultureHole.Matches(args))
                {
                    int start = LogLiteralStart(code, open + h.Index);
                    int before = start - 1;
                    while (before >= 0 && char.IsWhiteSpace(code[before]))
                    {
                        before--;
                    }
                    int wrap = before - InvariantWrap.Length + 1;
                    if (start < 0 || wrap < 0 || string.CompareOrdinal(code, wrap, InvariantWrap, 0, InvariantWrap.Length) != 0)
                    {
                        found.Add(System.IO.Path.GetFileName(file) + " " + h.Value);
                    }
                }
            }
        }
        Check(calls > 1500 && found.Count == 0, "log lines: no number in the " + calls + " calls to the logger is written in the machine's culture"
                                                + (found.Count > 0 ? "; " + found.Count + " are, among them " + string.Join(", ", found.GetRange(0, Math.Min(6, found.Count))) : string.Empty));
    }

    // The text with its comments blanked to spaces, strings kept, so every index still points at the same character.
    private static string LogCode(string text)
    {
        var code = new StringBuilder(text);
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '/' && i + 1 < text.Length && (text[i + 1] == '/' || text[i + 1] == '*'))
            {
                bool line = text[i + 1] == '/';
                int end = line ? text.IndexOf('\n', i) : text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? text.Length : line ? end : end + 2;
                for (int k = i; k < end; k++)
                {
                    code[k] = text[k] == '\n' ? '\n' : ' ';
                }
                i = end;
            }
            else if (text[i] == '"')
            {
                i = LogStringEnd(text, i) + 1;
            }
            else if (text[i] == '\'')
            {
                i = text[i + 1] == '\\' ? text.IndexOf('\'', i + 3) + 1 : i + 3;
            }
            else
            {
                i++;
            }
        }
        return code.ToString();
    }

    // The closing quote of the string literal opened at i: verbatim, interpolated and nested strings in holes allowed for.
    private static int LogStringEnd(string text, int i)
    {
        bool verbatim = false, interpolated = false;
        for (int k = i - 1; k >= 0 && (text[k] == '$' || text[k] == '@'); k--)
        {
            verbatim |= text[k] == '@';
            interpolated |= text[k] == '$';
        }
        int j = i + 1;
        while (j < text.Length)
        {
            char c = text[j];
            if (!verbatim && c == '\\')
            {
                j += 2;
            }
            else if (c == '"' && verbatim && j + 1 < text.Length && text[j + 1] == '"')
            {
                j += 2;
            }
            else if (c == '"')
            {
                return j;
            }
            else if (interpolated && c == '{' && j + 1 < text.Length && text[j + 1] == '{')
            {
                j += 2;
            }
            else if (interpolated && c == '{')
            {
                int depth = 1;
                j++;
                while (j < text.Length && depth > 0)
                {
                    if (text[j] == '"')
                    {
                        j = LogStringEnd(text, j) + 1;
                        continue;
                    }
                    depth += text[j] == '{' ? 1 : text[j] == '}' ? -1 : 0;
                    j++;
                }
            }
            else
            {
                j++;
            }
        }
        return text.Length - 1;
    }

    // The ')' that closes the call whose '(' is at open.
    private static int LogCallEnd(string code, int open)
    {
        int depth = 0;
        for (int j = open; j < code.Length; j++)
        {
            if (code[j] == '"')
            {
                j = LogStringEnd(code, j);
                continue;
            }
            depth += code[j] == '(' ? 1 : code[j] == ')' ? -1 : 0;
            if (depth == 0)
            {
                return j;
            }
        }
        return code.Length - 1;
    }

    // Where the interpolated string holding pos begins (its '$' or '@'); -1 if pos is in none.
    private static int LogLiteralStart(string code, int pos)
    {
        for (int i = pos; i > 0; i--)
        {
            if (code[i] != '"' || (code[i - 1] != '$' && code[i - 1] != '@'))
            {
                continue;
            }
            int start = i - 1;
            while (start > 0 && (code[start - 1] == '$' || code[start - 1] == '@'))
            {
                start--;
            }
            if (code.IndexOf('$', start, i - start) >= 0 && LogStringEnd(code, i) >= pos)
            {
                return start;
            }
        }
        return -1;
    }
}
