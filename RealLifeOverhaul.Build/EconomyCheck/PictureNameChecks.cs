using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using IOPath = System.IO.Path;

// Every name of the game's pictures the mod asks for (GameSprites.Get) is named before the one look behind the loading
// screen (GameSprites.Want, in an Awake): a name first asked for in play gives no picture until the next black screen,
// as a look through everything in memory is made only where nobody sees the frame it takes. The first check of it (CoreFixChecks.cs) read only a string written in the call itself, so a name
// passed in an array, a constant, a parameter, a local or a method's answer went unchecked. Here each such name is
// traced back to the strings it can be, and one that cannot be traced fails as surely as one never named.
internal static partial class Program
{
    private static void PictureNames()
    {
        string scripts = IOPath.GetFullPath(IOPath.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            files[file] = File.ReadAllText(file);
        }

        var wanted = new HashSet<string>(StringComparer.Ordinal);
        var untraced = new List<string>();
        var names = new List<string>();
        foreach (KeyValuePair<string, string> pair in files)
        {
            foreach (KeyValuePair<int, string> call in CallsOf(pair.Value, "GameSprites.Want("))
            {
                foreach (string part in Arguments(call.Value))
                {
                    if (!PictureStrings(files, pair.Key, part, call.Key, names, 0))
                    {
                        untraced.Add(IOPath.GetFileName(pair.Key) + " wants " + part);
                    }
                    wanted.UnionWith(names);
                }
            }
        }
        var unnamed = new List<string>();
        int asked = 0;
        foreach (KeyValuePair<string, string> pair in files)
        {
            foreach (KeyValuePair<int, string> call in CallsOf(pair.Value, "GameSprites.Get("))
            {
                asked++;
                if (!PictureStrings(files, pair.Key, call.Value, call.Key, names, 0))
                {
                    untraced.Add(IOPath.GetFileName(pair.Key) + " asks for " + call.Value);
                    continue;
                }
                foreach (string name in names)
                {
                    if (!wanted.Contains(name))
                    {
                        unnamed.Add(IOPath.GetFileName(pair.Key) + ": " + name);
                    }
                }
            }
        }
        Check(asked >= 25 && untraced.Count == 0, "pictures: every one of the " + asked + " asks for a picture by name, and every name wanted, is traced to the strings it can be"
                                                + (untraced.Count > 0 ? "; not: " + string.Join("; ", untraced) : string.Empty));
        Check(unnamed.Count == 0, "pictures: every name an ask can be, through an array, a constant, a parameter, a local or a method's answer, is named before the look"
                                  + (unnamed.Count > 0 ? "; not named: " + string.Join(", ", unnamed) : string.Empty));

        // Made to fail: a name built while playing is caught, and an array's names are found through a parameter.
        string code = "class A\n{\n        private static readonly string[] Glyphs = { \"Icon-A\", \"Icon-B\" };\n"
                      + "        private static void Draw(int i)\n        {\n            Sprite s = GameSprites.Get(\"Icon-\" + i);\n            Sprite t = Pick(Glyphs);\n        }\n"
                      + "        private static Sprite Pick(string[] names)\n        {\n            return GameSprites.Get(names[0]);\n        }\n}\n";
        var made = new Dictionary<string, string>(StringComparer.Ordinal) { [@"X\A.cs"] = code };
        bool built = !PictureStrings(made, @"X\A.cs", "\"Icon-\" + i", code.IndexOf("\"Icon-\" + i", StringComparison.Ordinal), names, 0);
        bool held = PictureStrings(made, @"X\A.cs", "names[0]", code.IndexOf("names[0]", StringComparison.Ordinal), names, 0) && names.Count == 2 && names.Contains("Icon-B");
        Check(built && held, "pictures: the tracing is shown to fail on a name built in play and to find an array's names through a parameter");
    }

    // Every call of a method in a source: where it is, and the text between its brackets.
    private static IEnumerable<KeyValuePair<int, string>> CallsOf(string code, string opening)
    {
        for (int at = code.IndexOf(opening, StringComparison.Ordinal); at >= 0; at = code.IndexOf(opening, at + 1, StringComparison.Ordinal))
        {
            int from = at + opening.Length, depth = 1, i = from;
            for (; i < code.Length && depth > 0; i++)
            {
                depth += code[i] == '(' ? 1 : code[i] == ')' ? -1 : 0;
            }
            yield return new KeyValuePair<int, string>(at, code.Substring(from, Math.Max(0, i - 1 - from)).Trim());
        }
    }

    // The arguments of a call, split at its top-level commas.
    private static List<string> Arguments(string text)
    {
        var parts = new List<string>();
        int depth = 0, from = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            depth += c == '(' || c == '[' || c == '{' ? 1 : c == ')' || c == ']' || c == '}' ? -1 : 0;
            if (c == ',' && depth == 0)
            {
                parts.Add(text.Substring(from, i - from).Trim());
                from = i + 1;
            }
        }
        parts.Add(text.Substring(from).Trim());
        return parts;
    }

    private static string Stem(string file) => IOPath.GetFileNameWithoutExtension(file).Split('.')[0];

    // The strings an expression can be: a string; an array or constant of its own class, or of the type named; a local
    // set from one; a parameter of the enclosing method, through every call of it; a method's answers. False when it
    // cannot be traced, which is what a name built while playing comes to.
    private static bool PictureStrings(Dictionary<string, string> files, string file, string expression, int at, List<string> names, int depth)
    {
        names.Clear();
        string e = expression.Trim();
        Match literal = Regex.Match(e, "^\"([^\"]*)\"$");
        if (literal.Success)
        {
            names.Add(literal.Groups[1].Value);
            return true;
        }
        if (depth > 3 || e.Length == 0)
        {
            return false;
        }
        string code = files[file];
        Match call = Regex.Match(e, @"^(\w+)\((.*)\)$");
        if (call.Success)
        {
            string body = MethodBody(code, call.Groups[1].Value);
            foreach (Match r in Regex.Matches(body, "return\\s+\"([^\"]*)\"\\s*;"))
            {
                names.Add(r.Groups[1].Value);
            }
            return names.Count > 0 && !Regex.IsMatch(body, "return\\s+(?!\")[^;]*;");
        }
        string bare = Regex.Replace(e, @"(?:\[[^\]]*\])+$", string.Empty);
        if (!Regex.IsMatch(bare, @"^[\w.]+$"))
        {
            return false;   // built while playing: "Icon-" + i, a format, a choice between two
        }
        string[] dotted = bare.Split('.');
        string field = dotted[dotted.Length - 1];
        string type = dotted.Length >= 2 && dotted[dotted.Length - 2] != "GameUi" ? dotted[dotted.Length - 2] : string.Empty;
        foreach (KeyValuePair<string, string> pair in files)
        {
            bool mine = type.Length == 0 ? Stem(pair.Key) == Stem(file) : Stem(pair.Key) == type;
            if (mine && FieldStrings(pair.Value, field, names))
            {
                return true;
            }
        }
        if (type.Length > 0)
        {
            return false;
        }
        string before = code.Substring(0, Math.Min(at, code.Length));
        MatchCollection heads = Regex.Matches(before, @"\n\s+(?:public |private |internal |protected )(?:static )?[\w\[\]<>]+ (\w+)\(([^)]*)\)");
        if (heads.Count == 0)
        {
            return false;
        }
        Match head = heads[heads.Count - 1];
        MatchCollection locals = Regex.Matches(before.Substring(head.Index), @"\bstring\[\] " + field + @"\s*=\s*([^;]+);");
        if (locals.Count > 0)
        {
            return PictureStrings(files, file, locals[locals.Count - 1].Groups[1].Value, head.Index, names, depth + 1);
        }
        if (!Regex.IsMatch(head.Groups[2].Value, @"\bstring\[\] " + field + @"\b"))
        {
            return false;
        }
        // A parameter: every call of the method, in its own class or named with it, traced where it is made.
        string method = head.Groups[1].Value, stem = Stem(file);
        var all = new List<string>();
        int calls = 0;
        foreach (KeyValuePair<string, string> pair in files)
        {
            foreach (Match c in Regex.Matches(pair.Value, @"(?<![\w.])(?:(\w+)\.)?" + method + @"\("))
            {
                bool ours = c.Groups[1].Success ? c.Groups[1].Value == stem : Stem(pair.Key) == stem;
                if (!ours || Regex.IsMatch(pair.Value.Substring(Math.Max(0, c.Index - 24), Math.Min(24, c.Index)), @"(?:Sprite|void|static)\s+$"))
                {
                    continue;   // another class's method of that name, or the method's own head
                }
                using (IEnumerator<KeyValuePair<int, string>> one = CallsOf(pair.Value.Substring(c.Index + c.Length - method.Length - 1), method + "(").GetEnumerator())
                {
                    List<string> args = one.MoveNext() ? Arguments(one.Current.Value) : new List<string>();
                    if (args.Count != 1 || !PictureStrings(files, pair.Key, args[0], c.Index, names, depth + 1))
                    {
                        names.Clear();
                        return false;
                    }
                }
                all.AddRange(names);
                calls++;
            }
        }
        names.Clear();
        names.AddRange(all);
        return calls > 0;
    }

    // A string constant's value, or every string in an array's (or an array of arrays') initializer; false for anything else.
    private static bool FieldStrings(string code, string field, List<string> names)
    {
        Match constant = Regex.Match(code, @"const string " + field + "\\s*=\\s*\"([^\"]*)\"\\s*;");
        if (constant.Success)
        {
            names.Add(constant.Groups[1].Value);
            return true;
        }
        Match array = Regex.Match(code, @"string(?:\[\])+ " + field + @"\s*=\s*(?:new[^{=;]*)?\{(.*?)\};", RegexOptions.Singleline);
        if (!array.Success)
        {
            return false;
        }
        string items = Regex.Replace(array.Groups[1].Value, @"//[^\n]*", string.Empty);
        if (Regex.IsMatch(Regex.Replace(Regex.Replace(items, "\"[^\"]*\"", string.Empty), @"\bnew\s*(?:string)?\[\]|\bnull\b", string.Empty), @"\w"))
        {
            return false;   // an item that is not a string: not traced
        }
        foreach (Match s in Regex.Matches(items, "\"([^\"]*)\""))
        {
            names.Add(s.Groups[1].Value);
        }
        return true;
    }

    // A method's body by its name, or nothing.
    private static string MethodBody(string code, string method)
    {
        Match head = Regex.Match(code, @"\n\s+(?:public |private |internal |protected )(?:static )?[\w\[\]<>]+ " + method + @"\([^)]*\)\s*\{");
        return head.Success ? Block(code, head.Index + head.Length - 1) : string.Empty;
    }
}
