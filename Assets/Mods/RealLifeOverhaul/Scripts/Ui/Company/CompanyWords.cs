using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The words the Company app writes with: a key's text in the game's language (the English given is what the offline checks and the previews read while a key is new), names filled in,
    /// and money in the game's form.</summary>
    public sealed class CompanyWords
    {
        private readonly UiWords _words;
        private readonly Func<float, string> _money;

        public CompanyWords(UiWords words, Func<float, string> money)
        {
            _words = words;
            _money = money;
        }

        public string T(string key, string english) => _words(key, english);

        /// <summary>A label as the kit shows one: an old row label's text with its colon taken off (":" or the full width one, with a space before it where a language writes it so).</summary>
        public string L(string key, string english)
        {
            string text = _words(key, english);
            int end = text.Length;
            while (end > 0 && (text[end - 1] == ':' || text[end - 1] == '：' || text[end - 1] == ' ' || text[end - 1] == ' '))
            {
                end--;
            }
            return end == text.Length ? text : text.Substring(0, end);
        }

        /// <summary>A text with its names filled in: pairs of a name and what goes in its place.</summary>
        public string F(string key, string english, params string[] pairs)
        {
            string text = _words(key, english);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        public string Money(float money) => _money(money);
    }
}
