using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

// What is gone stays gone: every object a feature lists for its clean-up (Meta/FeatureList and its parts) is made somewhere in the
// sources, and every text of leisure and photo mode in en.json is named in them.
internal static partial class Program
{
    private static void Leftovers()
    {
        var all = new System.Text.StringBuilder(1 << 24);
        string list = string.Empty;
        foreach (string file in Directory.GetFiles(PhotoScripts, "*.cs", SearchOption.AllDirectories))
        {
            string code = LogCode(File.ReadAllText(file));
            if (System.IO.Path.GetFileName(file).StartsWith("FeatureList.", System.StringComparison.Ordinal))
            {
                list += code + "\n";
            }
            else
            {
                all.Append(code).Append('\n');
            }
        }
        string sources = all.ToString();

        var listed = new List<string>();
        foreach (Match objects in Regex.Matches(list, @"\.Objects\(([^)]*)\)"))
        {
            foreach (Match name in Regex.Matches(objects.Groups[1].Value, @"""([^""]+)"""))
            {
                listed.Add(name.Groups[1].Value);
            }
        }
        var never = listed.FindAll(name => !sources.Contains("\"" + name + "\""));
        Check(listed.Count >= 8 && never.Count == 0, "features: every object listed for clean-up (" + listed.Count + ") is made in the sources"
                                                     + (never.Count > 0 ? "; never made: " + string.Join(", ", never) : string.Empty));

        var unread = new List<string>();
        foreach (string key in LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json")).Keys)
        {
            if ((key.StartsWith("reallife_leisure_") || key.StartsWith("reallife_photo_")) && !sources.Contains("\"" + key + "\"") && !list.Contains("\"" + key + "\""))
            {
                unread.Add(key);
            }
        }
        Check(unread.Count == 0, "leisure: every text of leisure and photo mode in en.json is read by the sources"
                                 + (unread.Count > 0 ? "; not: " + string.Join(", ", unread) : string.Empty));
    }
}
