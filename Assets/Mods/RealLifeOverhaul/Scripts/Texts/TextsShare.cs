using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// RLO Texts in a Going Public session:
    ///
    ///   personal   what is that player's own (texts from the partner, friends, family, the roommate, the in-laws, the boss;
    ///              Flare messages to them; replies addressed to them) is written on their own machine and never sent; a
    ///              player without a pack, or with it off, has the written lines
    ///   shared     what every player sees (the shared Flare feed: the city's people's posts and comments, the city's
    ///              accounts and their answers, the rivals; the shared City Ledger) is written once, by the host, checked
    ///              there, and sent as text in this part of the shared city (letter <see cref="Part"/>), small and in
    ///              the shared city's own pace; a guest that joins late has the whole part with the snapshot
    ///   guests     show the host's words for those items after checking them again on arrival
    ///              (<see cref="TextCheck.JudgeShared"/>); a guest never writes shared text. Without the host's words in
    ///              time (no pack, switched off, too slow) every player shows the written line, as before
    ///   older RLO  a part letter it does not know is passed over whole (SharedCity), and another version's messages are
    ///              never applied at all
    ///
    /// One line a text: "w|key|intent|words", the words escaped as the save's (TextsStore.Escape), at most
    /// <see cref="WordsMost"/> characters; the whole part at most <see cref="LinesMost"/> lines (the newest). The key is the
    /// session's number of the item ("fn:45" a post by the host's number, "fn:45:3:c17" a comment under it, "lp:day:n" a piece of the paper).
    ///
    /// Shared text carries its language: a "w" line is English (as every pack before data v6 wrote), and words of a data
    /// v6 pack in another language go as "x|key|intent|lang|words", a line kind an older RLO passes over (it shows its
    /// written line, as it would without the host's words). A guest keeps only words in its own game's language and
    /// shows its own written line for the rest (<see cref="Apply"/>), checking each in its language (TextCheck.JudgeSharedSix).
    /// Game-free (EconomyCheck, TextsChecks.cs, TextsLanguageChecks.cs). COST: a line a text written on the host; nothing per frame.
    /// </summary>
    public static class TextsShare
    {
        public const char Part = 'T';
        public const int WordsMost = 200;        // characters of one text (the longest intent's most)
        public const int LinesMost = 300;        // lines of the whole part (a day of the feed and the paper)
        public const int ReadMost = 400;         // lines a guest reads of one message

        /// <summary>One shared text as a line, ending with its line break (so that changes put together in a message stay lines).</summary>
        public static void Line(StringBuilder b, string key, string intent, string words) => Line(b, key, intent, words, null);

        /// <summary>The same in a language: English (or none) as a "w" line, any other as an "x" line with its language.</summary>
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

        /// <summary>The host's whole part: every shared text this machine wrote as the host (never one it was sent), the newest <see cref="LinesMost"/>; null with none.</summary>
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

        // A shared text this machine wrote itself as the host ("fp:", "fc:", "lp:"), not one a host sent it.
        private static bool Own(TextKept k) => k.Shared && !k.Received;

        /// <summary>
        /// A guest: reads the host's lines, checks each again, and keeps the ones that pass, as received (never saved, never
        /// sent on). The host's whole part replaces every text received before it. Returns how many were kept.
        /// </summary>
        public static int Apply(string text, TextsStore store, int day, List<string> refused, bool whole) => Apply(text, store, day, refused, whole, TextLanguages.English);

        /// <summary>
        /// The same for a guest whose game is in <paramref name="lang"/> (TextLanguages.Of): words in another language are
        /// not kept (its own written line is shown), and every one kept is checked in its language.
        /// </summary>
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
                    continue;   // (a line this version does not know)
                }
                string key = TextsStore.Unescape(f[1]), intentKey = TextsStore.Unescape(f[2]);
                string wordsLang = six ? TextsStore.Unescape(f[3]) : TextLanguages.English, words = TextsStore.Unescape(f[six ? 4 : 3]);
                if (!Shareable(key) || (six && !TextLanguages.Known(wordsLang)))
                {
                    continue;
                }
                if (wordsLang != (lang ?? TextLanguages.English))
                {
                    continue;   // (another language than this guest's game: its own written line)
                }
                TextIntent intent = TextIntents.Find(intentKey) ?? TextIntents.Find(intentKey, "on") ?? TextIntents.Six(intentKey, null);
                TextVerdict verdict = six ? TextCheck.JudgeSharedSix(words, intent, wordsLang) : TextCheck.JudgeShared(words, intent);
                if (verdict != TextVerdict.Ok)
                {
                    refused?.Add(key + " (" + verdict + ")");
                    continue;
                }
                store.Keep(key, day, intentKey, words, true, true, wordsLang);   // (in its language: a switch of the game's language shows the written line)
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
        /// The key a host's text goes under to the guests: a post of the host's own ("fp:12") by the session's number, the
        /// host's own ("fn:12"); a comment under one ("fc:12:3:c17", Flare/FlareCommentRules.Key) likewise ("fn:12:3:c17").
        /// An older RLO keeps such a comment's line under a key it never reads.
        /// </summary>
        public static string NetKey(string key)
        {
            return key != null && (key.StartsWith("fp:", System.StringComparison.Ordinal) || key.StartsWith("fc:", System.StringComparison.Ordinal)) ? "fn:" + key.Substring(3) : key;
        }

        /// <summary>The key of a shared post by the session's number.</summary>
        public static string PostKey(int netId) => "fn:" + netId.ToString(CultureInfo.InvariantCulture);
    }
}
