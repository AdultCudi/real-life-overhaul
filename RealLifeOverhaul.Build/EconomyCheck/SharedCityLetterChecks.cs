using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// The shared city's parts by letter (Scripts/Core/SharedCity.cs): a second part registered with a letter takes the first one's place
// (SharedCity.Register), so two systems sharing a letter would leave one of them out of every Going Public session without a word, as Flare in the
// city and the tech markets once did with R. Every part's letter as it is registered (a letter, or a class's const Part) is read from the sources:
// no letter is taken twice, each is a capital (a part's changes travel as its small letter), and every letter a system passes to the shared
// city's calls is a registered part's.
internal static partial class Program
{
    private static readonly Regex PartConst = new Regex(@"const\s+char\s+Part\s*=\s*'(.)'", RegexOptions.Compiled);
    private static readonly Regex PartNew = new Regex(@"new\s+SharedCityPart\b", RegexOptions.Compiled);
    private static readonly Regex PartId = new Regex(@"\bId\s*=\s*(?:'(?<lit>.)'|(?<cls>\w+)\.Part\b|(?<own>Part)\b)", RegexOptions.Compiled);
    private static readonly Regex PartCall = new Regex(@"SharedCity\.(?:Follows|Changed|Ask|ToPlayer|Waiting|AskAgain)\(\s*(?:'(?<lit>.)'|(?<cls>\w+)\.Part\b|(?<own>Part)\b)", RegexOptions.Compiled);
    private static readonly Regex ClassName = new Regex(@"(?m)^[ \t]*(?:(?:public|internal|private|protected|static|sealed|partial|abstract)[ \t]+)*class[ \t]+(\w+)", RegexOptions.Compiled);

    private static void SharedLetters()
    {
        string root = ModScripts();
        var sources = new List<KeyValuePair<string, string>>();
        var consts = new Dictionary<string, char>(StringComparer.Ordinal);   // a class's const Part, by the class's name
        var clashes = new List<string>();
        foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            string code = File.ReadAllText(file);
            sources.Add(new KeyValuePair<string, string>(file, code));
            foreach (Match m in PartConst.Matches(code))
            {
                string owner = SharedClassAt(code, m.Index);
                char letter = m.Groups[1].Value[0];
                if (owner == null)
                {
                    continue;
                }
                if (consts.TryGetValue(owner, out char was) && was != letter)
                {
                    clashes.Add(owner);   // two classes of one name with different letters: a reference to either is not certain
                }
                consts[owner] = letter;
            }
        }
        var parts = new List<KeyValuePair<char, string>>();
        var unread = new List<string>();
        foreach (KeyValuePair<string, string> s in sources)
        {
            foreach (Match m in PartNew.Matches(s.Value))
            {
                string where = System.IO.Path.GetFileName(s.Key) + ":" + SharedLineAt(s.Value, m.Index);
                Match id = PartId.Match(s.Value.Substring(m.Index, Math.Min(400, s.Value.Length - m.Index)));
                char letter = id.Success ? SharedLetterOf(id, consts, SharedClassAt(s.Value, m.Index)) : '\0';
                if (letter == '\0')
                {
                    unread.Add(where);
                }
                else
                {
                    parts.Add(new KeyValuePair<char, string>(letter, where));
                }
            }
        }
        List<string> twice = parts.GroupBy(p => p.Key).Where(g => g.Count() > 1).Select(g => g.Key + " at " + string.Join(" and ", g.Select(p => p.Value))).ToList();
        bool capitals = parts.All(p => p.Key >= 'A' && p.Key <= 'Z');
        string all = new string(parts.Select(p => p.Key).OrderBy(c => c).ToArray());
        Check(parts.Count >= 14 && unread.Count == 0 && clashes.Count == 0 && twice.Count == 0 && capitals,
            "shared city: every system's part has a capital letter of its own (" + parts.Count + " parts, " + all + (twice.Count > 0 ? "; taken twice: " + string.Join("; ", twice) : string.Empty)
            + (unread.Count > 0 ? "; a letter not read at " + string.Join(", ", unread) : string.Empty) + (clashes.Count > 0 ? "; classes of one name: " + string.Join(", ", clashes) : string.Empty) + ")");
        var letters = new HashSet<char>(parts.Select(p => p.Key));
        var strays = new List<string>();
        int calls = 0;
        foreach (KeyValuePair<string, string> s in sources)
        {
            foreach (Match m in PartCall.Matches(s.Value))
            {
                calls++;
                char letter = SharedLetterOf(m, consts, SharedClassAt(s.Value, m.Index));
                if (!letters.Contains(letter))
                {
                    strays.Add(System.IO.Path.GetFileName(s.Key) + ":" + SharedLineAt(s.Value, m.Index));
                }
            }
        }
        Check(calls >= 10 && strays.Count == 0,
            "shared city: every letter a system passes to the shared city is a registered part's (" + calls + " calls" + (strays.Count > 0 ? "; not a part's: " + string.Join(", ", strays) : string.Empty) + ")");
    }

    // A matched letter: written out, a class's const Part, or the enclosing class's own; '\0' when it cannot be told.
    private static char SharedLetterOf(Match m, Dictionary<string, char> consts, string enclosing)
    {
        if (m.Groups["lit"].Success)
        {
            return m.Groups["lit"].Value[0];
        }
        string owner = m.Groups["cls"].Success ? m.Groups["cls"].Value : enclosing;
        return owner != null && consts.TryGetValue(owner, out char letter) ? letter : '\0';
    }

    // The class a position in a file is in: the nearest class declared before it.
    private static string SharedClassAt(string code, int at)
    {
        string name = null;
        foreach (Match m in ClassName.Matches(code))
        {
            if (m.Index > at)
            {
                break;
            }
            name = m.Groups[1].Value;
        }
        return name;
    }

    private static int SharedLineAt(string code, int at)
    {
        int line = 1;
        for (int i = 0; i < at && i < code.Length; i++)
        {
            line += code[i] == '\n' ? 1 : 0;
        }
        return line;
    }
}
