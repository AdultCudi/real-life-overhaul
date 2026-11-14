using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// A refusal that names its figures. A reason is a locale key; one with figures to say (what is short, what it needs, what the player has) carries them
    /// after the key, so the reason stays one string wherever it is handed on, and the one place that says it (<see cref="CareersText.T(string)"/>, or a
    /// notice given <see cref="DataOf"/>) fills them in. A key with no figures is the key alone, so every comparison with one still holds.
    /// </summary>
    internal static class CareersWhy
    {
        public const char Sep = '\u001f';      // between the key and each name, and between a name and its value (a name, a value, a name...)
        public const int NoticeMost = 240;     // characters a notice may have

        /// <summary>A reason with its figures: the key and then name, value, name, value.</summary>
        public static string With(string key, params string[] pairs)
        {
            if (pairs == null || pairs.Length < 2)
            {
                return key;
            }
            var b = new StringBuilder(key, key.Length + 48);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                b.Append(Sep).Append(pairs[i]).Append(Sep).Append(pairs[i + 1] ?? string.Empty);
            }
            return b.ToString();
        }

        /// <summary>The locale key of a reason, with its figures taken off.</summary>
        public static string KeyOf(string why)
        {
            int at = why == null ? -1 : why.IndexOf(Sep);
            return at < 0 ? why : why.Substring(0, at);
        }

        /// <summary>The figures of a reason by name for a notice; null for a reason with none.</summary>
        public static Dictionary<string, string> DataOf(string why)
        {
            int at = why == null ? -1 : why.IndexOf(Sep);
            if (at < 0)
            {
                return null;
            }
            string[] part = why.Substring(at + 1).Split(Sep);
            var data = new Dictionary<string, string>(part.Length / 2 + 1);
            for (int i = 0; i + 1 < part.Length; i += 2)
            {
                data[part[i]] = part[i + 1];
            }
            return data;
        }

        /// <summary>The reason in words, its figures filled in.</summary>
        public static string Say(string why)
        {
            Dictionary<string, string> data = DataOf(why);
            string text = Economy.FinanceText.T(KeyOf(why));
            if (data != null)
            {
                foreach (KeyValuePair<string, string> pair in data)
                {
                    text = text.Replace("{" + pair.Key + "}", pair.Value);
                }
            }
            return text;
        }

        /// <summary>The sentences joined with a space, as many as a notice has room for: the first is always kept, a later one only whole.</summary>
        public static string Fit(IList<string> sentences, int most = NoticeMost) => StudyNeedRules.Fit(sentences, most);
    }
}
