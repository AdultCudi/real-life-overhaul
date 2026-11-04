using System.Text;
using System.Text.RegularExpressions;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A counted noun written with its ending in brackets ("{days} day(s)", "warranty(ies)", "hotel(es)") reads right for its number once the
    /// number is in: "1 day", "2 days", "1 warranty", "3 warranties". Where the old apps wrote "3 day(s)" this is what the new pages show.
    /// A text with no bracketed ending is left as it is. Game-free. Cost: one scan of a short text when it is written.
    /// </summary>
    public static class UiPlural
    {
        private static readonly Regex Counted = new Regex(@"(?<n>\d[\d,\.]*)(?<gap>\s+)(?<word>\p{L}+)\((?<end>ies|es|s)\)", RegexOptions.Compiled);

        public static string Fix(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('(') < 0)
            {
                return text;
            }
            return Counted.Replace(text, m =>
            {
                string n = m.Groups["n"].Value, word = m.Groups["word"].Value, end = m.Groups["end"].Value;
                bool one = n == "1";
                string noun = word;
                if (!one)
                {
                    noun = end == "ies" && word.EndsWith("y", System.StringComparison.Ordinal) ? word.Substring(0, word.Length - 1) + "ies" : word + end;
                }
                return n + m.Groups["gap"].Value + noun;
            });
        }
    }
}
