using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// RLO Texts in a Going Public session. A player's own texts are written on their own machine and never sent. What
    /// every player sees (the shared feed and paper) is written once by the host and sent as text in part
    /// <see cref="Part"/>; a guest checks each again on arrival and never writes shared text; without the host's words every
    /// player shows the written line. An older RLO passes over a part letter or a line kind it does not know.
    ///
    /// One line a text: "w|key|intent|words" in English, or "x|key|intent|lang|words" in another language, texts escaped as
    /// the save's, at most <see cref="WordsMost"/> characters and <see cref="LinesMost"/> lines. The key is the session's
    /// number of the item ("fn:45", "fn:45:3:c17", "lp:day:n"). A guest keeps only words in its own game's language.
    /// Game-free. COST: a line a text written on the host; nothing per frame.
    /// </summary>
    public static class TextsShare
    {
        public const char Part = 'T';
        public const int WordsMost = 200;        // characters of one text: the longest intent's most
        public const int LinesMost = 300;        // lines of the whole part: a day of the feed and the paper
        public const int ReadMost = 400;         // lines a guest reads of one message

        /// <summary>Appends one shared text as a line, ending with its line break so changes put together stay lines.</summary>
        public static void Line(StringBuilder b, string key, string intent, string words) => Line(b, key, intent, words, null);

        /// <summary>The same in a language: English or none as a "w" line, any other as an "x" line.</summary>
        public static void Line(StringBuilder b, string key, string intent, string words, string lang)
        {
            bool english = lang == null || lang == TextLanguages.English;
            b.Append(english ? "w|" : "x|");
            TextsStore.Escape(key, b);
            b.Append('|');
            TextsStore.Escape(intent ?? string.Empty, b);
            b.Append('|');
            if (!english)
            {
                TextsStore.Escape(lang, b);
                b.Append('|');
            }
            TextsStore.Escape(words.Length > WordsMost ? words.Substring(0, WordsMost) : words, b);
            b.Append('\n');
        }

        /// <summary>The host's whole part: the newest shared texts this machine wrote as the host, never one it was sent; null with none.</summary>
        public static string Whole(TextsStore store)
        {
            var b = new StringBuilder(4096);
            IReadOnlyList<TextKept> all = store.All;
            int from = 0, count = 0;
            for (int i = all.Count - 1; i >= 0 && count < LinesMost; i--)
            {
                if (Own(all[i]))
                {
                    from = i;
                    count++;
                }
            }
            for (int i = from; i < all.Count && count > 0; i++)
            {
                if (Own(all[i]))
                {
                    Line(b, NetKey(all[i].Key), all[i].Intent, all[i].Words, all[i].Lang);
                }
            }
            return b.Length > 0 ? b.ToString() : null;
        }

        // A shared text this machine wrote itself as the host, not one a host sent it.
        private static bool Own(TextKept k) => k.Shared && !k.Received;

        /// <summary>
        /// A guest: reads the host's lines, checks each again, and keeps those that pass as received, never saved or sent on.
        /// The host's whole part replaces every text received before it. How many were kept.
        /// </summary>
        public static int Apply(string text, TextsStore store, int day, List<string> refused, bool whole) => Apply(text, store, day, refused, whole, TextLanguages.English);

        /// <summary>The same for a guest whose game is in <paramref name="lang"/>: words in another language are not kept.</summary>
        public static int Apply(string text, TextsStore store, int day, List<string> refused, bool whole, string lang)
        {
            if (whole)
            {
                store.ClearReceived();
            }
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            string[] lines = text.Split('\n');
            int kept = 0;
            for (int i = 0; i < lines.Length && i < ReadMost; i++)
            {
                string[] f = lines[i].Split('|');
                bool six = f.Length == 5 && f[0] == "x";
                if (!six && (f.Length != 4 || f[0] != "w"))
                {
                    continue;   // a line this version does not know
                }
                string key = TextsStore.Unescape(f[1]), intentKey = TextsStore.Unescape(f[2]);
                string wordsLang = six ? TextsStore.Unescape(f[3]) : TextLanguages.English, words = TextsStore.Unescape(f[six ? 4 : 3]);
                if (!Shareable(key) || (six && !TextLanguages.Known(wordsLang)))
                {
                    continue;
                }
                if (wordsLang != (lang ?? TextLanguages.English))
                {
                    continue;   // another language than this guest's game: its own written line
                }
                TextIntent intent = TextIntents.Find(intentKey) ?? TextIntents.Find(intentKey, "on") ?? TextIntents.Six(intentKey, null);
                TextVerdict verdict = six ? TextCheck.JudgeSharedSix(words, intent, wordsLang) : TextCheck.JudgeShared(words, intent);
                if (verdict != TextVerdict.Ok)
                {
                    refused?.Add(key + " (" + verdict + ")");
                    continue;
                }
                store.Keep(key, day, intentKey, words, true, true, wordsLang);   // in its language: a switch of the game's language shows the written line
                kept++;
            }
            return kept;
        }

        /// <summary>Only the session's shared items are taken from the host: a post by its number or a piece of the paper.</summary>
        public static bool Shareable(string key)
        {
            return key != null && (key.StartsWith("fn:", System.StringComparison.Ordinal) || key.StartsWith("lp:", System.StringComparison.Ordinal)) && key.Length <= 40;
        }

        /// <summary>
        /// The key a host's text goes under to the guests: a post or comment of the host's own ("fp:12", "fc:12:3:c17") by
        /// the session's number ("fn:12", "fn:12:3:c17"). An older RLO keeps such a comment's line under a key it never reads.
        /// </summary>
        public static string NetKey(string key)
        {
            return key != null && (key.StartsWith("fp:", System.StringComparison.Ordinal) || key.StartsWith("fc:", System.StringComparison.Ordinal)) ? "fn:" + key.Substring(3) : key;
        }

        /// <summary>The key of a shared post by the session's number.</summary>
        public static string PostKey(int netId) => "fn:" + netId.ToString(CultureInfo.InvariantCulture);
    }
}
